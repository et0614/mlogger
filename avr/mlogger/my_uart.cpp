/**
 * @file my_uart.cpp
 * @brief AVR(ATMega328)でUART通信を行う
 * @author E.Togashi
 * @date 2020/8/4
 */

#include "my_uart.h"

#include <stdio.h>
#include <string.h>
#include <avr/io.h>
#include <avr/interrupt.h>

//#define BAUD_CALC(x)	((F_CPU+(x)*8UL) / (16UL*(x))-1UL)
#define BAUD_CALC(BAUD_RATE)	((float)(64 * F_CPU / (16 * (float)BAUD_RATE)) + 0.5)

//初期化
void my_uart::Initialize(void)
{
	//ポートの入出力設定
	PORTA.DIRSET = PIN0_bm; //TX:書き出し
	PORTA.DIRCLR = PIN1_bm; //RX:読み込み
	//RXをPullUp
	PORTA.OUTSET = PIN1_bm; //INT0：設定
	
	//ボーレートの設定
	USART0.BAUD = (uint16_t)BAUD_CALC(9600);
	
	USART0_CTRLA |= USART_RXCIF_bm; //受信完了イベント有効化
	USART0.CTRLB |= (USART_RXEN_bm | USART_TXEN_bm); //送受信有効化
	USART0.CTRLC = 0b00000011;//00 00 0 11: Asynchronous, noparity, stopbit=1, 8bit
}

bool my_uart::tx_done(void)
{
	return (USART0.STATUS & USART_DREIF_bm);
}

//1文字送信
void my_uart::send_char(const unsigned char data)
{
	while (!tx_done());
	USART0.TXDATAL = data;
}

//文字配列を送信
void my_uart::send_chars(const unsigned char data[])
{
	for(int i = 0; data[i] != '\0'; i++)
		send_char(data[i]);
}


