/**
 * @file my_uart.h
 * @brief AVR(ATMega328)でUART通信を行う
 * @author E.Togashi
 * @date 2020/8/4
 */

#include <stdio.h>

#ifndef MY_UART_H_
#define MY_UART_H_

class my_uart
{
	public:
		static void Initialize(void);
				
		/**
		* @fn
		* UART送信が終了したか否か
		* @param 無し
		* @return 終了していればtrue
		*/
		static bool tx_done(void);

		/**
		* @fn
		* UART通信で文字を送信する
		* @param (data) 文字
		* @return 無し
		*/
		static void send_char(const unsigned char data);

		/**
		* @fn
		* UART通信で文字列を送信する
		* @param (data) 文字列
		* @return 無し
		*/
		static void send_chars(const unsigned char data[]);
};

#endif /* MY_UART_H_ */