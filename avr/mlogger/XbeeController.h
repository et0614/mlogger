/**
 * @file XbeeController.h
 * @brief AVR(ATMega328)でXBeeと通信する
 * @author E.Togashi
 * @date 2021/11/28
 */ 

#ifndef XBEE_CONTROLLER_H_
#define XBEE_CONTROLLER_H_

#include <stdint.h>

// XBee APIに関する定数をまとめる名前空間
namespace XbeeApi {
	// APIフレームの基本要素
	constexpr uint8_t START_DELIMITER = 0x7E;
	constexpr uint8_t CHECKSUM_SUCCESS = 0xFF;

	// フレームタイプID
	namespace FrameType {
		constexpr uint8_t ZIGBEE_TX_REQUEST   = 0x10;
		constexpr uint8_t USER_DATA_RELAY     = 0x2D;
		constexpr uint8_t ZIGBEE_RECEIVE_PACKET = 0x90;
		constexpr uint8_t USER_DATA_RELAY_IN  = 0xAD; // 受信時のフレームタイプ
	}

	// フレーム構造に関する定数
	namespace TxRequest {
		constexpr uint8_t FRAME_ID_NO_ACK = 0x00;
		constexpr uint16_t ADDR16_COORDINATOR = 0xFFFE;
		constexpr uint8_t BROADCAST_RADIUS_MAX = 0x00;
		constexpr uint8_t OPTIONS_DEFAULT = 0x00;
		// ヘッダー長 (FrameTypeからOptionsまで)
		constexpr uint8_t HEADER_LENGTH = 14;
	}

	namespace UserDataRelay {
		constexpr uint8_t FRAME_ID_DEFAULT = 0x00;
		constexpr uint8_t INTERFACE_BLUETOOTH = 0x01;
		// ヘッダー長 (FrameTypeからInterfaceまで)
		constexpr uint8_t HEADER_LENGTH = 3;
	}

	// 受信フレームのペイロードオフセット
	namespace RxPayloadOffset {
		constexpr uint8_t ZIGBEE_RECEIVE_PACKET = 14;
		constexpr uint8_t USER_DATA_RELAY_IN  = 4;
	}
} // namespace XbeeApi

class XbeeController
{
	public:
		/**
		 * @fn
		 * XBee通信のための初期化処理
		 * @param 無し
		 * @return 無し
		 */
		static void initialize(void);
		
		/**
		 * @fn
		 * Frameに文字列を入れてXbee（Zigbee通信）で送信する
		 * @param (data) 文字列
		 * @return 無し
		 */
		static void txChars(const char data[]);
			
		/**
		 * @fn
		 * Frameに文字列を入れてXbee（Bluetooth通信）で送信する
		 * @param (data) 文字列
		 * @return 無し
		 */
		static void blChars(const char data[]);
	
		/**
		 * @fn
		 * Frameに文字列を入れてXbee（Zigbee+Bluetooth通信）で送信する
		 * @param (data) 文字列
		 * @return 無し
		 */
		static void bltxChars(const char data[]);

		/**
		 * @fn
		 * UART接続先のXBeeにATコマンドを送る
		 * @param (data) ATコマンド
		 * @return 無し
		 */
		static void sendAtCmd(const char data[]);
	
		/**
		 * @fn
		 * XBeeの設定を初期化する
		 * @return 初期化済または初期化成功の場合に1
		 */
		static bool xbeeSettingInitialized();
	
		// ...
		/**
		 * @brief 受信した1バイトを使い、XBeeフレームの解析を行う
		 * @param data 受信した1バイト
		 * @param output_buffer [out] 完成したコマンドを格納するバッファ
		 * @param buffer_size output_bufferのサイズ
		 * @return コマンドが完成した場合にtrueを返す
		 */
		static bool processXbeeByte(char data, char* output_buffer, int buffer_size);
	
	private:
	
		/**
		 * @fn
		 * 文字列の大きさを取得する
		 * @param (data) 文字列
		 * @return 文字列の大きさ
		 */
		static int getCharLength(const char data[]);

		/**
		 * @fn
		 * チェックサムを加算する
		 * @param (csum) 従前のチェックサム
		 * @param (nbyte) 追加するバイト
		 * @return 更新後のチェックサム
		 */
		static int addCsum(int csum, char nbyte);
	
		static void receiveMessage(char message[]);
};

#endif /* XBEE_CONTROLLER_H_ */