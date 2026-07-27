/*
 * File:   json_protocol.h
 * Author: e.togashi
 *
 * PC ↔ デバイス間の JSON コマンドプロトコル (USB-CDC 単一トランスポート)。
 *
 * リクエスト (PC → device, 1行 = 1コマンド, '\n' 終端):
 *     {"id":1,"command":"hello"}
 *     {"id":2,"command":"echo","params":{"n":42}}
 *
 * 応答 (device → PC, 1行 = 1応答, '\n' 終端):
 *     成功: {"id":1,"ok":true, ...任意の追加フィールド...}
 *     失敗: {"id":1,"ok":false,"error":"<code>"}
 *
 * '{' で始まらない行は無視する (将来の '#' デバッグ行等と衝突させないため)。
 */
#ifndef JSON_PROTOCOL_H
#define JSON_PROTOCOL_H

#ifdef __cplusplus
extern "C" {
#endif

/// 受信1文字をラインバッファに積む。'\r'/'\n' で1行確定→ JP_ProcessLine。
/// USB_Comm_Task() から1文字ずつ呼ばれる。
void JP_AppendChar(char c);

/// 確定した1行(ヌル終端JSON文字列)をパースしてコマンドをディスパッチする。
/// 通常は JP_AppendChar 経由で呼ばれるが、テスト用に直接呼んでもよい。
void JP_ProcessLine(const char *line);

#ifdef __cplusplus
}
#endif

#endif /* JSON_PROTOCOL_H */
