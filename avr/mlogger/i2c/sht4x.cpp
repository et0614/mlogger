/*
 * Sht4x.cpp
 *
 * Created: 2025/06/16 9:53:30
 *  Author: e.togashi
 */ 

#include "Sht4x.h"
#include "../Utilities.h"
#include "I2cDriver.h"
#include <util/delay.h>

// SHT4xのコマンド
const uint8_t CMD_MEASURE_MEDIUM = 0xF6; // 中精度での測定コマンド
const uint8_t CMD_SOFT_RESET = 0x94;   // ソフトリセット
const uint8_t CMD_READ_SERIAL = 0x89; // シリアル番号読み取り

bool Sht4x::isConnected(SHT4XType sht4xType) {
	return I2cDriver::isConnected(static_cast<uint8_t>(sht4xType));
}

bool Sht4x::initialize(SHT4XType sht4xType)
{
	const uint8_t address = static_cast<uint8_t>(sht4xType);
	const uint8_t command = CMD_SOFT_RESET;

	// ソフトリセットコマンド(0x94)を送信
	if (!I2cDriver::write(address, &command, 1)) 
		return false; // 通信失敗

	// リセット完了まで1ms待機
	_delay_ms(1);

	return true; // 成功
}

bool Sht4x::readValue(float* tempValue, float* humiValue, SHT4XType sht4xType)
{
	*humiValue = -99;
	*tempValue = -99;

	const uint8_t address = static_cast<uint8_t>(sht4xType);
	
	// 測定開始コマンドを送信 (Write APIを使用)
	const uint8_t command = CMD_MEASURE_MEDIUM;
	if (!I2cDriver::write(address, &command, 1)) {
		return false; // 通信失敗
	}

	// センサーの測定完了を待つ (データシート上は4.5ms)
	_delay_ms(10);

	// 測定結果（6バイト）を受信 (Read APIを使用)
	uint8_t buffer[6];
	if (!I2cDriver::read(address, buffer, 6)) {
		return false; // 通信失敗
	}

	// 温度と湿度のデータに分離
	uint8_t* tBuff = &buffer[0]; // 温度データ (3バイト)
	uint8_t* hBuff = &buffer[3]; // 湿度データ (3バイト)

	// 温度のCRCチェックと変換
	if (Utilities::crc8(tBuff, 2) == tBuff[2]) {
		uint16_t raw_t = (tBuff[0] << 8) | tBuff[1];
		*tempValue = -45.0f + 175.0f * (float)raw_t / 65535.0f;
	}
	else return false; // CRCエラー

	// 湿度のCRCチェックと変換
	if (Utilities::crc8(hBuff, 2) == hBuff[2]) {
		uint16_t raw_h = (hBuff[0] << 8) | hBuff[1];
		float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
		// 物理的な範囲内に値を収める
		if(rh < 0.0f) rh = 0.0f;
		if(rh > 100.0f) rh = 100.0f;
		*humiValue = rh;
	} else return false; // CRCエラー

	return true; // 成功
}

bool Sht4x::readSerial(uint32_t* serialNumber, SHT4XType sht4xType)
{
	*serialNumber = 0; // 事前に初期化
	const uint8_t address = static_cast<uint8_t>(sht4xType);
	const uint8_t command = CMD_READ_SERIAL;
	uint8_t buffer[6];

	// シリアル番号読み取りコマンド(0x89)を書き込み、続けて6バイトのデータを読み取る
	if (!I2cDriver::writeRead(address, &command, 1, buffer, 6)) 
		return false; // 通信失敗

	// 受信したデータのCRCチェック
	// データは2バイトのデータと1バイトのCRCが2セット
	uint8_t crc_word1 = Utilities::crc8(&buffer[0], 2);
	uint8_t crc_word2 = Utilities::crc8(&buffer[3], 2);
	if (crc_word1 != buffer[2] || crc_word2 != buffer[5]) 
		return false; // CRCエラー

	// バイトデータを32ビットのシリアル番号に結合
	uint16_t word1 = (buffer[0] << 8) | buffer[1];
	uint16_t word2 = (buffer[3] << 8) | buffer[4];
	*serialNumber = ((uint32_t)word1 << 16) | word2;

	return true; // 成功
}

