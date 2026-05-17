#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/timer/delay.h"
#include "Stcc4.h"
#include "i2c_master.h"
#include "crc.h"

//アドレス
#define ADDRESS 0x64 //(ADDR==GNDの設定)

//コマンド
#define CMD_PERFORM_CONDITIONING    0x29BC  //初期化
#define CMD_PERFORM_RECALIBRATION   0x362F  //強制校正
#define CMD_SOFT_RESET              0x06    // ソフトリセット
#define CMD_FACT_RESET              0x3632  // 工場出荷状態にリセット
#define CMD_ENTER_SLEEP             0x3650  // スリープ
#define CMD_EXIT_SLEEP              0x00    // スリープ解除
#define CMD_START_CONT_MEASUREMENT  0x218B  //連続測定開始
#define CMD_STOP_CONT_MEASUREMENT   0x3F86  //連続測定終了
#define CMD_MES_SINGLE_SHOT         0x219D  //Single shot測定
#define CMD_READ_MEASUREMENT        0xEC05  //計測結果読み取り
#define CMD_SET_RHT_COMPENSATION    0xE000  //補償用温湿度設定

// <editor-fold defaultstate="collapsed" desc="内部関数">

static bool sendCommand(uint16_t command) {
    uint8_t cmd_bytes[2];
    cmd_bytes[0] = (uint8_t)(command >> 8);
    cmd_bytes[1] = (uint8_t)(command & 0xFF);
    return I2C_Write(ADDRESS, cmd_bytes, sizeof(cmd_bytes));
}

static bool sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs)
{
	// 送信バッファを作成：コマンド(2B) + 引数(2B*N) + CRC(1B*N)
	uint8_t buffer_size = 2 + numArgs * 3;
	//uint8_t buffer[buffer_size];
	const uint8_t MAX_BUFFER_SIZE = 20; // 少し余裕を持たせる
	uint8_t buffer[MAX_BUFFER_SIZE];	

	// コマンドをバッファに格納
	buffer[0] = (uint8_t)(command >> 8);
	buffer[1] = (uint8_t)(command & 0xFF);

	// 引数とCRCをバッファに格納
	for (uint8_t i = 0; i < numArgs; i++) {
		uint16_t arg = args[i];
		uint8_t arg_msb = (uint8_t)(arg >> 8);
		uint8_t arg_lsb = (uint8_t)(arg & 0xFF);
		
		uint8_t base_index = 2 + i * 3;
		buffer[base_index] = arg_msb;
		buffer[base_index + 1] = arg_lsb;
		
		// 2バイトの引数データからCRC8を計算
		buffer[base_index + 2] = CRC_calc8(&buffer[base_index], 2);
	}
	
	// 組み立てたパケット全体を送信
	return I2C_Write(ADDRESS, buffer, buffer_size);
}

// </editor-fold>

bool STCC4_isConnected(){
    return I2C_IsConnected(ADDRESS);
}

bool STCC4_initialize(){
	const uint8_t command = CMD_SOFT_RESET;
	I2C_Write(0x00, &command, 1); //初期化はジェネラルコール
	DELAY_milliseconds(10); //待機

	return true; // 成功
}

bool STCC4_performConditioning(){
	if (!sendCommand(CMD_PERFORM_CONDITIONING))
		return false; // 通信失敗
	return true; // 成功（22秒間はコマンドを受け付けなくなる）
}

bool STCC4_performForcedRecalibration(uint16_t co2Level, int16_t* correction){
	const uint16_t arguments[] = { co2Level };

	// コマンドと1つの引数を送信
	if(!sendCommandWithArguments(CMD_PERFORM_RECALIBRATION, arguments, 1))
		return false; //I2C通信の失敗
	
	DELAY_milliseconds(90); // コマンド実行時間（90ms）待機

	// 応答を読み取る
	uint8_t buffer[3];
	if (!I2C_Read(ADDRESS, buffer, 3)) {
		return false; // I2C通信失敗
	}

	// CRCチェック
	if (CRC_calc8(buffer, 2) != buffer[2]) {
		return false; // CRCエラー
	}

	uint16_t response = (buffer[0] << 8) | buffer[1];
 
	// FRC失敗を示す
	if (response == 0xFFFF) *correction = 0xFFFF;
	// 成功時は補正値を計算 (Output - 32768) [cite: 379]
	else *correction = (int16_t)(response - 32768);

	return true; // 通信成功
}

bool STCC4_performFactoryReset(){
	if (!sendCommand(CMD_FACT_RESET))
		return false; // 通信失敗

	DELAY_milliseconds(90); //実行に90ms必要
	
	uint8_t buffer[3]; // データ(2B) + CRC(1B)
	if (!I2C_Read(ADDRESS, buffer, 3)) return false; // I2C受信失敗
	if (CRC_calc8(buffer, 2) != buffer[2]) return false; // CRCエラー

	// 処理成功時には0x0000が応答される
	uint16_t response = (buffer[0] << 8) | buffer[1];	
	if (response != 0x0000) return false; 

	return true;
}

bool STCC4_enterSleep(){
	if (!sendCommand(CMD_ENTER_SLEEP)) 
		return false; // 通信失敗

	DELAY_milliseconds(1); //待機

	return true; // 成功
}

bool STCC4_exitSleep(){
	const uint8_t command = CMD_EXIT_SLEEP;
	if (!I2C_WriteByteAndStop(ADDRESS, command)) 
		return false;

	DELAY_milliseconds(5); //待機

	return true; // 成功
}

bool STCC4_startContinuousMeasurement(){
	return sendCommand(CMD_START_CONT_MEASUREMENT);
}
		
bool STCC4_stopContinuousMeasurement(){
	return sendCommand(CMD_STOP_CONT_MEASUREMENT);	
	//注意：終了まで1200ms必要
}

bool STCC4_measureSingleShot(){
	if (!sendCommand(CMD_MES_SINGLE_SHOT)) 
		return false; // 通信失敗

	//注意：計測終了まで500ms必要
	return true; // 成功
}

bool STCC4_readMeasurement(uint16_t * co2, float * temperature, float * humidity){
	if (!sendCommand(CMD_READ_MEASUREMENT))
		return false; // 通信失敗
	
	DELAY_milliseconds(2); //待機
	
	uint8_t buffer[12];
	if (!I2C_Read(ADDRESS, buffer, 12)) 
		return false; // 通信失敗
	
	// データを分離
	uint8_t* co2Buff = &buffer[0]; // CO2データ (3バイト)
	uint8_t* dbtBuff = &buffer[3]; // 温度データ (3バイト)
	uint8_t* hmdBuff = &buffer[6]; // 湿度データ (3バイト)
	
	// CO2のCRCチェックと変換
	if (CRC_calc8(co2Buff, 2) == co2Buff[2]) 
		*co2 = (co2Buff[0] << 8) | co2Buff[1];
	else return false; // CRCエラー
	
	// 温度のCRCチェックと変換
	if (CRC_calc8(dbtBuff, 2) == dbtBuff[2]) {
		uint16_t raw_t = (dbtBuff[0] << 8) | dbtBuff[1];
		*temperature = -45.0f + 175.0f * (float)raw_t / 65535.0f;
	}
	else return false; // CRCエラー
	
	// 湿度のCRCチェックと変換
	if (CRC_calc8(hmdBuff, 2) == hmdBuff[2]) {
		uint16_t raw_h = (hmdBuff[0] << 8) | hmdBuff[1];
		float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
		// 物理的な範囲内に値を収める
		if(rh < 0.0f) rh = 0.0f;
		if(rh > 100.0f) rh = 100.0f;
		*humidity = rh;
	} else return false; // CRCエラー
	
	return true; // 成功
}

bool STCC4_setRHTCompensation(float temperature, float humidity){
	// float値を16bit整数に変換
	// Temperature: Input = (T[°C] + 45) * (2^16 - 1) / 175
	uint16_t temp_arg = (uint16_t)((temperature + 45.0f) * 65535.0f / 175.0f);
	
	// Humidity: Input = (RH[%RH] + 6) * (2^16 - 1) / 125
	uint16_t humi_arg = (uint16_t)((humidity + 6.0f) * 65535.0f / 125.0f);

	const uint16_t arguments[] = { temp_arg, humi_arg };

	// コマンドと2つの引数を送信
	return sendCommandWithArguments(CMD_SET_RHT_COMPENSATION, arguments, 2);
}

