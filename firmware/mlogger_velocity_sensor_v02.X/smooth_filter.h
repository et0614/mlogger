/* 
 * File:   smooth_filter.h
 * Author: e.togashi
 *
 * Created on January 14, 2026, 10:38 AM
 */

#ifndef SMOOTH_FILTER_H
#define	SMOOTH_FILTER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
    
// フィルタ構造体
typedef struct {
    int32_t acc;
    int32_t denom;
    int32_t out_y;
} SmoothFilter;

/**
 * @brief フィルタ構造体の初期化
 * 指定されたフィルタ係数 n に基づき、計算用の分母を事前計算します。
 * また、次回の Apply 実行時に立ち上がり遅延が発生しないよう、初期化フラグをリセットします。
 * @param f フィルタ構造体へのポインタ
 * @param n フィルタ係数 (0〜20)。
 * @param x 初期値。
 * この値は「2^n サンプルの単純移動平均」に近い応答特性を指定します。
 * n=0でスルー（フィルタなし）、nが大きいほど平滑化が強くなります。
 */
void SF_Init(SmoothFilter *f, int32_t n, int32_t x);

/**
 * @brief フィルタの適用（1サンプル更新）
 * 指数移動平均(EMA)アルゴリズムを用い、入力値 x に対して平滑化を行い、フィルタ構造体を更新します。
 * 内部的には Y = k*X + (1-k)*Y_prev を整数演算でシミュレートします。
 * @param f フィルタ構造体へのポインタ
 * @param x 入力値 (0〜2000 mV)。
 * 内部での 32bit オーバーフローを防止するため、関数内で 0-2000 に制限されます。 
 * * @note 
 * - 初回呼び出し時は、応答の遅れを防ぐため、入力値をそのまま初期値として採用します。
 * - n=20 の設定かつ入力 2000mV の時に int32_t の限界値（約21億）付近に達するため、
 * 入力値の範囲制限は計算の安全性を保つために必須です。
 */
void SF_Apply(SmoothFilter *f, int32_t x);

#ifdef	__cplusplus
}
#endif

#endif	/* SMOOTH_FILTER_H */

