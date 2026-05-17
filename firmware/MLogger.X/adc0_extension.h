/* 
 * File:   adc0_extension.h
 * Author: e.togashi
 *
 * Created on December 15, 2025, 2:22 PM
 */

#ifndef ADC0_EXTENSION_H
#define	ADC0_EXTENSION_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
#include "mcc_generated_files/adc/adc0.h"

// ユーザー定義の基準電圧リスト
typedef enum
{
    ADC_VREF_EXT_2V    = 0, // 外部2.0V (通常計測用)
    ADC_VREF_INT_1V    = 1, // 内部1.024V
    ADC_VREF_VDD       = 2  // VDD基準 (電池チェック用)
} adc_custom_ref_t;

// 拡張関数の宣言
void ADC0_SetReferenceVoltage(adc_custom_ref_t ref);

#ifdef	__cplusplus
}
#endif

#endif	/* ADC0_EXTENSION_H */

