 /*
 * AVR128DB32 による風速計子機 (PoEM-pod 用 OSL 子機)。
 * 定温熱線式風速計の電圧を読み取り、風速 [m/s] に換算して
 * 共通レジスタ仕様 (SHASE 2026 第2報) で I2C 経由に提供する。
 *
 * value[0] = 風速 [m/s]   (BACnet unit 74)
 * value[1] = 生電圧 [V]   (BACnet unit 5)
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

#define HEATING_MSEC    (5000)  // 白金抵抗予熱時間 [msec]
#define INTERRUPT_MSEC  (20)    // タイマ割り込み間隔 [msec]
#define RESET_TIME      (3000)  // Reset 押し続け時間 [msec]

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="変数定義">

volatile uint8_t  tick_count = 0;
volatile uint16_t reset_timer = RESET_TIME;
uint16_t heating_timer = HEATING_MSEC;

// 風速電圧平滑化フィルタ
SmoothFilter velFilter;

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="風速測定">

// 風速計回路の出力電圧を AD 変換 (mV, 0-2000)
static uint32_t adcVelocityVoltage(void)
{
    if (!SharedMemory.reg.enable) return 0;

    ADC0_ChannelSelect(ADC0_CHANNEL_AIN21);
    ADC0.SAMPCTRL = 0;
    ADC0.CTRLD |= ADC_INITDLY_DLY0_gc;
    ADC0_ConversionStart();
    while (!ADC0_IsConversionDone());
    adc_accumulate_t acc_val = ADC0_AccumulatedResultGet();

    uint32_t safe_val = (uint32_t)acc_val;
    // 12bit (4096), 128回平均 -> AVR の仕様で3bit切り捨てられるので 16 で割られる
    return safe_val * 2000UL / (4096UL * 16UL);
}

// 風速電圧 [V] を平滑化して value[1] に書き込み、フィルタ出力 [V] を返す
static float updateVelocityVoltage(void)
{
    uint32_t voltage_mv = adcVelocityVoltage();
    SF_Apply(&velFilter, (int32_t)voltage_mv);

    float voltage_v = (float)velFilter.out_y * 0.001f;

    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        SharedMemory.reg.value[VAL_IDX_VOLTAGE] = voltage_v;
    }
    return voltage_v;
}

// 電圧 [V] から風速 [m/s] を計算して value[0] に書き込み
static void updateVelocity(float voltage_v)
{
    // 補正係数取得 (native LE なのでそのまま memcpy)
    float coA[5];
    float coB[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(coA, (const void*)SharedMemory.reg.coefficientA, sizeof(coA));
        memcpy(coB, (const void*)SharedMemory.reg.coefficientB, sizeof(coB));
    }

    // King の法則 v = exp(m * ln(E^2 - E0^2) + lnC) を 3 区分でフィット。
    // coA = [E0, m1, lnC1, m2, lnC2] / coB = [m3, lnC3, v_split1, v_split2, _]
    float vol0     = coA[0]; // 無風電圧 [V]
    float m1       = coA[1];
    float lnC1     = coA[2];
    float m2       = coA[3];
    float lnC2     = coA[4];
    float m3       = coB[0];
    float lnC3     = coB[1];
    float v_split1 = coB[2]; // [m/s]
    float v_split2 = coB[3]; // [m/s]

    float evv = voltage_v * voltage_v - vol0 * vol0;
    float vel_f;
    if (evv <= 0.0f) {
        vel_f = 0.0f;
    } else {
        float lnev = logf(evv);
        vel_f = expf(lnev * m1 + lnC1);
        if (v_split1 <= vel_f) {
            vel_f = expf(lnev * m2 + lnC2);
            if (v_split2 <= vel_f) vel_f = expf(lnev * m3 + lnC3);
        }
    }

    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        SharedMemory.reg.value[VAL_IDX_VELOCITY] = vel_f;
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="main">

// 20msec タイマ用コールバック
void msecHandler(void)
{
    if (tick_count < 255) tick_count++;
}

int main(void)
{
    SYSTEM_Initialize();

    // EEPROM 読み込み (filter_n / coefficient* / new_addr が確定する)
    EM_loadEEPROM();

    // I2C アドレスを反映 (new_addr に EEPROM 値が入っている)
    TWI0.SADDR = SharedMemory.reg.new_addr << 1;

    // ===== OSL 共通レジスタの初期化 ===========================================
    // Device ID = SIGROW.SERNUM の FNV-1a ハッシュ (22-bit fold)。
    // 22-bit に揃えることで BACnet Object Instance としてそのまま使える。
    {
        uint8_t serial[16];
        memcpy(serial, (const void*)&SIGROW.SERNUM0, sizeof(serial));
        SharedMemory.reg.device_id = fnv1a_22(serial, sizeof(serial));
    }
    SharedMemory.reg.addr_key      = 0x00;
    SharedMemory.reg.data_count    = 2;
    SharedMemory.reg.unit_type[0]  = UNIT_METERS_PER_SECOND;
    SharedMemory.reg.unit_type[1]  = UNIT_VOLTS;
    SharedMemory.reg.status1       = 0;
    SharedMemory.reg.status2       = 0;
    for (uint8_t i = 0; i < 8; i++) SharedMemory.reg.value[i] = 0.0f;

    // ===== 拡張領域の初期化 ===================================================
    SharedMemory.reg.enable = 1;

    // 風速回路起動
    SLP_SetHigh();

    // イベントハンドラ登録
    RTC_SetOVFIsrCallback(msecHandler);

    // I2C 通信初期化
    I2C_Slave_Init();

    // 風速電圧平滑化フィルタの初期化
    SF_Init(&velFilter, SharedMemory.reg.filter_n, 0);

    set_sleep_mode(SLEEP_MODE_IDLE);
    sei();

    uint8_t prev_enabled = SharedMemory.reg.enable;
    while (1)
    {
        // マスタからの設定変更リクエスト
        if (I2C_Config_Update_Requested)
        {
            I2C_Config_Update_Requested = false;

            // enable 状態変化
            if (prev_enabled != SharedMemory.reg.enable)
            {
                if (SharedMemory.reg.enable == 1) {
                    SLP_SetHigh();
                    heating_timer = HEATING_MSEC;
                } else {
                    SLP_SetLow();
                }
                prev_enabled = SharedMemory.reg.enable;
            }

            // 新しい n 数でフィルタ再初期化
            SF_Init(&velFilter, SharedMemory.reg.filter_n, velFilter.out_y);

            EM_updateEEPROM();
        }

        // 係数変更リクエスト
        if (I2C_Coefficient_Update_Requested)
        {
            I2C_Coefficient_Update_Requested = false;
            EM_updateEEPROM();
        }

        // 定期更新処理
        if (tick_count)
        {
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) { tick_count--; }

            // I2C keepalive
            if (0 < I2C_KeepAlive_Ticks)
            {
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    I2C_KeepAlive_Ticks = (I2C_KeepAlive_Ticks < INTERRUPT_MSEC) ? 0 : (I2C_KeepAlive_Ticks - INTERRUPT_MSEC);
                }
            }

            // Reset スイッチ長押し -> EEPROM 初期化
            if (RST_SW_GetValue() == 0)
            {
                reset_timer = reset_timer < INTERRUPT_MSEC ? 0 : reset_timer - INTERRUPT_MSEC;
                if (reset_timer == 0)
                {
                    EM_resetEEPROM();
                    reset_timer = RESET_TIME;
                }
            }
            else reset_timer = RESET_TIME;

            // 残予熱時間
            heating_timer = (heating_timer < INTERRUPT_MSEC) ? 0 : (heating_timer - INTERRUPT_MSEC);

            // Status1 更新 (予熱中は両計測値とも stale)
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                if (heating_timer > 0) SharedMemory.reg.status1 = STATUS1_ALL_STALE;
                else                    SharedMemory.reg.status1 = 0x00;
            }

            // 計測準備が整っていれば計測
            if (SharedMemory.reg.enable == 1 && heating_timer == 0)
            {
                float voltage_v = updateVelocityVoltage();
                updateVelocity(voltage_v);

                // 計測値更新フラグを立てる (親機が読み取り後に 0 を書き戻す)
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    SharedMemory.reg.status2 = 1;
                }
            }
        }

        // スリープモードを選択
        if (SharedMemory.reg.enable == 1 || I2C_Is_Busy || 0 < I2C_KeepAlive_Ticks) {
            set_sleep_mode(SLEEP_MODE_IDLE);
        } else {
            set_sleep_mode(SLEEP_MODE_PWR_DOWN);
        }
        sleep_mode();
    }
}

// </editor-fold>
