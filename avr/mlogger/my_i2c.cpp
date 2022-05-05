/**
 * @file my_i2c.cpp
 * @brief AVRxx�V���[�Y��I2C�ʐM����
 *  �Q�l1: https://github.com/microchip-pic-avr-examples/avr128da48-cnano-i2c-send-receive-mplabx
 *  �Q�l2: https://www.avrfreaks.net/forum/aht20-sensor-and-i2c?skey=aht20
 * @author E.Togashi
 * @date 2020/12/25
 */

#include "my_i2c.h"
#include <avr/io.h>
#include <util/delay.h>

//VCNL�̃A�h���X�B�����i�ɂ�4��̃A�h���X������̂Ō^�Ԃɒ��ӁB
const uint8_t VCNL_ADD = 0x60 << 1;

enum 
{
	I2C_INIT = 0,
	I2C_ACKED,
	I2C_NACKED,
	I2C_READY,
	I2C_ERROR,
	I2C_SUCCESS
};
 
//�������ݏI����҂�
static uint8_t _i2c_WaitW(void)
 {
	 uint8_t state = I2C_INIT;
	 do
	 {
		 //�������݂������͓ǂݍ��݊����t���O���Ď�
		 if(TWI1.MSTATUS & (TWI_WIF_bm | TWI_RIF_bm))
		 //if(TWI1.MSTATUS & TWI_WIF_bm)
		 {
			 //ACK���󂯎�����ꍇ
			 if(!(TWI1.MSTATUS & TWI_RXACK_bm)) state = I2C_ACKED;
			 //ACK���󂯎��Ȃ������ꍇ
			 else state = I2C_NACKED;
		 }
		 //�G���[�����t���O���Ď�
		 else if(TWI1.MSTATUS & (TWI_BUSERR_bm | TWI_ARBLOST_bm)) state = I2C_ERROR;
	 } while(!state);
	 
	 return state;
 }

//�ǂݍ��ݏI����҂�
static uint8_t _i2c_WaitR(void)
 {
	 uint8_t state = I2C_INIT;
	 do
	 {
		 //�������݂������͓ǂݍ��݊����t���O���Ď�
		 if(TWI1.MSTATUS & (TWI_WIF_bm | TWI_RIF_bm)) state = I2C_READY;
		 //if(TWI1.MSTATUS & TWI_RIF_bm) state = I2C_READY;
		 //�G���[�����t���O���Ď�
		 else if(TWI1.MSTATUS & (TWI_BUSERR_bm | TWI_ARBLOST_bm)) state = I2C_ERROR;
	 } while(!state);
	 
	 return state;
 }
 
//I2C�ʐM�i�������݁j�J�n
static uint8_t _start_writing(uint8_t address)
{
	TWI1.MADDR = address & ~0x01; //Write����̏ꍇ�A1���ڂ�0
	
	//while(TWI_RXACK_bm & TWI1.MSTATUS);
	//return 1;
	return _i2c_WaitW();
}

//I2C�ʐM�i�ǂݍ��݁j�J�n
static uint8_t _start_reading(uint8_t address)
{
	TWI1.MADDR = address | 0x01; //Read����̏ꍇ�A1���ڂ�1
	
	//while(TWI_RXACK_bm & TWI1.MSTATUS);
	//return 1;
	return _i2c_WaitW();
}

//I2C�ʐM�̏I��
static void _bus_stop(void)
{
	TWI1.MCTRLB = TWI_ACKACT_bm | TWI_MCMD_STOP_gc; //NACK
}

//Write�����i�}�X�^����X���[�u�ւ̑��M�j
static uint8_t _bus_write(uint8_t data)
{
	TWI1.MDATA = data;
	return _i2c_WaitW();
}

//Read�����i�X���[�u����}�X�^�ւ̑��M�j
static uint8_t _bus_read(bool sendAck, bool withStopCondition, uint8_t* data)
{
	uint8_t rslt = _i2c_WaitR();	
	if(rslt == I2C_READY)
	{
		*data = TWI1.MDATA;
		
		if(sendAck) TWI1.MCTRLB &= ~TWI_ACKACT_bm; //ACK
		else TWI1.MCTRLB |= TWI_ACKACT_bm; //NACK
		
		if(withStopCondition) TWI1.MCTRLB |= TWI_MCMD_STOP_gc;
		else TWI1.MCTRLB |= TWI_MCMD_RECVTRANS_gc;

		return I2C_SUCCESS;
	}
	//�G���[����
	else return rslt;
}
 
//����璷�����l�𐶐�1
static uint16_t crc16(uint8_t *ptr, uint8_t len)
{
	uint16_t crc =0xFFFF;
	uint8_t i;
	while(len--) {
		crc ^=*ptr++;
		for(i=0;i<8;i++) {
			if (crc & 0x01) {
				crc>>=1;
				crc^=0xA001;
			} else {
				crc>>=1;
			}
		}
	}
	return crc;
}

//����璷�����l�𐶐�2
static uint8_t crc8(uint8_t *ptr, uint8_t len) 
{
	uint8_t crc = 0xFF;
	for(int i = 0; i < len; i++) {
		crc ^= *ptr++;
		for(uint8_t bit = 8; bit > 0; --bit) {
			if(crc & 0x80) {
				crc = (crc << 1) ^ 0x31u;
				} else {
				crc = (crc << 1);
			}
		}
	}
	return crc;
}
 
//�ȉ��Apublic���\�b�h*************************************

void my_i2c::InitializeI2C(void)
{
	// TWI�ʐM��PIN�ݒ� : SDA->PF2, SCL->PF3
	PORTMUX.TWIROUTEA = 0x00;
	
	// ���샂�[�h�̐ݒ�
	TWI1.CTRLA &= ~TWI_FMPEN_bm; //�f�t�H���g�iStandard �������� Fast�j
	
	// SDA hold time�iSCL��Low�ɂȂ�����A�ǂꂾ��SDA�M�����ێ����邩�j
	TWI1.CTRLA |= TWI_SDAHOLD_50NS_gc; //50ns
	
	// �{�[���[�g�̐ݒ�i���g�������܂�j
	//AVR32DB32�̃f�[�^�V�[�g:Clock Generation���
	const bool IS_STANDARD_MODE = true;
	float fScl = IS_STANDARD_MODE ? 100000 : 400000; //���g��[Hz]
	float tRise = 0.000000001 * (IS_STANDARD_MODE ? 1000 : 300); //Rise time [sec]
	float tOf = 0.000000001 * 250;
	float baud = (uint8_t)(((float)F_CPU / (2 * fScl)) - 5 - ((float)F_CPU * tRise / 2));
	float tLow = (baud + 5) / F_CPU - tOf;
	float tLowM = 0.000000001 * (IS_STANDARD_MODE ? 4700 : 1300);
	if(tLow < tLowM) baud = F_CPU * (tLowM + tOf) - 5;
	TWI1.MBAUD = baud;
	
	// �A�h���X���W�X�^�A�f�[�^���W�X�^��������
	TWI1.MADDR = 0x00;
	TWI1.MDATA = 0x00;

	TWI1.MCTRLA |= TWI_ENABLE_bm		// TWI�̗L����
				| TWI_TIMEOUT_200US_gc; //200uS�̒ʐM�s�ǂ�Skip	

	TWI1.MSTATUS = TWI_BUSSTATE_IDLE_gc; //�o�X��IDLE��Ԃɂ���
	TWI1.MSTATUS |= TWI_WIF_bm | TWI_CLKHOLD_bm; //�t���O�N���A

	TWI1.MCTRLB |= TWI_FLUSH_bm; //�ʐM��Ԃ�������
	
	//�Ɠx�v���ݒ�//�ݒ�͕ς��Ȃ��̂ŏ��������̂݌Ăяo��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; } //�Ɠx�v���ݒ�R�}���h
	if(_bus_write(0b00010000) != I2C_ACKED) { _bus_stop(); return; } //000 1 00 0 0: �v�����x��, �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v���L���i��ɗL���œd�͏���͖��Ȃ����H�j
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return; } //000000 0 1: reserved, ���x1�{, White channel�����i���̋@�\�͂悭�킩���j
	_bus_stop();
	
	//�����v���ݒ�
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return; } //�����v���ݒ�R�}���h
	if(_bus_write(0b11001100) != I2C_ACKED) { _bus_stop(); return; } //11 00 111 0: Duty ratio=1/320, �����񐔂͖���, Integration time=4T(200ms), �����v���L���i��ɗL���œd�͏���͖��Ȃ����H�j
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();
}

void my_i2c::InitializeOPT(uint8_t add)
{
	//Configuration
	_start_writing(add); //OPTxxxx�̃A�h���X
	_bus_write(0x01); //Configuration�v��
	_bus_write(0b11001110); //0b 1100 1 11 0 //automatic full-scale, 800ms, continuous conversions, read only field
	_bus_write(0b00000000); //0b 0 0 0 0 0 00//read only field * 3, hysteresis-style,
	_bus_stop();
	_delay_ms(1); //�K�v�ȑҋ@���Ԃ͋Z�p��������ǂݎ�ꂸ
	
	//OPTxxxx��Resister Address ��Result�ɐݒ�
	//�Đݒ肷��܂ňێ�����邽�߁A���������ɐݒ肵�Ă��܂�
	_start_writing(add); //OPTxxxx�̃A�h���X
	_bus_write(0x00); //Result�v��
	_bus_stop();
	_delay_ms(1); //�K�v�ȑҋ@���Ԃ͋Z�p��������ǂݎ�ꂸ
}

uint8_t my_i2c::ReadAM2320(float* tempValue, float* humiValue)
{
	const uint8_t AM_ADD = 0xB8; //AM2320�̃A�h���X�i0xB8=0b10111000�j
	uint8_t buffer[8];
	
	//�X���[�v��Ԃ���N�����BACK�͎擾�ł��Ȃ�
	_start_writing(AM_ADD);
	_delay_ms(1);
	_bus_stop();
	
	//�R�}���h���M�O����// SLA + address (0xB8) + starting address(0x00) + register length(0x04)	
	_start_writing(AM_ADD);
	_bus_write(0x03);
	_bus_write(0x00);
	_bus_write(0x04);
	_bus_stop();
	_delay_ms(1); //1.5ms�ȏ�̑ҋ@�I�I�I

	_start_reading(AM_ADD);
	_delay_us(50); //30us�ȏ�̑ҋ@
	for (uint8_t i = 0; i<7; i++) {		
		_bus_read(1, 0, &buffer[i]); //�ǂ��ACK
	}
	_bus_read(0, 1, &buffer[7]); //�ǂ��NACK
	//_bus_stop();
	
	//CRC16���`�F�b�N
	uint16_t Rcrc = ((uint16_t)buffer[7] << 8)+buffer[6];
	if (Rcrc == crc16(buffer, 6)) {
		//�����x�f�[�^�𕜌�
		int sigT = -1;
		if((buffer[2] & 0b10000000) == 0) sigT = 1;
		else buffer[2] = buffer[2] & 0b01111111;
		int sigH = -1;
		if((buffer[4] & 0b10000000) == 0) sigH = 1;
		else buffer[4] = buffer[4] & 0b01111111;

		*humiValue = 0.1 * (sigH * ((buffer[2] << 8) + buffer[3]));
		*tempValue = 0.1 * (sigT * ((buffer[4] << 8) + buffer[5]));
		return 1;
	}
	else{
		//CRC�s�����̏ꍇ
		*humiValue = -99.0;
		*tempValue = -99.0;
		return 0;
	}
}

uint8_t my_i2c::ReadAHT20(float* tempValue, float* humiValue)
{
	*humiValue = -99;
	*tempValue = -99;
	
	const uint8_t AHT_ADD = 0x38 << 1; //AHT20�̃A�h���X�i0x38=0b00111000�j
	uint8_t buffer[7];
	
	//�������R�}���h(���M��10ms�҂�)
	if(_start_writing(AHT_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0xBE) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; }
	_bus_stop();
	_delay_ms(10);
	
	//���薽��(�v���I���܂�80ms�K�v)
	if(_start_writing(AHT_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0xAC) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x33) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; }
	_bus_stop();
	_delay_ms(80);
			
	//����l����M
	if(_start_reading(AHT_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:���
	//Busy�̏ꍇ
	if((buffer[0] & (1<<7))) { _bus_stop(); return 0; }
	else
	{
		if(_bus_read(1, 0, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:���Ύ��x1
		if(_bus_read(1, 0, &buffer[2]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:���Ύ��x2
		if(_bus_read(1, 0, &buffer[3]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:���Ύ��x3,�������x1
		if(_bus_read(1, 0, &buffer[4]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:�������x2
		if(_bus_read(1, 0, &buffer[5]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:�������x3
		if(_bus_read(0, 1, &buffer[6]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK:CRC
		
		//CRC8���`�F�b�N
		volatile uint8_t rcrc = crc8((uint8_t*)buffer, 6);
		if (buffer[6] == rcrc) 
		{
			float hum = 0;
			hum += buffer[1];
			hum *= 256;
			hum += buffer[2];
			hum *= 16;
			hum += (buffer[3]>>4);
			hum *= 100;
			hum /= 1024;
			hum /= 1024;
			*humiValue = hum;
			
			float tmp = 0;
			tmp += (buffer[3] & 0x0F);
			tmp *= 256;
			tmp += buffer[4];
			tmp *= 256;
			tmp += buffer[5];
			tmp *= 200;
			tmp /= 1024;
			tmp /= 1024;
			tmp -= 50;
			*tempValue = tmp;

			return 1;
		}
		else return 0;
	}	
}

float my_i2c::ReadOPT(uint8_t add)
{
	uint8_t buffer[2];
	
	_start_reading(add); //OPTxxxx�̃A�h���X
	_delay_us(50); //�ҋ@���ׂ����Ԃ͕s��//AutoScale��10ms�~�����W�ύX�񐔂̎��Ԃ��K�v�̖͗l�B�����W��12�i�K�Ȃ̂ōő��110ms+�����H
	_bus_read(1, 0, &buffer[0]); //ACK
	_bus_read(0, 1, &buffer[1]); //NACK
	
	//Lux�ɕϊ�
	int expnt = (0b11110000 & buffer[0]) >> 4; //���4bit�������W��\��
	int val = ((0b00001111 & buffer[0]) << 8) + buffer[1]; //����12bit�͒l��\��
	float lux = 0.01 * pow(2, expnt) * val;
	
	if(83865.60 < lux) return 0; //�G���[����0�Ƃ���
	else return lux;
}

float my_i2c::ReadVCNL4030_ALS(void)
{
	//�Ɠx�ǂݎ��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x0B) != I2C_ACKED) { _bus_stop(); return 0; } //�Ɠx�v���R�}���h
	//stop�����ɑ����đ��M
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
		
	uint16_t data = (buffer[1] << 8) + buffer[0];
	return 0.064 * 4 * data; //�_�C�i�~�b�N�����W2�{�A���x1�{�ݒ�̂���:2/(1/2)=4
}

uint16_t my_i2c::ReadVCNL4030_PS(void)
{
	//1��݂̂̓ǂݎ��̂��߁AActive Force Mode���g��
	/*if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x04) != I2C_ACKED) { _bus_stop(); return 0; } //�����v���ݒ�R�}���h
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 1 0 0 0
	if(_bus_write(0b00000000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 0 000
	_bus_stop();*/
	
	//�����ǂݎ��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return 0; } //�����v���R�}���h
	//stop�����ɑ����đ��M
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
		
	uint16_t data = (buffer[1] << 8) + buffer[0];
	return data;
}

void my_i2c::ScanAddress(uint8_t minAddress, uint8_t maxAddress)
{
	for (uint8_t client_address = minAddress; client_address <= maxAddress; client_address++)
	{
		if(_start_writing(client_address<<1) == I2C_ACKED)
		{
			//debug�p
			//volatile uint8_t xxx = client_address;
		}
		_bus_stop();
		_delay_ms(10);
	}	
	
}
	
