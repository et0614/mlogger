#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/timer/delay.h"
#include "sht4x.h"
#include "i2c_master.h"
#include "crc.h"

#define CMD_MEASURE_HIGHEST 0xFD
#define CMD_MEASURE_MEDIUM 0xF6
#define CMD_MEASURE_LOWEST 0xE0
#define CMD_SOFT_RESET     0x94
#define CMD_READ_SERIAL    0x89

bool SHT4x_IsConnected(SHT4XType type)
{
    return I2C_IsConnected((uint8_t)type);
}

bool SHT4x_Initialize(SHT4XType type)
{
    const uint8_t addr = (uint8_t)type;
    const uint8_t cmd = CMD_SOFT_RESET;
    
    if (!I2C_Write(addr, &cmd, 1)) return false;
    DELAY_milliseconds(1);
    return true;
}

bool SHT4x_ReadValue(float *tempValue, float *humiValue, SHT4XType type)
{
    *humiValue = -99;
    *tempValue = -99;
    const uint8_t addr = (uint8_t)type;
    const uint8_t cmd = CMD_MEASURE_HIGHEST;

    // 測定開始
    if (!I2C_Write(addr, &cmd, 1)) return false;
    DELAY_milliseconds(10); // 待機

    // 読み出し (TempMSB, TempLSB, CRC, HumiMSB, HumiLSB, CRC)
    uint8_t buffer[6];
    if (!I2C_Read(addr, buffer, 6)) return false;

    // CRCチェック
    if (CRC_calc8(&buffer[0], 2) != buffer[2]) return false;
    if (CRC_calc8(&buffer[3], 2) != buffer[5]) return false;

    // 温度変換
    uint16_t raw_t = ((uint16_t)buffer[0] << 8) | buffer[1];
    *tempValue = -45.0f + 175.0f * (float)raw_t / 65535.0f;

    // 湿度変換
    uint16_t raw_h = ((uint16_t)buffer[3] << 8) | buffer[4];
    float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
    if (rh < 0.0f) rh = 0.0f;
    if (rh > 100.0f) rh = 100.0f;
    *humiValue = rh;

    return true;
}

bool SHT4x_ReadSerial(uint32_t *serialNumber, SHT4XType type)
{
    *serialNumber = 0; // 事前に初期化
	const uint8_t address = (uint8_t)type;
	const uint8_t command = CMD_READ_SERIAL;
	uint8_t buffer[6];

	// シリアル番号読み取りコマンド(0x89)を書き込み、続けて6バイトのデータを読み取る
    if(!I2C_WriteRead(address, &command, 1, buffer, 6))
		return false; // 通信失敗

	// 受信したデータのCRCチェック
	// データは2バイトのデータと1バイトのCRCが2セット
	uint8_t crc_word1 = CRC_calc8(&buffer[0], 2);
	uint8_t crc_word2 = CRC_calc8(&buffer[3], 2);
	if (crc_word1 != buffer[2] || crc_word2 != buffer[5]) 
		return false; // CRCエラー

	// バイトデータを32ビットのシリアル番号に結合
	uint16_t word1 = (buffer[0] << 8) | buffer[1];
	uint16_t word2 = (buffer[3] << 8) | buffer[4];
	*serialNumber = ((uint32_t)word1 << 16) | word2;

	return true; // 成功
}