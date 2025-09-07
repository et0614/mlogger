/**
 * @file XbeeController.h
 * @brief AVR(ATMega328)でXBeeと通信する
 * @author E.Togashi
 * @date 2021/11/28
 */

#include "XbeeController.h"
#include "UartDriver.h"
#include "EepromManager.h"
#include "parameters.h"

#include <string.h>
#include <avr/io.h>
#include <util/delay.h>

const int UART_RX_BUFFER_SIZE  = 256;

const size_t MBUFF_LENGTH = 20;

//XBeeフレーム解析用変数
static bool readingFrame = false; //フレーム読込中か否か
static uint8_t framePosition = 0; //フレーム読み込み位置
static uint8_t frameSize = 0; //フレームappend_commandサイズ
static char frameBuff[UART_RX_BUFFER_SIZE]; //フレーム
static uint8_t xbeeOffset=14; //受信frame typeに応じたオフセット
static uint8_t frameChecksum = 0; //受信フレームチェックサム計算用変数

void XbeeController::initialize(void)
{
	UartDriver::initialize();	
}

//コーディネータに対して文字配列を送信
void XbeeController::txChars(const char data[])
{
	int chkSum = 0;
	int cl = getCharLength(data);
	
	UartDriver::sendChar(XbeeApi::START_DELIMITER); //APIフレーム開始コード
	UartDriver::sendChar((char)(((cl + XbeeApi::TxRequest::HEADER_LENGTH) >> 8) & 0xff));	//データ長の上位バイト
	UartDriver::sendChar((char)((cl + XbeeApi::TxRequest::HEADER_LENGTH) & 0xff));			//データ長の下位バイト
	
	//ここからチェックサム加算*************
	UartDriver::sendChar(XbeeApi::FrameType::ZIGBEE_TX_REQUEST); //コマンドID（データ送信は0x10）
	chkSum = addCsum(chkSum, XbeeApi::FrameType::ZIGBEE_TX_REQUEST);
	
	UartDriver::sendChar(XbeeApi::TxRequest::FRAME_ID_NO_ACK); //フレームID（任意）//0以外だとACKが戻ってくる。
	//chkSumへの加算は0なので省略
	
	for(int i=0;i<8;i++) //64bit送信先アドレスはコーディネータへの送信なのですべて0でチェックサムは不変
		UartDriver::sendChar(0x00);
	//chkSumへの加算は0なので省略
	
	// 16bit宛先アドレス (不明な場合は0xFFFE)
	const uint8_t addr16_msb = (uint8_t)(XbeeApi::TxRequest::ADDR16_COORDINATOR >> 8);
	const uint8_t addr16_lsb = (uint8_t)(XbeeApi::TxRequest::ADDR16_COORDINATOR & 0xFF);	
	UartDriver::sendChar(addr16_msb); //16bit送信先アドレス_M
	chkSum = addCsum(chkSum, addr16_msb);	
	UartDriver::sendChar(addr16_lsb); //16bit送信先アドレス_L
	chkSum = addCsum(chkSum, addr16_lsb);
	
	UartDriver::sendChar(XbeeApi::TxRequest::BROADCAST_RADIUS_MAX); //ブロードキャスト半径（ユニキャストなので0でチェックサムは不変）
	//chkSumへの加算は0なので省略

	UartDriver::sendChar(XbeeApi::TxRequest::OPTIONS_DEFAULT); //送信オプションは0でチェックサムは不変
	//chkSumへの加算は0なので省略
	
	//送信データ
	for(int i=0;i<cl;i++)
	{
		UartDriver::sendChar(data[i]);		
		chkSum = addCsum(chkSum, data[i]);
	}
	
	UartDriver::sendChar((char)(XbeeApi::CHECKSUM_SUCCESS - chkSum)); //Checksum送信
}

int XbeeController::getCharLength(const char data[])
{
	int length = 0;
	for(length = 0; data[length]; length++);
	return length;
}

int XbeeController::addCsum(int csum, char nbyte)
{
	csum += (int)nbyte;
	csum = csum & 0x00ff;
	return csum;
}

//Bluetooth接続先に対して文字配列を送信
void XbeeController::blChars(const char data[])
{
	int chkSum = 0;
	int cl = getCharLength(data);
	
	UartDriver::sendChar(XbeeApi::START_DELIMITER); //APIフレーム開始コード
	UartDriver::sendChar((char)(((cl + XbeeApi::UserDataRelay::HEADER_LENGTH) >> 8) & 0xff));	//データ長の上位バイト
	UartDriver::sendChar((char)((cl + XbeeApi::UserDataRelay::HEADER_LENGTH) & 0xff));			//データ長の下位バイト
	
	//ここからチェックサム加算*************
	UartDriver::sendChar(XbeeApi::FrameType::USER_DATA_RELAY); //コマンドID（DataRelayは0x2D）
	chkSum = addCsum(chkSum, XbeeApi::FrameType::USER_DATA_RELAY);
	
	UartDriver::sendChar(XbeeApi::UserDataRelay::FRAME_ID_DEFAULT); //フレームID
	
	UartDriver::sendChar(XbeeApi::UserDataRelay::INTERFACE_BLUETOOTH); //Source interface（Bluetoothは0x01）
	chkSum = addCsum(chkSum, XbeeApi::UserDataRelay::INTERFACE_BLUETOOTH);
		
	//送信データ
	for(int i=0;i<cl;i++)
	{
		UartDriver::sendChar(data[i]);
		chkSum = addCsum(chkSum, data[i]);
	}
	
	UartDriver::sendChar((char)(XbeeApi::CHECKSUM_SUCCESS - chkSum)); //Checksum送信
}

void XbeeController::bltxChars(const char data[])
{
	txChars(data);
	blChars(data);	
}

void XbeeController::sendAtCmd(const char data[]){
	int cl = getCharLength(data);
	//送信データ
	for(int i=0;i<cl;i++)
		UartDriver::sendChar(data[i]);
}

bool XbeeController::xbeeSettingInitialized(){
	//XBeeが初期化済みならばスキップ
	if(EepromManager::isXBeeInitialized()) 
		return 1;
	
	bool hasChanged = false;
	char message[MBUFF_LENGTH];
	memset(message, 0, sizeof(message));

	//ATモードへ
	_delay_ms(1100); // ガードタイム
	UartDriver::sendChars("+++");
	_delay_ms(1100); // ガードタイム
	XbeeController::receiveMessage(message);
	bool apiEnabled = strcmp(message, "OK") != 0;
	
	//APIモードでの初期設定********************
	if(apiEnabled){
		const uint8_t frameIdNoAck = 0x00; // 応答を要求しないフレームID

		// 1. SP (Cyclic Sleep Period) を 0x64 (1000 ms) に設定
		const uint8_t sp_param[] = {0x64};
		sendAtCommandApiFrame("SP", frameIdNoAck, sp_param, sizeof(sp_param));
		_delay_ms(100);

		// 2. SN (Number of Cyclic Sleep Periods) を 3600 に設定
		const uint8_t sn_param[] = {0x0E, 0x10}; // 3600 = 0x0E10
		sendAtCommandApiFrame("SN", frameIdNoAck, sn_param, sizeof(sn_param));
		_delay_ms(100);

		// 3. CE (Coordinator Enable) を 0 (End Device) に設定
		const uint8_t ce_param[] = {0};
		sendAtCommandApiFrame("CE", frameIdNoAck, ce_param, sizeof(ce_param));
		_delay_ms(100);

		// 4. SM (Sleep Mode) を 1 (Pin Hibernate) に設定
		const uint8_t sm_param[] = {1};
		sendAtCommandApiFrame("SM", frameIdNoAck, sm_param, sizeof(sm_param));
		_delay_ms(100);

		// 5. BT (Bluetooth Enable) を 1 (Enabled) に設定
		const uint8_t bt_param[] = {1};
		sendAtCommandApiFrame("BT", frameIdNoAck, bt_param, sizeof(bt_param));
		_delay_ms(100);

		// 6. D5（Zigbee通信LED）を 4 (OFF/Low) に設定
		const uint8_t d5_param[] = {4};
		sendAtCommandApiFrame("D5", frameIdNoAck, d5_param, sizeof(d5_param));
		_delay_ms(100);

		// 7. BI（Bluetooth Identifier）をロガー名に設定
		sendAtCommandApiFrame("BI", frameIdNoAck, (const uint8_t*)ML_NAME, strlen(ML_NAME));
		_delay_ms(100);

		// 8. 全ての設定を不揮発性メモリに書き込み (重要)
		sendAtCommandApiFrame("WR", frameIdNoAck, nullptr, 0);
		_delay_ms(100);
	}
	
	//ATモードでの初期設定********************
	else{
		//SPは1000msec=0x64
		UartDriver::sendChars("atsp\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "64") != 0) {
			UartDriver::sendChars("atsp64\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//SNは3600sec
		UartDriver::sendChars("atsn\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "3600") != 0) {
			UartDriver::sendChars("atsn3600\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//CEはend device(0)
		UartDriver::sendChars("atce\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "0") != 0) {
			UartDriver::sendChars("atce0\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//SMはPin Hibernate(1)
		UartDriver::sendChars("atsm\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "1") != 0) {
			UartDriver::sendChars("atsm1\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//d5はZigbee通信状況のLED表示をOff（Out low:4)に設定。
		UartDriver::sendChars("atd5\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "0") != 0) {
			UartDriver::sendChars("atd54\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//Bluetooth有効/無効+password(ml_pass)
		UartDriver::sendChars("atbt\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "0") == 0) {
			UartDriver::sendChars("at$S28513497\r"); //salt
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$V6567694B0CA9ADCED8D5B2B0015718D1E2637B86E3E178E029936A078926C2B0\r"); //$V
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$W259F6833E1E1932E4485F48865FB6B76EC6E847A7272C77A8C27DD7DF94E44DC\r"); //$W
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$XA99A6E3937FBB8D05BBB4E4A8C4CB221C14D15CD004139C77B6FE0C8AF2932D8\r"); //$X
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$Y6D4411D507FC52AFD5877D6E8529AEE7FB931F10944BC0D058FB246D0DE071DB\r"); //$Y
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("atbt1\r"); //Bluetooth 有効化
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//biはBluetooth identifier
		UartDriver::sendChars("atbi\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, ML_NAME) != 0) {
			UartDriver::sendChars("atbi");
			UartDriver::sendChars(ML_NAME);
			UartDriver::sendChars("\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//AP
		UartDriver::sendChars("atap\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "1") != 0) {
			UartDriver::sendChars("atap1\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//変更があった場合にはメモリに書き込む
		if(hasChanged){
			UartDriver::sendChars("atwr\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
		}
	}
		
	//XBee初期化フラグを記録
	EepromManager::xbeeInitialized();
	return 1;
}

bool XbeeController::processXbeeByte(char dat,  char* output_buffer, int buffer_size)
{
	uint8_t current_byte = (uint8_t)dat;

	//開始コード「~」が来たら初期化。本当はEscape処理がいるが、「~」はコマンドで使わないので良いだろう
	if (current_byte == XbeeApi::START_DELIMITER)
	{
		framePosition = 1;
		readingFrame = true;
		frameChecksum = 0; //チェックサム初期化
		return false; // このバイトの処理は終了
	}
	
	//フレーム読込中では無い
	if(!readingFrame) return false;
	
	if(3 <= framePosition) frameChecksum += current_byte;

	//ここからフレーム解析
	switch(framePosition){
		case 1:  // データ長上位バイト。何もしない
			break;
			
		case 2: // データ長下位バイト
			frameSize = current_byte + 3;
			break;
			
		case 3: // コマンドID
			switch (current_byte) {
				case XbeeApi::FrameType::ZIGBEE_RECEIVE_PACKET:
					xbeeOffset = XbeeApi::RxPayloadOffset::ZIGBEE_RECEIVE_PACKET;
					break;
				case XbeeApi::FrameType::USER_DATA_RELAY_IN:
					xbeeOffset = XbeeApi::RxPayloadOffset::USER_DATA_RELAY_IN;
					break;
				default:
					readingFrame = false; // 未知のフレームタイプは解析中断
					break;
			}
			
		default:
			// データペイロード部を処理
			if(xbeeOffset < framePosition) {
				// チェックサムに到達
				if (frameSize <= framePosition) {
					// 状態をリセットして次のフレームに備える
					readingFrame = false;
					framePosition = 0;
					
					//チェックサム正常
					if(frameChecksum == XbeeApi::CHECKSUM_SUCCESS){
						// 内部バッファをNULL文字で終端させる
						int payload_len = framePosition - (xbeeOffset + 1);
						// バッファオーバーラン防止
						if (0 <= payload_len && payload_len < UART_RX_BUFFER_SIZE) frameBuff[payload_len] = '\0';
						else frameBuff[UART_RX_BUFFER_SIZE - 1] = '\0';
						
						// 完成したペイロードを、引数で渡された外部のバッファに安全にコピー
						strncpy(output_buffer, frameBuff, buffer_size - 1);
						output_buffer[buffer_size - 1] = '\0'; // 必ずNULL終端させる
						
						return true; //フレーム完成
					}
					//チェックサム異常：フレーム破棄してfalse
					else return false;
				}
				// バッファに格納
				else
				{
					// 内部バッファにデータを格納
					int buffer_index = framePosition - (xbeeOffset + 1);
					// バッファオーバーラン防止
					if (buffer_index < UART_RX_BUFFER_SIZE) frameBuff[buffer_index] = current_byte;
				} 
			}
			break;
	}
	framePosition++;
	// バッファサイズを超えたら、不正なフレームとして解析を中断
	if (framePosition >= UART_RX_BUFFER_SIZE + xbeeOffset + 2) {
		readingFrame = false;
		framePosition = 0;
	}

	return false; // フレーム未完成
}

void XbeeController::receiveMessage(char message[]){
	uint8_t index = 0;
	uint16_t timeout_counter = 0;
	const uint16_t TIMEOUT_LIMIT = 500; // タイムアウト時間(ms)

	memset(message, 0, MBUFF_LENGTH); // バッファをクリア

	while (timeout_counter < TIMEOUT_LIMIT)
	{
		//リングバッファからデータを取得する
		if (UartDriver::uartRingBufferHasData())
		{
			char c = UartDriver::uartRingBufferGet();
			if (c != '\n' && c != '\r') {
				// バッファサイズを超えないように
				if (index < MBUFF_LENGTH - 1) message[index++] = c;
			}

			if (c == '\r') {
				message[index] = '\0';
				return; // 正常に受信完了
			}
		}
		
		// データがなければ少し待つ
		_delay_ms(1);
		timeout_counter++;
	}
}

/**
 * @brief ATコマンドをAPIフレームとして送信する
 * @param at_command 2文字のATコマンド (例: "SP")
 * @param frame_id 応答を識別するためのフレームID (0は応答不要)
 * @param param 設定するパラメータ（問い合わせの場合はnullptr）
 * @param param_len パラメータの長さ
 */
void XbeeController::sendAtCommandApiFrame(const char at_command[2], uint8_t frame_id, const uint8_t* param, uint8_t param_len)
{
    uint8_t checksum = 0;
    uint8_t payload_len = 4 + param_len; // FrameType + FrameID + AT Command + Param

    UartDriver::sendChar(XbeeApi::START_DELIMITER);
    UartDriver::sendChar(0x00); // Length MSB
    UartDriver::sendChar(payload_len); // Length LSB

    // --- Checksum対象 ---
    UartDriver::sendChar(XbeeApi::FrameType::AT_COMMAND);
    checksum += XbeeApi::FrameType::AT_COMMAND;

    UartDriver::sendChar(frame_id);
    checksum += frame_id;

    UartDriver::sendChar(at_command[0]);
    checksum += at_command[0];
    UartDriver::sendChar(at_command[1]);
    checksum += at_command[1];

    for (uint8_t i = 0; i < param_len; i++) {
        UartDriver::sendChar(param[i]);
        checksum += param[i];
    }
    // --------------------

    UartDriver::sendChar(0xFF - checksum);
}