#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/timer/delay.h"
#include "adc0_extension.h"
#include <avr/io.h>

void ADC0_SetReferenceVoltage(adc_custom_ref_t ref)
{
    // 一旦 REFSELビット(下位3ビット)をクリアするためのマスク
    // ADC_REFSEL_gm は通常 0x07
    uint8_t new_ctrlc = ADC0.CTRLC & ~ADC_REFSEL_gm;
    
    // 内部 1.024V (INTERNAL) を選択
    if (ref == ADC_VREF_INT_1V)
    {        
        new_ctrlc |= ADC_REFSEL_1V024_gc;
        ADC0.CTRLC = new_ctrlc;
        DELAY_microseconds(50); // データシート推奨の待機時間 (Start-up time)
    }
    // VDDを選択
    else if (ref == ADC_VREF_VDD) {
        new_ctrlc |= ADC_REFSEL_VDD_gc;
        ADC0.CTRLC = new_ctrlc;
        DELAY_microseconds(5); // VDD参照への切り替えは高速だが念のため
    }
    // 外部 VREFA (EXTERNAL) を選択
    else
    {
        new_ctrlc |= ADC_REFSEL_VREFA_gc;
        ADC0.CTRLC = new_ctrlc;
        DELAY_microseconds(5);  // 外部参照の場合は基本的にWait不要だが、念のため
    }
}