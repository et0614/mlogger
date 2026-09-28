#include "protocol_handlers.h"
#include "protocol_codec.h"
#include "protocol_events.h"   // pe_emit_dump_end
#include "command_handler.h"   // CH_Reply
#include "version.h"
#include "logger_control.h"    // LC_IsLogging, LC_SetCurrentTime, LC_GetCurrentTime, LC_StartLoggingTask, LC_EndLoggingTask, LC_ClearData, LC_FactoryResetCO2, LC_CalibrateCO2
#include "eeprom_manager.h"    // EM_mlName, EM_cFactors, EM_mSettings, EM_save*
#include "usb_extension.h"     // USB_StartRecordStream, USB_SetStreamDoneCallback, rec_latest
#include "w25q256.h"           // SensorData_t (record_size 算出用), W25_ChipErase
#include "hal_io.h"            // turnOnRedLED / turnOffRedLED (erase_flash 中の処理中通知)
#include "main.h"              // getBatteryVoltage_mV (= ph_get_battery)
#include "th_probe.h"          // ThProbe_ReadInfo (= ph_get_probe_info)
#include "anemometer.h"        // Anemometer_ReadInfo (= ph_get_probe_info)
#include "xbee_controller.h"   // Xbee_QueryAt (= ph_get_radio_info)

#include "mcc_generated_files/usb/usb_device.h"      // USBDevice_Handle (erase_flash 待ち中の USB 処理)
#include "mcc_generated_files/timer/delay.h"         // DELAY_milliseconds

#include <avr/io.h>            // SIGROW
#include <avr/wdt.h>           // wdt_reset (erase_flash の長時間待ち)
#include <stdio.h>
#include <string.h>

// erase_flash の完了待ちの上限 [ms]。W25Q256 の chip erase は通常 80 秒、仕様上の
// 最大 250 秒強。余裕を持たせ、これを超えたらフラッシュ異常とみなす。
#define ERASE_TIMEOUT_MS  450000UL

// calibrate_co2 の target_ppm の受付範囲 [ppm]
#define CO2_TARGET_MIN_PPM  300UL
#define CO2_TARGET_MAX_PPM  5000UL

// 応答送信用バッファ (set_settings の全状態返却で ~290B、安全側で 512B)
static char s_tx_buf[512];

// ============================================================
// 内部ユーティリティ
// ============================================================
// FNV-1a 32-bit (vel_probe main.c:228 と同じアルゴリズム)
static uint32_t fnv1a_32(const void *data, size_t len) {
    const uint8_t *p = (const uint8_t *)data;
    uint32_t hash = 2166136261u;
    for (size_t i = 0; i < len; i++) {
        hash ^= p[i];
        hash *= 16777619u;
    }
    return hash;
}

static void make_hardware_id(char *out, size_t out_cap) {
    uint32_t h = fnv1a_32((const void *)&SIGROW.SERNUM0, 16);
    // 8 桁の大文字 16 進。snprintf("%08lX") だと XC8 が汎用 printf (浮動小数点書式
    // 込みで約 5 KB) をリンクしてしまうため自前で変換する。
    static const char HEX[] = "0123456789ABCDEF";
    if (out_cap < 9) { if (out_cap) out[0] = '\0'; return; }
    for (int8_t i = 7; i >= 0; i--) {
        out[i] = HEX[h & 0x0F];
        h >>= 4;
    }
    out[8] = '\0';
}

static void send_simple_error(int32_t id, CommandSource_t src, const char *code, const char *msg) {
    char buf[160];
    size_t n = pc_make_error(buf, sizeof(buf), id, code, msg);
    if (n > 0) CH_Reply(buf, src);
}

// 範囲チェック
static bool in_range_f(float v, float lo, float hi) { return v >= lo && v <= hi; }

// ============================================================
// センサカテゴリ ⇔ 設定のマッピング (v4 set_settings は 3 カテゴリ)
// (PATCH 適用 + 応答生成の両方で使う)
// ============================================================
// "general"     = 温湿度 + グローブ温度 + CO2 (th_probe 一括計測なので 1 つの設定で十分)
// "velocity"    = 風速
// "illuminance" = 照度
//
// 内部 EEPROM struct は v3 互換のため measure_th/glb/co2 等を個別に保持しているが、
// v4 protocol では "general" 1 つで th/glb/co2 を同時に on/off / interval 設定する。
typedef enum {
    SC_GENERAL = 0,
    SC_VELOCITY,
    SC_ILLUMINANCE,
} SensorCategory_t;

typedef struct {
    const char *name;
    SensorCategory_t category;
} sensor_setting_t;

static const sensor_setting_t SENSOR_SETTINGS[] = {
    { "general",     SC_GENERAL     },
    { "velocity",    SC_VELOCITY    },
    { "illuminance", SC_ILLUMINANCE },
};
#define NUM_SENSOR_SETTINGS (sizeof(SENSOR_SETTINGS) / sizeof(SENSOR_SETTINGS[0]))

static bool get_category_enabled(SensorCategory_t c) {
    switch (c) {
        case SC_GENERAL:     return EM_mSettings.measure_th;   // th/glb/co2 は常に同値で保持
        case SC_VELOCITY:    return EM_mSettings.measure_vel;
        case SC_ILLUMINANCE: return EM_mSettings.measure_ill;
    }
    return false;
}

static uint32_t get_category_interval(SensorCategory_t c) {
    switch (c) {
        case SC_GENERAL:     return EM_mSettings.interval_th;  // th/glb/co2 は常に同値で保持
        case SC_VELOCITY:    return EM_mSettings.interval_vel;
        case SC_ILLUMINANCE: return EM_mSettings.interval_ill;
    }
    return 0;
}

static void set_category_enabled(SensorCategory_t c, bool v) {
    switch (c) {
        case SC_GENERAL:
            EM_mSettings.measure_th  = v;
            EM_mSettings.measure_glb = v;
            EM_mSettings.measure_co2 = v;
            break;
        case SC_VELOCITY:    EM_mSettings.measure_vel = v; break;
        case SC_ILLUMINANCE: EM_mSettings.measure_ill = v; break;
    }
}

static void set_category_interval(SensorCategory_t c, uint32_t v) {
    switch (c) {
        case SC_GENERAL:
            EM_mSettings.interval_th  = v;
            EM_mSettings.interval_glb = v;
            EM_mSettings.interval_co2 = v;
            break;
        case SC_VELOCITY:    EM_mSettings.interval_vel = v; break;
        case SC_ILLUMINANCE: EM_mSettings.interval_ill = v; break;
    }
}

typedef struct {
    const char *name;
    float *a_ptr;
    float *b_ptr;
    float a_min, a_max;
    float b_min, b_max;
} correction_t;

static const correction_t CORRECTIONS[] = {
    { "t_dry",       &EM_cFactors.dbtA, &EM_cFactors.dbtB, 0.800f, 1.200f, -3.00f,   3.00f   },
    { "humidity",    &EM_cFactors.hmdA, &EM_cFactors.hmdB, 0.800f, 1.200f, -9.99f,   9.99f   },
    { "t_glb",       &EM_cFactors.glbA, &EM_cFactors.glbB, 0.800f, 1.200f, -3.00f,   3.00f   },
    { "illuminance", &EM_cFactors.luxA, &EM_cFactors.luxB, 0.800f, 1.200f, -999.0f,  999.0f  },
    { "velocity",    &EM_cFactors.velA, &EM_cFactors.velB, 0.800f, 1.200f, -0.500f,  0.500f  },
};
#define NUM_CORRECTIONS (sizeof(CORRECTIONS) / sizeof(CORRECTIONS[0]))

// ============================================================
// 応答ボディの組み立て (get/set で共有)
// ============================================================
static void write_settings_body(pc_writer_t *w) {
    for (size_t i = 0; i < NUM_SENSOR_SETTINGS; i++) {
        pc_key(w, SENSOR_SETTINGS[i].name);
        pc_obj_begin(w);
        pc_key(w, "enabled");  pc_bool(w, get_category_enabled(SENSOR_SETTINGS[i].category));
        pc_key(w, "interval"); pc_uint(w, get_category_interval(SENSOR_SETTINGS[i].category));
        pc_obj_end(w);
    }
    pc_key(w, "start_ts"); pc_uint(w, (uint32_t)EM_mSettings.start_dt);
}

static void write_correction_body(pc_writer_t *w) {
    for (size_t i = 0; i < NUM_CORRECTIONS; i++) {
        pc_key(w, CORRECTIONS[i].name);
        pc_obj_begin(w);
        pc_key(w, "a"); pc_float(w, *CORRECTIONS[i].a_ptr, 3);
        pc_key(w, "b"); pc_float(w, *CORRECTIONS[i].b_ptr, 3);
        pc_obj_end(w);
    }
}

static void send_settings_response(int32_t id, CommandSource_t src) {
    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    write_settings_body(&w);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

static void send_correction_response(int32_t id, CommandSource_t src) {
    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    write_correction_body(&w);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// hello
// ============================================================
void ph_hello(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    char hw_id[16];
    make_hardware_id(hw_id, sizeof(hw_id));

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "device");           pc_str(&w, "M-Logger");
    pc_key(&w, "firmware_version"); pc_str(&w, FW_VERSION);
    pc_key(&w, "protocol_version"); pc_uint(&w, PROTOCOL_VERSION);
    pc_key(&w, "hardware_id");      pc_str(&w, hw_id);
    pc_key(&w, "name");             pc_str(&w, EM_mlName);
    pc_key(&w, "logging");          pc_bool(&w, LC_IsLogging());
    pc_end_result(&w);

    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// get_battery
// ============================================================
void ph_get_battery(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    uint16_t vbat_mv = getBatteryVoltage_mV();

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "voltage_mv");  pc_uint(&w, vbat_mv);
    pc_key(&w, "low_battery"); pc_bool(&w, vbat_mv < 1800);
    pc_end_result(&w);

    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// set_name
// ============================================================
void ph_set_name(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }
    int name_tok = pc_obj_get(json, tokens, ntokens, params_tok, "name");
    if (name_tok < 0 || tokens[name_tok].type != JSMN_STRING) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'name'");
        return;
    }
    int len = tokens[name_tok].end - tokens[name_tok].start;
    if (len > 20) {
        send_simple_error(id, src, "out_of_range", "name max 20 chars");
        return;
    }

    pc_tok_strcpy(json, &tokens[name_tok], EM_mlName, sizeof(EM_mlName));
    EM_saveName();

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "name"); pc_str(&w, EM_mlName);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);

    // XBee の BLE アドバタイズ名 (BI) にも反映する (出荷時の個体番号付与や
    // アプリからの改名で BLE 名が初期値のまま残らないように)。
    // 応答送信後に行う (BI/WR の適用待ち ~200ms で応答を遅らせないため)。
    Xbee_ApplyBleName();
}

// ============================================================
// set_time
// ============================================================
void ph_set_time(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }
    int ts_tok = pc_obj_get(json, tokens, ntokens, params_tok, "ts");
    uint32_t ts;
    if (ts_tok < 0 || !pc_tok_u32(json, &tokens[ts_tok], &ts)) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'ts'");
        return;
    }
    if (!LC_SetCurrentTime((time_t)ts)) {
        send_simple_error(id, src, "out_of_range", "ts must be 2026-01-01 or later");
        return;
    }

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "ts"); pc_uint(&w, (uint32_t)LC_GetCurrentTime());
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// get_settings / set_settings
// ============================================================
void ph_get_settings(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;
    send_settings_response(id, src);
}

// set_settings / set_correction の検証エラー
typedef struct {
    const char *code;
    const char *msg;
} param_error_t;

// set_settings の params を EM_mSettings に適用する。エラー時は途中まで適用した
// 状態で戻るので、呼び出し側で元に戻すこと。
static bool apply_settings(const char *json, const jsmntok_t *tokens, int ntokens, int params_tok,
                           bool *changed, param_error_t *err) {
    // 各カテゴリについてキーが指定されていれば PATCH
    for (size_t i = 0; i < NUM_SENSOR_SETTINGS; i++) {
        int s_tok = pc_obj_get(json, tokens, ntokens, params_tok, SENSOR_SETTINGS[i].name);
        if (s_tok < 0) continue;
        if (tokens[s_tok].type != JSMN_OBJECT) {
            *err = (param_error_t){ "invalid_params", "sensor value must be object" };
            return false;
        }
        // enabled
        int en_tok = pc_obj_get(json, tokens, ntokens, s_tok, "enabled");
        if (en_tok >= 0) {
            bool en;
            if (!pc_tok_bool(json, &tokens[en_tok], &en)) {
                *err = (param_error_t){ "invalid_params", "enabled must be boolean" };
                return false;
            }
            set_category_enabled(SENSOR_SETTINGS[i].category, en);
            *changed = true;
        }
        // interval (0-99999 [sec])。EEPROM 側は uint32_t、比較系は int32_t で扱うので
        // この範囲を歪みなく保持できる (16bit 時代の wrap/負値化問題は解消済み)。
        int iv_tok = pc_obj_get(json, tokens, ntokens, s_tok, "interval");
        if (iv_tok >= 0) {
            uint32_t iv;
            if (!pc_tok_u32(json, &tokens[iv_tok], &iv) || iv > 99999) {
                *err = (param_error_t){ "out_of_range", "interval must be 0-99999" };
                return false;
            }
            set_category_interval(SENSOR_SETTINGS[i].category, iv);
            *changed = true;
        }
    }

    // start_ts は params 直下
    int st_tok = pc_obj_get(json, tokens, ntokens, params_tok, "start_ts");
    if (st_tok >= 0) {
        uint32_t st;
        if (!pc_tok_u32(json, &tokens[st_tok], &st)) {
            *err = (param_error_t){ "invalid_params", "start_ts must be number" };
            return false;
        }
        EM_mSettings.start_dt = st;
        *changed = true;
    }
    return true;
}

void ph_set_settings(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    // エラーなら何も変えない (途中まで反映した値が次の保存で EEPROM に書かれないように)
    MeasurementSettings backup = EM_mSettings;
    bool changed = false;
    param_error_t err;
    if (!apply_settings(json, tokens, ntokens, params_tok, &changed, &err)) {
        EM_mSettings = backup;
        send_simple_error(id, src, err.code, err.msg);
        return;
    }

    if (changed) EM_saveMeasurementSetting();
    send_settings_response(id, src);
}

// ============================================================
// get_correction / set_correction
// ============================================================
void ph_get_correction(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;
    send_correction_response(id, src);
}

// set_correction の params を EM_cFactors に適用する。エラー時は途中まで適用した
// 状態で戻るので、呼び出し側で元に戻すこと。
static bool apply_correction(const char *json, const jsmntok_t *tokens, int ntokens, int params_tok,
                             bool *changed, param_error_t *err) {
    for (size_t i = 0; i < NUM_CORRECTIONS; i++) {
        int s_tok = pc_obj_get(json, tokens, ntokens, params_tok, CORRECTIONS[i].name);
        if (s_tok < 0) continue;
        if (tokens[s_tok].type != JSMN_OBJECT) {
            *err = (param_error_t){ "invalid_params", "sensor value must be object" };
            return false;
        }
        int a_tok = pc_obj_get(json, tokens, ntokens, s_tok, "a");
        if (a_tok >= 0) {
            float a;
            if (!pc_tok_float(json, &tokens[a_tok], &a)) {
                *err = (param_error_t){ "invalid_params", "'a' must be number" };
                return false;
            }
            if (!in_range_f(a, CORRECTIONS[i].a_min, CORRECTIONS[i].a_max)) {
                *err = (param_error_t){ "out_of_range", "'a' out of range" };
                return false;
            }
            *CORRECTIONS[i].a_ptr = a;
            *changed = true;
        }
        int b_tok = pc_obj_get(json, tokens, ntokens, s_tok, "b");
        if (b_tok >= 0) {
            float b;
            if (!pc_tok_float(json, &tokens[b_tok], &b)) {
                *err = (param_error_t){ "invalid_params", "'b' must be number" };
                return false;
            }
            if (!in_range_f(b, CORRECTIONS[i].b_min, CORRECTIONS[i].b_max)) {
                *err = (param_error_t){ "out_of_range", "'b' out of range" };
                return false;
            }
            *CORRECTIONS[i].b_ptr = b;
            *changed = true;
        }
    }
    return true;
}

void ph_set_correction(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    // エラーなら何も変えない
    CorrectionFactors backup = EM_cFactors;
    bool changed = false;
    param_error_t err;
    if (!apply_correction(json, tokens, ntokens, params_tok, &changed, &err)) {
        EM_cFactors = backup;
        send_simple_error(id, src, err.code, err.msg);
        return;
    }

    if (changed) EM_saveCorrectionFactor();
    send_correction_response(id, src);
}

// ============================================================
// Phase C: action 系
// ============================================================

// 空 result {} の応答を返す
static void send_empty_result(int32_t id, CommandSource_t src) {
    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// start_logging
//   params: { transports: {zigbee, ble, flash, usb}, mode: "once"|"auto_restart" }
// ============================================================
void ph_start_logging(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    // transports (必須)
    int tx_tok = pc_obj_get(json, tokens, ntokens, params_tok, "transports");
    if (tx_tok < 0 || tokens[tx_tok].type != JSMN_OBJECT) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'transports'");
        return;
    }

    bool zb = false, ble = false, fl = false, usb = false;
    int t;
    if ((t = pc_obj_get(json, tokens, ntokens, tx_tok, "zigbee")) >= 0 && !pc_tok_bool(json, &tokens[t], &zb))   { send_simple_error(id, src, "invalid_params", "transports.zigbee must be bool"); return; }
    if ((t = pc_obj_get(json, tokens, ntokens, tx_tok, "ble"))    >= 0 && !pc_tok_bool(json, &tokens[t], &ble))  { send_simple_error(id, src, "invalid_params", "transports.ble must be bool"); return; }
    if ((t = pc_obj_get(json, tokens, ntokens, tx_tok, "flash"))  >= 0 && !pc_tok_bool(json, &tokens[t], &fl))   { send_simple_error(id, src, "invalid_params", "transports.flash must be bool"); return; }
    if ((t = pc_obj_get(json, tokens, ntokens, tx_tok, "usb"))    >= 0 && !pc_tok_bool(json, &tokens[t], &usb))  { send_simple_error(id, src, "invalid_params", "transports.usb must be bool"); return; }

    // mode (任意、デフォルト "once")
    bool auto_restart = false;
    int mode_tok = pc_obj_get(json, tokens, ntokens, params_tok, "mode");
    if (mode_tok >= 0) {
        if (tokens[mode_tok].type != JSMN_STRING) {
            send_simple_error(id, src, "invalid_params", "mode must be string");
            return;
        }
        if (pc_tok_eq(json, &tokens[mode_tok], "auto_restart")) {
            auto_restart = true;
        } else if (!pc_tok_eq(json, &tokens[mode_tok], "once")) {
            send_simple_error(id, src, "invalid_params", "mode must be 'once' or 'auto_restart'");
            return;
        }
    }

    if (!zb && !ble && !fl && !usb) {
        send_simple_error(id, src, "invalid_params", "no transport selected");
        return;
    }

    // dump 中に始めると計測データが dump のバイナリに混ざる
    if (USB_IsStreaming()) {
        send_simple_error(id, src, "busy", "dump in progress");
        return;
    }

    // 時刻が未設定 (2026-01-01 未満 = 電源投入直後の 2000-01-01 相当) なら拒否して
    // client に set_time を促す。flash 記録は日付が誤ったまま残り、それ以外の出力先も
    // 計測開始時刻 (start_ts) より前と判定されて何も送られないため。
    // (電源投入後の auto_restart 再開は本関数を通らないので影響しない)
    if (!LC_IsRtcSet()) {
        send_simple_error(id, src, "rtc_unset", "call set_time before start_logging");
        return;
    }

    EM_mSettings.start_auto = auto_restart;
    EM_saveMeasurementSetting();

    // ロギング開始でスリープに入る可能性があるので、応答を先に返す (v3 STL と同じパターン)
    send_empty_result(id, src);

    // USB 経由の場合は ACK が確実にホストへ届くまで待ってからロギング開始 (スリープで USB が落ちる前に)
    if (src == SRC_USB) USB_Flush();

    LC_StartLoggingTask(zb, ble, fl, usb);
}

// ============================================================
// stop_logging
// ============================================================
void ph_stop_logging(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;
    LC_EndLoggingTask();
    send_empty_result(id, src);
}

// ============================================================
// clear_data
// ============================================================
void ph_clear_data(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;
    if (LC_IsLogging() || USB_IsStreaming()) {
        send_simple_error(id, src, "busy", "stop logging or dump before clear");
        return;
    }
    LC_ClearData();
    send_empty_result(id, src);
}

// ============================================================
// erase_flash (USB-CDC専用)
//   W25Q256 を chip erase で完全初期化 + EM_generationNumber を 1 にリセット。
//   firmware 書き換え時や generation 番号が破損した場合の復旧手段。
//   約 40~80 秒の blocking、処理中は赤 LED を点灯し続けて視覚的に通知する。
// ============================================================
void ph_erase_flash(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    if (src != SRC_USB) {
        send_simple_error(id, src, "unsupported_transport", "erase_flash is USB-CDC only");
        return;
    }
    if (LC_IsLogging()) {
        send_simple_error(id, src, "busy", "logging in progress");
        return;
    }

    // 処理中通知: 赤 LED を消去完了まで点灯し続ける
    turnOnRedLED();
    W25_ChipEraseStart();

    // 完了待ち (W25Q256 は通常 80 秒、仕様上の最大 250 秒強)。待つ間も WDT をリセットし、
    // USB の処理を回してホスト側から見て応答不能にならないようにする。
    // フラッシュが応答しない (BUSY が立ったまま) 場合に備えてタイムアウトを持つ。
    uint32_t waited_ms = 0;
    bool erased = true;
    while (W25_IsBusy()) {
        wdt_reset();
        USBDevice_Handle();
        USB_CDCVirtualSerialPortHandler();
        DELAY_milliseconds(10);
        waited_ms += 10;
        if (waited_ms >= ERASE_TIMEOUT_MS) { erased = false; break; }
    }
    turnOffRedLED();
    if (!erased) {
        send_simple_error(id, src, "erase_timeout", "flash did not finish chip erase");
        return;
    }

    // 工場初期化相当 (initMemory) の generation/rec_latest 状態に揃える
    EM_generationNumber = 1;
    EM_saveGenerationNumber();
    rec_latest = 0;

    send_empty_result(id, src);
}

// ============================================================
// calibrate_co2
//   params: { mode: "forced"|"factory"|"reset", target_ppm: int? }
//
//   mode="forced":   30秒連続測定 → FRC (LC_CalibrateCO2)、target_ppm 必須
//   mode="factory":  factory_reset → 12時間 1 秒ごとに測定 → FRC (LC_FactoryResetCO2)、target_ppm 必須
//   mode="reset":    factory_reset 単独 (LC_FactoryResetCO2Only)、target_ppm 不要・無視
// ============================================================
void ph_calibrate_co2(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    // 校正中はロギングの計測処理が止まるので、ロギング中は受け付けない
    if (LC_IsLogging()) {
        send_simple_error(id, src, "busy", "stop logging before calibration");
        return;
    }

    int mode_tok = pc_obj_get(json, tokens, ntokens, params_tok, "mode");
    if (mode_tok < 0 || tokens[mode_tok].type != JSMN_STRING) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'mode'");
        return;
    }

    bool is_forced  = pc_tok_eq(json, &tokens[mode_tok], "forced");
    bool is_factory = pc_tok_eq(json, &tokens[mode_tok], "factory");
    bool is_reset   = pc_tok_eq(json, &tokens[mode_tok], "reset");
    if (!is_forced && !is_factory && !is_reset) {
        send_simple_error(id, src, "invalid_params", "mode must be 'forced', 'factory', or 'reset'");
        return;
    }

    // reset 以外は target_ppm が必須
    uint32_t target = 0;
    if (!is_reset) {
        int target_tok = pc_obj_get(json, tokens, ntokens, params_tok, "target_ppm");
        if (target_tok < 0 || !pc_tok_u32(json, &tokens[target_tok], &target)) {
            send_simple_error(id, src, "invalid_params", "missing or invalid 'target_ppm'");
            return;
        }
        // 基準にできるのは外気 (約 420 ppm) から校正用ガス程度まで
        if (target < CO2_TARGET_MIN_PPM || target > CO2_TARGET_MAX_PPM) {
            send_simple_error(id, src, "out_of_range", "target_ppm must be 300-5000");
            return;
        }
    }

    if (is_forced) {
        LC_CalibrateCO2((uint16_t)target, 30);                // 30秒モード
    } else if (is_factory) {
        LC_FactoryResetCO2((uint16_t)target, 12U * 3600U);    // 12時間モード
    } else { // is_reset
        LC_FactoryResetCO2Only();                             // factory_reset 単独 (~90ms)
    }

    // 即時 ACK (進捗は co2_calibration_progress event で送出 — forced/factory のみ)
    send_empty_result(id, src);
}

// ============================================================
// get_count
//   dump 実行前に件数 / record_size / format を取得して、所要時間予測と
//   ユーザー確認に使う。USB / BLE / Zigbee すべてで動作。
// ============================================================
void ph_get_count(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "count");       pc_uint(&w, rec_latest);
    pc_key(&w, "record_size"); pc_uint(&w, sizeof(SensorData_t));
    pc_key(&w, "format");      pc_str(&w, "<BIBIhhHHHH>");
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// dump
//   JSON ヘッダ送信後、バイナリストリームに切り替え。USB / BLE / Zigbee すべてで動作。
//   ロギング中は busy エラー (BLE/Zigbee dump は同 channel で smp event と干渉するため)。
//   USB-CDC / BLE / Zigbee とも、main loop の USB_Stream_Task が呼ばれるたびに
//   少しずつ送る (1 回の呼び出しで長時間ブロックしない)
//
//   params (省略可): { "from": N, "limit": M }
//   from  = 送信開始レコード index (省略時 0)
//   limit = 送信レコード数 (省略時/0 は末尾まで)
//   BLE/Zigbee は fire-and-forget でフレーム欠落があり得るため、クライアントは
//   小 block (~100 records) を limit 指定で pull し、不足 block だけ再要求する。
//   ヘッダの count/from は「この応答で送る範囲」(クランプ後) を返す。
// ============================================================
void ph_dump(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (LC_IsLogging()) {
        send_simple_error(id, src, "busy", "stop logging before dump");
        return;
    }

    uint32_t from = 0;
    uint32_t limit = 0;   // 0 = 末尾まで
    if (params_tok >= 0) {
        int from_tok = pc_obj_get(json, tokens, ntokens, params_tok, "from");
        if (from_tok >= 0 && tokens[from_tok].type == JSMN_PRIMITIVE) {
            int32_t v = pc_tok_int(json, &tokens[from_tok]);
            if (v > 0) from = (uint32_t)v;
        }
        int limit_tok = pc_obj_get(json, tokens, ntokens, params_tok, "limit");
        if (limit_tok >= 0 && tokens[limit_tok].type == JSMN_PRIMITIVE) {
            int32_t v = pc_tok_int(json, &tokens[limit_tok]);
            if (v > 0) limit = (uint32_t)v;
        }
    }

    // rec_latest でクランプした実送信範囲 (USB_StartRecordStream 内の計算と一致させる)
    if (from > rec_latest) from = rec_latest;
    uint32_t n = rec_latest - from;
    if (limit > 0 && limit < n) n = limit;

    // JSON ヘッダ送信。record_size は SensorData_t (=22 bytes) に追従する。
    // format string "<BIBIhhHHHH>" も 22 bytes (LE 無 alignment) で一致確認済。
    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "count");       pc_uint(&w, n);
    pc_key(&w, "from");        pc_uint(&w, from);
    pc_key(&w, "record_size"); pc_uint(&w, sizeof(SensorData_t));
    pc_key(&w, "format");      pc_str(&w, "<BIBIhhHHHH>");
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);

    // 完了時に dump_end イベントを送出するよう登録 (transport は dump 開始時の src と一致)
    USB_SetStreamDoneCallback(pe_emit_dump_end);

    // バイナリストリーム開始。transport に応じて stream destination を切替。
    USB_StartRecordStream(src, from, n);
}

// ============================================================
// get_probe_info (出荷検査・診断用)
//   接続中のプローブの INFO BLOCK (device_id / name / data_count) を返す。
//   response: { "result": { "th_probe":       {"connected":bool, "device_id":"XXXXXX", "name":"...", "data_count":N},
//                           "velocity_probe": { 同上 } } }
//   未接続のプローブは connected=false のみ。ロギング中でも安全 (INFO は静的領域)。
// ============================================================
static void write_probe_info(pc_writer_t *w, const char *key,
                             bool (*reader)(uint32_t*, uint8_t*, char[17])) {
    uint32_t dev_id = 0;
    uint8_t count = 0;
    char pname[17];
    bool ok = reader(&dev_id, &count, pname);

    pc_key(w, key);
    pc_obj_begin(w);
    pc_key(w, "connected"); pc_bool(w, ok);
    if (ok) {
        char idbuf[12];
        snprintf(idbuf, sizeof(idbuf), "%06lX", (unsigned long)dev_id);
        pc_key(w, "device_id");  pc_str(w, idbuf);
        pc_key(w, "name");       pc_str(w, pname);
        pc_key(w, "data_count"); pc_uint(w, count);
    }
    pc_obj_end(w);
}

void ph_get_probe_info(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    write_probe_info(&w, "th_probe",       ThProbe_ReadInfo);
    write_probe_info(&w, "velocity_probe", Anemometer_ReadInfo);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// XBee の AT 設定値を読み、数値 (big endian) として key に書く。読めなければ書かない
static void put_at_uint(pc_writer_t *w, const char *key, const char at[2]) {
    uint8_t v[4];
    int n = Xbee_QueryAt(at, v, sizeof(v));
    if (n <= 0) return;
    uint32_t x = 0;
    for (int i = 0; i < n; i++) x = (x << 8) | v[i];
    pc_key(w, key); pc_uint(w, x);
}

// XBee の AT 設定値を読み、16 進文字列として key に書く。読めなければ書かない
static void put_at_hex(pc_writer_t *w, const char *key, const char at[2]) {
    static const char HEX[] = "0123456789ABCDEF";
    uint8_t v[8];
    int n = Xbee_QueryAt(at, v, sizeof(v));
    if (n <= 0) return;
    char s[2 * sizeof(v) + 1];
    for (int i = 0; i < n; i++) {
        s[2 * i]     = HEX[v[i] >> 4];
        s[2 * i + 1] = HEX[v[i] & 0x0F];
    }
    s[2 * n] = '\0';
    pc_key(w, key); pc_str(w, s);
}

// ============================================================
// get_radio_info (出荷検査・診断用)
//   XBee モジュールの 64bit MAC (SH+SL)、firmware version (VR)、BLE MAC (BL) と、
//   動作に必要な設定値を返す (出荷検査で期待値と比較する)。
//   response: { "result": { "xbee_mac":"0013A200XXXXXXXX", "xbee_fw":"XXXX",
//                           "ble_mac":"XXXXXXXXXXXX", "pan_id":"XXXXXXXXXXXXXXXX",
//                           "ap":1, "sm":1, "bd":7, "bt":1, "ce":0 } }
//   XBee 無応答時は error (xbee_no_response)。
// ============================================================
void ph_get_radio_info(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    uint8_t sh[4] = {0}, sl[4] = {0}, vr[4] = {0};
    int nh = Xbee_QueryAt("SH", sh, sizeof(sh));
    int nl = Xbee_QueryAt("SL", sl, sizeof(sl));
    if (nh <= 0 || nl <= 0) {
        send_simple_error(id, src, "xbee_no_response", "SH/SL query failed");
        return;
    }
    int nv = Xbee_QueryAt("VR", vr, sizeof(vr));

    // SH/SL は上位バイトの 0 が省略されて返ることがあるため、8 バイト固定に右詰め
    uint8_t mac[8] = {0};
    for (int i = 0; i < nh; i++) mac[4 - nh + i] = sh[i];
    for (int i = 0; i < nl; i++) mac[8 - nl + i] = sl[i];
    char macbuf[20];
    snprintf(macbuf, sizeof(macbuf), "%02X%02X%02X%02X%02X%02X%02X%02X",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5], mac[6], mac[7]);

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "xbee_mac"); pc_str(&w, macbuf);
    if (nv > 0) {
        char vrbuf[12];
        char *p = vrbuf;
        for (int i = 0; i < nv && (p - vrbuf) < (int)sizeof(vrbuf) - 3; i++)
            p += snprintf(p, 3, "%02X", vr[i]);
        pc_key(&w, "xbee_fw"); pc_str(&w, vrbuf);
    }
    put_at_hex (&w, "ble_mac", "BL");   // BLE アドバタイズのアドレス
    put_at_hex (&w, "pan_id",  "ID");   // 親機と一致している必要がある
    put_at_uint(&w, "ap", "AP");        // 1 = API (エスケープなし)
    put_at_uint(&w, "sm", "SM");        // 1 = Pin Hibernate
    put_at_uint(&w, "bd", "BD");        // 7 = 115200 bps
    put_at_uint(&w, "bt", "BT");        // 1 = BLE 有効
    put_at_uint(&w, "ce", "CE");        // 0 = End Device (親機にはならない)
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}

// ============================================================
// get_diag (安定性診断用)
//   response: { "result": { "stack_free_min":N, "reset_flags":F,
//                           "zb_tx_status":S, "zb_tx_fail":E, "zb_tx_last_fail":C } }
//   zb_tx_*       : 起動以来の Zigbee 送信結果 (TX Status 0x8B)。受信数・失敗数
//                   (delivery != 0)・最後の失敗コード (0x21 = 無線 ACK なし、0x22 = 未参加 等)
//   stack_free_min: 起動以来、スタックが最も深く伸びた時点の残り RAM [byte]
//   reset_flags   : 起動時のリセット要因 (RSTCTRL.RSTFR)。bit0 PORF(電源投入),
//                   1 BORF(低電圧), 2 EXTRF(外部), 3 WDRF(WDT), 4 SWRF(ソフト), 5 UPDIRF
// ============================================================
//   params (省略可, USB-CDC のみ): { "hang": true }
//     応答を返した後にわざと WDT をリセットしないループに入る。約 8 秒後に WDT で
//     再起動し、次の get_diag の reset_flags に WDRF (bit3) が立つことで WDT の動作を
//     確認できる (検査用)。
void ph_get_diag(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    bool hang = false;
    if (params_tok >= 0 && src == SRC_USB) {
        int t = pc_obj_get(json, tokens, ntokens, params_tok, "hang");
        if (t >= 0) (void)pc_tok_bool(json, &tokens[t], &hang);
    }

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    uint16_t zb_status, zb_fail;
    uint8_t  zb_last_fail;
    Xbee_GetZigbeeTxStats(&zb_status, &zb_fail, &zb_last_fail);

    pc_key(&w, "stack_free_min");  pc_uint(&w, MAIN_GetStackFreeMin());
    pc_key(&w, "reset_flags");     pc_uint(&w, MAIN_GetResetFlags());
    pc_key(&w, "zb_tx_status");    pc_uint(&w, zb_status);
    pc_key(&w, "zb_tx_fail");      pc_uint(&w, zb_fail);
    pc_key(&w, "zb_tx_last_fail"); pc_uint(&w, zb_last_fail);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);

    if (hang) {
        USB_Flush();          // 応答をホストへ送り切ってから止まる
        while (1) { }         // WDT リセット待ち
    }
}

// ============================================================
// echo (diagnostic, no side effects)
//   request:  { "command":"echo", "params":{"size":N} }
//   response: { "result":{"size":N,"data":"xxx...x"} }   (N 文字の 'x')
//   size 省略時は data 無しで {"size":0}
// 応答サイズを自由に制御できるため chunk 分割・連続送信のバグ切り分けに使う。
// ============================================================
void ph_echo(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    int32_t size = 0;
    if (params_tok >= 0) {
        int sz_tok = pc_obj_get(json, tokens, ntokens, params_tok, "size");
        if (sz_tok >= 0 && tokens[sz_tok].type == JSMN_PRIMITIVE) {
            int32_t v = pc_tok_int(json, &tokens[sz_tok]);
            if (v > 0) size = v;
        }
    }
    // s_tx_buf は 512B。envelope `{"v":1,"id":XXXXX,"result":{"size":XXX,"data":""}}\n` で
    // 60B 程度消費するため、安全マージンを取って data 上限を 440B に。
    if (size > 440) size = 440;

    char filler[441];
    for (int i = 0; i < size; i++) filler[i] = 'x';
    filler[size] = '\0';

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "size"); pc_int(&w, size);
    pc_key(&w, "data"); pc_str(&w, filler);
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);
}
