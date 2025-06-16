/*
 * stcc4.cpp
 *
 * Created: 2025/06/16 17:05:59
 *  Author: e.togashi
 */ 

#include "stcc4.h"
#include "../utilities.h"
#include <util/delay.h>

//�A�h���X
const uint8_t ADDRESS = 0x64; //(ADDR==GND�̐ݒ�)

//�R�}���h
const uint8_t CMD_SOFT_RESET = 0x06;   // �\�t�g���Z�b�g
const uint16_t CMD_ENTER_SLEEP= 0x3650;   // �X���[�v
const uint8_t CMD_EXIT_SLEEP= 0x00;   // �X���[�v����
const uint16_t CMD_MES_SINGLE_SHOT = 0x219D;   //Single shot����
const uint16_t CMD_READ_MEASUREMENT = 0xEC05;   //�v�����ʓǂݎ��
const uint16_t CMD_SET_RHT_COMPENSATION = 0xE000;   //�⏞�p�����x�ݒ�

bool stcc4::sendCommand(uint16_t command) {
	const uint8_t command_bytes[] = {
		(uint8_t)(command >> 8),   // ��ʃo�C�g
		(uint8_t)(command & 0xFF)  // ���ʃo�C�g
	};
	return i2c_driver::Write(ADDRESS, command_bytes, sizeof(command_bytes));
}

bool stcc4::sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs)
{
	// ���M�o�b�t�@���쐬�F�R�}���h(2B) + ����(2B*N) + CRC(1B*N)
	uint8_t buffer_size = 2 + numArgs * 3;
	uint8_t buffer[buffer_size];

	// �R�}���h���o�b�t�@�Ɋi�[
	buffer[0] = (uint8_t)(command >> 8);
	buffer[1] = (uint8_t)(command & 0xFF);

	// ������CRC���o�b�t�@�Ɋi�[
	for (uint8_t i = 0; i < numArgs; i++) {
		uint16_t arg = args[i];
		uint8_t arg_msb = (uint8_t)(arg >> 8);
		uint8_t arg_lsb = (uint8_t)(arg & 0xFF);
		
		uint8_t base_index = 2 + i * 3;
		buffer[base_index] = arg_msb;
		buffer[base_index + 1] = arg_lsb;
		
		// 2�o�C�g�̈����f�[�^����CRC8���v�Z
		buffer[base_index + 2] = utilities::crc8(&buffer[base_index], 2);
	}
	
	// �g�ݗ��Ă��p�P�b�g�S�̂𑗐M
	return i2c_driver::Write(ADDRESS, buffer, buffer_size);
}

bool stcc4::IsConnected() {
	return i2c_driver::IsConnected(ADDRESS);
}

bool stcc4::Initialize(){
	const uint8_t command = CMD_SOFT_RESET;
	i2c_driver::Write(0x00, &command, 1); //�������̓W�F�l�����R�[��
	_delay_ms(10); //�ҋ@

	return true; // ����
}

bool stcc4::EnterSleep(){
	if (!sendCommand(CMD_ENTER_SLEEP)) 
		return false; // �ʐM���s

	_delay_ms(1); //�ҋ@

	return true; // ����
}

bool stcc4::ExitSleep(){
	const uint8_t command = CMD_EXIT_SLEEP;
	if (!i2c_driver::WriteByteAndStop(ADDRESS, command)) 
		return false;

	_delay_ms(5); //�ҋ@

	return true; // ����
}

bool stcc4::MeasureSingleShot(){
	if (!sendCommand(CMD_MES_SINGLE_SHOT)) 
		return false; // �ʐM���s

	//�v���I���܂�500ms�K�v
	return true; // ����
}

bool stcc4::ReadMeasurement(uint16_t * co2, float * temperature, float * humidity){
	if (!sendCommand(CMD_READ_MEASUREMENT))
		return false; // �ʐM���s
	
	_delay_ms(2); //�ҋ@
	
	uint8_t buffer[12];
	if (!i2c_driver::Read(ADDRESS, buffer, 12)) 
		return false; // �ʐM���s
	
	// �f�[�^�𕪗�
	uint8_t* co2Buff = &buffer[0]; // CO2�f�[�^ (3�o�C�g)
	uint8_t* dbtBuff = &buffer[3]; // ���x�f�[�^ (3�o�C�g)
	uint8_t* hmdBuff = &buffer[6]; // ���x�f�[�^ (3�o�C�g)
	
	// CO2��CRC�`�F�b�N�ƕϊ�
	if (utilities::crc8(co2Buff, 2) == co2Buff[2]) 
		*co2 = (co2Buff[0] << 8) | co2Buff[1];
	else return false; // CRC�G���[
	
	// ���x��CRC�`�F�b�N�ƕϊ�
	if (utilities::crc8(dbtBuff, 2) == dbtBuff[2]) {
		uint16_t raw_t = (dbtBuff[0] << 8) | dbtBuff[1];
		*temperature = -45.0f + 175.0f * (float)raw_t / 65535.0f;
	}
	else return false; // CRC�G���[
	
	// ���x��CRC�`�F�b�N�ƕϊ�
	if (utilities::crc8(hmdBuff, 2) == hmdBuff[2]) {
		uint16_t raw_h = (hmdBuff[0] << 8) | hmdBuff[1];
		float rh = -6.0f + 125.0f * (float)raw_h / 65535.0f;
		// �����I�Ȕ͈͓��ɒl�����߂�
		if(rh < 0.0f) rh = 0.0f;
		if(rh > 100.0f) rh = 100.0f;
		*humidity = rh;
	} else return false; // CRC�G���[
	
	return true; // ����
}

bool stcc4::SetRHTCompensation(float temperature, float humidity){
	// float�l��16bit�����ɕϊ�
	// Temperature: Input = (T[��C] + 45) * (2^16 - 1) / 175
	uint16_t temp_arg = (uint16_t)((temperature + 45.0f) * 65535.0f / 175.0f);
	
	// Humidity: Input = (RH[%RH] + 6) * (2^16 - 1) / 125
	uint16_t humi_arg = (uint16_t)((humidity + 6.0f) * 65535.0f / 125.0f);

	const uint16_t arguments[] = { temp_arg, humi_arg };

	// �R�}���h��2�̈����𑗐M
	return sendCommandWithArguments(CMD_SET_RHT_COMPENSATION, arguments, 2);
}