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
    bool ready;   // 完成コマンドが dispatch 待ち。処理完了まで同 source の新規バイトは破棄
} CommandBuffer_t;

// コマンド組み立て用バッファ。USB / Zigbee / BLE で完全独立に持つ。
// 以前は Zigbee と BLE で共有していたが、両方が同時にコマンドを送ると
// バイトが混ざって JSON が破損し pd_dispatch がコマンドを認識できなくなる
// 事象 (MLServer Zigbee 経由の get_settings が MLS_Mobile BLE 接続中に
// 無応答になる現象) が出たため分離。
static CommandBuffer_t usb_buffer    = { {0}, 0, false };
static CommandBuffer_t zigbee_buffer = { {0}, 0, false };
static CommandBuffer_t ble_buffer    = { {0}, 0, false };

// dispatch 再入防止カウンタ。
// 応答送信中の waitTxCompletion → Xbee_LoadUART 経由で別コマンドが完成した場合、
// そこから同期的に dispatch すると、外側の dispatch と静的バッファ (protocol_handlers
// の s_tx_buf、protocol_dispatch の s_tokens) を共有しているため送信途中の応答が
// 破壊される。depth > 0 の間は ready フラグを立てるだけにして、外側の dispatch
// 完了後に CH_DispatchPending のループが拾って処理する。
static uint8_t s_dispatch_depth = 0;

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
    CommandBuffer_t *b;
    switch (src) {
        case SRC_USB:  b = &usb_buffer;    break;
        case SRC_XBEE: b = &zigbee_buffer; break;
        case SRC_BLE:  b = &ble_buffer;    break;
        default:       return;
    }

    // 前のコマンドが dispatch 待ちの間に届いた同 source のバイトは破棄する。
    // 旧実装は処理中の b->buff 末尾に追記し、処理後の pos=0 リセットで次コマンドの
    // 先頭バイトが失われて JSON が破損していた。破棄なら少なくとも破損 JSON の
    // dispatch は起きず、上位の request/response 再送で回復できる。
    // (通常の request/response 運用ではクライアントは応答を待ってから次を送るため、
    //  ここに来るのは連投時のみ)
    if (b->ready) return;

    if (c == '\r' || c == '\n') {
        if (b->pos > 0) {
            b->buff[b->pos] = '\0';
            b->ready = true;
            CH_DispatchPending();  // depth 0 なら即時処理 (従来挙動)、dispatch 中なら遅延
        }
    } else if (b->pos < MAX_CMD_CHAR - 1) {
        b->buff[b->pos++] = c;
    }
}

// dispatch 待ちのコマンドを順に処理する。dispatch 中 (depth > 0) の呼び出しは
// 何もせず戻り、外側のこのループが処理を引き継ぐ。
void CH_DispatchPending(void) {
    if (s_dispatch_depth > 0) return;
    for (;;) {
        CommandBuffer_t *b;
        CommandSource_t src;
        if      (usb_buffer.ready)    { b = &usb_buffer;    src = SRC_USB;  }
        else if (zigbee_buffer.ready) { b = &zigbee_buffer; src = SRC_XBEE; }
        else if (ble_buffer.ready)    { b = &ble_buffer;    src = SRC_BLE;  }
        else break;

        s_dispatch_depth++;
        CH_ProcessCommand(b->buff, src);
        s_dispatch_depth--;

        // 処理完了後にバッファを解放 (この間に届いた同 source のバイトは破棄済み)
        b->pos = 0;
        b->ready = false;
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
    // 旧 DIAG: dispatch ごとに 2 行の '#' ログを吐いていたが、入力サイズに比例した
    // hex dump (DISPATCH_DATA) が USB-CDC TX buffer (512B) を食って大きな応答
    // (set_settings/echo size>=300) を truncate する原因になっていたため削除。
    // dispatch のデバッグは ble_trace.py 等で別に行う方針。
    if (command[0] == '{') {
        pd_dispatch(command, (int)strlen(command), src);
    }
    // 旧 v3 の 3文字 ASCII コマンドはサポート終了 (応答せず破棄)
}
