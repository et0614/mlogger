/*
 * stcc4.cpp
 *
 * Created: 2025/06/16 17:05:59
 *  Author: e.togashi
 */ 

#include "stcc4.h"
#include "../utilities.h"
#include <util/delay.h>

//アドレス
const uint8_t ADDRESS = 0x64; //(ADDR==GNDの設定)

//コマンド
const uint8_t CMD_SOFT_RESET = 0x06;   // ソフトリセット
const uint16_t CMD_ENTER_SLEEP= 0x3650;   // スリープ
const uint8_t CMD_EXIT_SLEEP= 0x00;   // スリープ解除
const uint16_t CMD_MES_SINGLE_SHOT = 0x219D;   //Single shot測定
const uint16_t CMD_READ_MEASUREMENT = 0xEC05;   //計測結果読み取り

bool stcc4::sendCommand(uint16_t command) {
	const uint8_t command_bytes[] = {
		(uint8_t)(command >> 8),   // 上位バイト
		(uint8_t)(command & 0xFF)  // 下位バイト
	};
	return i2c_driver::Write(ADDRESS, command_bytes, sizeof(command_bytes));
}

bool stcc4::Initialize(){
	const uint8_t command = CMD_SOFT_RESET;
	i2c_driver::Write(0x00, &command, 1); //初期化はジェネラルコール
	_delay_ms(10); //待機

	return true; // 成功
}

bool stcc4::EnterSleep(){
	if (!sendCommand(CMD_ENTER_SLEEP)) 
		return false; // 通信失敗

	_delay_ms(1); //待機

	return true; // 成功
}

bool stcc4::ExitSleep(){
	const uint8_t command = CMD_EXIT_SLEEP;
	if (!i2c_driver::WriteByteAndStop(ADDRESS, command)) 
		return false;

	_delay_ms(5); //待機

	return true; // 成功
}

bool stcc4::MeasureSingleShot(){
	if (!sendCommand(CMD_MES_SINGLE_SHOT)) 
		return false; // 通信失敗

	//計測終了まで500ms必要
	return true; // 成功
}

bool stcc4::ReadMeasurement(uint16_t * co2, float * temperature, float * humidity){
	if (!sendCommand(CMD_READ_MEASUREMENT))
		return false; // 通信失敗
	
	_delay_ms(2); //待機
	
	uint8_t buffer[12];
	if (!i2c_driver::Read(ADDRESS, buffer, 12)) 
		return false; // 通信失敗
	
	// データを分離
	uint8_t* co2Buff = &buffer[0]; // CO2データ (3バイト)
	uint8_t* dbtBuff = &buffer[3]; // 温度データ (3バイト)
	uint8_t* hmdBuff = &buffer[6]; // 湿度データ (3バイト)
	
	// CO2のCRCチェックと変換
	if (utilities::crc8(co2Buff, 2) == co2Buff[2]) 
		*co2 = (co2Buff[0] << 8) | co2Buff[1];
	else return false; // CRCエラー
	
	// 温度のCRCチェックと変換
	if (utilities::crc8(dbtBuff, 2) == dbtBuff[2]) {
		uint16_t raw_t = (dbtBuff[0] << 8) | dbtBuff[1];
		*temperature = -45.0f + 175.0f * (float)raw_t / 65535.0f;
	}
	else return false; // CRCエラー
	
	// 湿度のCRCチェックと変換
	if (utilities::crc8(hmdBuff, 2) == hmdBuff[2]) {
		uint16_t raw_h = (hmdBuff[0] << 8) | hmdBuff[1];
		float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
		// 物理的な範囲内に値を収める
		if(rh < 0.0f) rh = 0.0f;
		if(rh > 100.0f) rh = 100.0f;
		*humidity = rh;
	} else return false; // CRCエラー
	
	return true; // 成功
}