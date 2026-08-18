#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/uart/usart0.h" // MelodyのUARTドライバ
#include "mcc_generated_files/timer/delay.h"

#include "xbee_controller.h"
#include "eeprom_manager.h" // EM_isXBeeInitialized など
#include "command_handler.h"
#include "diag_usb.h"       // 診断用 USB-CDC ロガー

#include <string.h>
#include <stdio.h>

#include "hal_io.h"

// 定数
#define XB_RX_BUFFER_SIZE 256
#define XB_MBUFF_LENGTH   20

// 受け付ける最大フレームサイズ (position 換算)。XBee3 の API フレームは
// ヘッダ + payload (NP 上限 255) + checksum で高々 ~270。これを超える length
// 指定は化けたヘッダとみなして破棄する (旧実装は uint8_t で wrap していた)。
#define XB_RX_MAX_FRAME_SIZE (XB_RX_BUFFER_SIZE + 20)

// 内部状態変数 (staticで隠蔽)
static bool g_readingFrame = false;
static uint16_t g_framePosition = 0;
static uint16_t g_frameSize = 0;
static char g_frameBuff[XB_RX_BUFFER_SIZE];
static uint8_t g_xbeeOffset = 14;
static uint8_t g_frameChecksum = 0;
static uint8_t g_rxFrameId = 0;   // 受信フレームの position 4 (0x88/0x89/0x8B では Frame ID)

// 送信完了ステータス確認用
static uint8_t g_lastFrameId = 0x01; //送信フレームID
static bool g_txStatusReceived = false; //
static uint8_t g_lastApiId = 0;        // 現在受信中のフレームのAPI IDを保持

//XBee文字列受信用バッファ
static char xbee_payload_buffer[XB_RX_BUFFER_SIZE];

static bool g_communicating = false; // 通信中か否か
static bool g_shouldSleep = false; // 通信終了後にスリープすべきかのフラグ

static uint8_t g_association_status = 0xFF; // 0xFFは不明状態
static uint16_t g_join_timer = 0;
static uint16_t g_ai_request_timer = 0;

// BLE central (スマホ) が接続中か。Modem Status (0x8A) の 0x32/0x33 で追跡し、
// BLE 経由のコマンド受信 (0xAD interface=BLE) でも true にする (boot 前から
// 接続済みで 0x32 を取り逃した場合のフォールバック)。
// 接続中は XBee をスリープさせない (Pin Hibernate は BLE ごと落とすため)。
static bool g_bleConnected = false;

// 汎用 AT レスポンス捕捉 (Xbee_QueryAt 用)。最後に受信した 0x88 の内容を保持する。
static char    g_atrCmd[2] = {0, 0};
static uint8_t g_atrStatus = 0xFF;
static uint8_t g_atrData[8];
static uint8_t g_atrLen = 0;
static bool    g_atrReceived = false;

// <editor-fold defaultstate="collapsed" desc="内部関数">

static void sleepXBee(void)
{
    SLP_XBEE_SetHigh();
    g_shouldSleep = true;
}

static int getCharLength(const char *data) {
    return strlen(data);
}

static int addCsum(int csum, char nbyte) {
    csum += (int)nbyte;
    csum = csum & 0x00ff;
    return csum;
}

static void receiveMessage(char message[]) {
    uint8_t index = 0;
    uint16_t timeout_counter = 0;
    const uint16_t TIMEOUT_LIMIT = 500;

    memset(message, 0, XB_MBUFF_LENGTH);

    while (timeout_counter < TIMEOUT_LIMIT) {
        if (USART0_IsRxReady()) { // UartDriver::hasData() の代わり
            char c = (char)USART0_Read(); // UartDriver::get() の代わり
            
            if (c != '\n' && c != '\r') {
                if (index < XB_MBUFF_LENGTH - 1) message[index++] = c;
            }
            if (c == '\r') {
                message[index] = '\0';
                return;
            }
        }
        _delay_ms(1);
        timeout_counter++;
    }
}

static void sendAtCommandApiFrame(const char at_command[2], uint8_t frame_id, const uint8_t* param, uint8_t param_len)
{
    uint8_t checksum = 0;
    uint8_t payload_len = 4 + param_len;

    USART0_Write(XB_START_DELIMITER);
    USART0_Write(0x00);
    USART0_Write(payload_len);

    USART0_Write(XB_FRAME_AT_COMMAND);
    checksum += XB_FRAME_AT_COMMAND;

    USART0_Write(frame_id);
    checksum += frame_id;

    USART0_Write((uint8_t)at_command[0]);
    checksum += at_command[0];
    USART0_Write((uint8_t)at_command[1]);
    checksum += at_command[1];

    if (param && param_len > 0) {
        for (uint8_t i = 0; i < param_len; i++) {
            USART0_Write(param[i]);
            checksum += param[i];
        }
    }
    USART0_Write((uint8_t)(0xFF - checksum));
}

// 文字列送信ヘルパー
static void sendString(const char *str) {
    while (*str) {
        USART0_Write((uint8_t)*str++);
    }
}

// 送信完了を待つ
static bool waitTxCompletion(uint16_t timeout_ms) {
    uint16_t waited = 0;
    while (!g_txStatusReceived && waited < timeout_ms) {
        Xbee_LoadUART(); // 受信バッファを回してステータスを探す
        _delay_ms(1);
        waited++;
    }
    return g_txStatusReceived;
}

/**
 * @brief 受信バイトを解析してコマンドを構築する
 * @param data 受信した1バイト
 * @param output_buffer [out] 完成したデータを格納する
 * @param buffer_size output_bufferのサイズ
 * @return フレームが完成したら true
 */
static bool processXbeeByte(char dat, char* output_buffer, int buffer_size)
{
    uint8_t current_byte = (uint8_t)dat;

    if (current_byte == XB_START_DELIMITER) {
        g_framePosition = 1;
        g_readingFrame = true;
        g_frameChecksum = 0;
        return false;
    }
    
    if(!g_readingFrame) return false;
    
    if(g_framePosition >= 3) g_frameChecksum += current_byte;

    switch(g_framePosition) {
        case 1: // Len MSB (16bit length の上位バイト。旧実装は無視していた)
            g_frameSize = (uint16_t)current_byte << 8;
            break;
        case 2: // Len LSB
            g_frameSize = (g_frameSize | current_byte) + 3;
            if (g_frameSize > XB_RX_MAX_FRAME_SIZE) {
                // 化けた length ヘッダ。フレームを破棄して次の 0x7E を待つ
                g_readingFrame = false;
                g_framePosition = 0;
                return false;
            }
            break;
        case 3: // API ID
            g_lastApiId = current_byte;
            switch (g_lastApiId) {
                case XB_FRAME_ZIGBEE_RECEIVE_PACKET:
                    g_xbeeOffset = XB_RX_OFFSET_ZIGBEE_PACKET;
                    break;
                case XB_FRAME_USER_DATA_RELAY_IN:
                    g_xbeeOffset = XB_RX_OFFSET_USER_DATA_RELAY;
                    break;
                case XB_FRAME_TRANSMIT_STATUS:
                    g_xbeeOffset = XB_RX_OFFSET_TRANSMIT_STATUS;
                    break;
                case XB_FRAME_TX_STATUS:
                    g_xbeeOffset = XB_RX_OFFSET_TX_STATUS;
                    break;
                case XB_FRAME_MODEM_STATUS:
                    g_xbeeOffset = XB_RX_OFFSET_MODEM_STATUS;
                    break;
                case XB_FRAME_AT_COMMAND_RESPONSE:
                    g_xbeeOffset = XB_RX_OFFSET_AT_COMMAND_RESPONSE;
                    break;
                default:
                    g_readingFrame = false; // Unknown frame
                    break;
            }
            break; // missing break added
        case 4:
            // 0x88/0x89/0x8B では Frame ID。0x90 では 64bit アドレスの先頭バイト、
            // 0xAD ではインターフェース番号だが、これらでは g_rxFrameId は参照しない。
            g_rxFrameId = current_byte;
            break;
        default:
            if(g_xbeeOffset < g_framePosition) {
                if (g_frameSize <= g_framePosition) {
                    // Frame End (Checksum)
                    // payload_len は g_framePosition を 0 に reset する前に計算する
                    // (reset 後だと常に負値になり、下流の null 終端処理が
                    // g_frameBuff[XB_RX_BUFFER_SIZE-1] のみで終端する fragile な
                    // 状態だった。これを正しい payload 末尾での終端に修正)。
                    int payload_len_at_end = (int)g_framePosition - (int)(g_xbeeOffset + 1);
                    g_readingFrame = false;
                    g_framePosition = 0;

                    //正常にフレーム受信完了
                    if(g_frameChecksum == XB_CHECKSUM_SUCCESS) {
                        // モデムステータス (0x8A): status 値は position 4 (= g_rxFrameId に捕捉済み)。
                        // BLE central の接続/切断 (0x32/0x33) を追跡してスリープ制御に使う。
                        if (g_lastApiId == XB_FRAME_MODEM_STATUS) {
                            if (g_rxFrameId == XB_MODEM_STATUS_BLE_CONNECTED)
                                g_bleConnected = true;
                            else if (g_rxFrameId == XB_MODEM_STATUS_BLE_DISCONNECTED)
                                g_bleConnected = false;
                            diag_usb_logf("MODEM_STATUS 0x%02X bleConn=%d",
                                          (unsigned)g_rxFrameId, (int)g_bleConnected);
                            return false;
                        }
                        // 送信完了ステータス:
                        //   0x8B = ZIGBEE_TX_REQUEST (0x10) への応答。payload = [addr16(2), retry, delivery, discovery]
                        //   0x89 = USER_DATA_RELAY (0x2D) への応答。payload = [delivery]
                        // Frame ID は offset=4 のため payload に入らない。position 4 で捕捉した
                        // g_rxFrameId と比較する (旧実装は 0x8B の g_frameBuff[0] = addr16 上位
                        // バイトを Frame ID と誤読し、waitTxCompletion が毎回タイムアウトしていた)。
                        if (g_lastApiId == XB_FRAME_TRANSMIT_STATUS ||
                            g_lastApiId == XB_FRAME_TX_STATUS)
                        {
                            if (g_rxFrameId == g_lastFrameId) g_txStatusReceived = true;

                            // delivery status: 0x00 = 成功。0x8B は payload[3]、0x89 は payload[0]。
                            uint8_t delivery = 0xFF;
                            if (g_lastApiId == XB_FRAME_TRANSMIT_STATUS && payload_len_at_end >= 4)
                                delivery = (uint8_t)g_frameBuff[3];
                            else if (g_lastApiId == XB_FRAME_TX_STATUS && payload_len_at_end >= 1)
                                delivery = (uint8_t)g_frameBuff[0];
                            diag_usb_logf("TX_STATUS api=0x%02X recvFrameId=0x%02X expectFrameId=0x%02X delivery=0x%02X",
                                          (unsigned)g_lastApiId,
                                          (unsigned)g_rxFrameId, (unsigned)g_lastFrameId,
                                          (unsigned)delivery);
                            return false;

                        }
                        // 受信したフレームがATコマンドレスポンスだった場合
                        // payload = [ATコマンド文字(2), status, data...] (Frame ID は offset=4 で
                        // payload に入らない)。旧実装は buff[2]=='A' && buff[3]=='I' と誤った
                        // 位置 (実際には status と data) を見ていたため AI 応答が一度も認識されず、
                        // g_association_status が 0xFF のまま → Zigbee 利用時に XBee が永遠に
                        // スリープせず、CB (コミッショニング) を 30 秒毎に送り続けていた。
                        else if (g_lastApiId == XB_FRAME_AT_COMMAND_RESPONSE) {
                            if (payload_len_at_end >= 4 &&
                                g_frameBuff[0] == 'A' && g_frameBuff[1] == 'I' &&
                                (uint8_t)g_frameBuff[2] == 0x00) { // status 0x00 = OK
                                g_association_status = (uint8_t)g_frameBuff[3];
                            }
                            if (payload_len_at_end >= 3) {
                                // Xbee_QueryAt 向けに直近レスポンスを捕捉
                                g_atrCmd[0] = g_frameBuff[0];
                                g_atrCmd[1] = g_frameBuff[1];
                                g_atrStatus = (uint8_t)g_frameBuff[2];
                                g_atrLen = (uint8_t)(payload_len_at_end - 3);
                                if (g_atrLen > sizeof(g_atrData)) g_atrLen = sizeof(g_atrData);
                                memcpy(g_atrData, &g_frameBuff[3], g_atrLen);
                                g_atrReceived = true;

                                diag_usb_logf("AT_RESP cmd=%c%c status=0x%02X val=0x%02X",
                                              g_frameBuff[0], g_frameBuff[1],
                                              (unsigned)(uint8_t)g_frameBuff[2],
                                              (payload_len_at_end >= 4) ? (unsigned)(uint8_t)g_frameBuff[3] : 0xFFu);
                            }
                            return false;
                        }
                        //受信フレームがコマンドだった場合
                        else
                        {
                            // BLE 経由の受信 = BLE central 接続中とみなす (0x32 取り逃しの保険)。
                            // 0xAD の position 4 (interface 番号) は g_rxFrameId に入っている。
                            if (g_lastApiId == XB_FRAME_USER_DATA_RELAY_IN &&
                                g_rxFrameId == XB_UDR_INTERFACE_BLUETOOTH)
                                g_bleConnected = true;

                            int payload_len = payload_len_at_end;
                            if (payload_len < 0) payload_len = 0;
                            if (payload_len >= XB_RX_BUFFER_SIZE) payload_len = XB_RX_BUFFER_SIZE - 1;
                            g_frameBuff[payload_len] = '\0';  // payload 末尾できちんと終端する

                            // DIAG: 完成したコマンドフレームを USB に吐く
                            //   type=0xAD は USER_DATA_RELAY_OUTPUT (XBee->firmware, BLE or UART src)
                            //   type=0x90 は ZIGBEE_RECEIVE_PACKET (XBee->firmware, Zigbee 経由)
                            diag_usb_logf("RX_FRAME type=0x%02X plen=%d", (unsigned)g_lastApiId, payload_len);
                            diag_usb_hex("RX_FRAME_DATA", (const uint8_t*)g_frameBuff, payload_len, 96);

                            // strncpy ではなく memcpy + 明示的 \0 で確実に終端
                            int cp = payload_len;
                            if (cp > buffer_size - 1) cp = buffer_size - 1;
                            memcpy(output_buffer, g_frameBuff, cp);
                            output_buffer[cp] = '\0';
                            return true;
                        }
                    }
                    //チェックサムエラー
                    else return false;
                } else {
                    // Data Payload
                    int buffer_index = g_framePosition - (g_xbeeOffset + 1);
                    if (buffer_index < XB_RX_BUFFER_SIZE) {
                        g_frameBuff[buffer_index] = (char)current_byte;
                    }
                }
            }
            break;
    }
    g_framePosition++;
    // 暴走時のバックストップ。正常フレームは g_frameSize (<= XB_RX_MAX_FRAME_SIZE)
    // 到達時に終端処理されるので、ここに来るのは状態機械が壊れた場合のみ。
    // (旧実装は uint8_t の g_framePosition と 272 の比較で永遠に成立しなかった)
    if (g_framePosition > XB_RX_MAX_FRAME_SIZE) {
        g_readingFrame = false;
        g_framePosition = 0;
    }

    return false;
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：初期化処理">

bool Xbee_Initialize(void)
{
    // 既に初期化済みなら何もしない
    if (EM_isXBeeInitialized()) return true;

    bool hasChanged = false;
    char message[XB_MBUFF_LENGTH];
    memset(message, 0, sizeof(message));

    // --- ATモードへの移行シーケンス ---
    _delay_ms(1100); // ガードタイム
    sendString("+++");
    _delay_ms(1100); // ガードタイム
    
    receiveMessage(message);
    
    // "OK" が返ってくればATモード成功。返ってこなければ既にAPIモードであると判断
    bool apiEnabled = (strcmp(message, "OK") != 0);

    // ==========================================
    // パターンA: APIモードでの初期設定
    // ==========================================
    if (apiEnabled) {
        const uint8_t frameIdNoAck = 0x00; // 応答を要求しない

        // 1. SP (Cyclic Sleep Period) = 0x64 (1000 ms)
        const uint8_t sp_param[] = {0x64};
        sendAtCommandApiFrame("SP", frameIdNoAck, sp_param, sizeof(sp_param));
        _delay_ms(100);

        // 2. SN (Number of Cyclic Sleep Periods) = 3600 (0x0E10)
        const uint8_t sn_param[] = {0x0E, 0x10};
        sendAtCommandApiFrame("SN", frameIdNoAck, sn_param, sizeof(sn_param));
        _delay_ms(100);

        // 3. CE (Coordinator Enable) = 0 (End Device)
        const uint8_t ce_param[] = {0};
        sendAtCommandApiFrame("CE", frameIdNoAck, ce_param, sizeof(ce_param));
        _delay_ms(100);

        // 4. SM (Sleep Mode) = 1 (Pin Hibernate)
        const uint8_t sm_param[] = {1};
        sendAtCommandApiFrame("SM", frameIdNoAck, sm_param, sizeof(sm_param));
        _delay_ms(100);

        // 5. BT (Bluetooth Enable) = 1 (Enabled)
        const uint8_t bt_param[] = {1};
        sendAtCommandApiFrame("BT", frameIdNoAck, bt_param, sizeof(bt_param));
        _delay_ms(100);

        // 6. D5 (Zigbee Assoc LED) = 4 (OFF/Low)
        const uint8_t d5_param[] = {4};
        sendAtCommandApiFrame("D5", frameIdNoAck, d5_param, sizeof(d5_param));
        _delay_ms(100);

        // 7. BI (Bluetooth Identifier) = Device Name
        // EM_mlName は extern 宣言されている前提
        sendAtCommandApiFrame("BI", frameIdNoAck, (const uint8_t*)EM_mlName, strlen(EM_mlName));
        _delay_ms(100);

        // 8. WR (Write to non-volatile memory)
        sendAtCommandApiFrame("WR", frameIdNoAck, NULL, 0);
        _delay_ms(100);
    }
    // ==========================================
    // パターンB: ATモードでの初期設定
    // ==========================================
    else {
        // 1. SP Check (1000ms = 0x64)
        sendString("atsp\r");
        receiveMessage(message);
        if (strcmp(message, "64") != 0) {
            sendString("atsp64\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 2. SN Check (3600 sec)
        sendString("atsn\r");
        receiveMessage(message);
        if (strcmp(message, "3600") != 0) {
            sendString("atsn3600\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 3. CE Check (End Device = 0)
        sendString("atce\r");
        receiveMessage(message);
        if (strcmp(message, "0") != 0) {
            sendString("atce0\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 4. SM Check (Pin Hibernate = 1)
        sendString("atsm\r");
        receiveMessage(message);
        if (strcmp(message, "1") != 0) {
            sendString("atsm1\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 5. D5 Check (Out Low = 4)
        sendString("atd5\r");
        receiveMessage(message);
        if (strcmp(message, "0") != 0) { // Default is usually 1, check if not 0? Original logic was checking against "0"
            // Note: Original code logic: if (strcmp(message, "0") != 0) -> set to 4. 
            // Correct logic based on desired setting (4):
            sendString("atd54\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 6. BT Check & Password Setup
        sendString("atbt\r");
        receiveMessage(message);
        if (strcmp(message, "0") == 0) { // If BT is currently disabled
            // Salt ($S)
            sendString("at$S28513497\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;

            // Verifier ($V)
            sendString("at$V6567694B0CA9ADCED8D5B2B0015718D1E2637B86E3E178E029936A078926C2B0\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;

            // Salt ($W)
            sendString("at$W259F6833E1E1932E4485F48865FB6B76EC6E847A7272C77A8C27DD7DF94E44DC\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;

            // Iteration ($X)
            sendString("at$XA99A6E3937FBB8D05BBB4E4A8C4CB221C14D15CD004139C77B6FE0C8AF2932D8\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;

            // Key ($Y)
            sendString("at$Y6D4411D507FC52AFD5877D6E8529AEE7FB931F10944BC0D058FB246D0DE071DB\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;

            // Enable Bluetooth
            sendString("atbt1\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            
            hasChanged = true;
        }

        // 7. BI (Name) Check
        sendString("atbi\r");
        receiveMessage(message);
        if (strcmp(message, EM_mlName) != 0) {
            sendString("atbi");
            sendString(EM_mlName);
            sendString("\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 8. AP Check (API Enable = 1)
        sendString("atap\r");
        receiveMessage(message);
        if (strcmp(message, "1") != 0) {
            sendString("atap1\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
            hasChanged = true;
        }

        // 9. WR (Write if changed)
        if (hasChanged) {
            sendString("atwr\r");
            receiveMessage(message);
            if (strcmp(message, "OK") != 0) return false;
        }
    }

    // 初期化完了を記録
    EM_xbeeInitialized();
    return true;
}

void Xbee_SoftwareReset(void)
{
    // 送信前にスリープ中であれば起こす
    bool wasSleeping = Xbee_IsSleeping();
    if(wasSleeping) 
    {
        Xbee_Wakeup();
        _delay_ms(2); // 起床待ち
    }

    // FR (Software Reset) コマンドを API フレームで送信
    sendAtCommandApiFrame("FR", 0x01, NULL, 0);
    
    // リセット処理と再起動にかかる時間を待機
    _delay_ms(500);

    // リセット後は接続状態が不明になるため、内部変数を初期化
    g_association_status = 0xFF;
    g_join_timer = 0;
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：送受信">

void Xbee_LoadUART(void)
{
    // UARTリングバッファにデータがあれば、すべて処理する
    while (USART0_IsRxReady())
    {
        // 1バイトずつパーサーに渡し、戻り値がtrueならXbeeフレームの受信が完了
        if (processXbeeByte(USART0_Read(), xbee_payload_buffer, sizeof(xbee_payload_buffer)))
            CH_AppendString(xbee_payload_buffer, g_lastApiId == XB_FRAME_ZIGBEE_RECEIVE_PACKET ? SRC_XBEE : SRC_BLE);
    }
}

// 1 つの ZIGBEE_TX_REQUEST フレーム (coordinator 宛) を送信するヘルパ。
// data: 送信バイト先頭, len: この呼び出しで送るバイト数のみ。
// 呼び出し側で Wakeup/Sleep/g_communicating の管理を行う。
static void xbee_zb_send_chunk(const char *data, int len)
{
    int chkSum = 0;

    USART0_Write(XB_START_DELIMITER);
    USART0_Write((uint8_t)(((len + XB_TX_HEADER_LENGTH) >> 8) & 0xff));
    USART0_Write((uint8_t)((len + XB_TX_HEADER_LENGTH) & 0xff));

    USART0_Write(XB_FRAME_ZIGBEE_TX_REQUEST);
    chkSum = addCsum(chkSum, XB_FRAME_ZIGBEE_TX_REQUEST);

    g_txStatusReceived = false;
    g_lastFrameId++;
    if(g_lastFrameId == 0) g_lastFrameId = 0x01; // 0はNoACKなのでスキップ
    USART0_Write(g_lastFrameId);
    chkSum = addCsum(chkSum, g_lastFrameId);

    // 64bit Address (All 0)
    for(int i=0; i<8; i++) USART0_Write(0x00);

    // 16bit Address
    uint8_t addr_msb = (uint8_t)(XB_TX_ADDR16_COORDINATOR >> 8);
    uint8_t addr_lsb = (uint8_t)(XB_TX_ADDR16_COORDINATOR & 0xFF);
    USART0_Write(addr_msb); chkSum = addCsum(chkSum, addr_msb);
    USART0_Write(addr_lsb); chkSum = addCsum(chkSum, addr_lsb);

    USART0_Write(XB_TX_BROADCAST_RADIUS_MAX);
    USART0_Write(XB_TX_OPTIONS_DEFAULT);

    // Payload
    for(int i=0; i<len; i++) {
        USART0_Write((uint8_t)data[i]);
        chkSum = addCsum(chkSum, data[i]);
    }

    USART0_Write((uint8_t)(XB_CHECKSUM_SUCCESS - chkSum));

    waitTxCompletion(200);
}

// XBee 3 Zigbee の単一 API フレーム payload 上限 (NP) を超える長さを 1 発で
// 送ると ZB モジュールが silently drop する。256B 弱に余裕を持たせて
// 150B/chunk で複数 ZIGBEE_TX_REQUEST に分割して送る。
// (実例: hello 応答 156B は送れるが get_settings 応答 ~290B は届かなかった)
// 受信側 (MLServer JsonRpcV4Protocol の LineBuffer 等) は \n まで連結するため
// 分割は透過的。
#define XB_TX_MAX_CHUNK_BYTES 150

void Xbee_TxChars(const char *data)
{
    g_communicating = true;
    //Sleepの場合には一旦起こす
    bool wasSleeping = Xbee_IsSleeping();
    if(wasSleeping)
    {
        Xbee_Wakeup();
        DELAY_microseconds(150); //XBee立ち上げに0.05ms程度必要:3倍
    }

    int total = getCharLength(data);

    // DIAG: Zigbee 経由送信開始
    diag_usb_logf("TX_ZB_BEGIN total=%d wasSleeping=%d", total, (int)wasSleeping);
    diag_usb_hex("TX_ZB_DATA", (const uint8_t*)data, total, 96);

    int offset = 0;
    while (offset < total) {
        int chunk = total - offset;
        if (chunk > XB_TX_MAX_CHUNK_BYTES) chunk = XB_TX_MAX_CHUNK_BYTES;
        xbee_zb_send_chunk(data + offset, chunk);
        offset += chunk;
    }

    //Sleep状態だったもしくはSleep指令が来た場合には再びSleep
    if(wasSleeping || g_shouldSleep) sleepXBee();
    g_communicating = false;
}

// 1 つの USER_DATA_RELAY フレーム (BLE 宛) を送信するヘルパ。
// data: 送信バイト先頭, len: 送信バイト数 (この呼び出しで送るバイト数のみ)。
// 呼び出し側で Wakeup/Sleep/g_communicating の管理を行う。
//
// requestStatus=true:  Frame ID 非ゼロで送出し、TX Status (0x89) を最大 200ms 待機。
//                      ただし 0x89 は「エラー時のみ」返る仕様のため、成功時は必ず
//                      タイムアウトする。現在は未使用 (将来 BLE 切断検知等でエラー
//                      status を拾いたくなった場合のために残置)。
// requestStatus=false: Frame ID = 0 で送出する fire-and-forget (現在の標準経路)。
//                      XBee 内部 buffer に複数 chunk を積んで、BLE 配信は XBee に任せる。
static void xbee_bl_send_chunk_ex(const char *data, int len, bool requestStatus)
{
    int chkSum = 0;

    USART0_Write(XB_START_DELIMITER);
    USART0_Write((uint8_t)(((len + XB_UDR_HEADER_LENGTH) >> 8) & 0xff));
    USART0_Write((uint8_t)((len + XB_UDR_HEADER_LENGTH) & 0xff));

    USART0_Write(XB_FRAME_USER_DATA_RELAY);
    chkSum = addCsum(chkSum, XB_FRAME_USER_DATA_RELAY);

    uint8_t frameId;
    if (requestStatus) {
        g_txStatusReceived = false;
        g_lastFrameId++;
        if(g_lastFrameId == 0) g_lastFrameId = 0x01;
        frameId = g_lastFrameId;
    } else {
        frameId = 0x00;  // XBee は応答しない
    }
    USART0_Write(frameId);
    chkSum = addCsum(chkSum, frameId);

    USART0_Write(XB_UDR_INTERFACE_BLUETOOTH);
    chkSum = addCsum(chkSum, XB_UDR_INTERFACE_BLUETOOTH);

    for(int i = 0; i < len; i++) {
        USART0_Write((uint8_t)data[i]);
        chkSum = addCsum(chkSum, data[i]);
    }

    USART0_Write((uint8_t)(XB_CHECKSUM_SUCCESS - chkSum));

    if (requestStatus) waitTxCompletion(200);
}

// XBee 3 の BLE GATT notification MTU (~244B) を超える単一フレームは
// モジュールが silently drop してしまうため、安全側で 220B/chunk に分割して
// 複数の USER_DATA_RELAY フレームで送る。受信側 (MAUI LineBuffer 等) は \n
// まで連結するので分割は透過的。
// dump 用に 200B 超でも安定送信できることを実機で確認 (244B 上限に対し ~25B 余裕)。
#define XB_BL_MAX_CHUNK_BYTES 220

// 複数 chunk 送信時の chunk 間 pacing [ms]。XBee 内部 buffer の BLE 側 drain
// (実効 5-8 KB/sec) を待つ。dump 経路 (USB_Stream_Task) の 40ms と同じ根拠。
#define XB_BL_INTER_CHUNK_DELAY_MS 40

void Xbee_BlChars(const char *data)
{
    g_communicating = true;
    bool wasSleeping = Xbee_IsSleeping();
    if(wasSleeping)
    {
        Xbee_Wakeup();
        DELAY_microseconds(150);
    }

    int total = getCharLength(data);

    // DIAG: BLE 経由で送出する文字列全体 (chunking 前) を吐く
    diag_usb_logf("TX_BLE_BEGIN total=%d wasSleeping=%d", total, (int)wasSleeping);
    diag_usb_hex("TX_BLE_DATA", (const uint8_t*)data, total, 96);

    // fire-and-forget で送る。USER_DATA_RELAY への TX Status (0x89) は
    // 「エラー時のみ」返る仕様 (実機トレースで成功時に 0x89 が来ないことを確認
    // 2026-08-10)。ステータス待ちは成功時に必ずタイムアウトするだけなので廃止。
    // 複数 chunk の場合のみ、XBee 内部 buffer 溢れ防止に chunk 間 pacing を入れる。
    int offset = 0;
    while (offset < total) {
        int chunk = total - offset;
        if (chunk > XB_BL_MAX_CHUNK_BYTES) chunk = XB_BL_MAX_CHUNK_BYTES;
        xbee_bl_send_chunk_ex(data + offset, chunk, false);
        offset += chunk;
        if (offset < total) DELAY_milliseconds(XB_BL_INTER_CHUNK_DELAY_MS);
    }

    if(wasSleeping || g_shouldSleep) sleepXBee();
    g_communicating = false;
}

void Xbee_BlTxChars(const char *data)
{
    Xbee_TxChars(data);
    Xbee_BlChars(data);
}

// バイナリ送信 (BLE)。Xbee_BlChars の binary-safe 版。\0 を含むデータでも len バイト全てを送る。
// dump 用 (内蔵フラッシュからレコードを binary stream として書き出す目的)。
// fire-and-forget: TX_STATUS は要求せず連続送信。pacing は呼び出し側 (USB_Stream_Task)
// で 1 chunk ずつ呼んで chunk 間 delay を入れる方式 (XBee 内部 buffer overflow 防止)。
void Xbee_BlBytes(const uint8_t *data, int len)
{
    g_communicating = true;
    bool wasSleeping = Xbee_IsSleeping();
    if(wasSleeping)
    {
        Xbee_Wakeup();
        DELAY_microseconds(150);
    }

    int offset = 0;
    while (offset < len) {
        int chunk = len - offset;
        if (chunk > XB_BL_MAX_CHUNK_BYTES) chunk = XB_BL_MAX_CHUNK_BYTES;
        xbee_bl_send_chunk_ex((const char*)(data + offset), chunk, false);
        offset += chunk;
    }

    if(wasSleeping || g_shouldSleep) sleepXBee();
    g_communicating = false;
}

// バイナリ送信 (Zigbee)。Xbee_TxChars の binary-safe 版。
void Xbee_TxBytes(const uint8_t *data, int len)
{
    g_communicating = true;
    bool wasSleeping = Xbee_IsSleeping();
    if(wasSleeping)
    {
        Xbee_Wakeup();
        DELAY_microseconds(150);
    }

    int offset = 0;
    while (offset < len) {
        int chunk = len - offset;
        if (chunk > XB_TX_MAX_CHUNK_BYTES) chunk = XB_TX_MAX_CHUNK_BYTES;
        xbee_zb_send_chunk((const char*)(data + offset), chunk);
        offset += chunk;
    }

    if(wasSleeping || g_shouldSleep) sleepXBee();
    g_communicating = false;
}

void Xbee_SendAtCmd(const char *data)
{
    sendString(data);
}

// AT コマンドを API フレームで発行し、レスポンスの value バイト列を取得する (blocking)。
// 出荷検査・診断用 (例: SH/SL で 64bit MAC、VR で XBee firmware version)。
// 戻り値: 受信した value のバイト数 (0 = value 無しの OK)、-1 = timeout / ステータス異常。
int Xbee_QueryAt(const char at_command[2], uint8_t *out, uint8_t out_cap)
{
    bool wasSleeping = Xbee_IsSleeping();
    if (wasSleeping) {
        Xbee_Wakeup();
        DELAY_microseconds(150);
    }

    g_atrReceived = false;
    sendAtCommandApiFrame(at_command, 0x01, NULL, 0);

    int result = -1;
    uint16_t waited = 0;
    while (waited < 300) {
        Xbee_LoadUART();
        if (g_atrReceived &&
            g_atrCmd[0] == at_command[0] && g_atrCmd[1] == at_command[1]) {
            if (g_atrStatus != 0x00) break; // AT status エラー
            uint8_t n = g_atrLen;
            if (n > out_cap) n = out_cap;
            memcpy(out, g_atrData, n);
            result = n;
            break;
        }
        _delay_ms(1);
        waited++;
    }

    if (wasSleeping || g_shouldSleep) sleepXBee();
    return result;
}

// EEPROM の名称 (EM_mlName) を XBee の BLE アドバタイズ名 (BI) に反映して永続化する。
// set_name 受信時に呼ぶ (従来は Xbee_Initialize の初期設定でしか BI を書かず、
// 改名しても BLE 名が初期値のまま残っていた)。
// 広告名の確実な反映は XBee の再起動後だが、FR (ソフトリセット) は発行しない
// (BLE 経由の改名で接続が切断されるのを避けるため。出荷工程では試験後の
// 電源再投入で反映される)。
void Xbee_ApplyBleName(void)
{
    bool wasSleeping = Xbee_IsSleeping();
    if (wasSleeping) {
        Xbee_Wakeup();
        DELAY_microseconds(150);
    }

    const uint8_t frameIdNoAck = 0x00; // 応答を要求しない
    sendAtCommandApiFrame("BI", frameIdNoAck, (const uint8_t*)EM_mlName, strlen(EM_mlName));
    _delay_ms(100);
    sendAtCommandApiFrame("WR", frameIdNoAck, NULL, 0); // 不揮発メモリへ保存
    _delay_ms(100);

    if (wasSleeping || g_shouldSleep) sleepXBee();
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：スリープ">

void Xbee_MaintainTask(Xbee_InterfaceConfig_t config) {
    // 以下のいずれかの間はスリープ不可:
    //  - BLE 出力有効 (ロギングの BLE transport)
    //  - wake hold (time_sync window / 非ロギング時の BLE 接続受付)
    //  - BLE central (スマホ) 接続中 (Pin Hibernate は BLE ごと落とすため、
    //    Zigbee ロギング中でもスマホ接続が生きている限り起こしておく)
    if (config.ble_enabled || config.wake_hold_active || g_bleConnected) {
        Xbee_Wakeup();
        g_ai_request_timer = 0;
        return;
    }
    
    // ZigbeeもBLEも不要な場合にはスリープ
    if (!config.zigbee_enabled) {
        Xbee_Sleep();
        g_ai_request_timer = 0;
        return;
    }
        
    // Zigbeeが必要な場合は定期的にAI（接続状態）を問い合わせ
    g_ai_request_timer++;
    uint16_t interval = (g_association_status == 0x00) ? 60 : 10; // 接続済みなら60秒、未接続なら10秒
    if (interval <= g_ai_request_timer) {
        sendAtCommandApiFrame("AI", 0x01, NULL, 0);
        g_ai_request_timer = 0;
    }

    // 未接続（AI != 0）かつ Zigbee要求ありの場合は再接続を試行（CBコマンド）
    if (g_association_status != 0x00) {
        Xbee_Wakeup(); // 接続するまでは寝かせない
        g_join_timer++;

        if (g_join_timer == 30) {      // 30秒待ってもダメなら
            Xbee_SendCommissioningButton(2);  // ジョイン通知 (CB 2)
            g_join_timer = 0;
        }
    } else {
        // 接続済みなら送信時以外は寝て良い
        g_join_timer = 0;
        Xbee_Sleep();
    }
}

void Xbee_Sleep(void)
{
    if(!g_communicating) sleepXBee();
    else g_shouldSleep = true;
}

void Xbee_Wakeup(void)
{
    SLP_XBEE_SetLow();
    g_shouldSleep = false;
}

bool Xbee_IsSleeping(void)
{
    return SLP_XBEE_GetValue();
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：Zigbee通信">

// AIの値を外部から取得する
uint8_t Xbee_GetAssociationStatus(void) {
    return g_association_status;
}

// CBコマンドを送信する
void Xbee_SendCommissioningButton(uint8_t count) {
    sendAtCommandApiFrame("CB", 0x01, &count, 1);
}

// AI値を問い合わせる
void Xbee_RequestAssociationStatus(void) {
    sendAtCommandApiFrame("AI", 0x01, NULL, 0);
}

// </editor-fold>