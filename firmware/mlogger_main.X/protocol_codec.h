/*
 * File:   protocol_codec.h
 * Author: e.togashi
 *
 * v4 通信プロトコル用 JSON ヘルパ
 *   - 増分的なJSON書き出し (アロケーションなし)
 *   - jsmn トークン操作ユーティリティ
 *   - 応答エンベロープ生成
 */
#ifndef PROTOCOL_CODEC_H
#define PROTOCOL_CODEC_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>
#include "jsmn.h"

#ifdef __cplusplus
extern "C" {
#endif

#define PC_MAX_DEPTH 8

// ============================================================
// JSON Writer (incremental, no allocation)
// ============================================================
typedef struct {
    char *buf;
    size_t cap;
    size_t pos;
    bool   overflow;
    int    depth;
    bool   need_comma[PC_MAX_DEPTH]; // per-nesting-level state
} pc_writer_t;

void   pc_init(pc_writer_t *w, char *buf, size_t cap);
bool   pc_ok(const pc_writer_t *w);
size_t pc_len(const pc_writer_t *w);

// Containers
void pc_obj_begin(pc_writer_t *w);
void pc_obj_end(pc_writer_t *w);
void pc_arr_begin(pc_writer_t *w);
void pc_arr_end(pc_writer_t *w);

// Key (for objects)
void pc_key(pc_writer_t *w, const char *k);

// Values (writes preceding comma if needed)
void pc_str(pc_writer_t *w, const char *s);
void pc_int(pc_writer_t *w, int32_t v);
void pc_uint(pc_writer_t *w, uint32_t v);
void pc_bool(pc_writer_t *w, bool v);
void pc_null(pc_writer_t *w);
void pc_float(pc_writer_t *w, float v, uint8_t decimals);

// Newline terminator
void pc_finish(pc_writer_t *w);

// ============================================================
// 応答エンベロープ
// ============================================================
// {"v":1,"id":N,"result":{ ... まで書く (以降は pc_key/pc_xxx で result の中身を埋める)
void   pc_begin_result(pc_writer_t *w, char *buf, size_t cap, int32_t id);
// }} + \n を書く
void   pc_end_result(pc_writer_t *w);

// 一発で完全なエラー応答を生成: {"v":1,"id":N,"error":{"code":"X","message":"Y"}}\n
// 戻り値: 書き出した長さ (0 ならオーバーフロー)
size_t pc_make_error(char *buf, size_t cap, int32_t id, const char *code, const char *msg);

// ============================================================
// jsmn トークン操作
// ============================================================
// 文字列トークンが指定の C 文字列と一致するか
bool    pc_tok_eq(const char *json, const jsmntok_t *t, const char *s);
// オブジェクト obj_idx の中で key に対応する値トークンの index を返す (見つからなければ -1)
int     pc_obj_get(const char *json, const jsmntok_t *tokens, int ntokens, int obj_idx, const char *key);
// オブジェクトが正しい key:value ペアの並びになっているか検証する
// (各キーが string で、続く値が1つだけぶら下がっている = size==1 を確認)
bool    pc_obj_is_valid(const jsmntok_t *tokens, int ntokens, int obj_idx);
// プリミティブトークンを int に
int32_t pc_tok_int(const char *json, const jsmntok_t *t);
// 文字列トークンをバッファにコピー (戻り値: 書き込んだ長さ、末尾 \0 含めず)
size_t  pc_tok_strcpy(const char *json, const jsmntok_t *t, char *dst, size_t dst_cap);
// プリミティブ true/false を bool に。成功時 true を返す
bool    pc_tok_bool(const char *json, const jsmntok_t *t, bool *out);
// プリミティブ数値を float に。成功時 true を返す
bool    pc_tok_float(const char *json, const jsmntok_t *t, float *out);

#ifdef __cplusplus
}
#endif

#endif /* PROTOCOL_CODEC_H */
