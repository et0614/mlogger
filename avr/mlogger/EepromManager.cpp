/**
 * @file EepromManager.h
 * @brief AVR(AVRxxDB32)のEEPROMを処理する
 * @author E.Togashi
 * @date 2021/12/19
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <avr/eeprom.h>

#include "parameters.h"
#include "EepromManager.h"
#include "Utilities.h"

//EEPROMの初期化フラグ。コンパイル後最初の呼び出しのみ初期化する
static uint8_t EEMEM EEP_INITFLAG;

//XBEEの初期化フラグ
static uint8_t EEMEM EEP_XB_INITFLAG;

//補正係数
static CorrectionFactors EEMEM EEP_CFACTORS;

//風速特性係数
static VelocityCharacteristicCoefficients EEMEM EEP_VCCOEFS;

//計測設定
static MeasurementSettings EEMEM EEP_MSETTINGS;

//ロガー名称
static char EEMEM EEP_NAME[21];

//補正係数
CorrectionFactors EepromManager::cFactors;

//風速特性係数
VelocityCharacteristicCoefficients EepromManager::vcCoefficients;

//計測設定
MeasurementSettings EepromManager::mSettings;

//ロガー名称
char EepromManager::mlName[21];

void initCFactors(){
	EepromManager::cFactors = {
		1, //バージョン
		DBT_COEF_A, //乾球温度a
		DBT_COEF_B, //乾球温度b
		HMD_COEF_A, //相対湿度a
		HMD_COEF_B, //相対湿度b
		GLB_COEF_A, //グローブ温度a
		GLB_COEF_B, //グローブ温度b
		1.0, //照度a
		0.0, //照度b
		1.0, //風速a
		0.0, //風速b
		VEL_VEL0, //無風時
		0 //CRC（一旦0で初期化）
	};
}

void initVCCoefficients(){
	EepromManager::vcCoefficients = {
		1,		//バージョン
		VEL_COEF_A,		//係数A
		VEL_COEF_B, //係数B
		0,	//係数C
		0		//CRC（一旦0で初期化）
	};
}

void initMSettings(){
	EepromManager::mSettings = {
		1,		//バージョン
		false, //自動測定開始
		true, //乾球温度の計測真偽
		true, //グローブ温度の計測真偽
		true, //風速の計測真偽
		true, //照度の計測真偽
		false, //汎用AD1の計測真偽
		false, //汎用AD2の計測真偽
		false, //汎用AD3の計測真偽
		false, //近接センサの計測真偽
		false, //CO2の計測真偽
		1, //乾球温度の計測間隔[sec]
		1, //グローブ温度の計測間隔[sec]
		1, //風速の計測間隔[sec]
		1, //照度の計測間隔[sec]
		1, //汎用AD1の計測間隔[sec]
		1, //汎用AD2の計測間隔[sec]
		1, //汎用AD3の計測間隔[sec]
		1, //近接センサの計測間隔[sec]
		1, //CO2の計測間隔[sec]
		1609459200,	//計測開始日時 (UNIX時間,UTC時差0で2021/1/1 00:00:00)
		0		//CRC（一旦0で初期化）
	};
}

//補正係数を書き込む
void writeCFactors()
{
	// CRCを計算
	EepromManager::cFactors.crc = Utilities::crc8(
	(uint8_t*)&EepromManager::cFactors,
	sizeof(CorrectionFactors) - sizeof(EepromManager::cFactors.crc) //crcメンバー自身のサイズは計算範囲から除外する
	);
	
	eeprom_busy_wait();
	eeprom_update_block(&EepromManager::cFactors, &EEP_CFACTORS, sizeof(CorrectionFactors));
}

//風速特性係数を書き込む
void writeVCCoefficients()
{
	// CRCを計算
	EepromManager::vcCoefficients.crc = Utilities::crc8(
	(uint8_t*)&EepromManager::vcCoefficients,
	sizeof(VelocityCharacteristicCoefficients) - sizeof(EepromManager::vcCoefficients.crc) //crcメンバー自身のサイズは計算範囲から除外する
	);
	
	eeprom_busy_wait();
	eeprom_update_block(&EepromManager::vcCoefficients, &EEP_VCCOEFS, sizeof(VelocityCharacteristicCoefficients));
}

//計測設定を書き込む
void writeMSettings()
{
	// CRCを計算
	EepromManager::mSettings.crc = Utilities::crc8(
	(uint8_t*)&EepromManager::mSettings,
	sizeof(MeasurementSettings) - sizeof(EepromManager::mSettings.crc) //crcメンバー自身のサイズは計算範囲から除外する
	);
	
	eeprom_busy_wait();
	eeprom_update_block(&EepromManager::mSettings, &EEP_MSETTINGS, sizeof(MeasurementSettings));
}

//メモリを初期化する
void initMemory()
{
	//補正係数
	initCFactors();
	writeCFactors();

	//風速計特性係数
	initVCCoefficients();
	writeVCCoefficients();
	
	//計測設定
	initMSettings();
	writeMSettings();
	
	//名前
	eeprom_busy_wait();
	eeprom_update_block((const void *)ML_NAME, (void *)EEP_NAME, sizeof(EepromManager::mlName));
	
	//XBee初期化フラグ
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_XB_INITFLAG,'F');
	
	//初期化フラグ
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_INITFLAG,'T');
}

//補正係数を設定する
void EepromManager::setCorrectionFactor(const char data[])
{
	float buff;
	char num[5];
	num[4] = '\0';
	
	//乾球温度補正係数A
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EepromManager::cFactors.dbtA = buff;
	//乾球温度補正係数B
	strncpy(num, data + 7, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		EepromManager::cFactors.dbtB = buff;
	
	//相対湿度補正係数A
	strncpy(num, data + 11, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EepromManager::cFactors.hmdA = buff;
	//相対湿度補正係数B
	strncpy(num, data + 15, 4);
	buff = 0.01 * atol(num);
	if(-9.99 <= buff && buff <= 9.99)
		EepromManager::cFactors.hmdB = buff;
	
	//グローブ温度補正係数A
	strncpy(num, data + 19, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EepromManager::cFactors.glbA = buff;
	//グローブ温度補正係数B
	strncpy(num, data + 23, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		EepromManager::cFactors.glbB = buff;
	
	//照度補正係数A
	strncpy(num, data + 27, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EepromManager::cFactors.luxA = buff;
	//照度補正係数B
	strncpy(num, data + 31, 4);
	buff = atol(num);
	if(-999 <= buff && buff <= 999)
		EepromManager::cFactors.luxB = buff;
	
	//風速補正係数A
	strncpy(num, data + 35, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EepromManager::cFactors.velA = buff;
	//風速補正係数B
	strncpy(num, data + 39, 4);
	buff = 0.001 * atol(num);
	if(-0.5 <= buff && buff <= 0.5)
		EepromManager::cFactors.velB = buff;
	//風速無風電圧
	strncpy(num, data + 43, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.50)
		EepromManager::cFactors.vel0 = buff;
	
	//EEPROMに書き込む
	writeCFactors();
}

//補正係数を表す文字列を作成する
void EepromManager::makeCorrectionFactorString(char * txbuff, const char * command)
{
	char dbtA[6],dbtB[6],hmdA[6],hmdB[6],glbA[6],glbB[6],luxA[6],luxB[5],velA[6],velB[7],vel0[6];
	
	dtostrf(EepromManager::cFactors.dbtA,5,3,dbtA);
	dtostrf(EepromManager::cFactors.dbtB,5,2,dbtB);
	
	dtostrf(EepromManager::cFactors.hmdA,5,3,hmdA);
	dtostrf(EepromManager::cFactors.hmdB,5,2,hmdB);
	
	dtostrf(EepromManager::cFactors.glbA,5,3,glbA);
	dtostrf(EepromManager::cFactors.glbB,5,2,glbB);
	
	dtostrf(EepromManager::cFactors.luxA,5,3,luxA);
	dtostrf(EepromManager::cFactors.luxB,4,0,luxB);
	
	dtostrf(EepromManager::cFactors.velA,5,3,velA);
	dtostrf(EepromManager::cFactors.velB,6,3,velB);
	dtostrf(EepromManager::cFactors.vel0,5,3,vel0);
	
	sprintf(txbuff, "%s:%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\r",
		command, dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, vel0);
}

//風速の特性係数を設定する
void EepromManager::setVelocityCharacteristics(const char data[])
{
	float buff;
	char num[8];
	
	//無風電圧
	num[4] = '\0';
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.60)
	EepromManager::cFactors.vel0 = buff;
	
	//特性A
	num[7] = '\0';
	strncpy(num, data + 7, 7);
	buff = 0.001 * atol(num);
	EepromManager::vcCoefficients.ccA = buff;
	
	//特性B
	strncpy(num, data + 14, 7);
	buff = 0.001 * atol(num);
	EepromManager::vcCoefficients.ccB = buff;
	
	//特性C
	strncpy(num, data + 21, 7);
	buff = 0.001 * atol(num);
	EepromManager::vcCoefficients.ccC = buff;

	writeVCCoefficients();
}

//風速の特性係数を表す文字列を作成する
void EepromManager::makeVelocityCharateristicsString(char * txbuff, const char * command)
{
	char vel0[6],vccA[9],vccB[9],vccC[9];
	
	dtostrf(EepromManager::cFactors.vel0,5,3,vel0);
	dtostrf(EepromManager::vcCoefficients.ccA,8,3,vccA);
	dtostrf(EepromManager::vcCoefficients.ccB,8,3,vccB);
	dtostrf(EepromManager::vcCoefficients.ccC,8,3,vccC);
	
	sprintf(txbuff, "%s:%s,%s,%s,%s\r",
	command, vel0, vccA, vccB, vccC);
}

//計測設定を設定する
void EepromManager::setMeasurementSetting()
{
	writeMSettings();	
}

//名称を書き込む
void EepromManager::saveName()
{
	eeprom_busy_wait();	
	eeprom_update_block((const void *)mlName, (void *)EEP_NAME, sizeof(EepromManager::mlName));
}

//補正係数を読み込む
void LoadCorrectionFactor()
{
	eeprom_busy_wait();	
	eeprom_read_block(&EepromManager::cFactors, &EEP_CFACTORS, sizeof(CorrectionFactors));

	// 読み込んだデータのCRCを検証
	uint8_t expected_crc = EepromManager::cFactors.crc;
	uint8_t actual_crc = Utilities::crc8(
		(uint8_t*)&EepromManager::cFactors,
		sizeof(CorrectionFactors) - sizeof(EepromManager::cFactors.crc)
	);

	// CRCが一致しない（データ破損）場合にはデフォルト値で再初期化
	if (expected_crc != actual_crc) initCFactors();
}

//風速の特性係数を読み込む
void LoadVelocityCharateristics()
{
	eeprom_busy_wait();
	eeprom_read_block(&EepromManager::vcCoefficients, &EEP_VCCOEFS, sizeof(VelocityCharacteristicCoefficients));

	// 読み込んだデータのCRCを検証
	uint8_t expected_crc = EepromManager::vcCoefficients.crc;
	uint8_t actual_crc = Utilities::crc8(
		(uint8_t*)&EepromManager::vcCoefficients,
		sizeof(VelocityCharacteristicCoefficients) - sizeof(EepromManager::vcCoefficients.crc)
	);

	// CRCが一致しない（データ破損）場合にはデフォルト値で再初期化
	if (expected_crc != actual_crc) initVCCoefficients();
}

//計測設定を読み込む
void LoadMeasurementSetting()
{
	eeprom_busy_wait();
	eeprom_read_block(&EepromManager::mSettings, &EEP_MSETTINGS, sizeof(MeasurementSettings));

	// 読み込んだデータのCRCを検証
	uint8_t expected_crc = EepromManager::mSettings.crc;
	uint8_t actual_crc = Utilities::crc8(
		(uint8_t*)&EepromManager::mSettings,
		sizeof(MeasurementSettings) - sizeof(EepromManager::mSettings.crc)
	);

	// CRCが一致しない（データ破損）場合にはデフォルト値で再初期化
	if (expected_crc != actual_crc) initMSettings();
}

//名称を読み込む
void LoadName()
{
	eeprom_busy_wait();
	eeprom_read_block((void *)EepromManager::mlName, (const void *)EEP_NAME, sizeof(EepromManager::mlName));
}

//設定を読み込む
void EepromManager::loadEEPROM()
{
	//強制初期化処理
	//eeprom_busy_wait();
	//eeprom_update_byte(&EEP_INITFLAG, 'F');
	
	//初期化未了の場合は補正係数を初期化する
	//https://scienceprog.com/tip-on-storing-initial-values-in-eeprom-of-avr-microcontroller/
	//この方法だと、たまたまEEPROMの「EEP_INITFLAG」に「T」が設定されていた場合に処理が破綻するが良いのだろうか・・・
	eeprom_busy_wait(); //EEPROM読み書き可能まで待機
	if (eeprom_read_byte(&EEP_INITFLAG) != 'T') initMemory();
	
	LoadCorrectionFactor();
	LoadVelocityCharateristics();
	LoadMeasurementSetting();
	LoadName();
}

//XBeeが初期化済か否かを取得する
bool EepromManager::isXBeeInitialized(){
	eeprom_busy_wait(); //EEPROM読み書き可能まで待機
	return eeprom_read_byte(&EEP_XB_INITFLAG) == 'T';
}

//XBee初期化を記録する
void EepromManager::xbeeInitialized(){
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_XB_INITFLAG,'T');	
}