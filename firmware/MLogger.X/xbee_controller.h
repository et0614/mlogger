/* 
 * File:   xbee_controller.h
 * Author: e.togashi
 *
 * Created on 2025/12/14, 12:16
 */

#ifndef XBEE_CONTROLLER_H
#define	XBEE_CONTROLLER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
    
#define XB_START_DELIMITER   0x7E
#define XB_CHECKSUM_SUCCESS  0xFF

// Frame Types
#define XB_FRAME_AT_COMMAND            0x08
#define XB_FRAME_ZIGBEE_TX_REQUEST     0x10
#define XB_FRAME_USER_DATA_RELAY       0x2D
#define XB_FRAME_AT_COMMAND_RESPONSE   0x88
#define XB_FRAME_ZIGBEE_RECEIVE_PACKET 0x90
#define XB_FRAME_USER_DATA_RELAY_IN    0xAD
#define XB_FRAME_TRANSMIT_STATUS       0x8B

// TxRequest Constants
#define XB_TX_FRAME_ID_NO_ACK      0x00
#define XB_TX_ADDR16_COORDINATOR   0xFFFE
#define XB_TX_BROADCAST_RADIUS_MAX 0x00
#define XB_TX_OPTIONS_DEFAULT      0x00
#define XB_TX_HEADER_LENGTH        14

// UserDataRelay Constants
#define XB_UDR_FRAME_ID_DEFAULT    0x00
#define XB_UDR_INTERFACE_BLUETOOTH 0x01
#define XB_UDR_HEADER_LENGTH       3

// RxPayloadOffset
#define XB_RX_OFFSET_ZIGBEE_PACKET 14
#define XB_RX_OFFSET_USER_DATA_RELAY 4
#define XB_RX_OFFSET_TRANSMIT_STATUS 4
#define XB_RX_OFFSET_AT_COMMAND_RESPONSE 4

// 通信インターフェースの有効状態を伝えるための構造体
typedef struct {
    bool zigbee_enabled; // Zigbee通信（親機への送信）が必要か
    bool ble_enabled;    // BLE通信が必要か
} Xbee_InterfaceConfig_t;

/**
 * @brief XBeeの接続維持・スリープ制御タスク
 * @param config 現在の通信要求
 */
void Xbee_MaintainTask(Xbee_InterfaceConfig_t config);
    
// --- 関数プロトタイプ ---

void Xbee_LoadUART(void);
    
/**
 * @brief 文字列をZigbeeで送信
 */
void Xbee_TxChars(const char *data);

/**
 * @brief 文字列をBluetoothで送信
 */
void Xbee_BlChars(const char *data);

/**
 * @brief 文字列をZigbeeとBluetooth両方で送信
 */
void Xbee_BlTxChars(const char *data);

/**
 * @brief ATコマンドを直接送信 (APIフレームではない生コマンド用)
 */
void Xbee_SendAtCmd(const char *data);

/**
 * @brief XBeeの設定を初期化する
 * @return 初期化成功: true
 */
bool Xbee_Initialize(void);

/**
 * @brief スリープする
 */
void Xbee_Sleep(void);

/**
 * @brief スリープ解除する
 */
void Xbee_Wakeup(void);

/**
 * @brief スリープ中か否かを取得する
 * @return true: スリープ中 false: 起動中
 */
bool Xbee_IsSleeping(void);

/**
 * @brief XBeeをソフトウェアリセットする (FRコマンド)
 */
void Xbee_SoftwareReset(void);

// <editor-fold defaultstate="collapsed" desc="Zigbee通信">

/**
 * @brief AIの値を取得する
 * @return AIの値
 */
uint8_t Xbee_GetAssociationStatus(void);

/**
 * @brief CB（コミッショニングボタン）コマンドを送信する
 * @param count コミッショニングボタンの押下回数
 */
void Xbee_SendCommissioningButton(uint8_t count);

/**
 * @brief AI値を問い合わせる
 */
void Xbee_RequestAssociationStatus(void);

// </editor-fold>

#ifdef	__cplusplus
}
#endif

#endif	/* XBEE_CONTROLLER_H */

