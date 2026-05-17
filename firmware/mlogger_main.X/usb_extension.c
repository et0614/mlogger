#include "mcc_generated_files/usb/usb_cdc/usb_cdc_virtual_serial_port.h"
#include "mcc_generated_files/usb/usb_cdc/usb_cdc.h"
#include "mcc_generated_files/timer/delay.h"   // DELAY_milliseconds
#include "usb_extension.h"
#include "w25q512.h" // Flash読み出し用
#include "eeprom_manager.h" //EEPROM
#include "command_handler.h"

#include <string.h> // memcpy用

// 1回の最大転送サイズ
#define PAGE_DATA_SIZE RECORD_SIZE * RECS_PER_PAGE

// 状態管理
typedef enum {
    STREAM_IDLE,
    STREAM_SENDING
} StreamState_t;

static StreamState_t streamState = STREAM_IDLE;
static uint32_t currentReadIdx = 0;

// ストリーム完了コールバック (dump_end イベント送出用)
static USB_StreamDoneFn s_stream_done_cb = NULL;

// リングバッファにFlashから直接データを流し込む関数
static uint16_t directLoadFromFlash(uint32_t flashAddr, uint16_t length)
{
    // リングバッファの空き情報を取得
    uint16_t head = usbCDCTransmitBuffer.head;
    uint16_t maxSize = usbCDCTransmitBuffer.maxLength;
    uint16_t freeSpace = CIRCBUF_FreeSpace(&usbCDCTransmitBuffer);

    // 空きより多くは読み込まない
    if (length > freeSpace) length = freeSpace;
    if (length == 0) return 0;

    // リングバッファの終端までの距離
    uint16_t spaceToEnd = maxSize - head;

    // 折り返しがない場合：一発で書き込む
    if (length <= spaceToEnd)
    {        
        // headの位置にある配列のポインタを渡す
        W25_ReadData(flashAddr, &usbCDCTransmitBuffer.content[head], length);        
        usbCDCTransmitBuffer.head += length;
    }
    // 折り返しがある場合：2回に分けて書き込む
    else
    {        
        // 1回目：headから終端まで
        W25_ReadData(flashAddr, &usbCDCTransmitBuffer.content[head], spaceToEnd);
        
        // 2回目：先頭(0)から残りの分
        W25_ReadData(flashAddr + spaceToEnd, &usbCDCTransmitBuffer.content[0], length - spaceToEnd);        
        usbCDCTransmitBuffer.head = length - spaceToEnd;
    }

    // headの補正（ちょうど終端で終わった場合）
    if (maxSize <= usbCDCTransmitBuffer.head) 
        usbCDCTransmitBuffer.head = 0;

    return length; // 実際に読み込んだバイト数を返す
}

//USB送信バッファの現在の空き容量を取得する
static uint16_t getFreeSpace(void)
{
    return CIRCBUF_FreeSpace(&usbCDCTransmitBuffer);
}

uint16_t USB_CDC_SendString(const char *str)
{
    uint16_t count = 0;
    while (*str) 
    {
        // バッファがいっぱいになったらループを抜ける（または待機処理を入れる）
        if (USB_CDCWrite((uint8_t)*str) == CDC_SUCCESS)
        {
            str++;
            count++;
        }
        //バッファフルなら抜ける
        else break; 
    }
    return count;
}

void USB_Stream_Task(void)
{
    switch(streamState){
        //コマンド待受中
        case STREAM_IDLE:{
            // データ受信の確認
            uint8_t receivedChar;
            if (USB_CDCRead(&receivedChar) == CDC_SUCCESS)
                CH_AppendChar(receivedChar, SRC_USB);
            break;
        }
        //データのバルク転送中
        case STREAM_SENDING:{
            // バッファに「1ページ分」の空きがあるか確認
            while (!USB_CDCTxBusy() && (PAGE_DATA_SIZE <= getFreeSpace()))
            {
                // 終了判定：最新位置に到達
                if (rec_latest <= currentReadIdx) {
                    uint32_t sent = currentReadIdx;
                    streamState = STREAM_IDLE;
                    // 完了コールバック (dump_end イベント送出に使用)
                    if (s_stream_done_cb) s_stream_done_cb(sent);
                    return;
                }
                
                // 物理アドレスを取得
                uint32_t physAddr = W25_GetAddressFromRecordIndex(currentReadIdx);
                
                // 1. ページ境界を跨がない最大レコード数を計算
                uint32_t offsetIdx = currentReadIdx % RECS_PER_PAGE;
                uint32_t recordsLeftInPage = RECS_PER_PAGE - offsetIdx;
                
                // 2. DUMP完了までに必要な残りレコード数
                uint32_t recordsNeeded = rec_latest - currentReadIdx;
                
                // 3. USBバッファに入るレコード数
                uint32_t recordsFitInUsb = CIRCBUF_FreeSpace(&usbCDCTransmitBuffer) / RECORD_SIZE;
                
                // 上記3つの制約の中で最小のレコード数を算出
                uint32_t recordsToSend = recordsLeftInPage;
                if (recordsNeeded < recordsToSend) recordsToSend = recordsNeeded;
                if (recordsFitInUsb < recordsToSend) recordsToSend = recordsFitInUsb;

                if (recordsToSend == 0) break;
                
                // Flashからロード
                directLoadFromFlash(physAddr, (uint16_t)(recordsToSend * RECORD_SIZE));

                // インデックスを加算
                currentReadIdx += recordsToSend;
            }
            //バッファに空きがなければmainに戻る
            break;
        }
    }
}

// (旧 USB_DumpData は v4 移行で削除: v3 の DMP コマンド廃止に伴い不要)

// v4 dump 用: 件数prefix無しでレコードストリーム送信を開始
// (ヘッダJSON送信は呼び出し側の責任)
void USB_StartRecordStream(void)
{
    streamState = STREAM_SENDING;
    currentReadIdx = 0;
}

// レコードストリーム完了コールバックの登録
void USB_SetStreamDoneCallback(USB_StreamDoneFn cb)
{
    s_stream_done_cb = cb;
}

// USB CDC 送信バッファを強制 flush (スリープ移行直前用)
void USB_Flush(void)
{
    // バッファが空になるまで USB スタックを駆動 (タイムアウト ~500ms)
    int timeout_ms = 500;
    uint16_t max = usbCDCTransmitBuffer.maxLength;
    while (timeout_ms-- > 0) {
        USB_CDCVirtualSerialPortHandler();
        // 全送信完了 = バッファに最大空き = データ無し
        if (CIRCBUF_FreeSpace(&usbCDCTransmitBuffer) >= max && !USB_CDCTxBusy()) {
            // ホストへ最終パケットが渡るまでもう少しスタックを回す
            for (int i = 0; i < 10; i++) USB_CDCVirtualSerialPortHandler();
            return;
        }
        DELAY_milliseconds(1);
    }
}
