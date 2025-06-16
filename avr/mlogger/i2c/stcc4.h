/*
 * stcc4.h
 *
 * Created: 2025/06/16 17:05:49
 *  Author: etoga
 */ 


#ifndef STCC4_H_
#define STCC4_H_

#include "i2c_driver.h"

class stcc4{
	public:
		/**
		 * @fn
		 * 初期化する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool Initialize();
		
		/**
		 * @fn
		 * スリープさせる
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool EnterSleep();
		
		/**
		 * @fn
		 * スリープ解除する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool ExitSleep();
		
		/**
		 * @fn
		 * 1回測定する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool MeasureSingleShot();
		
		/**
		 * @fn
		 * 計測結果を読む
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool ReadMeasurement(uint16_t * co2, float * temperature, float * humidity);
		
	private:
		/**
		 * @brief 16bitのコマンドをセンサーに送信する
		 * @param command 送信する16bitコマンド
		 * @return 成功した場合はtrue
		 */
		static bool sendCommand(uint16_t command);
};


#endif /* STCC4_H_ */