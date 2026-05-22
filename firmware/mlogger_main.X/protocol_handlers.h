/*
 * File:   protocol_handlers.h
 * Author: e.togashi
 *
 * v4 各コマンドハンドラ実装
 */
#ifndef PROTOCOL_HANDLERS_H
#define PROTOCOL_HANDLERS_H

#include "protocol_dispatch.h"

#ifdef __cplusplus
extern "C" {
#endif

// Phase A
void ph_hello          (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);

// Phase B: get/set 系
void ph_set_name       (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_set_time       (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_get_settings   (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_set_settings   (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_get_correction (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_set_correction (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);

// Phase C: action 系
void ph_start_logging  (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_stop_logging   (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_clear_data     (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_erase_flash    (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_calibrate_co2  (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);
void ph_dump           (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);

// 診断用: 副作用なしで任意サイズの応答を生成
//   params: {"size": N}  → result.data に N 文字の 'x' を含めて返す (N の上限は s_tx_buf - envelope)
//   params 省略時は data 無し
void ph_echo           (int32_t id, const char *json, const jsmntok_t *tokens, int ntokens, int params_tok, CommandSource_t src);

#ifdef __cplusplus
}
#endif

#endif /* PROTOCOL_HANDLERS_H */
