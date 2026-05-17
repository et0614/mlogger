#include "protocol_codec.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

// ============================================================
// 内部ユーティリティ
// ============================================================
static void append_char(pc_writer_t *w, char c) {
    if (w->overflow) return;
    if (w->pos >= w->cap - 1) { w->overflow = true; return; }
    w->buf[w->pos++] = c;
    w->buf[w->pos] = '\0';
}

static void append_str(pc_writer_t *w, const char *s) {
    while (*s) append_char(w, *s++);
}

// 文字列を JSON 文字列リテラル本体として書く (両端の " は呼び出し側で)
// 必要最小限のエスケープ: " と \\ と制御文字
static void append_str_escaped(pc_writer_t *w, const char *s) {
    while (*s) {
        unsigned char c = (unsigned char)*s;
        if (c == '\"') { append_char(w, '\\'); append_char(w, '\"'); }
        else if (c == '\\') { append_char(w, '\\'); append_char(w, '\\'); }
        else if (c == '\n') { append_char(w, '\\'); append_char(w, 'n'); }
        else if (c == '\r') { append_char(w, '\\'); append_char(w, 'r'); }
        else if (c == '\t') { append_char(w, '\\'); append_char(w, 't'); }
        else if (c < 0x20) {
            char tmp[8];
            snprintf(tmp, sizeof(tmp), "\\u%04X", c);
            append_str(w, tmp);
        }
        else append_char(w, (char)c);
        s++;
    }
}

static void emit_comma_if_needed(pc_writer_t *w) {
    if (w->depth > 0 && w->need_comma[w->depth - 1]) {
        append_char(w, ',');
        w->need_comma[w->depth - 1] = false;
    }
}

static void mark_value_written(pc_writer_t *w) {
    if (w->depth > 0) w->need_comma[w->depth - 1] = true;
}

// ============================================================
// Writer API
// ============================================================
void pc_init(pc_writer_t *w, char *buf, size_t cap) {
    w->buf = buf;
    w->cap = cap;
    w->pos = 0;
    w->overflow = false;
    w->depth = 0;
    for (int i = 0; i < PC_MAX_DEPTH; i++) w->need_comma[i] = false;
    if (cap > 0) buf[0] = '\0';
}

bool   pc_ok(const pc_writer_t *w)  { return !w->overflow; }
size_t pc_len(const pc_writer_t *w) { return w->pos; }

void pc_obj_begin(pc_writer_t *w) {
    emit_comma_if_needed(w);
    append_char(w, '{');
    if (w->depth < PC_MAX_DEPTH) {
        w->need_comma[w->depth] = false;
        w->depth++;
    } else {
        w->overflow = true;
    }
}

void pc_obj_end(pc_writer_t *w) {
    append_char(w, '}');
    if (w->depth > 0) w->depth--;
    mark_value_written(w);
}

void pc_arr_begin(pc_writer_t *w) {
    emit_comma_if_needed(w);
    append_char(w, '[');
    if (w->depth < PC_MAX_DEPTH) {
        w->need_comma[w->depth] = false;
        w->depth++;
    } else {
        w->overflow = true;
    }
}

void pc_arr_end(pc_writer_t *w) {
    append_char(w, ']');
    if (w->depth > 0) w->depth--;
    mark_value_written(w);
}

void pc_key(pc_writer_t *w, const char *k) {
    emit_comma_if_needed(w);
    append_char(w, '\"');
    append_str_escaped(w, k);
    append_char(w, '\"');
    append_char(w, ':');
    // key 後は value を待つので need_comma は触らない
}

void pc_str(pc_writer_t *w, const char *s) {
    emit_comma_if_needed(w);
    append_char(w, '\"');
    append_str_escaped(w, s);
    append_char(w, '\"');
    mark_value_written(w);
}

void pc_int(pc_writer_t *w, int32_t v) {
    emit_comma_if_needed(w);
    char tmp[16];
    snprintf(tmp, sizeof(tmp), "%ld", (long)v);
    append_str(w, tmp);
    mark_value_written(w);
}

void pc_uint(pc_writer_t *w, uint32_t v) {
    emit_comma_if_needed(w);
    char tmp[16];
    snprintf(tmp, sizeof(tmp), "%lu", (unsigned long)v);
    append_str(w, tmp);
    mark_value_written(w);
}

void pc_bool(pc_writer_t *w, bool v) {
    emit_comma_if_needed(w);
    append_str(w, v ? "true" : "false");
    mark_value_written(w);
}

void pc_null(pc_writer_t *w) {
    emit_comma_if_needed(w);
    append_str(w, "null");
    mark_value_written(w);
}

void pc_float(pc_writer_t *w, float v, uint8_t decimals) {
    emit_comma_if_needed(w);
    char fmt[8];
    char tmp[24];
    if (decimals > 9) decimals = 9;
    snprintf(fmt, sizeof(fmt), "%%.%uf", (unsigned)decimals);
    snprintf(tmp, sizeof(tmp), fmt, (double)v);
    append_str(w, tmp);
    mark_value_written(w);
}

void pc_finish(pc_writer_t *w) {
    append_char(w, '\n');
}

// ============================================================
// 応答エンベロープ
// ============================================================
void pc_begin_result(pc_writer_t *w, char *buf, size_t cap, int32_t id) {
    pc_init(w, buf, cap);
    pc_obj_begin(w);
    pc_key(w, "v"); pc_uint(w, 1);
    pc_key(w, "id"); pc_int(w, id);
    pc_key(w, "result");
    pc_obj_begin(w);
}

void pc_end_result(pc_writer_t *w) {
    pc_obj_end(w); // close result
    pc_obj_end(w); // close envelope
    pc_finish(w);
}

size_t pc_make_error(char *buf, size_t cap, int32_t id, const char *code, const char *msg) {
    pc_writer_t w;
    pc_init(&w, buf, cap);
    pc_obj_begin(&w);
    pc_key(&w, "v"); pc_uint(&w, 1);
    pc_key(&w, "id"); pc_int(&w, id);
    pc_key(&w, "error");
    pc_obj_begin(&w);
    pc_key(&w, "code"); pc_str(&w, code);
    pc_key(&w, "message"); pc_str(&w, msg);
    pc_obj_end(&w);
    pc_obj_end(&w);
    pc_finish(&w);
    return pc_ok(&w) ? pc_len(&w) : 0;
}

// ============================================================
// jsmn ヘルパ
// ============================================================
bool pc_tok_eq(const char *json, const jsmntok_t *t, const char *s) {
    if (t->type != JSMN_STRING && t->type != JSMN_PRIMITIVE) return false;
    int len = t->end - t->start;
    if ((int)strlen(s) != len) return false;
    return strncmp(json + t->start, s, len) == 0;
}

// トークン idx 以降を1つスキップ (子トークン含む) し、次の sibling index を返す
static int skip_token(const jsmntok_t *tokens, int idx, int ntokens) {
    if (idx >= ntokens) return ntokens;
    int end_pos = tokens[idx].end;
    int next = idx + 1;
    while (next < ntokens && tokens[next].start < end_pos) next++;
    return next;
}

int pc_obj_get(const char *json, const jsmntok_t *tokens, int ntokens, int obj_idx, const char *key) {
    if (obj_idx < 0 || obj_idx >= ntokens) return -1;
    const jsmntok_t *obj = &tokens[obj_idx];
    if (obj->type != JSMN_OBJECT) return -1;

    int i = obj_idx + 1;
    for (int k = 0; k < obj->size && i < ntokens; k++) {
        if (i + 1 >= ntokens) return -1;
        if (pc_tok_eq(json, &tokens[i], key)) {
            return i + 1;
        }
        i = skip_token(tokens, i + 1, ntokens);
    }
    return -1;
}

bool pc_obj_is_valid(const jsmntok_t *tokens, int ntokens, int obj_idx) {
    if (obj_idx < 0 || obj_idx >= ntokens) return false;
    const jsmntok_t *obj = &tokens[obj_idx];
    if (obj->type != JSMN_OBJECT) return false;

    int i = obj_idx + 1;
    for (int k = 0; k < obj->size && i < ntokens; k++) {
        if (i + 1 >= ntokens) return false;
        // キーは string でなければならない、かつ値1つ (size==1) がぶら下がっている必要
        if (tokens[i].type != JSMN_STRING) return false;
        if (tokens[i].size != 1) return false;
        i = skip_token(tokens, i + 1, ntokens);
    }
    return true;
}

int32_t pc_tok_int(const char *json, const jsmntok_t *t) {
    if (t->type != JSMN_PRIMITIVE) return 0;
    char tmp[16];
    int len = t->end - t->start;
    if (len <= 0 || len >= (int)sizeof(tmp)) return 0;
    memcpy(tmp, json + t->start, len);
    tmp[len] = '\0';
    return (int32_t)atol(tmp);
}

size_t pc_tok_strcpy(const char *json, const jsmntok_t *t, char *dst, size_t dst_cap) {
    if (dst_cap == 0) return 0;
    int len = t->end - t->start;
    if (len < 0) len = 0;
    if ((size_t)len >= dst_cap) len = (int)dst_cap - 1;
    memcpy(dst, json + t->start, len);
    dst[len] = '\0';
    return (size_t)len;
}

bool pc_tok_bool(const char *json, const jsmntok_t *t, bool *out) {
    if (t->type != JSMN_PRIMITIVE) return false;
    char c = json[t->start];
    if (c == 't') { *out = true;  return true; }
    if (c == 'f') { *out = false; return true; }
    if (c == '1') { *out = true;  return true; }
    if (c == '0') { *out = false; return true; }
    return false;
}

bool pc_tok_float(const char *json, const jsmntok_t *t, float *out) {
    if (t->type != JSMN_PRIMITIVE) return false;
    char tmp[24];
    int len = t->end - t->start;
    if (len <= 0 || len >= (int)sizeof(tmp)) return false;
    memcpy(tmp, json + t->start, len);
    tmp[len] = '\0';
    char *endp = NULL;
    float v = (float)strtod(tmp, &endp);
    if (endp == tmp) return false;
    *out = v;
    return true;
}
