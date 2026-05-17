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
 * @brief 内蔵メモリからデータを全出力
 */
void USB_DumpData(void);

#ifdef	__cplusplus
}
#endif

#endif	/* MY_USB_EXT_H */

