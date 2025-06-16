/*
 * vcnl4030.cpp
 *
 * Created: 2025/06/16 12:31:44
 *  Author: etoga
 */ 

#include "vcnl4030.h"
#include <math.h>
#include <util/delay.h>

//VCNL4030�̃A�h���X�B�����i�ɂ�4��̃A�h���X������̂Ō^�Ԃɒ��ӁB
const uint8_t VCNL_ADD = 0x60;

// VCNL4030�̃R�}���h/���W�X�^�A�h���X
const uint8_t CMD_ALS_CONF   = 0x00; // �Ɠx�Z���T�[�ݒ�
const uint8_t CMD_PS_CONF    = 0x03; // �ߐڃZ���T�[�ݒ�(CONF1, CONF2)
const uint8_t CMD_PS_CONF3   = 0x04; // �ߐڃZ���T�[�ݒ�(CONF3, MS)
const uint8_t CMD_PS_DATA    = 0x08; // �ߐڃZ���T�[�f�[�^
const uint8_t CMD_ALS_DATA   = 0x0B; // �Ɠx�Z���T�[�f�[�^

bool vcnl4030::Initialize(void){
	// �Ɠx�Z���T�[(ALS)�̐ݒ����������
	const uint8_t als_config[] = {
		CMD_ALS_CONF,
		0b00010001, // ALS_CONF1: 000 1 00 0 1: �v�����x��, �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v������
		0b00000011  // ALS_CONF2: ���x�ݒ�
	};
	if (!i2c_driver::Write(VCNL_ADD, als_config, sizeof(als_config)))
		return false; // �ʐM���s

	// �ߐڃZ���T�[(PS)�̐ݒ����������
	const uint8_t ps_config[] = {
		CMD_PS_CONF,
		0b11001111, // PS_CONF1: 11 00 111 1: Duty ratio=1/320, �����񐔂͖���, Integration time=8T(400us), �����v������
		0b00001000  // PS_CONF2: 00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	};
	if (!i2c_driver::Write(VCNL_ADD, ps_config, sizeof(ps_config)))
		return false; // �ʐM���s

	return true; // ����
}

bool vcnl4030::ReadALS(float * als)
{
	//�Ɠx�Z���T��L���ɂ���
	const uint8_t enable_als[] = {
		CMD_ALS_CONF,	//�Ɠx�v���ݒ�R�}���h
		0b00010000,		//ALS_CONF1: 000 1 00 0 0: �v�����x��(50ms), �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v���L��
		0b00000011		//ALS_CONF2: 000000 1 1: reserved, ���x1�{, White channel�����i���̋@�\�͂悭�킩���j
	};
	if (!i2c_driver::Write(VCNL_ADD, enable_als, sizeof(enable_als)))
		return false; // �ʐM���s
	
	_delay_ms(100); //�v�����x��50ms�Ȃ̂ŁA���S�����Ă��̔{�A�ҋ@
	
	// �Ɠx�f�[�^���W�X�^(0x0B)���w�肵�A2�o�C�g�̃f�[�^��ǂݍ���
	const uint8_t read_command = CMD_ALS_DATA;
	uint8_t buffer[2];
	if (!i2c_driver::WriteRead(VCNL_ADD, &read_command, 1, buffer, sizeof(buffer))) 
		return false; // �ʐM���s
	
	// �Ɠx�Z���T�[�𖳌��ɖ߂�
	const uint8_t disable_als[] = { 
		CMD_ALS_CONF,	//�Ɠx�v���ݒ�R�}���h
		0b00010001,		//ALS_CONF1: 000 1 00 0 1: �v�����x��(50ms), �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v������
		0b00000011		//ALS_CONF2:000000 1 1: reserved, ���x1�{, White channel�����i���̋@�\�͂悭�킩���j
	};
	if (!i2c_driver::Write(VCNL_ADD, disable_als, sizeof(disable_als)))
		return false; // �ʐM���s

	// 5. �ǂݎ�����f�[�^���Ɠx[lx]�ɕϊ�
	uint16_t data = (buffer[1] << 8) | buffer[0];
	*als = 0.064f * 4.0f * data; // �_�C�i�~�b�N�����W2�{�A���x1�{�ݒ�̂���: 2/(1/2)=4

	return true;
}

bool vcnl4030::ReadPS(float * ps)
{
	*ps = 0; // ������

	// 1��݂̂̑���̂��߁AActive Force Mode�ɐݒ肷�� (Write API)
	const uint8_t force_mode[] = { 
		CMD_PS_CONF3,	//�����v���ݒ�R�}���h
		0b00001000,		//PS_CONF1:0 00 0 1 0 0 0
		0b00000000		//PS_CONF2:0 00 0 0 000
	};
	if (!i2c_driver::Write(VCNL_ADD, force_mode, sizeof(force_mode)))
		return false; // �ʐM���s
	
	// �����v����L���ɂ��� (Write API)
	const uint8_t enable_ps[] = { 
		CMD_PS_CONF,	//�����v���ݒ�R�}���h
		0b11001110,		//PS_CONF1:11 00 111 0: Duty ratio=1/320, �����񐔂͖���, Integration time=8T(400us), �����v���L��
		0b00001000		//PS_CONF2:00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	};
	if (!i2c_driver::Write(VCNL_ADD, enable_ps, sizeof(enable_ps)))
		return false; // �ʐM���s

	// ���莞�Ԃ�҂�: �Z�p�����ɂ���8T�̏ꍇ�ɂ�128us�̂悤�����B�B�B
	_delay_ms(150);

	// �����f�[�^���W�X�^(0x08)���w�肵�A2�o�C�g�̃f�[�^��ǂݍ���
	const uint8_t read_command = CMD_PS_DATA;
	uint8_t buffer[2];
	if (!i2c_driver::WriteRead(VCNL_ADD, &read_command, 1, buffer, sizeof(buffer)))
		return false; // �ʐM���s

	// �����v���𖳌��ɖ߂� (Write API)
	const uint8_t disable_ps[] = { 
		CMD_PS_CONF,	//�����v���ݒ�R�}���h
		0b11001111,		//PS_CONF1:11 00 111 0: Duty ratio=1/320, �����񐔂͖���, Integration time=8T(400us), �����v������
		0b00001000		//PS_CONF2:00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	};
	if (!i2c_driver::Write(VCNL_ADD, disable_ps, sizeof(disable_ps))) 
		return false; // �ʐM���s
	
	// �ǂݎ�����f�[�^������[mm]�ɕϊ�
	uint16_t data = (buffer[1] << 8) | buffer[0];
	if (data < 1) data = 1;
	float ld = log(data);
	*ps = exp((-0.018 * ld - 0.234) * ld + 6.564);

	return true;
}
