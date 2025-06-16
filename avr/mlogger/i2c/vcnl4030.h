/*
 * vcnl4030.h
 *
 * Created: 2025/06/16 12:31:34
 *  Author: etoga
 */ 


#ifndef VCNL4030_H_
#define VCNL4030_H_

#include "i2c_driver.h"

class vcnl4030{
	public:
		/**
		 * @brief センサーがバス上に存在するかを確認する
		 * @return センサーが応答すればtrue
		 */
		static bool IsConnected();
		
		/**
		 * @fn
		 * VCNL4030を初期化する
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool Initialize();
		
		/**
		 * @fn
		 * VCNL4030から照度(Ambient Light Sensor)を読み取る
		 * @param (als) 照度[lx]
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool ReadALS(float * als);
		
		/**
		 * @fn
		 * VCNL4030から距離[mm]を読み取る
		 * @param (ps) 距離[mm]
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool ReadPS(float * ps);		
};


#endif /* VCNL4030_H_ */