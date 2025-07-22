/*
 * Sht4x.h
 *
 * Created: 2025/06/16 9:53:19
 *  Author: e.togashi
 */ 


#ifndef SHT4X_H_
#define SHT4X_H_

#include <stdint.h>

class Sht4x{
	public:
	
		/**
		 * @brief SHT4Xの種類
		 */
		enum SHT4XType: uint8_t
		{
			SHT4_AD = 0x44,
			SHT4_BD = 0x45,
			SHT4_CD = 0x46
		};
		
		/**
		 * @brief センサーがバス上に存在するかを確認する
		 * @param (sht4xType) SHT4Xの種類
		 * @return センサーが応答すればtrue
		 */
		static bool isConnected(SHT4XType sht4xType);
		
		/**
		 * @fn
		 * SHT4X-ADを初期化する
		 * @param (sht4xType) SHT4Xの種類
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool initialize(SHT4XType sht4xType);
				
		/**
		 * @fn
		 * SHT4X-ADから乾球温度と相対湿度を読み取る
		 * @param (tempValue) 乾球温度
		 * @param (humiValue) 相対湿度
		 * @param (sht4xType) SHT4Xの種類
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool readValue(float * tempValue, float * humiValue, SHT4XType sht4xType);
		
		/**
		* @fn
		* SHT4Xからシリアル番号を読み取る
		* @param (serialNumber) 32ビットのシリアル番号を格納するポインタ
		* @param (sht4xType) SHT4Xの種類
		* @return 読取成功でtrue、失敗でfalse
		*/
		static bool readSerial(uint32_t* serialNumber, SHT4XType sht4xType);
	};


#endif /* SHT4X_H_ */