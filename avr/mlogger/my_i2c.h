/**
 * @file my_i2c.h
 * @brief AVR(ATMega328)でI2C通信を行う（AM2320とOPT3001）
 *  参考：http://cjtsx.blogspot.jp/2016/07/am2320-library-for-avrs-without.html
 * @author E.Togashi
 * @date 2020/12/25
 */
 
#ifndef AM2320S_H_
#define AM2320S_H_
 
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

};
 
#endif /* AM2320S_H_ */