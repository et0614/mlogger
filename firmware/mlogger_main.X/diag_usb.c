#include "diag_usb.h"
#include "usb_extension.h"

#include <stdio.h>
#include <stdarg.h>
#include <string.h>

// 1 行最大。少し大きめに取って snprintf のオーバーフロー検出に余裕を持たせる。
#define DIAG_BUF_SIZE 384

static char s_buf[DIAG_BUF_SIZE];

static void terminate_and_send(int n) {
    if (n < 0) n = 0;
    if (n >= DIAG_BUF_SIZE - 2) n = DIAG_BUF_SIZE - 2;
    s_buf[n]     = '\n';
    s_buf[n + 1] = '\0';
    USB_CDC_SendString(s_buf);
}

void diag_usb_logf(const char *fmt, ...) {
    s_buf[0] = '#';
    s_buf[1] = ' ';
    va_list ap;
    va_start(ap, fmt);
    int more = vsnprintf(s_buf + 2, DIAG_BUF_SIZE - 3, fmt, ap);
    va_end(ap);
    if (more < 0) more = 0;
    terminate_and_send(2 + more);
}

void diag_usb_hex(const char *tag, const uint8_t *data, int len, int limit_bytes) {
    int show = (len > limit_bytes) ? limit_bytes : len;
    int n = snprintf(s_buf, DIAG_BUF_SIZE - 1, "# %s len=%d hex=", tag, len);
    if (n < 0) return;

    static const char H[] = "0123456789ABCDEF";
    for (int i = 0; i < show && n + 2 < DIAG_BUF_SIZE - 4; i++) {
        s_buf[n++] = H[(uint8_t)data[i] >> 4];
        s_buf[n++] = H[(uint8_t)data[i] & 0x0F];
    }
    if (show < len && n + 3 < DIAG_BUF_SIZE - 2) {
        s_buf[n++] = '.';
        s_buf[n++] = '.';
        s_buf[n++] = '.';
    }
    terminate_and_send(n);
}
