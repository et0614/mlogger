/*
 * File:   diag_usb.h
 * Author: e.togashi
 *
 * 診断用 USB-CDC ロガー。 firmware の BLE / UART 観測情報を `# ...` 形式の
 * テキスト行で USB に流し、PC 側で listener (software/python/ble_trace.py)
 * が timestamp 付きで表示する。
 *
 * 出力行はすべて '#' で始まるため、v4 JSON-RPC ('{' 始まり) と衝突しない。
 * USB ホストが読んでいないときは即 drop されるので firmware の動作には
 * 影響しない (USB_CDC_SendString が buffer full で break する)。
 */
#ifndef DIAG_USB_H
#define DIAG_USB_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/// printf 風に 1 行 log を出す ('#' prefix + '\n' suffix は自動付与)
void diag_usb_logf(const char *fmt, ...);

/// バイナリを hex 文字列にして 1 行で出す:
///   "# <tag> len=N hex=AABBCC...\n"
/// limit_bytes を超える分は "..." で省略
void diag_usb_hex(const char *tag, const uint8_t *data, int len, int limit_bytes);

#ifdef __cplusplus
}
#endif

#endif /* DIAG_USB_H */
