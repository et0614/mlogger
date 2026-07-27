/**
 * @file main.c
 * @brief AVR64DU32 を使った th_calibrator 治具の骨格 (mlogger_th_sensor 一括校正)
 * @author E.Togashi
 *
 * 目的:
 *   8 個の TCA9548A I2C mux で 8ch × 8mux = 最大 64 台の mlogger_th_sensor を束ねて
 *   同一環境 (恒温恒湿槽 / CO2 一定環境) に放置しながら値を内蔵 SPI フラッシュに記録し、
 *   工場出荷校正の基準として使う。
 *
 * 動作モード (元 mlogger_th_calibrator と同じ骨格):
 *   USB通信モード (起動時の既定, isLogging=false):
 *     USBをポーリングしてPCコマンドを処理。LEDは1秒ごとに2回点滅(ハートビート)。
 *   ロギングモード (start_loggingで移行, isLogging=true):
 *     STANDBYスリープ + PIT(1秒)で定期計測。省電力のためLEDは消灯のまま。
 *     USBは処理しない(電池駆動なので USB は disable する)。
 *   共通: RSTボタンを押している間はLED点灯。5秒以上の長押しでソフトウェアリセット
 *         → USB通信モードに戻る。
 *
 * 実装ステータス (Step 0):
 *   本 main は「LED + USB + RTC + Flash 骨格 + JSON プロトコル基盤」だけ動く空箱状態。
 *   計測ロジック (TCA9548A + th_sensor scan/read + flash write) は未実装。
 */

// <editor-fold defaultstate="collapsed" desc="ヘッダインクルード">

//自動生成ヘッダ
#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/system/system.h"
#include "mcc_generated_files/timer/delay.h"

//自作ヘッダ
#include "main.h"
#include "hal_io.h"
#include "eeprom_manager.h" //EEPROM
#include "usb_comm.h"       //USB-CDC通信 (PCコマンド)

//標準ヘッダ
#include <avr/io.h>        // RTC / RSTCTRL / PORT レジスタ, _PROTECTED_WRITE
#include <avr/wdt.h>       // _PROTECTED_WRITE 提供のため
#include <avr/sleep.h>     // set_sleep_mode / sleep_mode
#include <avr/interrupt.h> // sei / cli

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="広域変数定義">

//1秒毎の処理実行フラグ (PIT割り込みで立つ)
volatile bool process_logging_flag = false;

//リセットボタン押し込み継続タイマ[sec]
static uint8_t reset_timer = 0;

//計測(ロギング)モードか否か。false = USB通信モード(既定)。
static bool isLogging = false;

//ソフトウェア時計 (UNIX epoch秒)。set_timeコマンドで設定し、PIT(1秒)毎にインクリメント。
static uint32_t g_epoch = 0;

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="main">

int main(void)
{
    SYSTEM_Initialize();   // クロック / ピン(LED,RST,RST1-8) / I2C / SPI / USB / RTC 等

    // SPIモジュールを有効化 (内蔵フラッシュ W25Q64 用)
    SPI0_Open(SPI0_DEFAULT);

    //EEPROMロード (計測設定・start_dt 等)
    EM_loadEEPROM();

    // PIT(1秒)割り込みコールバック登録
    RTC_SetPITIsrCallback(oneSecHandler);

    // RSTボタン(PA0)を両エッジ割り込みに設定し、押下/解放へ即時反応させる(LED点灯/消灯)。
    PORTA.PIN0CTRL = (PORTA.PIN0CTRL & ~PORT_ISC_gm) | PORT_ISC_BOTHEDGES_gc;
    RST_SetInterruptHandler(rstButtonHandler);

    // ロギング中のSTANDBYスリープでもPITで毎秒起床できるよう RUNSTDBY を有効化。
    while (RTC.STATUS > 0) ;
    RTC.CTRLA |= RTC_RUNSTDBY_bm;

    DELAY_milliseconds(100);
    sei();
    DELAY_milliseconds(100);

    // USB仮想シリアル通信の初期化 (CDC層。USBデバイス層はSYSTEM_Initializeで初期化済み)
    USB_Comm_Initialize();

    while (true)
    {
        //1秒毎の処理 (PITで process_logging_flag が立つ)
        if(process_logging_flag)
        {
            process_logging_flag = false;
            executeSecondlyTask();
        }

        if(isLogging)
        {
            // ロギングモード: STANDBYスリープし、PIT(1秒)で起床して計測する。
            // USBは処理しない(電池駆動での省電力のため)。
            set_sleep_mode(SLEEP_MODE_STANDBY);
            sleep_mode();
        }
        else
        {
            // USB通信モード: USBをポーリングしてコマンドを処理する。
            USB_Comm_Task();
        }
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="割り込みコールバック関数">

// 1秒ごとのコールバック関数 (PIT)
void oneSecHandler(void)
{
    process_logging_flag = true;
}

// RSTボタンのエッジ割り込み (PA0両エッジ)。押下/解放へ即時反応してLEDを点灯/消灯する。
// (5秒長押しによるリセット判定は executeSecondlyTask のポーリングで行う)
void rstButtonHandler(void)
{
    if (!RST_GetValue()) turnOnGreenLED();  // 押し込み(Low) → 点灯
    else                 turnOffGreenLED(); // 解放(High)   → 消灯
}

// </editor-fold>

void executeSecondlyTask(void)
{
    //ソフトウェア時計を1秒進める
    g_epoch++;

    // --- RSTボタン処理 & LED表示 -----------------------------------------
    //  ・押している間 : LED点灯 (計測できているか/生存の確認用)
    //  ・5秒以上長押し: ソフトウェアリセット → USB通信モードに戻る
    //  ・非押し込み時 : USB通信モード=1秒ごとに2回点滅 / ロギングモード=消灯
    if (!RST_GetValue())  // 押し込み中 (アクティブLow)
    {
        turnOnGreenLED();
        reset_timer++;
        if (reset_timer >= 5)
        {
            while (!RST_GetValue()) ; // ボタンが離されるまで待ってからリセット
            _PROTECTED_WRITE(RSTCTRL.SWRR, RSTCTRL_SWRST_bm);
        }
    }
    else
    {
        reset_timer = 0;
        if (isLogging) turnOffGreenLED();          // 計測モード: 消灯
        else           blinkGreenLEDHeartbeat();   // USB通信モード: 2回点滅
    }

    // TODO(Step 2+): ロギング中の計測処理 (TCA9548A ch 巡回 → th_sensor read → flash 書込)
}

// ロギング(計測)の開始/停止。USBコマンド(start_logging/stop_logging)から呼ばれる。
void setLoggingActive(bool on)
{
    isLogging = on;
}

bool getLoggingActive(void)
{
    return isLogging;
}

// ソフトウェア時計 (UNIX epoch秒) の設定/取得。set_time / get_status から使う。
void Clock_Set(uint32_t epoch) { g_epoch = epoch; }
uint32_t Clock_Get(void)       { return g_epoch; }

// エラー表示 (骨格用): 緑LEDを3秒ごとに1回点滅し続ける (停止状態)。
void showError(short int errNum)
{
    (void)errNum;
    while(true)
    {
        blinkGreenLED(1);
        DELAY_milliseconds(3000);
    }
}
