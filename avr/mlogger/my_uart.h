/**
 * @file my_uart.h
 * @brief AVR(ATMega328)��UART�ʐM���s��
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
		* UART���M���I���������ۂ�
		* @param ����
		* @return �I�����Ă����true
		*/
		static bool tx_done(void);

		/**
		* @fn
		* UART�ʐM�ŕ����𑗐M����
		* @param (data) ����
		* @return ����
		*/
		static void send_char(const unsigned char data);

		/**
		* @fn
		* UART�ʐM�ŕ�����𑗐M����
		* @param (data) ������
		* @return ����
		*/
		static void send_chars(const unsigned char data[]);
};

#endif /* MY_UART_H_ */