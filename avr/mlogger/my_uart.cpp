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

//ビット設定関数
#define cbi(addr, bit) addr &= ~(1 << bit) // addrのbit目を'0'にする。
#define sbi(addr, bit) addr |= (1 << bit)  // addrのbit目を'1'にする。

#define BAUD_CALC(x) ((F_CPU+(x)*8UL) / (16UL*(x))-1UL)

//初期化
void my_uart::Initialize(void)
{
	//ボーレートの設定
	unsigned int baud = BAUD_CALC(9600);
	UBRR0H = (unsigned char)(baud>>8);//ボーレート上位
	UBRR0L = (unsigned char)baud; //ボーレート下位
	
	UCSR0B = _BV(RXCIE0) | _BV(RXEN0) | _BV(TXEN0); //割り込みと送受信有効化
	UCSR0C = 0b00000110; //8bit, noparity, stopbit1, 非同期
}

bool my_uart::tx_done(void)
{
	return UCSR0A & 0b00100000;
}

//1文字送信
void my_uart::send_char(const unsigned char data)
{
	while(!(UCSR0A & 0b00100000));
	UDR0 = data;
}

//文字配列を送信
void my_uart::send_chars(const unsigned char data[])
{
	for(int i = 0; data[i] != '\0'; i++)
		send_char(data[i]);
}


