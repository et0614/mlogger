/*
 * Stcc4.cpp
 *
 * Created: 2025/06/16 17:05:59
 *  Author: e.togashi
 */ 

#include "Stcc4.h"
#include "../Utilities.h"
#include <util/delay.h>

//アドレス
const uint8_t ADDRESS = 0x64; //(ADDR==GNDの設定)

//コマンド
const uint8_t CMD_SOFT_RESET = 0x06;   // ソフトリセット
const uint16_t CMD_ENTER_SLEEP= 0x3650;   // スリープ
const uint8_t CMD_EXIT_SLEEP= 0x00;   // スリープ解除
const uint16_t CMD_MES_SINGLE_SHOT = 0x219D;   //Single shot測定
const uint16_t CMD_READ_MEASUREMENT = 0xEC05;   //計測結果読み取り
const uint16_t CMD_SET_RHT_COMPENSATION = 0xE000;   //補償用温湿度設定

bool Stcc4::_sendCommand(uint16_t command) {
	const uint8_t command_bytes[] = {
		(uint8_t)(command >> 8),   // 上位バイト
		(uint8_t)(command & 0xFF)  // 下位バイト
	};
	return I2cDriver::write(ADDRESS, command_bytes, sizeof(command_bytes));
}

bool Stcc4::_sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs)
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
		buffer[base_index + 2] = Utilities::crc8(&buffer[base_index], 2);
	}
	
	// 組み立てたパケット全体を送信
	return I2cDriver::write(ADDRESS, buffer, buffer_size);
}

bool Stcc4::isConnected() {
	return I2cDriver::isConnected(ADDRESS);
}

bool Stcc4::initialize(){
	const uint8_t command = CMD_SOFT_RESET;
	I2cDriver::write(0x00, &command, 1); //初期化はジェネラルコール
	_delay_ms(10); //待機

	return true; // 成功
}

bool Stcc4::enterSleep(){
	if (!_sendCommand(CMD_ENTER_SLEEP)) 
		return false; // 通信失敗

	_delay_ms(1); //待機

	return true; // 成功
}

bool Stcc4::exitSleep(){
	const uint8_t command = CMD_EXIT_SLEEP;
	if (!I2cDriver::writeByteAndStop(ADDRESS, command)) 
		return false;

	_delay_ms(5); //待機

	return true; // 成功
}

bool Stcc4::measureSingleShot(){
	if (!_sendCommand(CMD_MES_SINGLE_SHOT)) 
		return false; // 通信失敗

	//計測終了まで500ms必要
	return true; // 成功
}

bool Stcc4::readMeasurement(uint16_t * co2, float * temperature, float * humidity){
	if (!_sendCommand(CMD_READ_MEASUREMENT))
		return false; // 通信失敗
	
	_delay_ms(2); //待機
	
	uint8_t buffer[12];
	if (!I2cDriver::read(ADDRESS, buffer, 12)) 
		return false; // 通信失敗
	
	// データを分離
	uint8_t* co2Buff = &buffer[0]; // CO2データ (3バイト)
	uint8_t* dbtBuff = &buffer[3]; // 温度データ (3バイト)
	uint8_t* hmdBuff = &buffer[6]; // 湿度データ (3バイト)
	
	// CO2のCRCチェックと変換
	if (Utilities::crc8(co2Buff, 2) == co2Buff[2]) 
		*co2 = (co2Buff[0] << 8) | co2Buff[1];
	else return false; // CRCエラー
	
	// 温度のCRCチェックと変換
	if (Utilities::crc8(dbtBuff, 2) == dbtBuff[2]) {
		uint16_t raw_t = (dbtBuff[0] << 8) | dbtBuff[1];
		*temperature = -45.0f + 175.0f * (float)raw_t / 65535.0f;
	}
	else return false; // CRCエラー
	
	// 湿度のCRCチェックと変換
	if (Utilities::crc8(hmdBuff, 2) == hmdBuff[2]) {
		uint16_t raw_h = (hmdBuff[0] << 8) | hmdBuff[1];
		float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
		// 物理的な範囲内に値を収める
		if(rh < 0.0f) rh = 0.0f;
		if(rh > 100.0f) rh = 100.0f;
		*humidity = rh;
	} else return false; // CRCエラー
	
	return true; // 成功
}

bool Stcc4::setRHTCompensation(float temperature, float humidity){
	// float値を16bit整数に変換
	// Temperature: Input = (T[°C] + 45) * (2^16 - 1) / 175
	uint16_t temp_arg = (uint16_t)((temperature + 45.0f) * 65535.0f / 175.0f);
	
	// Humidity: Input = (RH[%RH] + 6) * (2^16 - 1) / 125
	uint16_t humi_arg = (uint16_t)((humidity + 6.0f) * 65535.0f / 125.0f);

	const uint16_t arguments[] = { temp_arg, humi_arg };

	// コマンドと2つの引数を送信
	return _sendCommandWithArguments(CMD_SET_RHT_COMPENSATION, arguments, 2);
}