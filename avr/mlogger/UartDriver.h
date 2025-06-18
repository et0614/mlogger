/**
 * @file UartDriver.h
 * @brief AVR(ATMega328)��UART�ʐM���s��
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
		* UART�ʐM�̂��߂̏���������
		* @param ����
		* @return ����
		*/
		static void initialize(void);

		/**
		* @fn
		* UART�ʐM�ŕ����𑗐M����
		* @param (data) ����
		* @return ����
		*/
		static void sendChar(const char data);

		/**
		* @fn
		* UART�ʐM�ŕ�����𑗐M����
		* @param (data) ������
		* @return ����
		*/
		static void sendChars(const char data[]);
		
		/**
		 * @fn
		 * ��M�����O�o�b�t�@�Ƀf�[�^�����邩�m�F����
		 * @return �f�[�^�������true
		 */
		static bool uartRingBufferHasData(void);

		/**
		 * @fn
		 * ��M�����O�o�b�t�@����1�o�C�g�ǂݏo��
		 * @return �ǂݏo��������
		 * @note ���O��uart_ring_buffer_has_data()�Ńf�[�^�����邱�Ƃ��m�F����
		 */
		static char uartRingBufferGet(void);
		
		/**
		 * @fn
		 * ���M���������s�����ۂ����m�F����
		 * @brief ���M�����O�o�b�t�@�ɖ����M�̃f�[�^���c���Ă���ꍇ��true��Ԃ�
		 * @return ���M���ł����true
		 */
		static bool isTransmitting(void);
	
	private:

};

#endif /* UART_DRIVER_H_ */