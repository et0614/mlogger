/*
 * File:   protocol_dispatch.h
 * Author: e.togashi
 *
 * v4 通信プロトコル: JSON コマンドのルーティング
 */
#ifndef PROTOCOL_DISPATCH_H
#define PROTOCOL_DISPATCH_H

#include "command_handler.h"  // CommandSource_t
#include "jsmn.h"
#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// 各コマンドハンドラのシグネチャ
//   id          : リクエスト id (応答に必要)
//   json        : 完全な受信JSON文字列
//   tokens      : jsmn パース結果
//   ntokens     : 有効トークン数
//   params_tok  : "params" 値のトークン index (無ければ -1)
//   src         : 送信元 (応答先 transport)
typedef void (*pd_handler_fn)(
    int32_t id,
    const char *json,
    const jsmntok_t *tokens,
    int ntokens,
    int params_tok,
    CommandSource_t src
);

typedef struct {
    const char *name;
    pd_handler_fn handler;
} pd_command_t;

// JSON コマンドを受け取りディスパッチする
// JSON parse 失敗時/未知コマンド時は err 応答を src に送る
void pd_dispatch(const char *json, size_t len, CommandSource_t src);

#ifdef __cplusplus
}
#endif

#endif /* PROTOCOL_DISPATCH_H */
