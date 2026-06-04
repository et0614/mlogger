#include "protocol_events.h"
#include "protocol_codec.h"
#include "xbee_controller.h"   // Xbee_TxChars, Xbee_BlChars, Xbee_BlTxChars
#include "usb_extension.h"     // USB_CDC_SendString
#include "logger_control.h"    // LC_GetCurrentTime

#include <time.h>

// イベント送信バッファ (smp ~90B, co2_cal ~110B, dump_end ~80B、安全側で 256B)
static char s_evt_buf[256];

// ============================================================
// 内部: エンベロープ書き出しヘルパ
//   {"v":1,"event":"<name>","ts":<ts>,"data":{ ... }}\n
// ============================================================
static void begin_event(pc_writer_t *w, const char *event_name) {
    pc_init(w, s_evt_buf, sizeof(s_evt_buf));
    pc_obj_begin(w);
    pc_key(w, "v");     pc_uint(w, 1);
    pc_key(w, "event"); pc_str(w, event_name);
    pc_key(w, "ts");    pc_uint(w, (uint32_t)LC_GetCurrentTime());
    pc_key(w, "data");
    pc_obj_begin(w);
}

static void end_event(pc_writer_t *w) {
    pc_obj_end(w);  // data
    pc_obj_end(w);  // envelope
    pc_finish(w);
}

// ============================================================
// smp イベント
//   data: {t,h,g,vel,l,c} 計測しないキーは省略
// ============================================================
void pe_emit_smp(const SensorData_t *data,
                 bool warmingGeneral, bool warmingVelocity,
                 bool disconnectedGeneral, bool disconnectedVelocity,
                 bool toZigbee, bool toBLE, bool toUSB) {
    pc_writer_t w;
    pc_init(&w, s_evt_buf, sizeof(s_evt_buf));
    pc_obj_begin(&w);
    pc_key(&w, "v");     pc_uint(&w, 1);
    pc_key(&w, "event"); pc_str(&w, "smp");
    pc_key(&w, "ts");    pc_uint(&w, (uint32_t)data->timestamp);
    pc_key(&w, "data");
    pc_obj_begin(&w);

    if (data->valid_flags & FLAG_TEMP_DRY) {
        pc_key(&w, "t");   pc_float(&w, (float)data->temp_dry   / 100.0f,   2);
    }
    if (data->valid_flags & FLAG_HUMIDITY) {
        pc_key(&w, "h");   pc_float(&w, (float)data->humidity   / 100.0f,   1);
    }
    if (data->valid_flags & FLAG_TEMP_GLOBE) {
        pc_key(&w, "g");   pc_float(&w, (float)data->temp_globe / 100.0f,   2);
    }
    if (data->valid_flags & FLAG_WIND_SPEED) {
        pc_key(&w, "v"); pc_float(&w, (float)data->wind_speed / 10000.0f, 3);
    }
    if (data->valid_flags & FLAG_VOLTAGE) {
        // 風速プローブの熱線 bridge 電圧 (mV)。
        // 風速 OOR や異常時の原因切り分け用。dump record にも同じ値が記録される。
        pc_key(&w, "vv"); pc_uint(&w, data->voltage);
    }
    if (data->valid_flags & FLAG_ILLUMINANCE) {
        // illuminance は 0.1 lx 単位 → 整数 lx に丸める
        pc_key(&w, "l");   pc_uint(&w, (uint32_t)(data->illuminance / 10));
    }
    if (data->valid_flags & FLAG_CO2_PPM) {
        pc_key(&w, "c");   pc_uint(&w, data->co2_ppm);
    }

    // ウォームアップ中カテゴリ一覧 (該当ある時のみ). MAUI 側で「ウォームアップ中」表示用。
    // カテゴリ ID は計測設定 3 カテゴリ (general/velocity/illuminance) と smp キーを揃え:
    //   "g" = 一般 (CO2/th_probe)、t/h/g/c の欠損は warmup によるものと判別可能
    //   "v" = 風速 (熱線 wake-up 中)
    //   "l" = 照度 (将来予約)
    if (warmingGeneral || warmingVelocity) {
        pc_key(&w, "wu");
        pc_arr_begin(&w);
        if (warmingGeneral)  pc_str(&w, "g");
        if (warmingVelocity) pc_str(&w, "v");
        pc_arr_end(&w);
    }

    // 切断中カテゴリ一覧 (該当ある時のみ). 計測対象だが probe が応答しない状態。
    // MAUI 側で「センサ未接続」表示用。
    if (disconnectedGeneral || disconnectedVelocity) {
        pc_key(&w, "dc");
        pc_arr_begin(&w);
        if (disconnectedGeneral)  pc_str(&w, "g");
        if (disconnectedVelocity) pc_str(&w, "v");
        pc_arr_end(&w);
    }

    pc_obj_end(&w);
    pc_obj_end(&w);
    pc_finish(&w);

    if (!pc_ok(&w)) return;
    if (toZigbee) Xbee_TxChars(s_evt_buf);
    if (toBLE)    Xbee_BlChars(s_evt_buf);
    if (toUSB)    USB_CDC_SendString(s_evt_buf);
}

// ============================================================
// co2_calibration_progress イベント
// ============================================================
void pe_emit_co2_calibration_progress(uint16_t remaining_s, const char *state,
                                      int16_t correction_ppm, uint16_t current_ppm) {
    pc_writer_t w;
    begin_event(&w, "co2_calibration_progress");
    pc_key(&w, "remaining_s");    pc_uint(&w, remaining_s);
    pc_key(&w, "state");          pc_str(&w, state);
    pc_key(&w, "correction_ppm"); pc_int(&w, correction_ppm);
    pc_key(&w, "current_ppm");    pc_uint(&w, current_ppm);
    end_event(&w);

    if (!pc_ok(&w)) return;
    // 既存の CCL は XBee+BLE 両方に送出していたので踏襲
    Xbee_BlTxChars(s_evt_buf);
}

// ============================================================
// dump_end イベント (dump 開始時の transport に送出)
// ============================================================
void pe_emit_dump_end(uint32_t records_sent, CommandSource_t dest) {
    pc_writer_t w;
    begin_event(&w, "dump_end");
    pc_key(&w, "sent"); pc_uint(&w, records_sent);
    end_event(&w);

    if (!pc_ok(&w)) return;
    switch (dest) {
        case SRC_USB:  USB_CDC_SendString(s_evt_buf); break;
        case SRC_XBEE: Xbee_TxChars(s_evt_buf);       break;
        case SRC_BLE:  Xbee_BlChars(s_evt_buf);       break;
    }
}

// ============================================================
// ready イベント (現状未配線)
// ============================================================
void pe_emit_ready(uint32_t uptime_s, bool logging, bool toZigbee, bool toBLE) {
    pc_writer_t w;
    begin_event(&w, "ready");
    pc_key(&w, "uptime_s"); pc_uint(&w, uptime_s);
    pc_key(&w, "logging");  pc_bool(&w, logging);
    end_event(&w);

    if (!pc_ok(&w)) return;
    if (toZigbee) Xbee_TxChars(s_evt_buf);
    if (toBLE)    Xbee_BlChars(s_evt_buf);
}

// ============================================================
// time_sync_request イベント (spec 5.4)
//   親機 (MLServer 等) に時刻設定要求を投げる。送出後は logger_control が
//   window_s 秒間 sleep に入らず set_time コマンドの受信を待つ。
// ============================================================
void pe_emit_time_sync_request(uint16_t window_s) {
    pc_writer_t w;
    begin_event(&w, "time_sync_request");
    pc_key(&w, "window_s"); pc_uint(&w, window_s);
    end_event(&w);

    if (!pc_ok(&w)) return;
    // Zigbee 経由が主用途 (MLServer 想定)、BLE にも吐く (BLE 接続中も同期可能に)
    Xbee_BlTxChars(s_evt_buf);
}
