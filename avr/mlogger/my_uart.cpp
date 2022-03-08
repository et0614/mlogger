/**
 * @file my_uart.cpp
 * @brief AVR(ATMega328)��UART�ʐM���s��
 * @author E.Togashi
 * @date 2020/8/4
 */

#include "my_uart.h"

#include <stdio.h>
#include <string.h>
#include <avr/io.h>
#include <avr/interrupt.h>

//�r�b�g�ݒ�֐�
#define cbi(addr, bit) addr &= ~(1 << bit) // addr��bit�ڂ�'0'�ɂ���B
#define sbi(addr, bit) addr |= (1 << bit)  // addr��bit�ڂ�'1'�ɂ���B

#define BAUD_CALC(x) ((F_CPU+(x)*8UL) / (16UL*(x))-1UL)

//������
void my_uart::Initialize(void)
{
	//�{�[���[�g�̐ݒ�
	unsigned int baud = BAUD_CALC(9600);
	UBRR0H = (unsigned char)(baud>>8);//�{�[���[�g���
	UBRR0L = (unsigned char)baud; //�{�[���[�g����
	
	UCSR0B = _BV(RXCIE0) | _BV(RXEN0) | _BV(TXEN0); //���荞�݂Ƒ���M�L����
	UCSR0C = 0b00000110; //8bit, noparity, stopbit1, �񓯊�
}

bool my_uart::tx_done(void)
{
	return UCSR0A & 0b00100000;
}

//1�������M
void my_uart::send_char(const unsigned char data)
{
	while(!(UCSR0A & 0b00100000));
	UDR0 = data;
}

//�����z��𑗐M
void my_uart::send_chars(const unsigned char data[])
{
	for(int i = 0; data[i] != '\0'; i++)
		send_char(data[i]);
}


