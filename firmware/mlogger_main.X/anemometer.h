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

// 起動する
void Anemometer_Wakeup(void);

// 休止する
void Anemometer_Sleep(void);

#ifdef	__cplusplus
}
#endif

#endif	/* ANEMOMETER_H */

