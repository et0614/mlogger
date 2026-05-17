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
#include "w25q512.h"   // SensorData_t

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief 計測サンプル smp イベントを送出。
 *        計測しないセンサのキーは省略 (spec 5.2 準拠)。
 */
void pe_emit_smp(const SensorData_t *data, bool toZigbee, bool toBLE, bool toUSB);

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
 * @brief dump 完了イベントを送出 (USB-CDC)。
 *        usb_extension の stream-done コールバックから呼ばれる。
 */
void pe_emit_dump_end(uint32_t records_sent);

/**
 * @brief ready ハートビートを送出 (現状未配線)。
 */
void pe_emit_ready(uint32_t uptime_s, bool logging, bool toZigbee, bool toBLE);

#ifdef __cplusplus
}
#endif

#endif /* PROTOCOL_EVENTS_H */
