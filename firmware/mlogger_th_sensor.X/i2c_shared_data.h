/*
 * File:   i2c_shared_data.h
 * Author: E. Togashi
 *
 * SHASE 2026 第2報 共通レジスタ仕様に準拠した子機 (温湿度 + CO2 + グローブ温度
 * 一体プローブ) のレジスタマップ。
 * 0x00-0x4B が共通領域、0x4C 以降が拡張領域。
 *
 * 共通領域は 静的情報 (INFO BLOCK 0x00-0x27) と 動的情報 (POLL BLOCK 0x28-0x4B)
 * に分離。親機は走査時に INFO を 1 回、毎周期 POLL を 1 回読む。
 *
 * 計測値構成 (data_count = 4):
 *   value[0] = 乾球温度   [°C]    : STCC4 経由 (SHT4x が STCC4 controller bus に従属接続)
 *   value[1] = 相対湿度   [%RH]   : STCC4 経由 (同上)
 *   value[2] = CO2 濃度   [ppm]   : STCC4 計測値
 *   value[3] = グローブ温度 [°C]  : AVR TWI1 上の SHT4x (アドレス 0x44, STCC4 のものとは別バス)
 *
 * 拡張領域には 4 計測値ぶんの線形補正係数 (a, b: corrected = a*raw + b) と、
 * STCC4 制御用の非同期コマンドレジスタ (FRC / factory_reset / perform_conditioning) を持つ。
 * 24 hour 安定化後の FRC のような長時間スケジュールは親機 (M-Logger 本体) 側で
 * 管理し、最後の FRC 発行だけ stcc4_cmd 経由で本子機に依頼する。
 */

#ifndef I2C_SHARED_DATA_H
#define	I2C_SHARED_DATA_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stddef.h>

// 装置ラベル長 (15 文字 + NUL)
#define NODE_NAME_LEN     16

// 構造体のパッキング (隙間埋めを禁止してバイトオフセットを仕様に揃える)
#pragma pack(push, 1)

typedef struct {
    // ===== INFO BLOCK (0x00-0x27, 走査時に 1 回読む) =========================
    uint32_t device_id;             // 0x00 R   : FNV-1a 22-bit (BACnet Object Instance 互換, 0..0x3FFFFE)
    uint8_t  addr_key;              // 0x04 W   : アドレス変更鍵 (STOP で自動クリア)
    uint8_t  new_addr;              // 0x05 R/W : 新しい I2C アドレス
    uint8_t  data_count;            // 0x06 R   : 有効計測値の数 (=4)
    uint8_t  reserved_07;           // 0x07     : 予約 (uint16 alignment)
    uint16_t unit_type[8];          // 0x08 R   : BACnet 単位コード (LE)
    char     name[NODE_NAME_LEN];   // 0x18 R/W : 装置ラベル (NUL 終端、EEPROM 永続)

    // ===== POLL BLOCK (0x28-0x4B, 毎周期読む) ================================
    uint8_t  status1;               // 0x28 R   : 状態フラグ (per-value stale bitmask、後述)
    uint8_t  status2;               // 0x29 R/W : 計測値更新フラグ (0=計測中/未了, 1=更新済)
                                    //            親機が 0 を書くと本子機が即 single-shot 計測を開始するトリガとして扱う
    uint8_t  reserved_2A[2];        // 0x2A     : 予約 (float alignment)
    float    value[8];              // 0x2C R   : 計測値 (LE float)

    // ===== 拡張領域 (0x4C-) ==================================================
    // --- 線形補正係数 (corrected = a * raw + b、EEPROM 永続) ---
    float    t_coef_a;              // 0x4C R/W : 乾球温度補正 a (default 1.0)
    float    t_coef_b;              // 0x50 R/W : 乾球温度補正 b (default 0.0)
    float    rh_coef_a;             // 0x54 R/W : 相対湿度補正 a (default 1.0)
    float    rh_coef_b;             // 0x58 R/W : 相対湿度補正 b (default 0.0)
    float    co2_coef_a;            // 0x5C R/W : CO2 補正 a (FRC とは独立した手動オフセット、default 1.0)
    float    co2_coef_b;            // 0x60 R/W : CO2 補正 b (default 0.0)
    float    t_glb_coef_a;          // 0x64 R/W : グローブ温度補正 a (default 1.0)
    float    t_glb_coef_b;          // 0x68 R/W : グローブ温度補正 b (default 0.0)

    // --- STCC4 非同期コマンド (volatile、EEPROM 永続なし) ---
    uint8_t  stcc4_cmd;             // 0x6C W   : 親機が書込んでコマンド発行 (後述 STCC4_CMD_*)。
                                    //            firmware が受理した時点で 0x00 に自動クリア
    uint16_t stcc4_cmd_arg;         // 0x6D W   : コマンド引数 (FRC では target ppm、他は未使用)
    uint8_t  stcc4_state;           // 0x6F R   : コマンド実行状態 (後述 STCC4_STATE_*)
    int16_t  frc_correction;        // 0x70 R   : 最後の FRC で STCC4 が返した補正値 [ppm signed]
} SensorData_t;

// 共用体
typedef union {
    SensorData_t reg;
    uint8_t      bytes[sizeof(SensorData_t)];
} I2C_Map_t;

#pragma pack(pop)

// 実体は i2c_slave.c で定義
extern volatile I2C_Map_t SharedMemory;

// ===== 共通レジスタアドレス (親機 slave_node.h と同期) =====================
#define REG_DEVICE_ID         0x00
#define REG_ADDR_KEY          0x04
#define REG_NEW_ADDR          0x05
#define REG_DATA_COUNT        0x06
#define REG_UNIT_TYPE         0x08
#define REG_NAME              0x18
#define REG_STATUS1           0x28
#define REG_STATUS2           0x29
#define REG_VALUE             0x2C
#define REG_EXTENSION         0x4C

// ===== 拡張領域のレジスタアドレス ==========================================
#define REG_T_COEF_A          0x4C
#define REG_T_COEF_B          0x50
#define REG_RH_COEF_A         0x54
#define REG_RH_COEF_B         0x58
#define REG_CO2_COEF_A        0x5C
#define REG_CO2_COEF_B        0x60
#define REG_T_GLB_COEF_A      0x64
#define REG_T_GLB_COEF_B      0x68
#define REG_STCC4_CMD         0x6C
#define REG_STCC4_CMD_ARG     0x6D
#define REG_STCC4_STATE       0x6F
#define REG_FRC_CORRECTION    0x70

// ===== アドレス変更鍵 ======================================================
#define ADDR_KEY_UNLOCK       0xA5

// ===== Status1 ビット規約 =================================================
// per-value bitmask: bit i (i < data_count) が立っていると value[i] が stale / 異常。
// 全計測値が信頼できない場合は 0xFF を立てる慣例 (STCC4 通信全断などの致命系)。
//
// 例:
//   0x01 : T のみ stale (STCC4 経由の SHT4x 読出し失敗)
//   0x03 : T + RH stale (同上、両方落ち)
//   0x04 : CO2 のみ stale (STCC4 自体は通信できたが measurement 取得失敗)
//   0x08 : T_glb のみ stale (グローブ用 SHT4x 通信失敗)
//   0xFF : STCC4 が全く応答しないなど致命
#define STATUS1_STALE_T            (0x01)
#define STATUS1_STALE_RH           (0x02)
#define STATUS1_STALE_CO2          (0x04)
#define STATUS1_STALE_T_GLB        (0x08)
#define STATUS1_ALL_STALE          (0xFF)

// ===== 計測値インデックス ==================================================
#define VAL_IDX_TEMPERATURE   (0)  // value[0] = 乾球温度 [°C]
#define VAL_IDX_HUMIDITY      (1)  // value[1] = 相対湿度 [%RH]
#define VAL_IDX_CO2           (2)  // value[2] = CO2 濃度 [ppm]
#define VAL_IDX_GLOBE_TEMP    (3)  // value[3] = グローブ温度 [°C]

// ===== BACnet Engineering Units (ASHRAE 135) ==============================
#define UNIT_DEGREES_CELSIUS              (62)
#define UNIT_PERCENT_RELATIVE_HUMID       (29)
#define UNIT_PARTS_PER_MILLION            (96)

// ===== STCC4 コマンド (REG_STCC4_CMD に書込む値) ===========================
// 値は親機 → 子機の write のみ。子機 firmware がコマンドを受理した時点で 0x00 に
// 自動クリアされる。実行進捗は REG_STCC4_STATE を polling して確認する。
//
//   STCC4_CMD_FRC          : 30 sec 連続測定後、stcc4_cmd_arg [ppm] を基準として
//                            FRC (perform_forced_recalibration) を発行。所要 ~35 sec。
//                            完了後 REG_FRC_CORRECTION に STCC4 が返した補正値 [ppm signed] が入る。
//   STCC4_CMD_FACTORY_RESET: perform_factory_reset を即時発行 (FRC/ASC 履歴消去)。所要 ~90 ms。
//   STCC4_CMD_CONDITIONING : perform_conditioning を発行 (センサ立ち上げ調整)。所要 ~22 sec。
//
// 24 hour 安定化後 FRC のような長時間プロセスは親機側 (M-Logger 本体) でスケジュールし、
// 終端の FRC だけ STCC4_CMD_FRC で本子機に依頼する。
#define STCC4_CMD_NONE             (0x00)
#define STCC4_CMD_FRC              (0x01)
#define STCC4_CMD_FACTORY_RESET    (0x02)
#define STCC4_CMD_CONDITIONING     (0x03)

// ===== STCC4 実行状態 (REG_STCC4_STATE) =====================================
// 子機 firmware が更新する read-only。コマンド完了 / 失敗は親機が polling して確認する。
// 新コマンドを受理すると対応する _RUNNING にいったん遷移し、終了時に _DONE / _FAIL へ。
// 次のコマンドが受理されるか、または親機が状態を取り直すまで _DONE/_FAIL は保持される。
#define STCC4_STATE_IDLE                (0x00)
#define STCC4_STATE_FRC_RUNNING         (0x01)
#define STCC4_STATE_FRC_DONE            (0x02)
#define STCC4_STATE_FRC_FAIL            (0x03)
#define STCC4_STATE_FACTORY_RESET_RUNNING (0x04)
#define STCC4_STATE_FACTORY_RESET_DONE  (0x05)
#define STCC4_STATE_CONDITIONING_RUNNING (0x06)
#define STCC4_STATE_CONDITIONING_DONE   (0x07)

#ifdef	__cplusplus
}
#endif

#endif	/* I2C_SHARED_DATA_H */
