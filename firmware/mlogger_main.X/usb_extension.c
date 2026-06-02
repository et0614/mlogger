#include "mcc_generated_files/usb/usb_cdc/usb_cdc_virtual_serial_port.h"
#include "mcc_generated_files/usb/usb_cdc/usb_cdc.h"
#include "mcc_generated_files/timer/delay.h"   // DELAY_milliseconds
#include "usb_extension.h"
#include "w25q256.h" // Flash読み出し用
#include "eeprom_manager.h" //EEPROM
#include "command_handler.h"
#include "xbee_controller.h" // Xbee_BlBytes / Xbee_TxBytes (BLE/Zigbee dump 用)

#include <string.h> // memcpy用

// 1回の最大転送サイズ
#define PAGE_DATA_SIZE RECORD_SIZE * RECS_PER_PAGE

// BLE dump 用 chunk size。XBee BLE は 220B/frame に拡大、record 22B なので 10 records
// = 220B が単一 USER_DATA_RELAY frame で送れる上限。Zigbee は別途 150B 上限なので、
// XB_TX_MAX_CHUNK_BYTES (150) 内に収まる 6 records も別途使う必要があるが、
// Xbee_TxBytes は内部で chunking してくれるので 10 records 渡しても問題ない。
#define RECS_PER_BLE_CHUNK 10
#define BLE_CHUNK_BYTES   (RECS_PER_BLE_CHUNK * RECORD_SIZE)

// 状態管理
typedef enum {
    STREAM_IDLE,
    STREAM_SENDING
} StreamState_t;

static StreamState_t streamState = STREAM_IDLE;
static uint32_t currentReadIdx = 0;
// dump の送出先 transport (USB / BLE / Zigbee)。STREAM_SENDING 中のみ意味がある。
static CommandSource_t streamDest = SRC_USB;

// BLE/Zigbee dump 用の chunk バッファ (1 tick で 6 records をまとめて XBee UART に送出)
static uint8_t bleChunkBuf[BLE_CHUNK_BYTES];

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
    // バッファフル時の総待機時間上限 [ms]。下記の理由で長めに取る:
    //  - 大きなレスポンス (~500B) を 1 packet (64B) ずつ送るのに数 ms 単位かかる
    //  - 旧実装は break で即諦めて silent truncate していたため、size>=300 の
    //    echo や set_settings の応答が ~270-322B で切れる bug を踏んでいた
    int budget_ms = 100;
    while (*str)
    {
        if (USB_CDCWrite((uint8_t)*str) == CDC_SUCCESS)
        {
            str++;
            count++;
        }
        else
        {
            // バッファフル: USB スタックを駆動してドレインを進める
            USB_CDCVirtualSerialPortHandler();
            if (budget_ms-- <= 0) break;
            DELAY_milliseconds(1);
        }
    }
    return count;
}

// 完了判定: rec_latest に到達したら streamState を IDLE に戻して完了 cb を呼ぶ。
// done cb には dest を渡して dump_end イベントを正しい transport に送れるようにする。
static void finalize_stream_if_done(void)
{
    if (rec_latest <= currentReadIdx) {
        uint32_t sent = currentReadIdx;
        CommandSource_t dest = streamDest;
        streamState = STREAM_IDLE;
        if (s_stream_done_cb) s_stream_done_cb(sent, dest);
    }
}

void USB_Stream_Task(void)
{
    // USB の構成完了フラグ。BLE のみ接続のときは false なので USB CDC 操作を skip する
    // (本関数は BLE/Zigbee dump 駆動のために USB 未接続時も呼ばれる)。
    bool usbReady = (USB_DescriptorActiveConfigurationValueGet() == 1);

    switch(streamState){
        //コマンド待受中
        case STREAM_IDLE:{
            // データ受信の確認 (USB 未接続なら skip)
            if (usbReady) {
                uint8_t receivedChar;
                if (USB_CDCRead(&receivedChar) == CDC_SUCCESS)
                    CH_AppendChar(receivedChar, SRC_USB);
            }
            break;
        }
        //データのバルク転送中
        case STREAM_SENDING:{
            if (streamDest == SRC_USB) {
                // USB が物理的に切断された場合は send しても drain されないため抜ける
                if (!usbReady) break;
                // USB-CDC: CDC tx リングバッファに「1ページ分」の空きがある間 burst write
                while (!USB_CDCTxBusy() && (PAGE_DATA_SIZE <= getFreeSpace()))
                {
                    if (rec_latest <= currentReadIdx) {
                        finalize_stream_if_done();
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
            } else {
                // BLE / Zigbee: 1 tick で 1 chunk (= 最大 10 records / 220B) を XBee UART
                // 経由で同期送信。fire-and-forget なので chunk 間に pacing delay (40ms) を
                // 入れて XBee 内部 buffer overflow を防ぐ (BLE 実効 5-8 KB/sec)。
                // 220B / 40ms = 5.5 KB/sec で BLE drain rate と同等。
                if (rec_latest <= currentReadIdx) {
                    finalize_stream_if_done();
                    break;
                }

                // ページ境界を跨がない & 残り件数 & chunk 上限 の最小値
                uint32_t offsetIdx = currentReadIdx % RECS_PER_PAGE;
                uint32_t recordsLeftInPage = RECS_PER_PAGE - offsetIdx;
                uint32_t recordsNeeded = rec_latest - currentReadIdx;
                uint32_t recordsToSend = RECS_PER_BLE_CHUNK;
                if (recordsLeftInPage < recordsToSend) recordsToSend = recordsLeftInPage;
                if (recordsNeeded < recordsToSend)     recordsToSend = recordsNeeded;
                if (recordsToSend == 0) break;

                uint32_t physAddr = W25_GetAddressFromRecordIndex(currentReadIdx);
                W25_ReadData(physAddr, bleChunkBuf, (uint16_t)(recordsToSend * RECORD_SIZE));

                int chunkLen = (int)(recordsToSend * RECORD_SIZE);
                if (streamDest == SRC_BLE) {
                    Xbee_BlBytes(bleChunkBuf, chunkLen);
                } else {
                    Xbee_TxBytes(bleChunkBuf, chunkLen);
                }

                currentReadIdx += recordsToSend;
                if (rec_latest <= currentReadIdx) {
                    // 最後の binary chunk と dump_end JSON の間にも pacing delay を入れる。
                    // これを入れないと XBee 内部 buffer (~150-256B) が直前の binary で
                    // 飽和した状態で dump_end (~60B) が流入し、buffer overflow で dump_end
                    // frame が drop されることがある (phone 側は _dumpBytesReceived は成立
                    // するが _dumpEndReceived が永久に来ず timeout する事象)。
                    DELAY_milliseconds(40);
                    finalize_stream_if_done();
                } else {
                    // chunk 間 pacing: XBee 内部 buffer の drain を待つ
                    DELAY_milliseconds(40);
                }
            }
            break;
        }
    }
}

// (旧 USB_DumpData は v4 移行で削除: v3 の DMP コマンド廃止に伴い不要)

// v4 dump 用: 件数prefix無しでレコードストリーム送信を開始
// (ヘッダJSON送信は呼び出し側の責任)
// dest で送出 transport (SRC_USB / SRC_BLE / SRC_XBEE) を指定する。
void USB_StartRecordStream(CommandSource_t dest)
{
    streamDest = dest;
    streamState = STREAM_SENDING;
    currentReadIdx = 0;
}

// stream が現在 active で、引数の dest に送出中かどうか。
// ready event などを抑止して dump binary stream と干渉しないようにする目的。
bool USB_IsStreamingTo(CommandSource_t dest)
{
    return streamState == STREAM_SENDING && streamDest == dest;
}

// 現在 dump streaming 中かどうか (transport 問わず)。
// UI フィードバック (赤 LED 点滅で「dump 中 = 操作不能」表示) に使う。
bool USB_IsStreaming(void)
{
    return streamState == STREAM_SENDING;
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
