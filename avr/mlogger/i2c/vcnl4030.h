/*
 * Vcnl4030.h
 *
 * Created: 2025/06/16 12:31:34
 *  Author: e.togashi
 */ 


#ifndef VCNL4030_H_
#define VCNL4030_H_

#include "I2cDriver.h"

class Vcnl4030{
	public:
		/**
		 * @brief センサーがバス上に存在するかを確認する
		 * @return センサーが応答すればtrue
		 */
		static bool isConnected();
		
		/**
		 * @fn
		 * VCNL4030を初期化する
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool initialize();
		
		/**
		 * @fn
		 * VCNL4030から照度(Ambient Light Sensor)を読み取る
		 * @param (als) 照度[lx]
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool readALS(float * als);
		
		/**
		 * @fn
		 * VCNL4030から距離[mm]を読み取る
		 * @param (ps) 距離[mm]
		 * @return 読取成功でtrue、失敗でfalse
		 */
		static bool readPS(float * ps);		
};


#endif /* VCNL4030_H_ */