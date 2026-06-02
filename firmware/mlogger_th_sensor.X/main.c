 /*
 * AVR128DB32 による温湿度 + CO2 + グローブ温度プローブ (PoEM-pod 用 OSL 子機)。
 *
 * 計測構成:
 *   value[0] T (乾球温度)   : STCC4 read_measurement の T フィールド
 *   value[1] RH (相対湿度)  : STCC4 read_measurement の RH フィールド
 *   value[2] CO2            : STCC4 read_measurement の CO2 フィールド
 *   value[3] T_glb (グローブ温度) : AVR TWI1 上の SHT4x (0x44)
 *
 * 配線:
 *   AVR TWI1 (master)
 *     ├── STCC4 (0x64)  ─── SCL_C/SDA_C ─── SHT4x_amb (0x44, STCC4 配下)
 *     └── SHT4x_glb (0x44, グローブ温度用、STCC4 のものとは別バス)
 *   AVR TWI0 (slave)    ─── PoEM-pod 親機からの共通レジスタアクセス
 *
 * 計測トリガモデル (1 サンプル遅延ゼロ):
 *   親機が status2 = 0 を書込 → 即 single-shot 計測を発行 → ~520ms 後に value[] 更新
 *   + status2 = 1 セット → 親機が status2+value を read → 計測点ジャストの値を取得
 *
 * STCC4 校正コマンド (拡張領域 REG_STCC4_CMD 経由):
 *   STCC4_CMD_FRC           : 30 sec 連続測定 → FRC 発行 (~35 sec)
 *   STCC4_CMD_FACTORY_RESET : 90 ms
 *   STCC4_CMD_CONDITIONING  : 22 sec
 *
 * 電力戦略:
 *   待機中 = POWER-DOWN (~1μA、TWI address match で wake)
 *   計測/校正中 = IDLE (TWI 受信を取りこぼさないため)
 *   STCC4 計測完了待ち (500ms) は busy-wait (簡素優先、後日 PIT で sleep 化検討)
 */

// <editor-fold defaultstate="collapsed" desc="include">

#include "mcc_generated_files/system/system.h"
#include "mcc_generated_files/system/pins.h"
#include "mcc_generated_files/timer/rtc.h"
#include "mcc_generated_files/timer/delay.h"
#include "mcc_generated_files/i2c_host/twi1.h"

#include "utility.h"
#include "sht4x.h"
#include "stcc4.h"
#include "i2c_master.h"
#include "i2c_slave.h"
#include "i2c_shared_data.h"
#include "eeprom_manager.h"

#include <util/atomic.h>
#include <string.h>
#include <avr/sleep.h>
#include <avr/wdt.h>
#include <stdbool.h>

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="消費電流計測用 テストモード">

// 消費電流計測用のテストモード切替。本番リリース時は TEST_MODE_NONE に戻すこと。
//   TEST_MODE_NONE   : 通常 firmware (本番動作)
//   TEST_MODE_SLEEP  : 初期化完了後 POWER-DOWN sleep に固定 (B モード測定用)
//                      WDT は disable する (POWER-DOWN だと wake できず WDT reset
//                      されて測定にならないため)。I2C ピンは pull-up (= VDD 直結)
//                      で外部からアクセスしない前提。
//   TEST_MODE_ACTIVE : measureOnce() を sleep 挟まず連続実行 (C モード測定用)
//                      STCC4 は exitSleep 〜 enterSleep を毎回繰り返す。WDT 有効。
#define TEST_MODE_NONE       0
#define TEST_MODE_SLEEP      1
#define TEST_MODE_ACTIVE     2

#ifndef TEST_MODE
#define TEST_MODE TEST_MODE_NONE
#endif

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="定数">

// グローブ用 SHT4x のアドレス (STCC4 配下の SHT4x_amb とは別バスなので衝突しない)
#define SHT4X_GLB_TYPE       SHT4_AD

// 校正コマンドの所要時間 (RTC 1 sec tick でカウント)
#define FRC_WARMUP_SECONDS   (30)
#define CONDITIONING_SECONDS (22)

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="状態">

typedef enum {
    PHASE_IDLE,                  // POWER-DOWN で待機、新トリガを待つ
    PHASE_MEASURING,             // 通常 single-shot 計測中
    PHASE_FRC_WARMUP,            // FRC 用 30 sec 連続測定中
    PHASE_FRC_APPLY,             // FRC コマンド発行 + 補正値読み取り
    PHASE_FACTORY_RESET,         // STCC4 factory_reset 実行中
    PHASE_CONDITIONING,          // STCC4 perform_conditioning 実行中 (22 sec)
} Phase;

static Phase    g_phase = PHASE_IDLE;
static uint16_t g_phase_timer = 0;   // PHASE_FRC_WARMUP / PHASE_CONDITIONING の 1 sec カウンタ
static uint16_t g_frc_target_ppm = 0;

// 1 秒ごとに立つ tick (RTC OVF で true)
volatile bool g_one_sec_tick = false;

static void secHandler(void)
{
    g_one_sec_tick = true;
    // I2C keep-alive 残時間を 1 秒分減算 (KEEP_ALIVE_DURATION = 1000 msec 前提)。
    // RTC OVF は POWER-DOWN では停止するが、その時点で I2C_KeepAlive_Ticks も
    // 意味を持たない (= 既に POWER-DOWN まで落ちている) ので問題ない。
    if (I2C_KeepAlive_Ticks > 0) {
        I2C_KeepAlive_Ticks = (I2C_KeepAlive_Ticks <= 1000) ? 0 : (I2C_KeepAlive_Ticks - 1000);
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="補正係数の atomic 取得">

static void readCoefs(uint8_t idx, float* a, float* b)
{
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        switch (idx) {
            case 0: *a = SharedMemory.reg.t_coef_a;     *b = SharedMemory.reg.t_coef_b;     break;
            case 1: *a = SharedMemory.reg.rh_coef_a;    *b = SharedMemory.reg.rh_coef_b;    break;
            case 2: *a = SharedMemory.reg.co2_coef_a;   *b = SharedMemory.reg.co2_coef_b;   break;
            case 3: *a = SharedMemory.reg.t_glb_coef_a; *b = SharedMemory.reg.t_glb_coef_b; break;
            default: *a = 1.0f; *b = 0.0f; break;
        }
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="計測処理">

// STCC4 から CO2/T/RH を読み、グローブ用 SHT4x から T_glb を読み、
// 補正をかけて SharedMemory.value[] に書き込む。
// 戻り値: status1 ビット (各値が stale なら立てる)
static uint8_t measureOnce(void)
{
    uint8_t  stale = 0;
    uint16_t co2_raw = 0;
    float    t_raw = 0.0f, rh_raw = 0.0f;
    float    t_glb = 0.0f, dummy_h = 0.0f;

    STCC4_exitSleep();

    if (!STCC4_measureSingleShot()) {
        STCC4_enterSleep();
        return STATUS1_ALL_STALE;
    }
    DELAY_milliseconds(500);  // single-shot 完了待ち

    bool ok_stcc4 = STCC4_readMeasurement(&co2_raw, &t_raw, &rh_raw);
    STCC4_enterSleep();

    if (!ok_stcc4) {
        stale |= STATUS1_STALE_T | STATUS1_STALE_RH | STATUS1_STALE_CO2;
    }

    // グローブ用 SHT4x (TWI1 上の別アドレス 0x44、STCC4 とは別系統)
    bool ok_glb = SHT4x_ReadValue(&t_glb, &dummy_h, SHT4X_GLB_TYPE);
    if (!ok_glb) stale |= STATUS1_STALE_T_GLB;

    // 補正適用
    float ca, cb;
    if (ok_stcc4) {
        readCoefs(0, &ca, &cb); float corr_t   = ca * t_raw  + cb;
        readCoefs(1, &ca, &cb); float corr_rh  = ca * rh_raw + cb;
        readCoefs(2, &ca, &cb); float corr_co2 = ca * (float)co2_raw + cb;
        ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
            SharedMemory.reg.value[VAL_IDX_TEMPERATURE] = corr_t;
            SharedMemory.reg.value[VAL_IDX_HUMIDITY]    = corr_rh;
            SharedMemory.reg.value[VAL_IDX_CO2]         = corr_co2;
        }
    }
    if (ok_glb) {
        readCoefs(3, &ca, &cb); float corr_glb = ca * t_glb + cb;
        ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
            SharedMemory.reg.value[VAL_IDX_GLOBE_TEMP] = corr_glb;
        }
    }
    return stale;
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="STCC4 校正コマンド state machine">

// PHASE_IDLE で stcc4_cmd != 0 を検出したら呼び出される。
// 該当コマンドの phase へ遷移し、stcc4_cmd を 0 にクリア、stcc4_state を *_RUNNING に。
static void startStcc4Cmd(uint8_t cmd, uint16_t arg)
{
    switch (cmd) {
        case STCC4_CMD_FRC:
            g_frc_target_ppm = arg;
            STCC4_exitSleep();
            if (STCC4_startContinuousMeasurement()) {
                g_phase = PHASE_FRC_WARMUP;
                g_phase_timer = 0;
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    SharedMemory.reg.stcc4_state = STCC4_STATE_FRC_RUNNING;
                }
            } else {
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    SharedMemory.reg.stcc4_state = STCC4_STATE_FRC_FAIL;
                }
                STCC4_enterSleep();
            }
            break;

        case STCC4_CMD_FACTORY_RESET: {
            STCC4_exitSleep();
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                SharedMemory.reg.stcc4_state = STCC4_STATE_FACTORY_RESET_RUNNING;
            }
            // 即時実行 (~90 ms ブロッキング)。完了後 IDLE 復帰
            bool fr_ok = STCC4_performFactoryReset();
            STCC4_enterSleep();
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                SharedMemory.reg.stcc4_state = fr_ok ? STCC4_STATE_FACTORY_RESET_DONE
                                                    : STCC4_STATE_FRC_FAIL;
            }
            g_phase = PHASE_IDLE;
            break;
        }

        case STCC4_CMD_CONDITIONING:
            STCC4_exitSleep();
            if (STCC4_performConditioning()) {
                g_phase = PHASE_CONDITIONING;
                g_phase_timer = 0;
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    SharedMemory.reg.stcc4_state = STCC4_STATE_CONDITIONING_RUNNING;
                }
            } else {
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    SharedMemory.reg.stcc4_state = STCC4_STATE_FRC_FAIL;
                }
                STCC4_enterSleep();
            }
            break;

        default:
            break;
    }

    // 受理したコマンドは自動クリア
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        SharedMemory.reg.stcc4_cmd = STCC4_CMD_NONE;
    }
}

// FRC warmup の最終局面で perform_forced_recalibration を発行して結果を読む
static void finishFrc(void)
{
    STCC4_stopContinuousMeasurement();
    // datasheet: stop 後 1200ms 待つ必要あり
    DELAY_milliseconds(1200);

    int16_t correction = 0;
    bool ok = STCC4_performForcedRecalibration(g_frc_target_ppm, &correction);

    STCC4_enterSleep();
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        SharedMemory.reg.frc_correction = correction;
        SharedMemory.reg.stcc4_state = (ok && correction != (int16_t)0xFFFF)
            ? STCC4_STATE_FRC_DONE
            : STCC4_STATE_FRC_FAIL;
    }
    g_phase = PHASE_IDLE;
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="main">

int main(void)
{
    SYSTEM_Initialize();
    TWI1_Initialize();        // STCC4 / SHT4x_glb 用 master

    EM_loadEEPROM();          // new_addr / 8 補正係数 / name を SharedMemory に反映

    TWI0.SADDR = SharedMemory.reg.new_addr << 1;

    // ===== 共通レジスタの初期化 =============================================
    {
        uint8_t serial[16];
        memcpy(serial, (const void*)&SIGROW.SERNUM0, sizeof(serial));
        SharedMemory.reg.device_id = fnv1a_22(serial, sizeof(serial));
    }
    SharedMemory.reg.addr_key      = 0x00;
    SharedMemory.reg.data_count    = 4;
    SharedMemory.reg.unit_type[0]  = UNIT_DEGREES_CELSIUS;        // value[0] = T
    SharedMemory.reg.unit_type[1]  = UNIT_PERCENT_RELATIVE_HUMID; // value[1] = RH
    SharedMemory.reg.unit_type[2]  = UNIT_PARTS_PER_MILLION;      // value[2] = CO2
    SharedMemory.reg.unit_type[3]  = UNIT_DEGREES_CELSIUS;        // value[3] = T_glb
    // 全 STALE で開始する。boot 直後 (CONDITIONING 開始前) に親機が POLL 読み出ししても
    // 「value=0 が valid」として誤受信されるのを防ぐ。最初の measureOnce / partial 読み出しで
    // 必要なビットだけ clear される。
    SharedMemory.reg.status1       = STATUS1_ALL_STALE;
    SharedMemory.reg.status2       = 0;
    // 起動時に自動で perform_conditioning を実行 (datasheet §1.1.3: 3 時間以上 idle
    // 後の感度低下を回復するための warmup、~22 秒)。main loop が最初の iteration
    // で PHASE_IDLE → cmd 検出 → CONDITIONING へ遷移する。これにより親機が初回計測
    // を要求するまでに STCC4 が warm-up を完了している可能性が高い。
    SharedMemory.reg.stcc4_cmd     = STCC4_CMD_CONDITIONING;
    SharedMemory.reg.stcc4_state   = STCC4_STATE_IDLE;
    SharedMemory.reg.frc_correction = 0;
    for (uint8_t i = 0; i < 8; i++) SharedMemory.reg.value[i] = 0.0f;

    // ===== センサ初期化 ====================================================
    STCC4_initialize();       // soft-reset (general call)、~10 ms 待機含む
    STCC4_enterSleep();       // 待機モード固定で開始 (single-shot 駆動)

    if (!SHT4x_Initialize(SHT4X_GLB_TYPE)) {
        SharedMemory.reg.status1 |= STATUS1_STALE_T_GLB;
    }

    // ===== タイマ・I2C slave 起動 ===========================================
    RTC_SetOVFIsrCallback(secHandler);   // 1 sec tick (RTC OVF, 32768/32768)
    I2C_Slave_Init();                    // TWI0 client

#if TEST_MODE == TEST_MODE_SLEEP
    // ----- 消費電流テスト: SLEEP モード -----
    // 全初期化完了後、WDT を disable してから POWER-DOWN 固定で停止。
    // POWER-DOWN だと TWI ADDR_MATCH 以外 wake できないため、master 接続なしでは
    // wdt_reset() を呼ぶ機会がない → WDT を一旦切る必要がある。
    // 計測中は SDA/SCL を VDD 直結すること (floating だと spurious wake する)。
    _PROTECTED_WRITE(WDT.CTRLA, 0);
    set_sleep_mode(SLEEP_MODE_PWR_DOWN);
    sei();
    while (1) { sleep_mode(); }

#elif TEST_MODE == TEST_MODE_ACTIVE
    // ----- 消費電流テスト: ACTIVE モード -----
    // measureOnce() を sleep を挟まず連続実行。各 iter で STCC4 exitSleep →
    // single shot 500ms → readMeasurement → enterSleep + SHT4x_glb 1 回。
    // 実運用での「計測中の平均電流」を見る。WDT は有効のまま (measureOnce ~520ms
    // < WDT 4.1sec)。
    sei();
    while (1) {
        wdt_reset();
        (void)measureOnce();
    }

#else
    // ----- 本番動作 (TEST_MODE_NONE) -----
    // 待機中は POWER-DOWN (TWI address match で wake、校正中は IDLE に切替える)
    set_sleep_mode(SLEEP_MODE_PWR_DOWN);
    sei();

    while (1)
    {
        // WDT (4.1 sec, MCC config) を pet。これを怠ると SharedMemory ごとリセットされ、
        // 親機の Read が status1=0/value=0 の BSS 初期化状態を valid 扱いして
        // 「4 秒ごとに t/h/g が 0 になる」現象が出る (2026/05 実機確認)。
        // measureOnce は ~520ms、conditioning は 22sec で WDT より長くなり得るので
        // 該当ループ箇所でも追加で wdt_reset() を呼ぶ余地あり。
        wdt_reset();

        // --- 拡張領域 (補正係数 / name) の EEPROM 反映 -----------------------
        if (I2C_Config_Update_Requested) {
            I2C_Config_Update_Requested = false;
            EM_updateEEPROM();
        }

        // --- 校正コマンドの受理 (PHASE_IDLE 時のみ) --------------------------
        if (g_phase == PHASE_IDLE) {
            uint8_t cmd; uint16_t arg;
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                cmd = SharedMemory.reg.stcc4_cmd;
                arg = SharedMemory.reg.stcc4_cmd_arg;
            }
            if (cmd != STCC4_CMD_NONE) {
                startStcc4Cmd(cmd, arg);
                continue;   // 後続の measurement トリガ判定は次ループへ
            }
        }

        // --- 通常計測トリガ判定 (status2 = 0 を pre-trigger として扱う) ----
        // PHASE_IDLE 中に status2 が 0 なら新たに single-shot 計測を始める。
        // 既に value[] が fresh (status2 == 1) の場合は何もせず POWER-DOWN へ。
        if (g_phase == PHASE_IDLE) {
            uint8_t s2;
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) { s2 = SharedMemory.reg.status2; }
            if (s2 == 0) {
                g_phase = PHASE_MEASURING;
            }
        }

        // --- phase に応じた処理 ---------------------------------------------
        switch (g_phase)
        {
            case PHASE_MEASURING:
            {
                uint8_t stale = measureOnce();
                ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                    SharedMemory.reg.status1 = stale;
                    SharedMemory.reg.status2 = 1;   // 新サンプル READY
                }
                g_phase = PHASE_IDLE;
                break;
            }

            case PHASE_FRC_WARMUP:
                if (g_one_sec_tick) {
                    g_one_sec_tick = false;
                    g_phase_timer++;
                    if (g_phase_timer >= FRC_WARMUP_SECONDS) {
                        finishFrc();
                    }
                }
                break;

            case PHASE_CONDITIONING:
                // STCC4 は warmup 中で T/RH/CO2 計測不可だが、グローブ用 SHT4x は
                // STCC4 とは別 I2C バスにあるので独立に計測できる。親機からの計測要求
                // (status2=0) があれば SHT4x_glb のみ部分計測して T_glb を返す。
                // T/RH/CO2 は status1 で stale を立てて「未計測」として扱わせる。
                {
                    uint8_t s2;
                    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) { s2 = SharedMemory.reg.status2; }
                    if (s2 == 0) {
                        float t_glb = 0.0f, dummy_h = 0.0f;
                        bool ok_glb = SHT4x_ReadValue(&t_glb, &dummy_h, SHT4X_GLB_TYPE);
                        uint8_t stale = STATUS1_STALE_T | STATUS1_STALE_RH | STATUS1_STALE_CO2;
                        if (ok_glb) {
                            float ca, cb;
                            readCoefs(3, &ca, &cb);
                            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                                SharedMemory.reg.value[VAL_IDX_GLOBE_TEMP] = ca * t_glb + cb;
                            }
                        } else {
                            stale |= STATUS1_STALE_T_GLB;
                        }
                        ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                            SharedMemory.reg.status1 = stale;
                            SharedMemory.reg.status2 = 1;   // 部分サンプル READY
                        }
                    }
                }
                if (g_one_sec_tick) {
                    g_one_sec_tick = false;
                    g_phase_timer++;
                    if (g_phase_timer >= CONDITIONING_SECONDS) {
                        STCC4_enterSleep();
                        ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                            SharedMemory.reg.stcc4_state = STCC4_STATE_CONDITIONING_DONE;
                        }
                        g_phase = PHASE_IDLE;
                    }
                }
                break;

            case PHASE_IDLE:
            case PHASE_FRC_APPLY:
            case PHASE_FACTORY_RESET:
            default:
                break;
        }

        // --- 次の sleep mode 決定 --------------------------------------------
        // I2C 通信中 (ADDR_MATCH 〜 STOP の間) は IDLE 固定。
        //   POWER-DOWN だと TWI 周辺機が止まるので、ADDR_MATCH 後の data byte に
        //   応答できず host 側が BUSY/timeout で hang する (= 旧 bug)。
        //   風速プローブ (動作実績あり) の main.c と同じ判定ロジックに統一。
        // PHASE_IDLE かつ I2C アイドル: POWER-DOWN (TWI ADDR_MATCH で wake)
        // 校正フェーズ (FRC_WARMUP, CONDITIONING): IDLE (RTC OVF が必要)
        if (I2C_Is_Busy || I2C_KeepAlive_Ticks > 0) {
            set_sleep_mode(SLEEP_MODE_IDLE);
        } else if (g_phase == PHASE_IDLE) {
            set_sleep_mode(SLEEP_MODE_PWR_DOWN);
        } else {
            set_sleep_mode(SLEEP_MODE_IDLE);
        }
        sleep_mode();
    }
#endif // TEST_MODE
}

// </editor-fold>
