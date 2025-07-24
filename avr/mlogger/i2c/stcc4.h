/*
 * Stcc4.h
 *
 * Created: 2025/06/16 17:05:49
 *  Author: e.togashi
 */ 


#ifndef STCC4_H_
#define STCC4_H_

#include "I2cDriver.h"

class Stcc4{
	public:
		/**
		 * @brief センサーがバス上に存在するかを確認する
		 * @return センサーが応答すればtrue
		 */
		static bool isConnected();
	
		/**
		 * @fn
		 * 初期化する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool initialize();
		
		/**
		 * @fn
		 * 3時間以上の不使用時などの初期化処理（22秒かかる）
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool performConditioning();
		
		/**
		 * @fn
		 * 強制校正処理
		 * @param correction 補正した濃度[ppm]
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool performForcedRecalibration(uint16_t co2Level, int16_t* correction);
		
		/**
		 * @fn
		 * スリープさせる
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool enterSleep();
		
		/**
		 * @fn
		 * スリープ解除する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool exitSleep();
		
		/**
		 * @fn
		 * 1回測定する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool measureSingleShot();
		
		/**
		 * @fn
		 * 計測結果を読む
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool readMeasurement(uint16_t * co2, float * temperature, float * humidity);
		
		/**
		 * @fn
		 * 調整用温湿度を設定する
		 * @return 成功でtrue、失敗でfalse
		 */
		static bool setRHTCompensation(float temperature, float humidity);
		
	private:
		/**
		 * @brief 16bitのコマンドをセンサーに送信する
		 * @param command 送信する16bitコマンド
		 * @return 成功した場合はtrue
		 */
		static bool _sendCommand(uint16_t command);
		
		/**
		 * @brief 16bitコマンドと複数の引数データを送信する
		 * @brief 各引数(16bit)の後には自動でCRC8が付与される
		 * @param command 送信する16bitコマンド
		 * @param args 送信する16bit引数の配列
		 * @param numArgs 引数の数
		 * @return 成功した場合はtrue
		 */
		static bool _sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs);
};


#endif /* STCC4_H_ */