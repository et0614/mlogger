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

//AHT20�̃A�h���X (0x38=0b00111000; 0x70=0b01110000)
const uint8_t AHT20_ADD = 0x38 << 1;

//P3T1750DP�̃A�h���X�i0x24=0b10010000; 0x48=0b01001000, A0=A1=A2=GND�j
const uint8_t P3T1750DP_ADD = 0x48 << 1;

//STC31C�̃A�h���X
const uint8_t STC31C_ADD = 0x29 << 1;

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
 
//����璷�����l�𐶐�
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
	float tRise = 0.000000001 * (IS_STANDARD_MODE ? 1000 : 250); //Rise time [sec]
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
}

void my_i2c::InitializeVCNL4030(void){
	//�Ɠx�v�E�����v�𖳌��ɂ���//���̏����͕s�v��������Ȃ�
	//�Ɠx�v��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; } //�Ɠx�v���ݒ�R�}���h
	if(_bus_write(0b00010001) != I2C_ACKED) { _bus_stop(); return; } //000 1 00 0 1: �v�����x��, �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v������
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return; } //000000 0 1: reserved, ���x1�{, White channel�����i���̋@�\�͂悭�킩���j
	_bus_stop();
	
	//�����v��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return; } //�����v���ݒ�R�}���h1(PS_CONF1, PS_CONF2)
	if(_bus_write(0b11001111) != I2C_ACKED) { _bus_stop(); return; } //11 00 111 1: Duty ratio=1/320, �����񐔂͖���, Integration time=8T(400us), �����v������
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();
}

float my_i2c::ReadVCNL4030_ALS(void)
{	
	//�Ɠx�Z���T��L���ɂ���
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; } //�Ɠx�v���ݒ�R�}���h
	if(_bus_write(0b00010000) != I2C_ACKED) { _bus_stop(); return 0; } //000 1 00 0 0: �v�����x��(50ms), �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v���L��
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return 0; } //000000 0 1: reserved, ���x1�{, White channel�����i���̋@�\�͂悭�킩���j
	_bus_stop();
	_delay_ms(100); //�v�����x��50ms�Ȃ̂ŁA���S�����Ă��̔{�A�ҋ@
	
	//�Ɠx�ǂݎ��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x0B) != I2C_ACKED) { _bus_stop(); return 0; } //�Ɠx�v���R�}���h
	//stop�����ɑ����đ��M
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
	uint16_t data = (buffer[1] << 8) + buffer[0];
	
	//�Ɠx�Z���T�𖳌��ɂ���
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; } //�Ɠx�v���ݒ�R�}���h
	if(_bus_write(0b00010001) != I2C_ACKED) { _bus_stop(); return 0; } //000 1 00 0 1: �v�����x��(50ms), �_�C�i�~�b�N�����W2�{, �����񐔂͖���, ��������, �Ɠx�v������
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return 0; } //000000 0 1: reserved, ���x1�{, White channel�����i���̋@�\�͂悭�킩���j
	_bus_stop();
	
	return 0.064 * 4 * data; //�_�C�i�~�b�N�����W2�{�A���x1�{�ݒ�̂���:2/(1/2)=4
}

float my_i2c::ReadVCNL4030_PS(void)
{
	//1��݂̂̓ǂݎ��̂��߁AActive Force Mode�ɂ���
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x04) != I2C_ACKED) { _bus_stop(); return 0; } //�����v���ݒ�R�}���h
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 1 0 0 0
	if(_bus_write(0b00000000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 0 000
	_bus_stop();
	//�����v����L���ɂ���
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return 0; } //�����v���ݒ�R�}���h1(PS_CONF1, PS_CONF2)
	if(_bus_write(0b11001110) != I2C_ACKED) { _bus_stop(); return 0; } //11 00 111 0: Duty ratio=1/320, �����񐔂͖���, Integration time=8T(400us), �����v���L��
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();
	_delay_ms(150); //�Z�p�����ɂ���8T�̏ꍇ�ɂ�128ms�̂悤�����B�B�B
	
	//�����ǂݎ��
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return 0; } //�����v���R�}���h
	//stop�����ɑ����đ��M
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
	uint16_t data = (buffer[1] << 8) + buffer[0];
	
	//�����v���𖳌��ɂ���
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return 0; } //�����v���ݒ�R�}���h1(PS_CONF1, PS_CONF2)
	if(_bus_write(0b11001111) != I2C_ACKED) { _bus_stop(); return 0; } //11 00 111 1: Duty ratio=1/320, �����񐔂͖���, Integration time=8T(400us), �����v������
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();	
	
	float ld = log(data);
	return exp((-0.018 * ld - 0.234) * ld + 6.564);
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

void my_i2c::InitializeAHT20(void)
{
	//Status��0x18�ȊO�̏ꍇ�ɂ̓��Z�b�g
	if((ReadAHT20Status()&0x18)!=0x18)
	{
		//���W�X�^������
		ResetAHT20(0x1b);
		ResetAHT20(0x1c);
		ResetAHT20(0x1e);
		_delay_ms(10);
	}
	
	if(_start_writing(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0xa8) != I2C_ACKED) { _bus_stop(); return; } //NOR operating mode
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; }
	_bus_stop();
	_delay_ms(10);
	
	//�������R�}���h(���M��10ms�҂�)
	if(_start_writing(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0xBE) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; }
	_bus_stop();
	_delay_ms(10);
}

uint8_t my_i2c::ReadAHT20(float* tempValue, float* humiValue)
{
	*humiValue = -99;
	*tempValue = -99;
	
	uint8_t buffer[7];
	
	if((ReadAHT20Status()&0x18)!=0x18) //Status��0x18�ȊO�̏ꍇ�ɂ̓��Z�b�g
	{
		//���W�X�^������
		ResetAHT20(0x1b);
		ResetAHT20(0x1c);
		ResetAHT20(0x1e);
		_delay_ms(10);
	}
	
	//���薽��(�v���I���܂�80ms�K�v)
	if(_start_writing(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0xAC) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x33) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; }
	_bus_stop();
	_delay_ms(80);
	
	uint16_t cnt = 0;
	while(((ReadAHT20Status()&0x80)==0x80)) //bit[7]=1�̊Ԃ�busy
	{
		_delay_ms(2);
		if(cnt++>=100) break;
	}
	
	//����l����M
	if(_start_reading(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
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

uint8_t my_i2c::ReadAHT20Status(void)
{
	uint8_t buff;
	if(_start_reading(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_read(0, 1, &buff) != I2C_SUCCESS) { _bus_stop(); return 0; }
	return buff;
}

void my_i2c::ResetAHT20(uint8_t code)
{
	uint8_t Byte_first,Byte_second,Byte_third;
	
	if(_start_writing(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(code) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; }
	_bus_stop();
	_delay_ms(5);
	
	if(_start_reading(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_read(1, 0, &Byte_first) != I2C_SUCCESS) { _bus_stop(); return; }
	if(_bus_read(1, 0, &Byte_second) != I2C_SUCCESS) { _bus_stop(); return; }
	if(_bus_read(0, 1, &Byte_third) != I2C_SUCCESS) { _bus_stop(); return; }
	_delay_ms(10);
	
	if(_start_writing(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0xB0|code) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(Byte_second) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(Byte_third) != I2C_ACKED) { _bus_stop(); return; }
	_bus_stop();
	
	Byte_second=0x00;
	Byte_third =0x00;
}
	
void my_i2c::InitializeP3T1750DP(void)
{
	if(_start_writing(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0b00000001) != I2C_ACKED) { _bus_stop(); return; } //Configuration register�𑀍삷�邽�߂̃|�C���^���W�X�^�i01�j����������
	if(_bus_write(0b00101001) != I2C_ACKED) { _bus_stop(); return; } //Shutdown���[�h�Ƃ���B���̑��̃r�b�g�̓f�t�H���g�i55ms�Ōv���j
	_bus_stop();
}

uint8_t my_i2c::ReadP3T1750DP(float *tempValue)
{
	*tempValue = -99;
	
	//Shutdown���[�h����N������1��̂݌v��������
	if(_start_writing(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0b00000001) != I2C_ACKED) { _bus_stop(); return 0; } //Configuration register�𑀍삷�邽�߂̃|�C���^���W�X�^�i01�j����������
	if(_bus_write(0b10101001) != I2C_ACKED) { _bus_stop(); return 0; } //One-Shot�v��
	_bus_stop();
	
	_delay_ms(55); //�v���̂��߂�55ms�̂��x��
	
	//���x��ǂݎ��
	uint8_t buffer[2];
	if(_start_writing(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0b00000000) != I2C_ACKED) { _bus_stop(); return 0; } //Temperature register�𑀍삷�邽�߂̃|�C���^���W�X�^�i00�j����������
	if(_start_reading(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return 0; } //Restart
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK�Ōp��
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK�ŏI��
	
	//MSB�̍ŏ�ʂ�1�̏ꍇ�i�}�C�i�X�j
	if(buffer[0] & 0b10000000)
	{
		uint16_t data = ~((buffer[0] << 4) + (buffer[1] >> 4)) + 0b0000001;
		*tempValue = -0.0625 * data;
	}
	//���̑��i�v���X�j
	else
	{
		uint16_t data = (buffer[0] << 4) + (buffer[1] >> 4);
		*tempValue = 0.0625 * data;
	}
	return 1; //����
}

uint8_t my_i2c::HasSTC31C(void)
{
	if(_start_writing(STC31C_ADD) != I2C_ACKED)
	{
		_bus_stop();
		return 0; 
	}
	else 
	{
		_bus_stop();
		return 1;
	}
}

void my_i2c::InitializeSTC31C(void)
{	
	//�X���[�v������
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return; } //�X���[�v�R�}���h1
	if(_bus_write(0x77) != I2C_ACKED) { _bus_stop(); return; } //�X���[�v�R�}���h2
	_bus_stop();
}

uint8_t my_i2c::ReadSTC31C(float temperature, float relatvieHumid, uint16_t *co2Level)
{
	*co2Level = 0;
	
	//�v���[�u�����������ɂ͏���������Ă��܂����߁A�O�ׁ̈A����A�K�X�ݒ������
	uint8_t buffer[2];
	buffer[0] = 0x00;
	buffer[1] = 0x13;
	uint8_t rcrc = crc8((uint8_t*)buffer, 2);
	
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //����K�X��ʐݒ�R�}���h1
	if(_bus_write(0x15) != I2C_ACKED) { _bus_stop(); return 0; } //����K�X��ʐݒ�R�}���h2
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; } //����1�F��C���̓�_���Y�f(40%=400000ppm�܂�)
	if(_bus_write(0x13) != I2C_ACKED) { _bus_stop(); return 0; } //����2�F��C���̓�_���Y�f(40%=400000ppm�܂�)
	if(_bus_write(rcrc) != I2C_ACKED) { _bus_stop(); return 0; } //CRC
	_bus_stop();
	
	//�␳�p���x���L���X�g
	uint16_t tmp = (temperature * 200.0f);
	uint8_t tmps[2];
	tmps[0] = (uint8_t)(tmp >> 8);
	tmps[1] = (uint8_t)(tmp & 0xFF);
	rcrc = crc8((uint8_t*)tmps, 2);
	
	//���x�𑗐M
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //�␳�p���x���M�R�}���h1
	if(_bus_write(0x1E) != I2C_ACKED) { _bus_stop(); return 0; } //�␳�p���x���M�R�}���h2
	if(_bus_write(tmps[0]) != I2C_ACKED) { _bus_stop(); return 0; } //����1�F�������x�i��ʃr�b�g�j
	if(_bus_write(tmps[1]) != I2C_ACKED) { _bus_stop(); return 0; } //����2�F�������x�i���ʃr�b�g�j
	if(_bus_write(rcrc) != I2C_ACKED) { _bus_stop(); return 0; } //CRC
	_bus_stop();
	
	//�␳�p���x���L���X�g
	uint16_t hmd = (relatvieHumid * 65535 / 100);
	uint8_t hmds[2];
	hmds[0] = (uint8_t)(hmd >> 8);
	hmds[1] = (uint8_t)(hmd & 0xFF);
	rcrc = crc8((uint8_t*)hmds, 2);
	
	//���x�𑗐M
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //�␳�p���x���M�R�}���h1
	if(_bus_write(0x24) != I2C_ACKED) { _bus_stop(); return 0; } //�␳�p���x���M�R�}���h2
	if(_bus_write(hmds[0]) != I2C_ACKED) { _bus_stop(); return 0; } //����1�F���Ύ��x�i��ʃr�b�g�j
	if(_bus_write(hmds[1]) != I2C_ACKED) { _bus_stop(); return 0; } //����2�F���Ύ��x�i���ʃr�b�g�j
	if(_bus_write(rcrc) != I2C_ACKED) { _bus_stop(); return 0; } //CRC
	_bus_stop();
	
	//�C���␳�����邪�v�����Ă��Ȃ����߁A�f�t�H���g��101.3kPa�ƂȂ�
	//***
	
	//�v���R�}���h���M
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //�v���R�}���h1
	if(_bus_write(0x39) != I2C_ACKED) { _bus_stop(); return 0; } //�v���R�}���h2
		
	//�v���ɂ͍ő��110ms�K�v
	_delay_ms(110);
	
	//�v���l��M
	if(_start_reading(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; } //�v���l���Ȃ��ꍇ�ɂ�NACK����M
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //�v���l��ʃr�b�g
	if(_bus_read(1, 0, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //�v���l���ʃr�b�g
	if(_bus_read(0, 1, &rcrc) != I2C_SUCCESS) { _bus_stop(); return 0; } //CRC, NACK�Ŏ�M���I��������
	
	//CRC�`�F�b�N
	if(crc8((uint8_t*)buffer, 2) != rcrc) return 0;

	// 16�r�b�g�̐����l�ɍč\��
	*co2Level = (uint16_t)(buffer[0] << 8) | buffer[1];
	*co2Level = (uint16_t)(((float)(*co2Level - 16384) / 32768.0f) * 1000000.0f);

	//�X���[�v������
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //�X���[�v�R�}���h1
	if(_bus_write(0x77) != I2C_ACKED) { _bus_stop(); return 0; } //�X���[�v�R�}���h2
	_bus_stop();

	return 1;
}