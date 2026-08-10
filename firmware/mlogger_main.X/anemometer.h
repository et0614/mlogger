/* 
 * File:   anemometer.h
 * Author: etoga
 *
 * Created on January 7, 2026, 10:58 AM
 */

#ifndef ANEMOMETER_H
#define	ANEMOMETER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

// 風速計の状態を管理する構造体
typedef struct {
    uint16_t adc_value;         // ADC値 [mV]
    float    wind_speed_mps;    // 風速 (m/s)
    bool     i2c_ok;            // 直近 Update で子機との I2C 通信が成功したか
                                // (probe 物理切断検知用。warmup 中も true になる)
    bool     voltage_valid;     // 直近 Update で adc_value が有効か (I2C 成功 & status1 OK)
    bool     wind_valid;        // 直近 Update で wind_speed_mps が有効か (同上)
} Anemometer_t;

// --- 公開関数プロトタイプ宣言 ---

// 初期化
void Anemometer_Init(Anemometer_t* anemo);

// 0.1秒ごとに呼ぶ更新関数
void Anemometer_Update(Anemometer_t* anemo);

// INFO BLOCK から個体識別情報を取得する (出荷検査・診断用)。
// device_id: FNV-1a 22bit ID、data_count: 有効計測値数 (=2)、name: 装置ラベル (NUL 終端保証)
// 戻り値 false = I2C 失敗 (プローブ未接続)
bool Anemometer_ReadInfo(uint32_t *device_id, uint8_t *data_count, char name[17]);

// 起動する
void Anemometer_Wakeup(void);

// 休止する
void Anemometer_Sleep(void);

#ifdef	__cplusplus
}
#endif

#endif	/* ANEMOMETER_H */

