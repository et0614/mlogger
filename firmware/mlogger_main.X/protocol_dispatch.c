#include "protocol_dispatch.h"
#include "protocol_codec.h"
#include "protocol_handlers.h"
#include "command_handler.h"   // CH_Reply

#include <string.h>

// jsmn 用トークンバッファ (set_settings 等の最大想定: 6センサ×3キー+envelope ≒ 30-50)
#define PD_MAX_TOKENS  64
static jsmntok_t s_tokens[PD_MAX_TOKENS];

// エラー応答用バッファ
static char s_err_buf[160];

// コマンドテーブル
static const pd_command_t s_commands[] = {
    { "hello",          ph_hello          },
    { "set_name",       ph_set_name       },
    { "set_time",       ph_set_time       },
    { "get_settings",   ph_get_settings   },
    { "set_settings",   ph_set_settings   },
    { "get_correction", ph_get_correction },
    { "set_correction", ph_set_correction },
    { "start_logging",  ph_start_logging  },
    { "stop_logging",   ph_stop_logging   },
    { "clear_data",     ph_clear_data     },
    { "calibrate_co2",  ph_calibrate_co2  },
    { "dump",           ph_dump           },
    { "echo",           ph_echo           },
    { NULL,             NULL              }
};

static void send_error(int32_t id, CommandSource_t src, const char *code, const char *msg) {
    size_t n = pc_make_error(s_err_buf, sizeof(s_err_buf), id, code, msg);
    if (n > 0) CH_Reply(s_err_buf, src);
}

void pd_dispatch(const char *json, size_t len, CommandSource_t src) {
    // 1. パース
    jsmn_parser parser;
    jsmn_init(&parser);
    int n = jsmn_parse(&parser, json, len, s_tokens, PD_MAX_TOKENS);
    if (n < 1 || s_tokens[0].type != JSMN_OBJECT) {
        send_error(0, src, "invalid_params", "JSON parse failed or not an object");
        return;
    }
    // ルート object が正しい key:value 並びか検証 (jsmn は ':' 抜けを完全に弾かないため)
    if (!pc_obj_is_valid(s_tokens, n, 0)) {
        send_error(0, src, "invalid_params", "malformed object");
        return;
    }

    // 2. id を抽出 (なければ 0 として進める)
    int32_t id = 0;
    int id_tok = pc_obj_get(json, s_tokens, n, 0, "id");
    if (id_tok >= 0) id = pc_tok_int(json, &s_tokens[id_tok]);

    // 3. command を抽出
    int cmd_tok = pc_obj_get(json, s_tokens, n, 0, "command");
    if (cmd_tok < 0 || s_tokens[cmd_tok].type != JSMN_STRING) {
        send_error(id, src, "invalid_params", "missing or invalid 'command'");
        return;
    }

    // 4. params (任意)
    int params_tok = pc_obj_get(json, s_tokens, n, 0, "params");
    // params は無くてよい。あればオブジェクトであるべき。
    if (params_tok >= 0 && s_tokens[params_tok].type != JSMN_OBJECT) {
        send_error(id, src, "invalid_params", "'params' must be an object");
        return;
    }

    // 5. コマンドテーブルでハンドラを検索
    for (const pd_command_t *c = s_commands; c->name != NULL; c++) {
        if (pc_tok_eq(json, &s_tokens[cmd_tok], c->name)) {
            c->handler(id, json, s_tokens, n, params_tok, src);
            return;
        }
    }

    // 6. 未知のコマンド
    send_error(id, src, "unknown_command", "no handler for this command");
}
