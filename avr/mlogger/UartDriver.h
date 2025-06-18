/**
 * @file UartDriver.h
 * @brief AVR(ATMega328)でUART通信を行う
 * @author E.Togashi
 * @date 2020/8/4
 */

#include <stdio.h>

#ifndef UART_DRIVER_H_
#define UART_DRIVER_H_

class UartDriver
{
	public:
		/**
		* @fn
		* UART通信のための初期化処理
		* @param 無し
		* @return 無し
		*/
		static void initialize(void);

		/**
		* @fn
		* UART通信で文字を送信する
		* @param (data) 文字
		* @return 無し
		*/
		static void sendChar(const char data);

		/**
		* @fn
		* UART通信で文字列を送信する
		* @param (data) 文字列
		* @return 無し
		*/
		static void sendChars(const char data[]);
		
		/**
		 * @fn
		 * 受信リングバッファにデータがあるか確認する
		 * @return データがあればtrue
		 */
		static bool uartRingBufferHasData(void);

		/**
		 * @fn
		 * 受信リングバッファから1バイト読み出す
		 * @return 読み出した文字
		 * @note 事前にuart_ring_buffer_has_data()でデータがあることを確認する
		 */
		static char uartRingBufferGet(void);
		
		/**
		 * @fn
		 * 送信処理が実行中か否かを確認する
		 * @brief 送信リングバッファに未送信のデータが残っている場合にtrueを返す
		 * @return 送信中であればtrue
		 */
		static bool isTransmitting(void);
	
	private:

};

#endif /* UART_DRIVER_H_ */