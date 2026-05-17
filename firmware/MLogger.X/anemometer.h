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
} Anemometer_t;

// --- 公開関数プロトタイプ宣言 ---

// 初期化
void Anemometer_Init(Anemometer_t* anemo);

// 0.1秒ごとに呼ぶ更新関数
void Anemometer_Update(Anemometer_t* anemo);

// 起動する
void Anemometer_Wakeup();

// 休止する
void Anemometer_Sleep();

#ifdef	__cplusplus
}
#endif

#endif	/* ANEMOMETER_H */

