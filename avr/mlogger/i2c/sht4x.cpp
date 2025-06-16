/*
 * sht4x.cpp
 *
 * Created: 2025/06/16 9:53:30
 *  Author: e.togashi
 */ 

#include "sht4x.h"
#include "../utilities.h"
#include "i2c_driver.h"
#include <util/delay.h>

//SHT4X-ADX�̃A�h���X
const uint8_t SHT4X_ADX_ADD = 0x44; //BDX��CDX�͈قȂ�̂Œ���

//SHT4X-BDX�̃A�h���X
const uint8_t SHT4X_BDX_ADD = 0x45; //ADX��CDX�͈قȂ�̂Œ���

// SHT4x�̃R�}���h
const uint8_t CMD_MEASURE_MEDIUM = 0xF6; // �����x�ł̑���R�}���h
const uint8_t CMD_SOFT_RESET = 0x94;   // �\�t�g���Z�b�g
const uint8_t CMD_READ_SERIAL = 0x89; // �V���A���ԍ��ǂݎ��

bool sht4x::Initialize(bool isAD)
{	
	const uint8_t address = isAD ? SHT4X_ADX_ADD : SHT4X_BDX_ADD;
	const uint8_t command = CMD_SOFT_RESET;

	// �\�t�g���Z�b�g�R�}���h(0x94)�𑗐M
	if (!i2c_driver::Write(address, &command, 1)) 
		return false; // �ʐM���s

	// ���Z�b�g�����܂�1ms�ҋ@
	_delay_ms(1);

	return true; // ����
}

bool sht4x::ReadValue(float* tempValue, float* humiValue, bool isAD)
{
	*humiValue = -99;
	*tempValue = -99;

	const uint8_t address = isAD ? SHT4X_ADX_ADD : SHT4X_BDX_ADD;
	
	// ����J�n�R�}���h�𑗐M (Write API���g�p)
	const uint8_t command = CMD_MEASURE_MEDIUM;
	if (!i2c_driver::Write(address, &command, 1)) {
		return false; // �ʐM���s
	}

	// �Z���T�[�̑��芮����҂� (�f�[�^�V�[�g���4.5ms)
	_delay_ms(10);

	// ���茋�ʁi6�o�C�g�j����M (Read API���g�p)
	uint8_t buffer[6];
	if (!i2c_driver::Read(address, buffer, 6)) {
		return false; // �ʐM���s
	}

	// ���x�Ǝ��x�̃f�[�^�ɕ���
	uint8_t* tBuff = &buffer[0]; // ���x�f�[�^ (3�o�C�g)
	uint8_t* hBuff = &buffer[3]; // ���x�f�[�^ (3�o�C�g)

	// ���x��CRC�`�F�b�N�ƕϊ�
	if (utilities::crc8(tBuff, 2) == tBuff[2]) {
		uint16_t raw_t = (tBuff[0] << 8) | tBuff[1];
		*tempValue = -45.0f + 175.0f * (float)raw_t / 65535.0f;
	}
	else return false; // CRC�G���[

	// ���x��CRC�`�F�b�N�ƕϊ�
	if (utilities::crc8(hBuff, 2) == hBuff[2]) {
		uint16_t raw_h = (hBuff[0] << 8) | hBuff[1];
		float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
		// �����I�Ȕ͈͓��ɒl�����߂�
		if(rh < 0.0f) rh = 0.0f;
		if(rh > 100.0f) rh = 100.0f;
		*humiValue = rh;
	} else return false; // CRC�G���[

	return true; // ����
}

bool sht4x::ReadSerial(uint32_t* serialNumber, bool isAD)
{
	*serialNumber = 0; // ���O�ɏ�����
	const uint8_t address = isAD ? SHT4X_ADX_ADD : SHT4X_BDX_ADD;
	const uint8_t command = CMD_READ_SERIAL;
	uint8_t buffer[6];

	// �V���A���ԍ��ǂݎ��R�}���h(0x89)���������݁A������6�o�C�g�̃f�[�^��ǂݎ��
	if (!i2c_driver::WriteRead(address, &command, 1, buffer, 6)) 
		return false; // �ʐM���s

	// ��M�����f�[�^��CRC�`�F�b�N
	// �f�[�^��2�o�C�g�̃f�[�^��1�o�C�g��CRC��2�Z�b�g
	uint8_t crc_word1 = utilities::crc8(&buffer[0], 2);
	uint8_t crc_word2 = utilities::crc8(&buffer[3], 2);
	if (crc_word1 != buffer[2] || crc_word2 != buffer[5]) 
		return false; // CRC�G���[

	// �o�C�g�f�[�^��32�r�b�g�̃V���A���ԍ��Ɍ���
	uint16_t word1 = (buffer[0] << 8) | buffer[1];
	uint16_t word2 = (buffer[3] << 8) | buffer[4];
	*serialNumber = ((uint32_t)word1 << 16) | word2;

	return true; // ����
}

