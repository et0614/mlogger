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
		
		static void InitializeOPT(uint8_t add);
		
		/**
		 * @fn
		 * AM2320から乾球温度と相対湿度を読み取る
		 * @param (tempValue) 乾球温度
		 * @param (humiValue) 相対湿度
		 * @return 読取成功で1、失敗で0
		 */
		static uint8_t ReadAM2320(float * tempValue, float * humiValue);
		
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
		 * OPTxxxから照度を読み取る
		 * @param (add) アドレス
		 * @return 照度[lx]
		 */
		static float ReadOPT(uint8_t add);
		
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
};
  
#endif /* MY_I2C_H_ */