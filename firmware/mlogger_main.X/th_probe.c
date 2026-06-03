#include "th_probe.h"
#include "i2c_master.h"

#include <string.h>   // memcpy

// ===== OSL 共通レジスタ (mlogger_th_sensor.X/i2c_shared_data.h と同期) =====
// 0x06 = data_count (INFO BLOCK 内、接続確認用)
#define REG_DATA_COUNT        0x06

// 0x28-0x4B = POLL ブロック (status1/status2/reserved + value[8] float)
#define REG_POLL_BASE         0x28
#define POLL_BLOCK_SIZE       36

#define POLL_OFS_STATUS1      0
#define POLL_OFS_STATUS2      1
#define POLL_OFS_VALUE        4    // value[0] の先頭バイト (Little Endian float)

// 0x29 = status2 (親機が 0 を書くと single-shot 計測トリガ)
#define REG_STATUS2           0x29

// value 配列インデックス (子機 i2c_shared_data.h の VAL_IDX_* と同期)
#define VAL_IDX_TEMPERATURE   0
#define VAL_IDX_HUMIDITY      1
#define VAL_IDX_CO2           2
#define VAL_IDX_GLOBE_TEMP    3

// Status1 stale ビット
#define STATUS1_STALE_T       (0x01)
#define STATUS1_STALE_RH      (0x02)
#define STATUS1_STALE_CO2     (0x04)
#define STATUS1_STALE_T_GLB   (0x08)

// 拡張領域 (子機固有 STCC4 制御)
#define REG_STCC4_CMD         0x6C
#define REG_STCC4_CMD_ARG     0x6D    // uint16_t LE (0x6D-0x6E)
#define REG_STCC4_STATE       0x6F
#define REG_FRC_CORRECTION    0x70    // int16_t LE (0x70-0x71)

// STCC4 コマンド (子機 i2c_shared_data.h と同期)
#define STCC4_CMD_FRC              (0x01)
#define STCC4_CMD_FACTORY_RESET    (0x02)
#define STCC4_CMD_CONDITIONING     (0x03)


// <editor-fold defaultstate="collapsed" desc="内部ユーティリティ">

// 単一レジスタへの 1 byte 書き込み
static bool write_reg_u8(uint8_t reg, uint8_t value)
{
    uint8_t buf[2] = { reg, value };
    return I2C_Write(TH_PROBE_ADDRESS, buf, 2);
}

// 単一レジスタからの 1 byte 読み出し
static bool read_reg_u8(uint8_t reg, uint8_t* out)
{
    return I2C_WriteRead(TH_PROBE_ADDRESS, &reg, 1, out, 1);
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数の実装">

void ThProbe_Init(ThProbe_t* p)
{
    p->temp_c     = 0.0f;
    p->rh_pct     = 0.0f;
    p->glb_c      = 0.0f;
    p->co2_ppm    = 0;
    p->i2c_ok     = false;
    p->t_valid    = false;
    p->rh_valid   = false;
    p->co2_valid  = false;
    p->glb_valid  = false;
}

bool ThProbe_IsConnected(void)
{
    // INFO ブロックの data_count (0x06) を読んで値が 4 (= T/RH/CO2/glb) であることを確認。
    // 子機が外れていれば I2C_WriteRead は false を返す。
    uint8_t count = 0;
    if (!read_reg_u8(REG_DATA_COUNT, &count)) return false;
    return (count == 4);
}

void ThProbe_Trigger(void)
{
    // REG_STATUS2 に 0 を書くと子機が single-shot 計測を開始する。
    // I2C 失敗は無視 (次サイクルで Read 側でリカバリ判定)。
    (void)write_reg_u8(REG_STATUS2, 0);
}

void ThProbe_Read(ThProbe_t* p)
{
    // POLL BLOCK (0x28-0x4B, 36B) を 1 トランザクションで読む。
    const uint8_t cmd = REG_POLL_BASE;
    uint8_t buffer[POLL_BLOCK_SIZE];
    bool ok = I2C_WriteRead(TH_PROBE_ADDRESS, &cmd, 1, buffer, POLL_BLOCK_SIZE);

    // I2C 失敗 = 子機が物理的に外れている等。全 valid を倒す。
    // i2c_ok は dc 判定 (probe 物理切断) 用、valid 系は warmup/STALE 判定用。
    p->i2c_ok = ok;
    if (!ok) {
        p->t_valid   = false;
        p->rh_valid  = false;
        p->co2_valid = false;
        p->glb_valid = false;
        return;
    }

    uint8_t status1 = buffer[POLL_OFS_STATUS1];

    // value[0] = 乾球温度 [°C]
    if (!(status1 & STATUS1_STALE_T)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_TEMPERATURE * 4], 4);
        p->temp_c  = v;
        p->t_valid = true;
    } else {
        p->t_valid = false;
    }

    // value[1] = 相対湿度 [%RH]
    if (!(status1 & STATUS1_STALE_RH)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_HUMIDITY * 4], 4);
        p->rh_pct   = v;
        p->rh_valid = true;
    } else {
        p->rh_valid = false;
    }

    // value[2] = CO2 濃度 [ppm] (float のまま受け取って uint16 へ丸める)
    if (!(status1 & STATUS1_STALE_CO2)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_CO2 * 4], 4);
        if      (v < 0.0f)     v = 0.0f;
        else if (v > 65535.0f) v = 65535.0f;
        p->co2_ppm   = (uint16_t)(v + 0.5f);
        p->co2_valid = true;
    } else {
        p->co2_valid = false;
    }

    // value[3] = グローブ温度 [°C]
    if (!(status1 & STATUS1_STALE_T_GLB)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_GLOBE_TEMP * 4], 4);
        p->glb_c     = v;
        p->glb_valid = true;
    } else {
        p->glb_valid = false;
    }
}

void ThProbe_StartFrc(uint16_t target_ppm)
{
    // 引数 → コマンドの順に書く。REG_STCC4_CMD_ARG は uint16 LE。
    uint8_t arg_buf[3] = {
        REG_STCC4_CMD_ARG,
        (uint8_t)(target_ppm & 0xFF),
        (uint8_t)((target_ppm >> 8) & 0xFF)
    };
    (void)I2C_Write(TH_PROBE_ADDRESS, arg_buf, 3);
    (void)write_reg_u8(REG_STCC4_CMD, STCC4_CMD_FRC);
}

void ThProbe_StartFactoryReset(void)
{
    (void)write_reg_u8(REG_STCC4_CMD, STCC4_CMD_FACTORY_RESET);
}

void ThProbe_StartConditioning(void)
{
    (void)write_reg_u8(REG_STCC4_CMD, STCC4_CMD_CONDITIONING);
}

uint8_t ThProbe_GetState(void)
{
    uint8_t state = TH_PROBE_STATE_IDLE;
    if (!read_reg_u8(REG_STCC4_STATE, &state)) return TH_PROBE_STATE_IDLE;
    return state;
}

int16_t ThProbe_GetFrcCorrection(void)
{
    const uint8_t cmd = REG_FRC_CORRECTION;
    uint8_t buf[2] = {0, 0};
    if (!I2C_WriteRead(TH_PROBE_ADDRESS, &cmd, 1, buf, 2)) return 0;
    int16_t v;
    memcpy(&v, buf, 2);
    return v;
}

// </editor-fold>
