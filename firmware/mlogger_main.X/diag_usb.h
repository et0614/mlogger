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

/// 1: diag log を USB-CDC に流す (debug build。ble_trace.py 等で観測するとき)
/// 0: 全 diag 呼び出しが no-op (release build。既定)
/// 切替時は firmware を再ビルドする必要あり。
/// 1 にすると diag_usb_logf の vsnprintf が XC8 の汎用 printf (浮動小数点書式込み
/// 約 5 KB) を引き込み、フラッシュの空きが約 7.4 KB → 約 1.1 KB に減る。
#ifndef DIAG_USB_ENABLED
#define DIAG_USB_ENABLED 0
#endif

#ifdef __cplusplus
extern "C" {
#endif

#if DIAG_USB_ENABLED

/// printf 風に 1 行 log を出す ('#' prefix + '\n' suffix は自動付与)
void diag_usb_logf(const char *fmt, ...);

/// バイナリを hex 文字列にして 1 行で出す:
///   "# <tag> len=N hex=AABBCC...\n"
/// limit_bytes を超える分は "..." で省略
void diag_usb_hex(const char *tag, const uint8_t *data, int len, int limit_bytes);

#else

// no-op マクロ。コンパイラが完全に消す (引数の評価も無し)。
#define diag_usb_logf(...)       ((void)0)
#define diag_usb_hex(tag, d, l, n) ((void)0)

#endif // DIAG_USB_ENABLED

#ifdef __cplusplus
}
#endif

#endif /* DIAG_USB_H */
