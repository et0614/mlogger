/*
 * File:   th_probe.h
 * Author: e.togashi
 *
 * 温湿度 + CO2 + グローブ温度 一体プローブ (mlogger_th_sensor) との I2C 通信ラッパ。
 * mlogger_th_sensor.X/i2c_shared_data.h で定義された SHASE 2026 第2報 共通レジスタ
 * 仕様に従って通信する。
 *
 * 計測モデルは「pre-trigger」方式:
 *   - 計測時刻の 1 sec 前に ThProbe_Trigger() を呼び子機に single-shot 計測を開始させる
 *   - 計測時刻に ThProbe_Read() を呼んで POLL ブロックを 1 回で取得する
 *
 * STCC4 校正コマンド (FRC / factory_reset / perform_conditioning) は本子機が
 * 内部で実行する。本体側は ThProbe_Start* を 1 回呼んで後は ThProbe_GetState() を
 * polling して完了/失敗を検知する。
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

// 計測値スナップショット (生値、main 側で EM_cFactors のユーザー補正を後段で適用)
typedef struct {
    float    temp_c;       // 乾球温度 [°C]
    float    rh_pct;       // 相対湿度 [%RH]
    float    glb_c;        // グローブ温度 [°C]
    uint16_t co2_ppm;      // CO2 濃度 [ppm]
    bool     i2c_ok;       // 直近 ThProbe_Read で I2C 通信が成功したか
                           // (probe 物理切断検知用。warmup 中 / STCC4 fail でも true になる)
    bool     t_valid;      // value[0] が有効か (I2C OK + status1 STALE_T クリア)
    bool     rh_valid;     // value[1]
    bool     co2_valid;    // value[2]
    bool     glb_valid;    // value[3]
} ThProbe_t;

// 初期化 (構造体をゼロ化)。実機との通信は伴わない。
void ThProbe_Init(ThProbe_t* p);

// 接続確認 (data_count = 0x06 を 1 byte 読んでみて値が 4 か確認)
bool ThProbe_IsConnected(void);

// 子機に single-shot 計測の開始を依頼 (REG_STATUS2 に 0 を書き込む)
// 計測時刻の 1 sec 前に呼び、1 sec 後に ThProbe_Read() で取得する。
void ThProbe_Trigger(void);

// POLL ブロック (0x28-0x4B, 36 byte) を一括取得し、status1 を見て valid フラグを設定する。
// I2C 失敗時は全 valid フラグを false に倒す。
void ThProbe_Read(ThProbe_t* p);

// FRC (Forced Recalibration) コマンド発行。子機側で 30 sec 連続測定 → FRC 実行 (~35 sec 所要)。
// 完了後 ThProbe_GetState() が _FRC_DONE / _FRC_FAIL になる。
// target_ppm: 基準濃度 [ppm]
void ThProbe_StartFrc(uint16_t target_ppm);

// STCC4 factory_reset コマンド発行 (FRC/ASC 履歴消去、~90 ms 所要)。
void ThProbe_StartFactoryReset(void);

// perform_conditioning コマンド発行 (センサ立ち上げ調整、~22 sec 所要)。
void ThProbe_StartConditioning(void);

// 現在の STCC4 状態を取得 (REG_STCC4_STATE 1 byte 読出し)。
// I2C 失敗時は TH_PROBE_STATE_IDLE を返す。
uint8_t ThProbe_GetState(void);

// 最後の FRC で STCC4 が返した補正値 [ppm signed] を取得 (REG_FRC_CORRECTION 2 byte 読出し)。
int16_t ThProbe_GetFrcCorrection(void);

#ifdef __cplusplus
}
#endif

#endif /* TH_PROBE_H */
