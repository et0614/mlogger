/*
 * File:   usb_comm.h
 * Author: e.togashi
 *
 * USB-CDC 仮想シリアル通信の薄いラッパ (USB 単一トランスポート用)。
 *   - 初期化         : USB_Comm_Initialize()
 *   - メインタスク   : USB_Comm_Task()  (mainループのアイドル時に毎回呼ぶ)
 *   - 文字列送信     : USB_Comm_SendString()
 *
 * 受信は USB_Comm_Task() 内で 1 文字ずつ json_protocol (JP_AppendChar) に
 * 渡し、'\n'/'\r' で 1 コマンドを確定→ディスパッチする。
 *
 * 注: USB デバイス層 (USBDevice_Initialize) は SYSTEM_Initialize() が呼ぶので、
 *     ここでは CDC 層の初期化のみ行う。
 */
#ifndef USB_COMM_H
#define USB_COMM_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>

/// CDC 仮想シリアルポートを初期化する (SYSTEM_Initialize の後に1回呼ぶ)
void USB_Comm_Initialize(void);

/// USB 通信のメインタスク。mainループのアイドル時に毎回呼ぶ。
///   - USB スタックを駆動 (USBDevice_Handle / CDC handler)
///   - 受信バッファをドレインして json_protocol に流す
void USB_Comm_Task(void);

/// ヌル終端文字列を USB-CDC へ送信する。
/// 送信バッファが詰まったら USB スタックを駆動してドレインを待つ (最大~100ms)。
/// @return 実際に送信できたバイト数
uint16_t USB_Comm_SendString(const char *str);

/// 生バイナリを len バイト送信する (0x00 を含んでよい)。dump のバルク転送用。
/// @return 実際に送信できたバイト数
uint16_t USB_Comm_SendBytes(const uint8_t *data, uint16_t len);

/// 送信バッファをホストへ確実に押し出す (応答送出後にSTANDBYへ移行する前に呼ぶ)。
void USB_Comm_Flush(void);

/// USBスタック全体(USBDevice_Handle + CDCハンドラ)を1回駆動する。
/// dump等の長時間バルク送信中に定期的に呼び、デバイスを無応答化させない(ホスト側パイプ停止防止)。
void USB_Comm_Pump(void);

/// USBモジュールを完全停止する (ロギング=省電力モード移行時に使う)。
/// USBが有効だと主発振 OSCHF(12MHz) が起き続け、STANDBYでも ~2mA 消費するため、
/// バスからdetachしてUSBEN=0にし、OSCHFがSTANDBYで停止できるようにする。
/// 【重要】必ず最後の応答を USB_Comm_Flush() で送出した後に呼ぶこと(呼ぶとUSB送信不可)。
/// USB通信に戻すにはデバイスをリセット(SYSTEM_Initializeで再有効化)。
void USB_Comm_Disable(void);

#ifdef __cplusplus
}
#endif

#endif /* USB_COMM_H */
