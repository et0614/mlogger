/**
 * @file my_i2c.h
 * @brief AVRxxシリーズでI2C通信する
 *  参考：http://cjtsx.blogspot.jp/2016/07/am2320-library-for-avrs-without.html
 * @author E.Togashi
 * @date 2020/12/25
 */
 
#ifndef MY_I2C_H_
#define MY_I2C_H_
 
#include <avr/io.h>

class my_i2c
{	
	public:
		static void InitializeI2C(void);

		/**
		 * @fn
		 * AHT20を初期化する
		 */
		static void InitializeAHT20();
				
		/**
		 * @fn
		 * AHT20から乾球温度と相対湿度を読み取る
		 * @param (tempValue) 乾球温度
		 * @param (humiValue) 相対湿度
		 * @return 読取成功で1、失敗で0
		 */
		static uint8_t ReadAHT20(float * tempValue, float * humiValue);
		
		/**
		 * @fn
		 * AHT20をリセットする
		 * @param (code) リセットするコード（0x1B or 0x1C or 0x1E）
		 */
		static void ResetAHT20(uint8_t code);
		
		/**
		 * @fn
		 * AHT20の状態を読み取る
		 * @return 状態を表すバイト
		 */
		static uint8_t ReadAHT20Status();
		
		/**
		 * @fn
		 * VCNL4030を初期化する
		 */
		static void InitializeVCNL4030();
		
		/**
		 * @fn
		 * VCNL4030から照度を読み取る
		 * @return 照度[lx]
		 */
		static float ReadVCNL4030_ALS(void);
		
		/**
		 * @fn
		 * VCNL4030から距離を読み取る
		 * @return 距離[mm]
		 */
		static float ReadVCNL4030_PS(void);
		
		static void ScanAddress(uint8_t minAddress, uint8_t maxAddress);
		
		/**
		 * @fn
		 * P3T1750DPを初期化する
		 */
		static void InitializeP3T1750DP();
		
		/**
		 * @fn
		 * P3T1750DPから温度を読み取る
		 * @return 温度[C]
		 */
		static float ReadP3T1750DP(void);
};
  
#endif /* MY_I2C_H_ */