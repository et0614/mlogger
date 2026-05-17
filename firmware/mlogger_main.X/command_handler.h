/* 
 * File:   command_handler.h
 * Author: e.togashi
 *
 * Created on 2026/01/01, 15:20
 */

#ifndef COMMAND_HANDLER_H
#define	COMMAND_HANDLER_H

#ifdef	__cplusplus
extern "C" {
#endif

// コマンドの送信元
typedef enum {
    SRC_USB,   // USB CDC経由
    SRC_XBEE,  // Zigbee経由
    SRC_BLE    // Bluetooth LE経由
} CommandSource_t;

/**
 * @brief 文字からコマンドを組み立てる
 * @param cmd 文字
 * @param src 文字送信元
 */
void CH_AppendChar(char c, CommandSource_t src);

/**
 * @brief 文字列からコマンドを組み立てる
 * @param cmd 文字列
 * @param src 文字列送信元
 */
void CH_AppendString(const char *str, CommandSource_t src);

/**
 * @brief コマンドを処理する
 * @param cmd コマンド文字列
 * @param src コマンド送信元
 */
void CH_ProcessCommand(const char *cmd, CommandSource_t src);

/**
 * @brief 応答を送信元 transport へ送り返す (v4 プロトコルハンドラ用に公開)
 * @param msg 送信文字列 (改行終端済み)
 * @param src 送信先 transport
 */
void CH_Reply(const char *msg, CommandSource_t src);

#ifdef	__cplusplus
}
#endif

#endif	/* COMMAND_HANDLER_H */

