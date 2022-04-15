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

//#define BAUD_CALC(x)	((F_CPU+(x)*8UL) / (16UL*(x))-1UL)
#define BAUD_CALC(BAUD_RATE)	((float)(64 * F_CPU / (16 * (float)BAUD_RATE)) + 0.5)

//������
void my_uart::Initialize(void)
{
	//�|�[�g�̓��o�͐ݒ�
	PORTA.DIRSET = PIN0_bm; //TX:�����o��
	PORTA.DIRCLR = PIN1_bm; //RX:�ǂݍ���
	//RX��PullUp
	PORTA.OUTSET = PIN1_bm; //INT0�F�ݒ�
	
	//�{�[���[�g�̐ݒ�
	USART0.BAUD = (uint16_t)BAUD_CALC(9600);
	
	USART0_CTRLA |= USART_RXCIF_bm; //��M�����C�x���g�L����
	USART0.CTRLB |= (USART_RXEN_bm | USART_TXEN_bm); //����M�L����
	USART0.CTRLC = 0b00000011;//00 00 0 11: Asynchronous, noparity, stopbit=1, 8bit
}

bool my_uart::tx_done(void)
{
	return (USART0.STATUS & USART_DREIF_bm);
}

//1�������M
void my_uart::send_char(const unsigned char data)
{
	while (!tx_done());
	USART0.TXDATAL = data;
}

//�����z��𑗐M
void my_uart::send_chars(const unsigned char data[])
{
	for(int i = 0; data[i] != '\0'; i++)
		send_char(data[i]);
}


