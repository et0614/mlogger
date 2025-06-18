/**
 * @file UartDriver.cpp
 * @brief AVR(ATMega328)��UART�ʐM���s��
 * @author E.Togashi
 * @date 2020/8/4
 */

#include "UartDriver.h"

#include <stdio.h>
#include <string.h>
#include <avr/io.h>
#include <avr/interrupt.h>

#define BAUD_CALC(BAUD_RATE)	((float)(64 * F_CPU / (16 * (float)BAUD_RATE)) + 0.5)

//��M�����O�o�b�t�@
#define UART_RX_BUFFER_SIZE 128 //�T�C�Y�i256�ȉ���2�ׂ̂��悪�����I�j
static char g_rx_buffer[UART_RX_BUFFER_SIZE]; //�����O�o�b�t�@
static volatile uint8_t g_rx_head = 0; // ���ɏ������ޏꏊ�i��M�����O�o�b�t�@�j
static volatile uint8_t g_rx_tail = 0; // ���ɓǂݏo���ꏊ�i��M�����O�o�b�t�@�j

//���M�����O�o�b�t�@
#define UART_TX_BUFFER_SIZE 128
static char g_tx_buffer[UART_TX_BUFFER_SIZE];
static volatile uint8_t g_tx_head = 0;
static volatile uint8_t g_tx_tail = 0;

//������
void UartDriver::initialize(void)
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
	
	// �����O�o�b�t�@�̃C���f�b�N�X��������
	g_rx_head = g_rx_tail = 0;
	g_tx_head = g_tx_tail = 0;
}

//1�������M
void UartDriver::sendChar(const char data)
{
	// ����head�̈ʒu���v�Z
	uint8_t next_head = (g_tx_head + 1) % UART_TX_BUFFER_SIZE;

	// ���M�o�b�t�@�����t�ɂȂ�܂őҋ@
	// (�ʏ�A���C�����[�v���\���ɑ�����΂����ő҂��Ƃ͋H)
	while (next_head == g_tx_tail);

	// �o�b�t�@�Ƀf�[�^���i�[���Ahead��i�߂�
	g_tx_buffer[g_tx_head] = data;
	g_tx_head = next_head;
	
	// �u���M�f�[�^���W�X�^��(DRE)�v���荞�݂�L��������i���M�\�ȏ�ԂɂȂ����玩���I��ISR���Ă΂��j
	USART0.CTRLA |= USART_DREIE_bm;
}

//�����z��𑗐M
void UartDriver::sendChars(const char data[])
{
	for(int i = 0; data[i] != '\0'; i++)
		sendChar(data[i]);
}

//���M���������s�����m�F
bool UartDriver::isTransmitting(void)
{
	return (g_tx_head != g_tx_tail); // ���M�o�b�t�@����łȂ���Α��M��
}

//��M�����O�o�b�t�@�Ƀf�[�^�����邩�m�F����
bool UartDriver::uartRingBufferHasData(void)
{
    // head��tail���Ⴄ�ꏊ�ɂ���΁A���ǃf�[�^�����݂���
    return (g_rx_head != g_rx_tail);
}

//��M�����O�o�b�t�@����1�o�C�g�ǂݏo��
char UartDriver::uartRingBufferGet(void)
{
    // �o�b�t�@����̏ꍇ�͓ǂݏo���Ȃ��i�Ăяo������has_data()���m�F����O��j
    if (g_rx_head == g_rx_tail) {
        return 0; 
    }

    // tail�̈ʒu����f�[�^��ǂݏo��
    char data = g_rx_buffer[g_rx_tail];
    
    // tail��i�߂�i�o�b�t�@�̏I�[�ɒB������0�ɖ߂�j
    g_rx_tail = (g_rx_tail + 1) % UART_RX_BUFFER_SIZE;
    
    return data;
}

/**
 * @brief UART��M���荞�݃T�[�r�X���[�`��
 */
ISR(USART0_RXC_vect)
{
    // ��M�����f�[�^��ǂݏo��
    char data = USART0.RXDATAL;

    // ����head�̈ʒu���v�Z
    uint8_t next_head = (g_rx_head + 1) % UART_RX_BUFFER_SIZE;

    // �o�b�t�@�����t�łȂ���΃f�[�^���i�[
    if (next_head != g_rx_tail)
    {
        g_rx_buffer[g_rx_head] = data;
        g_rx_head = next_head;
    }
    // �o�b�t�@�����t�̏ꍇ�A��M�����f�[�^�͔j�������i�I�[�o�[�t���[�j
}

/**
 * @brief UART���M�f�[�^���W�X�^�󂫊��荞�݃T�[�r�X���[�`��
 */
ISR(USART0_DRE_vect)
{
	// ���M�o�b�t�@�Ƀf�[�^�������
	if (g_tx_head != g_tx_tail)
	{
		// �o�b�t�@����1�������o���đ��M
		USART0.TXDATAL = g_tx_buffer[g_tx_tail];
		g_tx_tail = (g_tx_tail + 1) % UART_TX_BUFFER_SIZE;
	}
	// ���M����f�[�^���Ȃ��Ȃ�����A���荞�݂𖳌���
	else USART0.CTRLA &= ~USART_DREIE_bm;
}

