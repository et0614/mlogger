/* * File: usb_extension.h
 * Author: e.togashi
 *
 * Created on December 11, 2025
 *
 * Summary:
 * MCC Melody USB CDCドライバの拡張機能およびタスク管理。
 * PCからのコマンド受信("DUMP", "VERS"等)と、
 * Flashデータのバルク転送処理を提供。
 */

#ifndef MY_USB_EXT_H
#define	MY_USB_EXT_H

#ifdef	__cplusplus
extern "C" {
#endif

#include "mcc_generated_files/usb/usb_cdc/usb_cdc_virtual_serial_port.h"

// ==========================================
// 外部共有変数 (main.c等でvolatileで実体を定義する)
// ==========================================
    
/**
 * @brief データ送信範囲の管理変数
 * "DUMP"コマンド受信時に、この範囲のレコードを送信する。
 * recordIndex単位で管理。
 */
extern uint32_t rec_latest; // 最新データの書き込み位置インデックス
    
/**
 * @brief 文字列を送信する関数 (ヌル終端文字列用)
 * @param str 送信する文字列 (例: "Hello\n")
 * @return 送信に成功したバイト数
 */
uint16_t USB_CDC_SendString(const char *str);   

/**
 * @brief USB通信のメインタスク
 * mainループ内で定期的に呼び出す。
 * - アイドル時: コマンド受信待ち
 * - 送信時: Flashからデータを読み出しUSBへバースト転送
 */
void USB_Stream_Task(void);

/**
 * @brief 内蔵メモリのレコードストリーム送信を開始する (v4 dump 用: 件数prefix無し)
 *        ヘッダは呼び出し側で別途送信すること。
 *        実際の転送は USB_Stream_Task() が非同期に行う。
 */
void USB_StartRecordStream(void);

/**
 * @brief USB CDC 送信バッファの中身を強制 flush する (TX完了まで待機)
 *        スリープ移行直前 等で ACK が確実に送出されてほしい時に呼ぶ。
 *        タイムアウト約500ms。
 */
void USB_Flush(void);

/**
 * @brief レコードストリーム送信完了時に呼ばれるコールバック型
 * @param records_sent 送信完了したレコード数
 */
typedef void (*USB_StreamDoneFn)(uint32_t records_sent);

/**
 * @brief レコードストリーム完了コールバックを登録 (NULL でクリア)
 *        USB_Stream_Task が STREAM_SENDING → STREAM_IDLE 遷移時に呼ぶ。
 */
void USB_SetStreamDoneCallback(USB_StreamDoneFn cb);

#ifdef	__cplusplus
}
#endif

#endif	/* MY_USB_EXT_H */

