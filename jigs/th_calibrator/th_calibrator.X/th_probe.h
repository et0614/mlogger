/*
 * File:   th_probe.h
 * Author: e.togashi
 *
 * mlogger_th_sensor (温湿度 + CO2 子機) との I2C 通信ラッパ (th_calibrator 治具用)。
 * firmware/mlogger_main.X/th_probe.c と同じ OSL 共通レジスタ仕様で通信する。
 * 治具では校正対象の T/RH/CO2 のみ扱う (グローブ温度は読まない)。
 *
 * 計測モデルは「pre-trigger」方式:
 *   - ThProbe_Trigger() で子機に single-shot 計測を開始させる (~500ms 所要)
 *   - 約 1 sec 後に ThProbe_ReadSample() で POLL ブロックを一括取得する
 *
 * 呼び出し前に TCA9548A で対象スロットのサブバスを select しておくこと。
 */

#ifndef TH_PROBE_H
#define TH_PROBE_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

// I2C アドレス (mlogger_th_sensor 子機の DEFAULT_I2C_ADDRESS = 0x11 と同期)
#define TH_PROBE_ADDRESS  0x11

// STCC4 実行状態 (子機 i2c_shared_data.h の STCC4_STATE_* と同期)
#define TH_PROBE_STATE_IDLE                    (0x00)
#define TH_PROBE_STATE_FRC_RUNNING             (0x01)
#define TH_PROBE_STATE_FRC_DONE                (0x02)
#define TH_PROBE_STATE_FRC_FAIL                (0x03)
#define TH_PROBE_STATE_FACTORY_RESET_RUNNING   (0x04)
#define TH_PROBE_STATE_FACTORY_RESET_DONE      (0x05)
#define TH_PROBE_STATE_CONDITIONING_RUNNING    (0x06)
#define TH_PROBE_STATE_CONDITIONING_DONE       (0x07)

// 計測値スナップショット (整数スケール: JSON 化と flash 記録に都合が良い)
typedef struct {
    bool     i2c_ok;     // POLL 読み出しの I2C 通信が成功したか (子機存在判定)
    bool     t_valid;    // 温度が有効か (I2C OK + stale ビットクリア)
    bool     rh_valid;   // 湿度
    bool     co2_valid;  // CO2
    int16_t  t_c100;     // 乾球温度 [°C * 100]
    uint16_t rh_100;     // 相対湿度 [% * 100]
    uint16_t co2_ppm;    // CO2 濃度 [ppm]
    uint8_t  status1;    // 生 status1 (stale bitmask、ホスト側の状態表示用)
    uint8_t  status2;    // 生 status2 (0=トリガ待ち/計測中, 1=サンプル READY)
} ThSample_t;

// 子機に single-shot 計測の開始を依頼する (REG_STATUS2 に 0 を書き込む)。
// I2C 失敗 (子機不在) は無視する。
void ThProbe_Trigger(void);

// POLL ブロック (0x28-0x4B, 36 byte) を一括取得して整数スケールに変換する。
// I2C 失敗時は i2c_ok=false + 全 valid=false。
void ThProbe_ReadSample(ThSample_t* s);

// STCC4 実行状態 (REG_STCC4_STATE 0x6F) を 1 byte 読む。
// 0x06 = conditioning 実行中 (boot 後 ~22sec、この間 T/RH/CO2 は stale が正常)。
// @return true: 読み出し成功
bool ThProbe_ReadStcc4State(uint8_t* state);

// FRC (Forced Recalibration) を開始させる。子機側で 30 sec 連続測定 →
// STCC4_performForcedRecalibration 実行 (~35 sec 所要)。完了は
// ThProbe_ReadStcc4State が FRC_DONE / FRC_FAIL になることで検知する。
// 子機が conditioning 中の場合は conditioning 完了後に開始される。
// @param target_ppm 基準 CO2 濃度 [ppm]
// @return true: コマンド書き込み成功 (= 子機が受理)
bool ThProbe_StartFrc(uint16_t target_ppm);

// 最後の FRC で STCC4 が返した補正値 [ppm signed] を読む (REG_FRC_CORRECTION)。
// FRC_DONE 後にのみ意味を持つ。
// @return true: 読み出し成功
bool ThProbe_ReadFrcCorrection(int16_t* corr);

#ifdef __cplusplus
}
#endif

#endif /* TH_PROBE_H */
