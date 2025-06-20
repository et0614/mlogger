/*
 * Sht4x.h
 *
 * Created: 2025/06/16 9:53:19
 *  Author: e.togashi
 */ 


#ifndef SHT4X_H_
#define SHT4X_H_

#include <stdint.h>
#include "Sht4x.h"

class Sht4x{
	public:
		/**
		 * @brief センサーがバス上に存在するかを確認する
		 * @param (isAD) SHT4X-ADか否か（否の場合にはSHT4X-BD）
		 * @return センサーが応答すればtrue
		 */
		static bool isConnected(bool isAD);
		
		/**
		 * @fn
		 * SHT4X-ADを初期化する
		 * @param (isAD) SHT4X-ADか否か（否の場合にはSHT4X-BD）
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool initialize(bool isAD);
				
		/**
		 * @fn
		 * SHT4X-ADから乾球温度と相対湿度を読み取る
		 * @param (tempValue) 乾球温度
		 * @param (humiValue) 相対湿度
		 * @param (isAD) SHT4X-ADか否か（否の場合にはSHT4X-BD）
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool readValue(float * tempValue, float * humiValue, bool isAD);
		
		/**
		* @fn
		* SHT4Xからシリアル番号を読み取る
		* @param (serialNumber) 32ビットのシリアル番号を格納するポインタ
		* @param (isAD) SHT4X-ADか否か（否の場合にはSHT4X-BD）
		* @return 読取成功でtrue、失敗でfalse
		*/
		static bool readSerial(uint32_t* serialNumber, bool isAD);
	
	};


#endif /* SHT4X_H_ */