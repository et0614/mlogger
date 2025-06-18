/*
 * utilities.h
 *
 * Created: 2025/06/16 9:19:14
 *  Author: e.togashi
 */ 

#ifndef UTILITIES_H_
#define UTILITIES_H_

#include <stdint.h>

class Utilities
{	
	public:
		/**
		 * @fn
		 * CRC-8�̏���璷�����l�𐶐�����
		 * @brief SHT4x��AHT20�Ȃǂ̃Z���T�[�ŗ��p�����CRC-8�`�F�b�N�T�����v�Z����B
		 * ������: 0x31 (x^8 + x^5 + x^4 + 1)
		 * �����l: 0xFF
		 * @param (ptr) �`�F�b�N�T�����v�Z����f�[�^�z��ւ̃|�C���^
		 * @param (len) �f�[�^���i�o�C�g�j
		 * @return 8�r�b�g��CRC�`�F�b�N�T���l
		 */
		static uint8_t crc8(uint8_t *ptr, uint8_t len);
		
		/**
		 * @fn
		 * CRC-16/CCITT-FALSE�̏���璷�����l�𐶐�����
		 * @brief XMODEM�Ȃǂŗ��p�����W���I��CRC-16�`�F�b�N�T�����v�Z����B
		 * ������: 0x1021 (x^16 + x^12 + x^5 + 1)
		 * �����l: 0xFFFF
		 * @param (ptr) �`�F�b�N�T�����v�Z����f�[�^�z��ւ̃|�C���^
		 * @param (len) �f�[�^���i�o�C�g�j
		 * @return 16�r�b�g��CRC�`�F�b�N�T���l
		 */
		 static uint16_t crc16(uint8_t *ptr, uint8_t len);

};

#endif /* UTILITIES_H_ */