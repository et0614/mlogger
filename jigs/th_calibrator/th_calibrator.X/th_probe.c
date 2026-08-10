#include "th_probe.h"
#include "i2c_master.h"

#include <string.h>   // memcpy

// ===== OSL 共通レジスタ (mlogger_th_sensor.X/i2c_shared_data.h と同期) =====
// 0x28-0x4B = POLL ブロック (status1/status2/reserved + value[8] float)
#define REG_POLL_BASE         0x28
#define POLL_BLOCK_SIZE       36

#define POLL_OFS_STATUS1      0
#define POLL_OFS_VALUE        4    // value[0] の先頭バイト (Little Endian float)

// 0x29 = status2 (親機が 0 を書くと single-shot 計測トリガ)
#define REG_STATUS2           0x29

// value 配列インデックス (子機 i2c_shared_data.h の VAL_IDX_* と同期)
#define VAL_IDX_TEMPERATURE   0
#define VAL_IDX_HUMIDITY      1
#define VAL_IDX_CO2           2

// Status1 stale ビット
#define STATUS1_STALE_T       (0x01)
#define STATUS1_STALE_RH      (0x02)
#define STATUS1_STALE_CO2     (0x04)

// 拡張領域 (子機 i2c_shared_data.h と同期): STCC4 校正コマンド制御
#define REG_STCC4_CMD         0x6C
#define REG_STCC4_CMD_ARG     0x6D    // uint16_t LE (0x6D-0x6E)
#define REG_STCC4_STATE       0x6F
#define REG_FRC_CORRECTION    0x70    // int16_t LE (0x70-0x71)

// STCC4 コマンド (子機 i2c_shared_data.h と同期)
#define STCC4_CMD_FRC         (0x01)


void ThProbe_Trigger(void)
{
    // REG_STATUS2 に 0 を書くと子機が single-shot 計測を開始する。
    // I2C 失敗 (子機不在) は無視 (Read 側で i2c_ok=false になる)。
    uint8_t buf[2] = { REG_STATUS2, 0 };
    (void)I2C_Write(TH_PROBE_ADDRESS, buf, 2);
}

void ThProbe_ReadSample(ThSample_t* s)
{
    s->i2c_ok    = false;
    s->t_valid   = false;
    s->rh_valid  = false;
    s->co2_valid = false;
    s->t_c100    = 0;
    s->rh_100    = 0;
    s->co2_ppm   = 0;
    s->status1   = 0xFF;
    s->status2   = 0;

    // POLL BLOCK (0x28-0x4B, 36B) を 1 トランザクションで読む。
    const uint8_t cmd = REG_POLL_BASE;
    uint8_t buffer[POLL_BLOCK_SIZE];
    if (!I2C_WriteRead(TH_PROBE_ADDRESS, &cmd, 1, buffer, POLL_BLOCK_SIZE)) return;
    s->i2c_ok = true;

    uint8_t status1 = buffer[POLL_OFS_STATUS1];
    s->status1 = status1;
    s->status2 = buffer[POLL_OFS_STATUS1 + 1];

    // value[0] = 乾球温度 [°C] → ℃*100 に丸め
    if (!(status1 & STATUS1_STALE_T)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_TEMPERATURE * 4], 4);
        if      (v < -327.0f) v = -327.0f;   // int16 範囲へクランプ
        else if (v >  327.0f) v =  327.0f;
        s->t_c100  = (int16_t)(v * 100.0f + (v >= 0 ? 0.5f : -0.5f));
        s->t_valid = true;
    }

    // value[1] = 相対湿度 [%RH] → %*100
    if (!(status1 & STATUS1_STALE_RH)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_HUMIDITY * 4], 4);
        if      (v < 0.0f)   v = 0.0f;
        else if (v > 100.0f) v = 100.0f;
        s->rh_100   = (uint16_t)(v * 100.0f + 0.5f);
        s->rh_valid = true;
    }

    // value[2] = CO2 濃度 [ppm] (float のまま受け取って uint16 へ丸める)
    if (!(status1 & STATUS1_STALE_CO2)) {
        float v;
        memcpy(&v, &buffer[POLL_OFS_VALUE + VAL_IDX_CO2 * 4], 4);
        if      (v < 0.0f)     v = 0.0f;
        else if (v > 65535.0f) v = 65535.0f;
        s->co2_ppm   = (uint16_t)(v + 0.5f);
        s->co2_valid = true;
    }
}

bool ThProbe_ReadStcc4State(uint8_t* state)
{
    const uint8_t cmd = REG_STCC4_STATE;
    return I2C_WriteRead(TH_PROBE_ADDRESS, &cmd, 1, state, 1);
}

bool ThProbe_StartFrc(uint16_t target_ppm)
{
    // 引数 → コマンドの順に書く (mlogger_main.X/th_probe.c と同じ手順)。
    uint8_t arg_buf[3] = {
        REG_STCC4_CMD_ARG,
        (uint8_t)(target_ppm & 0xFF),
        (uint8_t)((target_ppm >> 8) & 0xFF)
    };
    if (!I2C_Write(TH_PROBE_ADDRESS, arg_buf, 3)) return false;

    uint8_t cmd_buf[2] = { REG_STCC4_CMD, STCC4_CMD_FRC };
    return I2C_Write(TH_PROBE_ADDRESS, cmd_buf, 2);
}

bool ThProbe_ReadFrcCorrection(int16_t* corr)
{
    const uint8_t cmd = REG_FRC_CORRECTION;
    uint8_t buf[2] = { 0, 0 };
    if (!I2C_WriteRead(TH_PROBE_ADDRESS, &cmd, 1, buf, 2)) return false;
    memcpy(corr, buf, 2);
    return true;
}
