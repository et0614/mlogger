#include "tca9548a.h"
#include "i2c_master.h"
#include "mcc_generated_files/system/pins.h"
#include "mcc_generated_files/timer/delay.h"

// TCA9548A リセット時間:
//   datasheet: t_WL (Reset pulse duration) min = 6 ns、t_RESET (Time to reset) max = 6 ns。
//   MCU の GPIO 遅延 + マージンとして 1 us も待てば十分。DELAY_microseconds は最短 1us なので採用。
#define TCA_RESET_PULSE_US   1

// mux 番号 (0..7) → RST ピン制御。基板配線: RST1 = mux #0, ..., RST8 = mux #7。
// マクロ (関数ポインタ化できない) なので switch で分岐する。
static void rst_pin_low(uint8_t mux)
{
    switch (mux) {
        case 0: RST1_SetLow(); break;
        case 1: RST2_SetLow(); break;
        case 2: RST3_SetLow(); break;
        case 3: RST4_SetLow(); break;
        case 4: RST5_SetLow(); break;
        case 5: RST6_SetLow(); break;
        case 6: RST7_SetLow(); break;
        case 7: RST8_SetLow(); break;
        default: break;
    }
}

static void rst_pin_high(uint8_t mux)
{
    switch (mux) {
        case 0: RST1_SetHigh(); break;
        case 1: RST2_SetHigh(); break;
        case 2: RST3_SetHigh(); break;
        case 3: RST4_SetHigh(); break;
        case 4: RST5_SetHigh(); break;
        case 5: RST6_SetHigh(); break;
        case 6: RST7_SetHigh(); break;
        case 7: RST8_SetHigh(); break;
        default: break;
    }
}


void Tca_ResetAll(void)
{
    // 8 本の RST を同時に low → 1us 保持 → 同時に high。
    // 個別に SetLow/SetHigh を呼ぶと厳密には同時ではないが、TCA9548A の要求
    // (t_WL >= 6ns) はどちらの順でも十分満たされるので問題ない。
    for (uint8_t i = 0; i < TCA_MUX_COUNT; i++) rst_pin_low(i);
    DELAY_microseconds(TCA_RESET_PULSE_US);
    for (uint8_t i = 0; i < TCA_MUX_COUNT; i++) rst_pin_high(i);
    // リセット完了は 6ns 以内。以降 I2C 可。
}

void Tca_Init(void)
{
    Tca_ResetAll();
    Tca_DeselectAll();
}

bool Tca_Select(uint8_t mux, uint8_t ch)
{
    if (mux >= TCA_MUX_COUNT || ch >= TCA_CH_PER_MUX) return false;
    uint8_t mask = (uint8_t)(1u << ch);
    return I2C_WriteByteAndStop((uint8_t)(TCA_ADDR_BASE + mux), mask);
}

bool Tca_Deselect(uint8_t mux)
{
    if (mux >= TCA_MUX_COUNT) return false;
    return I2C_WriteByteAndStop((uint8_t)(TCA_ADDR_BASE + mux), 0x00);
}

void Tca_DeselectAll(void)
{
    for (uint8_t i = 0; i < TCA_MUX_COUNT; i++) (void)Tca_Deselect(i);
}

void Tca_SetReset(uint8_t mux, bool assert_reset)
{
    if (mux >= TCA_MUX_COUNT) return;
    if (assert_reset) rst_pin_low(mux);
    else              rst_pin_high(mux);
}
