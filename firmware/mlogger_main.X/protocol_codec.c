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

// 数値の文字列化は snprintf を使わず自前で行う。このファイルの snprintf 呼び出しに
// XC8 が汎用版 (浮動小数点書式・64bit 対応込みで約 5 KB) を割り当ててしまい、
// フラッシュを圧迫していたため。
static void append_u32(pc_writer_t *w, uint32_t v) {
    char buf[11];                        // 4294967295 + NUL
    uint8_t i = sizeof(buf) - 1;
    buf[i] = '\0';
    do {
        buf[--i] = (char)('0' + (v % 10));
        v /= 10;
    } while (v != 0);
    append_str(w, &buf[i]);
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
            static const char HEX[] = "0123456789ABCDEF";
            append_str(w, "\\u00");
            append_char(w, HEX[c >> 4]);
            append_char(w, HEX[c & 0x0F]);
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
    if (v < 0) {
        append_char(w, '-');
        append_u32(w, (uint32_t)(-(v + 1)) + 1u);   // INT32_MIN でも桁あふれしない
    } else {
        append_u32(w, (uint32_t)v);
    }
    mark_value_written(w);
}

void pc_uint(pc_writer_t *w, uint32_t v) {
    emit_comma_if_needed(w);
    append_u32(w, v);
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

// float を固定小数点の 10 進文字列で出す。printf の %f を使うと XC8 の浮動小数点
// 書式エンジン一式 (%f / %a / 変換部で約 4.4 KB) がリンクされフラッシュを圧迫するため、
// 整数演算で組み立てる。NaN / ±Inf は JSON に書けないので null を出す。
void pc_float(pc_writer_t *w, float v, uint8_t decimals) {
    if (v != v || v > 3.4e38f || v < -3.4e38f) {   // NaN / ±Inf
        pc_null(w);
        return;
    }
    if (decimals > 6) decimals = 6;
    uint32_t scale = 1;
    for (uint8_t i = 0; i < decimals; i++) scale *= 10;

    bool neg = (v < 0.0f);
    float a = neg ? -v : v;
    if (a >= 4.0e9f) {                    // 整数部が uint32 に収まらない
        pc_null(w);
        return;
    }
    // 整数部を先に切り離し、小数部だけを scale 倍して丸める (大きな値に scale を
    // 掛けると float の有効桁が足りず最下位桁がずれるため)。ちょうど .5 は切り上げ。
    uint32_t ip = (uint32_t)a;
    uint32_t fp = (uint32_t)((a - (float)ip) * (float)scale + 0.5f);
    if (fp >= scale) { fp -= scale; ip++; }    // 0.9996 → 1.000 の繰り上がり
    uint32_t n = ip | fp;                      // 0 判定用 ("-0.000" を避ける)

    // 小数部は桁数ぶん 0 埋めで並べる
    char frac[8];                       // "." + 最大 6 桁 + NUL
    frac[0] = '\0';
    if (decimals > 0) {
        frac[0] = '.';
        for (int8_t i = (int8_t)decimals; i >= 1; i--) {
            frac[i] = (char)('0' + (fp % 10));
            fp /= 10;
        }
        frac[decimals + 1] = '\0';
    }

    emit_comma_if_needed(w);
    if (neg && n != 0) append_char(w, '-');   // "-0.000" は出さない
    append_u32(w, ip);
    append_str(w, frac);
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

bool pc_tok_u32(const char *json, const jsmntok_t *t, uint32_t *out) {
    if (t->type != JSMN_PRIMITIVE || t->end <= t->start) return false;
    uint32_t v = 0;
    for (int i = t->start; i < t->end; i++) {
        char c = json[i];
        if (c < '0' || c > '9') return false;
        uint8_t d = (uint8_t)(c - '0');
        if (v > (0xFFFFFFFFUL - d) / 10) return false;   // 桁あふれ
        v = v * 10 + d;
    }
    *out = v;
    return true;
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

// JSON の数値トークンを float に変換する。strtod (XC8 では約 1.9 KB) の代わりの最小実装。
// 書式: [-]digits[.digits][(e|E)[+|-]digits]。トークン全体が数値でなければ false。
bool pc_tok_float(const char *json, const jsmntok_t *t, float *out) {
    if (t->type != JSMN_PRIMITIVE) return false;
    const char *s   = json + t->start;
    const char *end = json + t->end;
    if (s >= end) return false;

    bool  neg    = false;
    float v      = 0.0f;
    int   digits = 0;
    int   exp10  = 0;

    if (*s == '-') { neg = true; s++; }
    while (s < end && *s >= '0' && *s <= '9') { v = v * 10.0f + (float)(*s - '0'); s++; digits++; }
    if (s < end && *s == '.') {
        s++;
        while (s < end && *s >= '0' && *s <= '9') {
            v = v * 10.0f + (float)(*s - '0'); exp10--; s++; digits++;
        }
    }
    if (digits == 0) return false;
    if (s < end && (*s == 'e' || *s == 'E')) {
        s++;
        bool eneg = false;
        if (s < end && (*s == '-' || *s == '+')) { eneg = (*s == '-'); s++; }
        int e = 0, edigits = 0;
        while (s < end && *s >= '0' && *s <= '9') {
            if (e < 100) e = e * 10 + (*s - '0');
            s++; edigits++;
        }
        if (edigits == 0) return false;
        exp10 += eneg ? -e : e;
    }
    if (s != end) return false;   // 数値の後ろに余計な文字

    // 10 の冪を作って 1 回だけ掛ける/割る (10 で繰り返し割ると丸め誤差が累積する)。
    // 10^10 までは float で正確に表せる。
    float p = 1.0f;
    for (int k = (exp10 < 0) ? -exp10 : exp10; k > 0; k--) p *= 10.0f;
    v = (exp10 < 0) ? v / p : v * p;
    *out = neg ? -v : v;
    return true;
}
