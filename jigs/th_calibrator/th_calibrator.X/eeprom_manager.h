/* 
 * File:   EepromManager.h
 * Author: e.togashi
 *
 * Created on December 13, 2025, 10:51 AM
 */

#ifndef EEPROMMANAGER_H
#define	EEPROMMANAGER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
    
// 補正係数
typedef struct {
	uint16_t version; //バージョン
	float dbtA; //乾球温度a
	float dbtB; //乾球温度b
	float hmdA; //相対湿度a
	float hmdB; //相対湿度b
	float glbA; //グローブ温度a
	float glbB; //グローブ温度b
	float luxA; //照度a
	float luxB; //照度b
	float velA; //風速a
	float velB; //風速b
	uint8_t crc; //CRC
}CorrectionFactors;

// 風速特性係数
typedef struct{
	uint16_t version; //バージョン
    float vol0;
	float ccA1;
	float ccB1;
	float ccA2;
    float ccB2;
    float vel_swt;
	uint8_t crc; //CRC
}VelocityCharacteristicCoefficients;

//計測設定
typedef struct{
	uint16_t version; //バージョン
	bool start_auto; //自動測定開始
	bool measure_th; //乾球温度の計測真偽
	bool measure_glb; //グローブ温度の計測真偽
	bool measure_vel; //風速の計測真偽
	bool measure_ill; //照度の計測真偽
	bool measure_AD1; //汎用AD1の計測真偽
	bool measure_AD2; //汎用AD2の計測真偽
	bool measure_AD3; //汎用AD3の計測真偽
	bool measure_Prox; //近接センサの計測真偽
	bool measure_co2; //CO2の計測真偽
	unsigned int interval_th; //乾球温度の計測間隔[sec]
	unsigned int interval_glb; //グローブ温度の計測間隔[sec]
	unsigned int interval_vel; //風速の計測間隔[sec]
	unsigned int interval_ill; //照度の計測間隔[sec]
	unsigned int interval_AD1; //汎用AD1の計測間隔[sec]
	unsigned int interval_AD2; //汎用AD2の計測間隔[sec]
	unsigned int interval_AD3; //汎用AD3の計測間隔[sec]
	unsigned int interval_Prox; //近接センサの計測間隔[sec]
	unsigned int interval_co2; //CO2の計測間隔[sec]
	uint32_t start_dt;	//計測開始日時
	uint8_t crc; //CRC
}MeasurementSettings;

extern uint8_t EM_generationNumber;

//補正係数
extern CorrectionFactors EM_cFactors;

//風速特性係数
extern VelocityCharacteristicCoefficients EM_vcCoefficients;

//計測設定
extern MeasurementSettings EM_mSettings;

//名称
extern char EM_mlName[21];

//EEPROMを読み込む
void EM_loadEEPROM();

//計測設定を保存する
void EM_saveMeasurementSetting();

//名称を書き込む
void EM_saveName();

//補正係数を保存する (v4 set_correction 用)
void EM_saveCorrectionFactor();

//データ世代番号を書き込む
void EM_saveGenerationNumber();

//XBeeが初期化済か否かを取得する
bool EM_isXBeeInitialized();

//XBee初期化を記録する
void EM_xbeeInitialized();


#ifdef	__cplusplus
}
#endif

#endif	/* EEPROMMANAGER_H */

