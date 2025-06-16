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
		 * @brief センサーがバス上に存在するかを確認する
		 * @return センサーが応答すればtrue
		 */
		static bool IsConnected();
	
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
		
		/**
		 * @fn
		 * 調整用温湿度を設定する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool SetRHTCompensation(float temperature, float humidity);
		
	private:
		/**
		 * @brief 16bitのコマンドをセンサーに送信する
		 * @param command 送信する16bitコマンド
		 * @return 成功した場合はtrue
		 */
		static bool sendCommand(uint16_t command);
		
		/**
		 * @brief 16bitコマンドと複数の引数データを送信する
		 * @brief 各引数(16bit)の後には自動でCRC8が付与される
		 * @param command 送信する16bitコマンド
		 * @param args 送信する16bit引数の配列
		 * @param numArgs 引数の数
		 * @return 成功した場合はtrue
		 */
		static bool sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs);
};


#endif /* STCC4_H_ */