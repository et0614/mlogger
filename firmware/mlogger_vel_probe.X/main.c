 /*
 * AVR128DB32による風速計プログラム。
 * ブリッジ回路を応用した定温熱線式風速計の電圧を読み取り、風速に換算してI2C通信でデータを送受信する
*/

// <editor-fold defaultstate="collapsed" desc="include">

#include "mcc_generated_files/system/system.h"
#include "mcc_generated_files/adc/adc0.h"
#include "mcc_generated_files/system/pins.h"
#include "mcc_generated_files/timer/rtc.h"

#include "utility.h"
#include "i2c_slave.h"
#include "i2c_shared_data.h"
#include "smooth_filter.h"
#include "eeprom_manager.h"

#include <util/atomic.h>
#include <string.h>
#include <math.h>
#include <avr/sleep.h>

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="定数宣言">

#define VERSION_NUMBER  (1)

#define HEATING_MSEC    (5000)  //立ち上げのための白金抵抗予熱時間[msec]

#define INTERRUPT_MSEC  (20)    //タイマの割り込み時間間隔[msec]

#define TEMP_UPDATE_MSEC (1000) //温度の更新時間間隔[msec]

#define RESET_TIME (3000)       // Resetをかけるまでの時間 [msec]

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="変数定義">

//定期更新カウンタ
volatile uint8_t tick_count = 0;

volatile uint16_t reset_timer = RESET_TIME;

//立ち上げのための白金抵抗予熱時間[msec]
uint16_t heating_timer = HEATING_MSEC;

uint16_t temp_timer = TEMP_UPDATE_MSEC;

//風速電圧平滑化フィルタ
SmoothFilter velFilter;

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="温度測定">

// 温度測定
void updateTemperature()
{
    // チャンネルを温度センサに切り替える
    ADC0_ChannelSelect(ADC0_CHANNEL_TEMPSENSE);
    
    // サンプリング時間（風速計とは異なり高インピーダンスのため）
    ADC0.SAMPCTRL = 100;
    
    // 初期遅延(INITDLY)を設定：最大の 256 CLK_ADC を待機
    ADC0.CTRLD &= 0x1F;
    
    ADC0_ConversionStart(); // 変換開始
    while(!ADC0_IsConversionDone()); // 完了待ち (BUSYフラグが落ちるのを待つ)
    adc_accumulate_t acc_val = ADC0_AccumulatedResultGet(); // 結果を取得

    //符号反転を防ぎながらキャスト
    uint32_t safe_val = (uint32_t)acc_val;
    uint32_t voltage = safe_val * 2000UL / (4096UL * 16UL); // (12bit(4096), 128 回平均, 19bitになりAVRの仕様で3bit切り捨てられるので8で割られていることに等しい)
    
    // 2.048V基準時のADCカウント数に換算
    // 12bit ADC(4096)において 2048mV = 4096カウント なので 1mV = 2カウント
    int32_t adc_equiv = (int32_t)voltage * 2;

    // 5. 特性式を適用 (公式: T[K] = (ADC - Offset) * Gain / 4096)
    int32_t gain = (int32_t)SIGROW.TEMPSENSE0;   // 16bit Gain
    int32_t offset = (int32_t)SIGROW.TEMPSENSE1; // 16bit Offset
    
    // 0.1度単位でのケルビン温度を計算
    int32_t temp_k_x100 = ((offset - adc_equiv) * gain * 100) / 4096;
    
    uint8_t vol[2];
    vol[0] = (uint8_t)(temp_k_x100 >> 8);
    vol[1] = (uint8_t)(temp_k_x100 & 0xFF);
    uint8_t volCrc = calc_crc8(vol, sizeof(vol));
    
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        SharedMemory.reg.mcu_temp_high = vol[0];
        SharedMemory.reg.mcu_temp_low  = vol[1];
        SharedMemory.reg.mcu_temp_crc = volCrc;
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="風速測定関連">

// 風速を表す電圧のAD変換処理
uint32_t adcVelocityVoltage()
{
    if(!SharedMemory.reg.enable) return 0;
    
    //チャンネルを風速計に切り替える
    ADC0_ChannelSelect(ADC0_CHANNEL_AIN21);
    
    // サンプリング時間（小さくて良い）
    ADC0.SAMPCTRL = 0;
    
    // 初期遅延(INITDLY)を設定：0
    ADC0.CTRLD |= ADC_INITDLY_DLY0_gc;
    
    ADC0_ConversionStart(); // 変換開始
    while(!ADC0_IsConversionDone()); // 完了待ち (BUSYフラグが落ちるのを待つ)
    adc_accumulate_t acc_val = ADC0_AccumulatedResultGet(); // 結果を取得
    
    //符号反転を防ぎながらキャスト
    uint32_t safe_val = (uint32_t)acc_val;
    return  safe_val * 2000UL / (4096UL * 16UL); // (12bit(4096), 128 回平均, 19bitになりAVRの仕様で3bit切り捨てられるので8で割られていることに等しい)
}

// 風速を表す電圧を平滑化して更新
void updateVelocityVoltage()
{
    uint32_t voltage = adcVelocityVoltage();
    SF_Apply(&velFilter, voltage);

    uint8_t vol[2];
    vol[0] = (uint8_t) (velFilter.out_y >> 8);
    vol[1] = (uint8_t) (velFilter.out_y & 0xFF);
    uint8_t volCrc = calc_crc8(vol, sizeof(vol));

    //安全に書き込み
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        // ビッグエンディアンで保存(下位と上位を入れ替え)
        SharedMemory.reg.voltage_high = vol[0]; // Voltage High Byte
        SharedMemory.reg.voltage_low = vol[1]; // Voltage Low Byte
        SharedMemory.reg.voltage_crc = volCrc; // Voltage CRC
    }
}

// 風速を更新
void updateVelocity()
{
    //係数取得
    float coA[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(coA, (void*)SharedMemory.reg.coefficientA, sizeof(coA));
    }
    for(int i=0; i<5; i++) swap_float(&coA[i]);
    float coB[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(coB, (void*)SharedMemory.reg.coefficientB, sizeof(coB));
    }
    for(int i=0; i<5; i++) swap_float(&coB[i]);
    
    //特性式を適用
    float vol0 = coA[0]; //無風電圧[V]
    float ccA1 = coA[1];
    float ccB1 = coA[2];
    float ccA2 = coA[3];
    float ccB2 = coA[4];
    float vel_swt = coB[0]; //切替風速
    uint16_t mVolt = (SharedMemory.reg.voltage_high << 8) + (SharedMemory.reg.voltage_low);
    float evv = (0.001 * mVolt) * (0.001 * mVolt) - vol0 * vol0;
    float vel_f;
    //無風電圧以下は確定的に0m/s
    if(evv <= 0) vel_f = 0;
    else 
    {
        float lnev = log(evv);
        vel_f = exp(lnev * ccA1 + ccB1);
        if(vel_swt < vel_f) vel_f = exp(lnev * ccA2 + ccB2);
    }
    
    // mm/sに変換して16ビット整数化
    uint16_t vel_ui = (uint16_t) (vel_f * 1000);
    uint8_t vel[2];
    vel[0] = (uint8_t)(vel_ui >> 8);
    vel[1] = (uint8_t)(vel_ui & 0xFF);
    
    //CRC計算
    uint8_t velCrc = calc_crc8(vel, sizeof(vel));
    
    //安全に書き込み
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        SharedMemory.reg.velocity_high = vel[0]; // Velocity High Byte
        SharedMemory.reg.velocity_low = vel[1]; // Velocity Low Byte
        SharedMemory.reg.velocity_crc = velCrc; // Velocity CRC
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="main">

// 20msecタイマ用コールバック関数
void msecHandler(void) 
{
    if (tick_count < 255) tick_count++;
}

bool ledDbg = true;

int main(void)
{
    SYSTEM_Initialize();
        
    // EEPROM読み込み
    EM_loadEEPROM();
    
    // I2Cアドレスを反映
    TWI0.SADDR = SharedMemory.reg.i2c_address << 1;
    
    // デフォルトの設定
    SharedMemory.reg.version = VERSION_NUMBER;
    SharedMemory.reg.enable = 1;
    SharedMemory.reg.updated = 0;
    memcpy((void*)SharedMemory.reg.id, (const void*)&SIGROW.SERNUM0, 16); // AVR製造IDをコピー
    SharedMemory.reg.id_crc = calc_crc8((uint8_t*)SharedMemory.reg.id, 16);
    uint32_t hash = fnv1a_32((void*)SharedMemory.reg.id, sizeof(SharedMemory.reg.id));
    SharedMemory.reg.id_hash[0] = (uint8_t)(hash >> 24);
    SharedMemory.reg.id_hash[1] = (uint8_t)(hash >> 16);
    SharedMemory.reg.id_hash[2] = (uint8_t)(hash >> 8);
    SharedMemory.reg.id_hash[3] = (uint8_t)(hash & 0xFF);
    SharedMemory.reg.id_hash_crc = calc_crc8((uint8_t*)SharedMemory.reg.id_hash, 4);
    
     //風速回路起動
    SLP_SetHigh();
    
    // イベントハンドラ登録
    RTC_SetOVFIsrCallback(msecHandler);
    
    // I2C通信初期化
    I2C_Slave_Init();
    
    // 風速電圧平滑化フィルタの初期化
    SF_Init(&velFilter, SharedMemory.reg.filter_n, 0); //平滑化の目安：2^6*INTERRUPT_MSEC(1.28 sec)
        
    // スリープモード設定
    set_sleep_mode(SLEEP_MODE_IDLE);
    
    // 割り込み開始
    sei();
    
    uint8_t prev_enabled = SharedMemory.reg.enable;
    while(1)
    {        
        // マスタからの設定変 更リクエストを確認
        if (I2C_Config_Update_Requested) 
        {
            I2C_Config_Update_Requested = false;

            // スリープ状態変更
            if (prev_enabled != SharedMemory.reg.enable)
            {
                if(SharedMemory.reg.enable == 1)
                {                    
                    // 5V回路起動
                    SLP_SetHigh();
                    
                    // 立ち上げ時間をリセット
                    heating_timer = HEATING_MSEC;
                }
                // 5V回路停止
                else SLP_SetLow();
                
                // 状態を保存
                prev_enabled = SharedMemory.reg.enable;
            }            
            
            // 現在の出力値を初期値として、新しい n 数でフィルタを再初期化
            SF_Init(&velFilter, SharedMemory.reg.filter_n, velFilter.out_y);
            
            //EEPROMを更新
            EM_updateEEPROM();
        }
        
        //係数変更リクエストを確認
        if(I2C_Coefficient_Update_Requested)
        {
            I2C_Coefficient_Update_Requested = false;
            
            //EEPROMを更新
            EM_updateEEPROM();
        }
        
        // 定期更新処理
        if(tick_count)
        {
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) { tick_count--; }
            // I2C有効時間を更新
            if(0 < I2C_KeepAlive_Ticks)
            {
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) { 
                    I2C_KeepAlive_Ticks = (I2C_KeepAlive_Ticks < INTERRUPT_MSEC) ? 0 : (I2C_KeepAlive_Ticks - INTERRUPT_MSEC);
                }
            }
                        
            //リセット処理
            if(RST_SW_GetValue() == 0)
            {
                reset_timer = reset_timer < INTERRUPT_MSEC ? 0 : reset_timer - INTERRUPT_MSEC;
                if(reset_timer == 0)
                {
                    // EEPROMの初期化処理
                    EM_resetEEPROM();
                    reset_timer = RESET_TIME;
                }
            }
            else reset_timer = RESET_TIME;
            
            //残予熱時間を更新
            heating_timer = (heating_timer < INTERRUPT_MSEC) ? 0 : (heating_timer - INTERRUPT_MSEC);
            
            // ステータスレジスタの更新
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                if (heating_timer > 0) SharedMemory.reg.status |= STATUS_HEATING_MASK; // 予熱中
                else SharedMemory.reg.status &= ~STATUS_HEATING_MASK; // 予熱完了
            }
                        
            //計測準備が整っていれば
            if (SharedMemory.reg.enable == 1 && heating_timer == 0) 
            {
                // 風速電圧[mV]を更新
                updateVelocityVoltage();
                
                // 風速[mm/s]を更新
                updateVelocity();
                
                // 計測フラグ更新
                SharedMemory.reg.updated = 1;
            }
            
            // 温度更新
            temp_timer = (temp_timer < INTERRUPT_MSEC) ? 0 : (temp_timer - INTERRUPT_MSEC);
            if(temp_timer == 0)
            {
                updateTemperature();
                temp_timer = TEMP_UPDATE_MSEC;
            }
        }
        
        // スリープモードを選択して実行
        if(SharedMemory.reg.enable == 1 || I2C_Is_Busy || 0 < I2C_KeepAlive_Ticks) set_sleep_mode(SLEEP_MODE_IDLE);
        else set_sleep_mode(SLEEP_MODE_PWR_DOWN);
        sleep_mode();
    }    
}

// </editor-fold>