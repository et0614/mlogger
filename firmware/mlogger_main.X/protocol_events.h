/*
 * File:   protocol_events.h
 * Author: e.togashi
 *
 * v4 通信プロトコル 自発イベント送出
 *   - smp (計測サンプル)
 *   - co2_calibration_progress (CO2校正進捗)
 *   - dump_end (ダンプ完了)
 *   - ready (ハートビート、未配線 — main.c 改修待ち)
 */
#ifndef PROTOCOL_EVENTS_H
#define PROTOCOL_EVENTS_H

#include <stdint.h>
#include <stdbool.h>
#include "w25q256.h"   // SensorData_t
#include "command_handler.h"   // CommandSource_t

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief 計測サンプル smp イベントを送出。
 *        計測しないセンサのキーは省略 (spec 5.2 準拠)。
 *        warmup 中のカテゴリは smp.data.wu 配列、切断中のカテゴリは smp.data.dc 配列で通知:
 *          - "g": 一般 (温湿度/グローブ温度/CO2)
 *          - "v": 風速
 *          - "l": 照度 (将来予約)
 * @param data               計測値 + valid_flags
 * @param warmingGeneral     一般カテゴリ (CO2/th_probe) が warmup 中なら true
 * @param warmingVelocity    風速 (熱線) が warmup 中なら true
 * @param disconnectedGeneral 一般 probe が切断されている (測定試みたが全 invalid) なら true
 * @param disconnectedVelocity 風速 probe が切断されている なら true
 */
void pe_emit_smp(const SensorData_t *data,
                 bool warmingGeneral, bool warmingVelocity,
                 bool disconnectedGeneral, bool disconnectedVelocity,
                 bool toZigbee, bool toBLE, bool toUSB);

/**
 * @brief CO2 校正進捗イベントを送出 (XBee+BLE)。
 * @param remaining_s     残り秒数
 * @param state           "measuring" / "pass" / "fail"
 * @param correction_ppm  校正完了時の補正値 [ppm]
 * @param current_ppm     現在の計測値 [ppm]
 */
void pe_emit_co2_calibration_progress(uint16_t remaining_s, const char *state,
                                      int16_t correction_ppm, uint16_t current_ppm);

/**
 * @brief dump 完了イベントを dest で指定された transport (USB/BLE/Zigbee) に送出。
 *        usb_extension の stream-done コールバックから呼ばれ、dest は dump 開始時の
 *        送出 transport と一致する。
 */
void pe_emit_dump_end(uint32_t records_sent, CommandSource_t dest);

/**
 * @brief ready ハートビートを送出 (現状未配線)。
 */
void pe_emit_ready(uint32_t uptime_s, bool logging, bool toZigbee, bool toBLE);

/**
 * @brief time_sync_request イベントを送出 (Zigbee + BLE)。
 *        子機が長期計測中の RTC drift 補正を目的として親機に時刻設定を能動的に
 *        要求する。送出後 window_s 秒間は無線を awake 維持して set_time を待つ。
 *        spec 5.4 準拠。
 * @param window_s  親機が set_time を送れる猶予秒数 (子機の wake 維持時間)
 */
void pe_emit_time_sync_request(uint16_t window_s);

#ifdef __cplusplus
}
#endif

#endif /* PROTOCOL_EVENTS_H */
