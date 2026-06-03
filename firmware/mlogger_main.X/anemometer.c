#include "anemometer.h"
#include "i2c_master.h"

#include <string.h>   // memcpy

// I2C アドレス (poem_velocity_sensor の DEFAULT_I2C_ADDRESS と同期)
#define ANEMO_ADDRESS     0x10

// ===== OSL 共通レジスタ (poem_velocity_sensor/i2c_shared_data.h と同期) =====
// 0x28-0x4B = 動的情報 (POLL BLOCK)。1 トランザクションで読む。
#define REG_POLL_BASE     0x28   // status1 から読み始めて value 配列までを一括取得
#define POLL_BLOCK_SIZE   36     // status1(1) + status2(1) + reserved(2) + value[8](32)

// POLL ブロック内オフセット
#define POLL_OFS_STATUS1  0
#define POLL_OFS_STATUS2  1
#define POLL_OFS_VALUE    4      // value[0] の先頭バイト (Little Endian float)

// poem 子機の value 配列インデックス (i2c_shared_data.h の VAL_IDX_* と同期)
#define VAL_IDX_VELOCITY  0      // value[0] = 風速 [m/s]
#define VAL_IDX_VOLTAGE   1      // value[1] = 風速計回路の生電圧 [V]

// Status1: bit i (i < data_count) = value[i] が stale / 異常
#define STATUS1_VELOCITY_BAD   (1 << VAL_IDX_VELOCITY)
#define STATUS1_VOLTAGE_BAD    (1 << VAL_IDX_VOLTAGE)

// ===== poem 拡張領域 (0x4C-、子機固有) =====
#define REG_EXT_ENABLE    0x4C   // 風速計回路の起動フラグ
#define REG_EXT_FILTER_N  0x4D   // 平滑化フィルタ係数

// 平滑化フィルタの初期値 (旧実装と同じ)
#define FILTER_N_DEFAULT  6


// <editor-fold defaultstate="collapsed" desc="公開関数の実装">

void Anemometer_Init(Anemometer_t* anemo) {
    anemo->adc_value = 0;
    anemo->wind_speed_mps = 0.0f;
    anemo->i2c_ok = false;
    anemo->voltage_valid = false;
    anemo->wind_valid = false;

    // 平滑化フィルタ係数を設定 (拡張領域への 1byte 書き込み)
    uint8_t writeBuffer[2];
    writeBuffer[0] = REG_EXT_FILTER_N;
    writeBuffer[1] = FILTER_N_DEFAULT;
    I2C_Write(ANEMO_ADDRESS, writeBuffer, 2);
}

void Anemometer_Update(Anemometer_t* anemo) {
    // POLL BLOCK (0x28-0x4B, 36B) を 1 トランザクションで読む。
    // 内訳: status1(1B) + status2(1B) + reserved(2B) + value[8] float (32B)
    const uint8_t cmd = REG_POLL_BASE;
    uint8_t buffer[POLL_BLOCK_SIZE];
    bool ok = I2C_WriteRead(ANEMO_ADDRESS, &cmd, 1, buffer, POLL_BLOCK_SIZE);

    // 子機が物理的に外れている等で I2C が失敗したら全部 invalid。
    // 過去値もそのまま残すと誤検出になるので、ここで明示的に invalid 化する。
    // i2c_ok は dc 判定 (probe 物理切断) 用、valid 系は warmup と切断の区別に使う。
    anemo->i2c_ok = ok;
    if (!ok) {
        anemo->voltage_valid = false;
        anemo->wind_valid = false;
        return;
    }

    uint8_t status1 = buffer[POLL_OFS_STATUS1];

    // value[0] = 風速 [m/s]
    if (!(status1 & STATUS1_VELOCITY_BAD)) {
        float velocity_mps;
        memcpy(&velocity_mps, &buffer[POLL_OFS_VALUE + VAL_IDX_VELOCITY * 4], 4);
        anemo->wind_speed_mps = velocity_mps;
        anemo->wind_valid = true;
    } else {
        anemo->wind_valid = false;
    }

    // value[1] = 風速計回路の生電圧 [V]
    // 旧 API 互換のため adc_value は mV 単位 uint16 で保持する (旧 vel_probe は mV を直接返していた)。
    if (!(status1 & STATUS1_VOLTAGE_BAD)) {
        float voltage_v;
        memcpy(&voltage_v, &buffer[POLL_OFS_VALUE + VAL_IDX_VOLTAGE * 4], 4);
        if (voltage_v < 0.0f)       voltage_v = 0.0f;
        else if (voltage_v > 65.0f) voltage_v = 65.0f;   // uint16 max / 1000 = 65.535
        anemo->adc_value = (uint16_t)(voltage_v * 1000.0f);
        anemo->voltage_valid = true;
    } else {
        anemo->voltage_valid = false;
    }
}

void Anemometer_Wakeup(void) {
    uint8_t writeBuffer[2];
    writeBuffer[0] = REG_EXT_ENABLE;
    writeBuffer[1] = 1;
    I2C_Write(ANEMO_ADDRESS, writeBuffer, 2);
}

void Anemometer_Sleep(void) {
    uint8_t writeBuffer[2];
    writeBuffer[0] = REG_EXT_ENABLE;
    writeBuffer[1] = 0;
    I2C_Write(ANEMO_ADDRESS, writeBuffer, 2);
}

// </editor-fold>
