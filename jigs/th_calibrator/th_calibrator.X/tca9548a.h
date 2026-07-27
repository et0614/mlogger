/*
 * File:   tca9548a.h
 * Author: e.togashi
 *
 * TI TCA9548A × 8 個を束ねる I2C mux ドライバ (th_calibrator 治具用)。
 *
 *   親 I2C バス (SDA/SCL, PA2/PA3) の下に mux #0..#7 が並列に繋がり、
 *   各 mux は 8 ch のサブ I2C バスを提供する (= 8×8 = 64 スロット)。
 *
 * mux アドレス配置 (基板配線上の前提):
 *   mux #i の 7bit I2C アドレス = TCA_ADDR_BASE + i     (0x70..0x77)
 *   すなわち mux 側の A0-A2 は基板上で mux 番号と 1 対 1 でアサインされている。
 *
 * RST 配線:
 *   MCC 生成の RST1..RST8 マクロがそれぞれ mux #0..#7 の /RESET に接続されている前提。
 *   (基板の RST1 は「1 番目」 = mux #0 と対応する慣例)
 *
 * 使い方:
 *   Tca_Init();                       // 起動時 1 回。RST 一括叩き + 全 mux disable。
 *   Tca_Select(mux, ch);              // ch を切替 → 以降の I2C_* はそのサブバスへ届く
 *   I2C_WriteRead(...);               // th_sensor 等を叩く
 *   Tca_Deselect(mux);                // 使い終わったら disable (バス衝突防止)
 */

#ifndef TCA9548A_H
#define TCA9548A_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

// mux 数と 1 mux あたり ch 数
#define TCA_MUX_COUNT   8
#define TCA_CH_PER_MUX  8

// mux #0 の I2C アドレス (7bit)。以降 mux #i = TCA_ADDR_BASE + i。
#define TCA_ADDR_BASE   0x70

/// 全 mux をハードウェアリセット (RST1-8 を全て low→high) → 全 mux disable。
/// 起動時に 1 回呼ぶ。副作用: 呼び出し後は全サブバス切断状態。
void Tca_Init(void);

/// 全 mux をハードウェアリセットする (RST を low pulse)。バス固着復旧用。
void Tca_ResetAll(void);

/// mux (0..TCA_MUX_COUNT-1) の ch (0..TCA_CH_PER_MUX-1) を単独選択する。
/// 同じ mux の他 ch は自動的に disable される (1 byte write の bit フィールド仕様)。
/// @return true: I2C ACK OK、false: mux 無応答 / mux番号や ch番号が範囲外
bool Tca_Select(uint8_t mux, uint8_t ch);

/// 指定 mux の全 ch を disable (制御レジスタに 0x00 を書く)。
/// @return true: I2C ACK OK、false: mux 無応答 / mux番号範囲外
bool Tca_Deselect(uint8_t mux);

/// 全 mux を disable する (バスをクリーンな状態に戻す)。
void Tca_DeselectAll(void);

#ifdef __cplusplus
}
#endif

#endif /* TCA9548A_H */
