/* 
 * File:   smAverage.h
 * Author: e.togashi
 *
 * Created on 2025/12/14, 12:03
 */

#ifndef SMAVERAGE_H
#define	SMAVERAGE_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
#include <string.h> // memset用

// 平均化するサンプル数
#define SMA_WINDOW_SIZE 10

/**
 * @brief 移動平均用構造体
 * クラスのメンバ変数をここに格納
 */
typedef struct {
    uint16_t buffer[SMA_WINDOW_SIZE]; // データバッファ
    uint32_t currentSum;              // 合計値 (オーバーフロー防止のため32bit)
    uint8_t  currentIndex;            // 現在のインデックス
    bool     bufferFilled;            // バッファが満杯になったかフラグ
} SmAverage;

/**
 * @brief 初期化関数
 * @param[out] self 対象の構造体へのポインタ
 */
static inline void SMA_Init(SmAverage* self) {
    self->currentSum = 0;
    self->currentIndex = 0;
    self->bufferFilled = false;
    // バッファを0クリア
    memset(self->buffer, 0, sizeof(self->buffer));
}

/**
 * @brief 新しいデータを追加し、状態を更新する
 * @param[in,out] self 対象の構造体へのポインタ
 * @param[in]     value 追加する新しい値
 */
static inline void SMA_Add(SmAverage* self, uint16_t value) {
    // 1. バッファから一番古い値を取得
    uint16_t oldData = self->buffer[self->currentIndex];

    // 2. 合計値を更新 (新しい値を足し、古い値を引く)
    self->currentSum = self->currentSum + value - oldData;

    // 3. バッファを更新 (新しい値で上書き)
    self->buffer[self->currentIndex] = value;

    // 4. インデックスを更新 (0-WINDOW_SIZEで巡回)
    self->currentIndex = (self->currentIndex + 1) % SMA_WINDOW_SIZE;

    // 5. バッファが一杯になったかチェック
    if (!self->bufferFilled && self->currentIndex == 0)
        self->bufferFilled = true;
}

/**
 * @brief 現在の移動平均値を取得
 * @param[in] self 対象の構造体へのポインタ
 * @return uint16_t 計算された移動平均値
 */
static inline uint16_t SMA_GetAverage(const SmAverage* self) {
    // まだデータがWINDOW_SIZE個溜まっていない（起動直後などの）場合
    if (!self->bufferFilled) {
        // まだ1つもデータが追加されていない場合
        if (self->currentIndex == 0) return 0;
        // 現在溜まっている個数で割る (四捨五入あり)
        else return (uint16_t)((self->currentSum + (self->currentIndex / 2)) / self->currentIndex);
    }
    // データがWINDOW_SIZE個溜まっている場合 (通常の動作)
    else
        return (uint16_t)((self->currentSum + (SMA_WINDOW_SIZE / 2)) / SMA_WINDOW_SIZE);
}

/**
 * @brief バッファが満たされたか確認する
 * @param[in] self 対象の構造体へのポインタ
 * @return true: 満杯, false: まだ
 */
static inline bool SMA_IsFilled(const SmAverage* self) {
    return self->bufferFilled;
}

#ifdef	__cplusplus
}
#endif

#endif	/* SMAVERAGE_H */

