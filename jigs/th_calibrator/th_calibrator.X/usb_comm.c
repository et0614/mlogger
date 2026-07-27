#include "usb_comm.h"
#include "json_protocol.h"

#include "mcc_generated_files/usb/usb_device.h"                        // USBDevice_Handle
#include "mcc_generated_files/usb/usb_cdc/usb_cdc_virtual_serial_port.h" // USB_CDCxxx
#include "mcc_generated_files/timer/delay.h"                          // DELAY_milliseconds
#include <avr/io.h>                                                   // USB0 / USB_ENABLE_bm / USB_ATTACH_bm

void USB_Comm_Initialize(void)
{
    // USB デバイス層 (USBDevice_Initialize) は SYSTEM_Initialize() 済み。
    // ここでは CDC 仮想シリアルポート層のみ初期化する。
    USB_CDCVirtualSerialPortInitialize();
}

void USB_Comm_Task(void)
{
    // 1. USB スタックを駆動 (エニュメレーション応答・TX/RX の進行)
    USBDevice_Handle();
    USB_CDCVirtualSerialPortHandler();

    // 2. 受信バッファをドレインして 1 文字ずつコマンドパーサに渡す。
    //    while で空になるまで読み切ることで 1 tick の受信スループットを稼ぐ。
    uint8_t ch;
    while (USB_CDCRead(&ch) == CDC_SUCCESS)
    {
        JP_AppendChar((char)ch);
    }
}

// 送信バッファが空(=直前の転送が完了し head/tail がリセット済み)になるまで
// USB スタックを駆動して待つ。
//
// 【なぜ必要か】CDC 送信バッファ(usb_cdc_virtual_serial_port.c)は循環バッファを
// 「リニア利用」している: USB_CDCWrite が head を進め、Handler が head バイトを
// 一括送出し、完了コールバックで head=tail=0 にリセットする。このため【転送中
// (in-flight)に USB_CDCWrite で追記すると、完了時の head リセットで追記分が丸ごと
// 破棄される(=バイト欠落)】。これを避けるため、追記の前に必ずバッファが空
// (CIRCBUF_Empty: head==tail)になるまで待ち、「転送中は追記しない」を保証する。
// @return true:空になった / false:budget_ms 内に空にならなかった(ホスト未読など)
static bool wait_tx_drained(int budget_ms)
{
    while (!CIRCBUF_Empty(&usbCDCTransmitBuffer))
    {
        USB_Comm_Pump();
        if (budget_ms-- <= 0) return false;
        DELAY_milliseconds(1);
    }
    return true;
}

// データを「バッファが空になるまで待つ→満杯まで詰める→転送開始」の繰り返しで送る。
// 転送中の追記を一切行わないのでバイト欠落しない。TXバッファ(512B)単位で送出するため
// 大きなデータでも往復回数は少ない (例: 4.4KB ≒ 9 往復)。
// @return 実際に送信できたバイト数
static uint16_t send_block(const uint8_t *data, uint16_t len)
{
    uint16_t sent = 0;
    while (sent < len)
    {
        // 直前の転送完了を待つ(ここで追記しない=欠落防止の肝)
        if (!wait_tx_drained(2000)) break;

        // 空いたバッファに入るだけ詰める (満杯=BUFFER_FULL で停止)
        while (sent < len && USB_CDCWrite(data[sent]) == CDC_SUCCESS)
            sent++;

        // この塊の転送を開始 (Handler が head バイトを WriteStart)
        USB_Comm_Pump();
    }
    // 最後の塊を確実にホストへ送出 (次の送信が転送中追記で潰さないよう空を待つ)
    wait_tx_drained(2000);
    return sent;
}

uint16_t USB_Comm_SendString(const char *str)
{
    uint16_t n = 0;
    while (str[n]) n++;
    return send_block((const uint8_t *)str, n);
}

void USB_Comm_Flush(void)
{
    // 送信バッファの中身を確実にホストへ押し出す (STANDBYスリープ移行直前など、
    // 応答を出してから寝るケースで使う)。USBスタックを少し多めに駆動して TX を drain する。
    for (int i = 0; i < 50; i++)
    {
        USB_CDCVirtualSerialPortHandler();
        DELAY_milliseconds(1);
    }
}

void USB_Comm_Pump(void)
{
    // USBスタック全体を1回駆動する。長時間のバルク送信(dump)中も、デバイスコア
    // (バスイベント/EP0応答)とCDC送出を進めることで、「デバイス無応答化によるホスト側
    // パイプ停止(転送が途中で止まる)」を防ぐ。
    USBDevice_Handle();
    USB_CDCVirtualSerialPortHandler();
}

uint16_t USB_Comm_SendBytes(const uint8_t *data, uint16_t len)
{
    // 生バイナリ送信 (0x00 を含むデータOK)。dump のバルク転送用。
    // 文字列送信と同じ欠落しない送出経路 (send_block) を使う。
    return send_block(data, len);
}

void USB_Comm_Disable(void)
{
    // ロギング(省電力)移行時にUSBモジュールを完全停止する。
    // 【なぜ】USBが有効(USBEN=1)だと主発振 OSCHF(12MHz) をSTANDBY中も要求し続け、MCUが
    // 実質スリープできず ~2mA 消費する(実測)。detach + USBEN=0 で OSCHF が STANDBY で
    // 停止できるようにし、ベース電流を大幅に下げる。
    // 【前提】最後の応答は呼び出し側が USB_Comm_Flush() 済みであること(これ以降USB送信不可)。
    // 【復帰】USB通信に戻すにはリセット(SYSTEM_Initialize が USB を再有効化)。
    USB0.CTRLB &= ~USB_ATTACH_bm;   // バスから detach (ホストには切断として見える)
    USB0.CTRLA &= ~USB_ENABLE_bm;   // USBモジュール無効化 → OSCHFクロック要求を解除
}
