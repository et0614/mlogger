#include "protocol_handlers.h"
#include "protocol_codec.h"
#include "command_handler.h"   // CH_Reply
#include "version.h"
#include "logger_control.h"    // LC_IsLogging, LC_SetCurrentTime, LC_GetCurrentTime, LC_StartLoggingTask, LC_EndLoggingTask, LC_ClearData, LC_FactoryResetCO2, LC_CalibrateCO2
#include "eeprom_manager.h"    // EM_mlName, EM_cFactors, EM_mSettings, EM_save*
#include "usb_extension.h"     // USB_StartRecordStream, rec_latest

#include <avr/io.h>            // SIGROW
#include <stdio.h>
#include <string.h>

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
    snprintf(out, out_cap, "%08lX", (unsigned long)h);
}

static void send_simple_error(int32_t id, CommandSource_t src, const char *code, const char *msg) {
    char buf[160];
    size_t n = pc_make_error(buf, sizeof(buf), id, code, msg);
    if (n > 0) CH_Reply(buf, src);
}

// 範囲チェック
static bool in_range_f(float v, float lo, float hi) { return v >= lo && v <= hi; }

// ============================================================
// センサ ⇔ 設定/補正のマッピング
// (PATCH 適用 + 応答生成の両方で使う)
// ============================================================
typedef struct {
    const char *name;
    bool *enabled_ptr;
    unsigned int *interval_ptr;
} sensor_setting_t;

// 注: t_dry と humidity はハードウェア (SHT4x) 上で同一の measure_th/interval_th を共有
// プロトコル上では分けて見えるが、片方を変えるともう片方も変わる
static const sensor_setting_t SENSOR_SETTINGS[] = {
    { "t_dry",       &EM_mSettings.measure_th,  &EM_mSettings.interval_th  },
    { "humidity",    &EM_mSettings.measure_th,  &EM_mSettings.interval_th  },
    { "t_glb",       &EM_mSettings.measure_glb, &EM_mSettings.interval_glb },
    { "velocity",    &EM_mSettings.measure_vel, &EM_mSettings.interval_vel },
    { "illuminance", &EM_mSettings.measure_ill, &EM_mSettings.interval_ill },
    { "co2",         &EM_mSettings.measure_co2, &EM_mSettings.interval_co2 },
};
#define NUM_SENSOR_SETTINGS (sizeof(SENSOR_SETTINGS) / sizeof(SENSOR_SETTINGS[0]))

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
        pc_key(w, "enabled");  pc_bool(w, *SENSOR_SETTINGS[i].enabled_ptr);
        pc_key(w, "interval"); pc_uint(w, *SENSOR_SETTINGS[i].interval_ptr);
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
    if (ts_tok < 0 || tokens[ts_tok].type != JSMN_PRIMITIVE) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'ts'");
        return;
    }
    int32_t ts = pc_tok_int(json, &tokens[ts_tok]);
    LC_SetCurrentTime((time_t)ts);

    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "ts"); pc_int(&w, (int32_t)LC_GetCurrentTime());
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

void ph_set_settings(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    bool changed = false;

    // 各センサについてキーが指定されていれば PATCH
    for (size_t i = 0; i < NUM_SENSOR_SETTINGS; i++) {
        int s_tok = pc_obj_get(json, tokens, ntokens, params_tok, SENSOR_SETTINGS[i].name);
        if (s_tok < 0) continue;
        if (tokens[s_tok].type != JSMN_OBJECT) {
            send_simple_error(id, src, "invalid_params", "sensor value must be object");
            return;
        }
        // enabled
        int en_tok = pc_obj_get(json, tokens, ntokens, s_tok, "enabled");
        if (en_tok >= 0) {
            bool en;
            if (!pc_tok_bool(json, &tokens[en_tok], &en)) {
                send_simple_error(id, src, "invalid_params", "enabled must be boolean");
                return;
            }
            *SENSOR_SETTINGS[i].enabled_ptr = en;
            changed = true;
        }
        // interval
        int iv_tok = pc_obj_get(json, tokens, ntokens, s_tok, "interval");
        if (iv_tok >= 0) {
            int32_t iv = pc_tok_int(json, &tokens[iv_tok]);
            if (iv < 0 || iv > 99999) {
                send_simple_error(id, src, "out_of_range", "interval must be 0-99999");
                return;
            }
            *SENSOR_SETTINGS[i].interval_ptr = (unsigned int)iv;
            changed = true;
        }
    }

    // start_ts は params 直下
    int st_tok = pc_obj_get(json, tokens, ntokens, params_tok, "start_ts");
    if (st_tok >= 0) {
        if (tokens[st_tok].type != JSMN_PRIMITIVE) {
            send_simple_error(id, src, "invalid_params", "start_ts must be number");
            return;
        }
        int32_t st = pc_tok_int(json, &tokens[st_tok]);
        EM_mSettings.start_dt = (uint32_t)st;
        changed = true;
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

void ph_set_correction(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    bool changed = false;

    for (size_t i = 0; i < NUM_CORRECTIONS; i++) {
        int s_tok = pc_obj_get(json, tokens, ntokens, params_tok, CORRECTIONS[i].name);
        if (s_tok < 0) continue;
        if (tokens[s_tok].type != JSMN_OBJECT) {
            send_simple_error(id, src, "invalid_params", "sensor value must be object");
            return;
        }
        int a_tok = pc_obj_get(json, tokens, ntokens, s_tok, "a");
        if (a_tok >= 0) {
            float a;
            if (!pc_tok_float(json, &tokens[a_tok], &a)) {
                send_simple_error(id, src, "invalid_params", "'a' must be number");
                return;
            }
            if (!in_range_f(a, CORRECTIONS[i].a_min, CORRECTIONS[i].a_max)) {
                send_simple_error(id, src, "out_of_range", "'a' out of range");
                return;
            }
            *CORRECTIONS[i].a_ptr = a;
            changed = true;
        }
        int b_tok = pc_obj_get(json, tokens, ntokens, s_tok, "b");
        if (b_tok >= 0) {
            float b;
            if (!pc_tok_float(json, &tokens[b_tok], &b)) {
                send_simple_error(id, src, "invalid_params", "'b' must be number");
                return;
            }
            if (!in_range_f(b, CORRECTIONS[i].b_min, CORRECTIONS[i].b_max)) {
                send_simple_error(id, src, "out_of_range", "'b' out of range");
                return;
            }
            *CORRECTIONS[i].b_ptr = b;
            changed = true;
        }
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
    LC_ClearData();
    send_empty_result(id, src);
}

// ============================================================
// calibrate_co2
//   params: { mode: "forced"|"factory", target_ppm: int }
// ============================================================
void ph_calibrate_co2(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    if (params_tok < 0) {
        send_simple_error(id, src, "invalid_params", "missing params");
        return;
    }

    int mode_tok = pc_obj_get(json, tokens, ntokens, params_tok, "mode");
    if (mode_tok < 0 || tokens[mode_tok].type != JSMN_STRING) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'mode'");
        return;
    }
    int target_tok = pc_obj_get(json, tokens, ntokens, params_tok, "target_ppm");
    if (target_tok < 0 || tokens[target_tok].type != JSMN_PRIMITIVE) {
        send_simple_error(id, src, "invalid_params", "missing or invalid 'target_ppm'");
        return;
    }
    int32_t target = pc_tok_int(json, &tokens[target_tok]);
    if (target < 0 || target > 65535) {
        send_simple_error(id, src, "out_of_range", "target_ppm must be 0-65535");
        return;
    }

    bool is_forced  = pc_tok_eq(json, &tokens[mode_tok], "forced");
    bool is_factory = pc_tok_eq(json, &tokens[mode_tok], "factory");
    if (!is_forced && !is_factory) {
        send_simple_error(id, src, "invalid_params", "mode must be 'forced' or 'factory'");
        return;
    }

    if (is_forced) {
        LC_CalibrateCO2((uint16_t)target, 30);                // 30秒モード
    } else {
        LC_FactoryResetCO2((uint16_t)target, 12U * 3600U);    // 12時間モード
    }

    // 即時 ACK (進捗は co2_calibration_progress event で送出 — Phase D)
    send_empty_result(id, src);
}

// ============================================================
// dump (USB-CDC専用)
//   JSON ヘッダ送信後、バイナリストリームに切り替え
//   dump_end イベントは Phase D で実装予定 (現状はクライアント側で count*record_size を受け切ったら完了とみなす)
// ============================================================
void ph_dump(int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src) {
    (void)json; (void)tokens; (void)ntokens; (void)params_tok;

    if (src != SRC_USB) {
        send_simple_error(id, src, "unsupported_transport", "dump is USB-CDC only");
        return;
    }

    // JSON ヘッダ送信
    pc_writer_t w;
    pc_begin_result(&w, s_tx_buf, sizeof(s_tx_buf), id);
    pc_key(&w, "count");       pc_uint(&w, rec_latest);
    pc_key(&w, "record_size"); pc_uint(&w, 18);
    pc_key(&w, "format");      pc_str(&w, "<BIBIhhHHHH>");
    pc_end_result(&w);
    if (pc_ok(&w)) CH_Reply(s_tx_buf, src);

    // バイナリストリーム開始 (USB_Stream_Task が非同期にレコードを送出)
    USB_StartRecordStream();
}
