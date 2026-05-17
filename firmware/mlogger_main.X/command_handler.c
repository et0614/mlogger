#include "command_handler.h"
#include "xbee_controller.h"
#include "usb_extension.h"
#include "protocol_dispatch.h"   // v4 JSON ディスパッチャ

#include <string.h>

// コマンド全体の最大文字数 (v4 set_settings の全6センサ指定で ~330B になるため 512)
#define MAX_CMD_CHAR  512

// ソースごとの受信状態を管理する構造体
typedef struct {
    char buff[MAX_CMD_CHAR];
    uint16_t pos;
} CommandBuffer_t;

// コマンド組み立て用バッファ (USB/XBee で別管理、BLE は XBee と共有)
static CommandBuffer_t usb_buffer  = { {0}, 0 };
static CommandBuffer_t xbee_buffer = { {0}, 0 };

// 応答送信 (v4 ハンドラは CH_Reply 経由でこれを呼ぶ)
static void reply(const char *msg, CommandSource_t src) {
    switch(src)
    {
        case SRC_USB:  USB_CDC_SendString(msg); break;
        case SRC_XBEE: Xbee_TxChars(msg);       break;
        case SRC_BLE:  Xbee_BlChars(msg);       break;
    }
}

void CH_Reply(const char *msg, CommandSource_t src) {
    reply(msg, src);
}

// 1文字をバッファに追加、\r/\n でコマンド確定
static void append_char_internal(char c, CommandSource_t src) {
    CommandBuffer_t *b = (src == SRC_USB) ? &usb_buffer : &xbee_buffer;

    if (c == '\r' || c == '\n') {
        if (b->pos > 0) {
            b->buff[b->pos] = '\0';
            CH_ProcessCommand(b->buff, src);
            b->pos = 0;
        }
    } else if (b->pos < MAX_CMD_CHAR - 1) {
        b->buff[b->pos++] = c;
    }
}

void CH_AppendChar(char c, CommandSource_t src) {
    append_char_internal(c, src);
}

void CH_AppendString(const char *str, CommandSource_t src) {
    while (*str) {
        append_char_internal(*str++, src);
    }
}

// v4 では JSON コマンドのみ受け付ける ('{' 始まり以外は無視)
void CH_ProcessCommand(const char *command, CommandSource_t src) {
    if (command[0] == '{') {
        pd_dispatch(command, strlen(command), src);
    }
    // 旧 v3 の 3文字 ASCII コマンドはサポート終了 (応答せず破棄)
}
