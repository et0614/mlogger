/**
 * @file my_i2c.cpp
 * @brief AVR(ATMega328)��I2C�ʐM���s���iAM2320��OPT3001�j
 *  �Q�l1�Fhttp://cjtsx.blogspot.jp/2016/07/am2320-library-for-avrs-without.html
 *  �Q�l2�Fhttps://www.avrfreaks.net/forum/aht20-sensor-and-i2c?skey=aht20
 * @author E.Togashi
 * @date 2020/12/25
 */

#include "my_i2c.h"
#include <util/delay.h>
 
/************************************************************************/
/* Port functions                                                       */
/************************************************************************/
 
//AVR�}�C�R����I2C�֘A�̃s������ݒ�
#define I2C_PORT PORTC
#define I2C_DDR DDRC
#define I2C_PIN PINC
#define I2C_SDA_BIT 4
#define I2C_SCL_BIT 5
 
#define SDA_LOW I2C_DDR |= _BV(I2C_SDA_BIT)
#define SDA_HIGH I2C_DDR &= ~_BV(I2C_SDA_BIT)
#define SCL_LOW I2C_DDR |= _BV(I2C_SCL_BIT)
#define SCL_HIGH I2C_DDR &= ~_BV(I2C_SCL_BIT)
#define SCL_IS_LOW (!(I2C_PIN & _BV(I2C_SCL_BIT)))
 
#define SCL_WAIT_HIGH while (!(I2C_PIN & _BV(I2C_SCL_BIT))) {}

static void _bus_delay(void)
{
	_delay_us(50);
}

//#ifdef USE_Q_DELAY
static void _bus_q_delay(void)
{
	_delay_us(30);
}
//#else
//#define _bus_q_delay() _bus_delay()
//#endif
 
static void SCL_SetHigh(void)
{
 SCL_HIGH;
 _bus_delay();
}
 
static void SCL_SetLow(void)
{
 SCL_LOW;
 _bus_delay();
}
 
/************************************************************************/
/* Bus functions                                                        */
/************************************************************************/

//I2C�ʐM�̊J�n
static void _bus_start(void)
{
	//SCL-H + SDA-L�ŊJ�n
	SCL_HIGH;
	SDA_LOW;
	//�ҋ@����SCL��L��
	_bus_delay();
	SCL_SetLow();
}

//I2C�ʐM�̏I��
static void _bus_stop(void)
{
	//SCL-H�̂Ƃ���SDA��L����H�ɕύX�ŏI��
	SDA_LOW;
	_bus_delay();
	SCL_HIGH;
	_bus_q_delay();
	SDA_HIGH;
	_bus_delay();
}

//Write�����i�}�X�^����X���[�u�ւ̑��M�j
static uint8_t _bus_write(uint8_t data)
{
	//�ŏ��bit�iMSB�j���珇�ɑ��M
	for (uint8_t i = 0; i<8; i++) {
		SCL_SetLow(); //SCL����U�����āB�B�B
 
		//�ŏ��bit��1,0����
		if (data & 0x80) SDA_HIGH;
		else SDA_LOW;
		_bus_delay();
 
		SCL_SetHigh(); //�p�ӂ��ł�����SCL���グ��
		SCL_WAIT_HIGH;
		
		//�ŏ��bit���V�t�g�i����bit�ցj
		data <<= 1;
	} 
	SCL_SetLow();
	SDA_HIGH;
	_bus_delay();
 
	//�X���[�u��ACK��҂�
	SCL_HIGH;
	SCL_WAIT_HIGH; // wait for slave ack
	_bus_delay();
	bool ack = !(I2C_PIN & _BV(I2C_SDA_BIT));
	SCL_SetLow();
 
	return (uint8_t)ack;
}

//Read�����i�X���[�u����}�X�^�ւ̑��M�j
static uint8_t _bus_read(bool sendAck)
{
	uint8_t data = 0;
 
	//�ŏ��bit�iMSB�j���珇�Ɏ�M
	for (uint8_t i=0; i<8; i++) {
		SCL_SetLow();
		SCL_SetHigh();
 
		SCL_WAIT_HIGH;
 
		if (I2C_PIN & _BV(I2C_SDA_BIT)) data |= (0x80 >> i);
	}
	SCL_SetLow();
 
	//ACK or NACK�𑗐M
	if (sendAck) SDA_LOW;
	else SDA_HIGH;
	_bus_delay();
 
	SCL_SetHigh();
	SCL_SetLow();
 
	SDA_HIGH;
	_bus_q_delay();
 
	return data;
}
 
//����璷�����l�𐶐�
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
 
void my_i2c::InitializeI2C(void)
{
	I2C_DDR &= ~(_BV(I2C_SCL_BIT)|_BV(I2C_SDA_BIT));
	I2C_PORT &= (_BV(I2C_SCL_BIT)|_BV(I2C_SDA_BIT));
}

//����������OPTxxx
void my_i2c::InitializeOPT(uint8_t add)
{
	//Configuration
	_bus_start();
	_bus_write(add + 0); //OPTxxxx�̃A�h���X
	_bus_write(0x01); //Configuration�v��
	_bus_write(0b11001110); //0b 1100 1 11 0 //automatic full-scale, 800ms, continuous conversions, read only field
	_bus_write(0b00000000); //0b 0 0 0 0 0 00//read only field * 3, hysteresis-style,
	_bus_stop();
	_delay_ms(1); //�K�v�ȑҋ@���Ԃ͋Z�p��������ǂݎ�ꂸ
	
	//OPTxxxx��Resister Address ��Result�ɐݒ�
	//�Đݒ肷��܂ňێ�����邽�߁A���������ɐݒ肵�Ă��܂�
	_bus_start();
	_bus_write(add + 0); //OPTxxxx�̃A�h���X
	_bus_write(0x00); //Result�v��
	_bus_stop();
	_delay_ms(1); //�K�v�ȑҋ@���Ԃ͋Z�p��������ǂݎ�ꂸ
}

uint8_t my_i2c::ReadAM2320(float* tempValue, float* humiValue)
{
	uint8_t buffer[8];
	
	//�X���[�v��Ԃ���N�����BACK�͎擾�ł��Ȃ�
	_bus_start();
	_bus_write(0xB8); //AM2320�̃A�h���X�i10111000�j	
	_delay_ms(1);
	_bus_stop();
	
	//�R�}���h���M�O����// SLA + address (0xB8) + starting address(0x00) + register length(0x04)
	_bus_start();
	_bus_write(0xB8);
	_bus_write(0x03);
	_bus_write(0x00);
	_bus_write(0x04);
	_bus_stop();
	_delay_ms(1); //1.5ms�ȏ�̑ҋ@�I�I�I
	
	_bus_start();
	_bus_write(0xB8 + 1); //Read���߁iaddress + 1�j
	_delay_us(50); //30us�ȏ�̑ҋ@
	for (uint8_t i = 0; i<7; i++) {
		buffer[i] = _bus_read(1); //�ǂ��ACK
	}
	buffer[7] = _bus_read(0); //�ǂ��NACK
	_bus_stop();
	
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
	uint8_t buffer[7];
	
	_bus_start();
	_bus_write(0x70); //AHT20�̃A�h���X�i0x70,0b01110000�j+ write(0)
	_bus_write(0xBE); //������
	_bus_write(0x08);
	_bus_write(0x00);
	_bus_stop();
	_delay_ms(10);
	
	_bus_start();
	_bus_write(0x70);
	_bus_write(0xF5);   // RH command no holding mode
	_delay_ms(20);
	_bus_stop();
	
	_bus_start();
	_bus_write(0x70);
	_bus_write(0xF3);   // Temp command no holding mode
	_delay_ms(20);
	_bus_stop();
	
	_bus_start();
	_bus_write(0x70); //AHT20�̃A�h���X�i0x70,0b01110000�j+ write(0)
	_bus_write(0xAC); //���薽��
	_bus_write(0x33);
	_bus_write(0x00);
	_bus_stop();
	_delay_ms(100);
		
	//����l����M
	_bus_start();
	_bus_write(0x70 + 1); //AHT20�̃A�h���X�i0x70,0b01110000�j+ read(1)
	buffer[0]=_bus_read(1); //ACK:���
	if(buffer[0] & (1<<7))
	{
		//�v��������	
		*humiValue = -99;
		*tempValue = -99;
		_bus_stop();
		return 0;
	}
	else
	{
		buffer[1]=_bus_read(1); //ACK:���Ύ��x1
		buffer[2]=_bus_read(1); //ACK:���Ύ��x2
		buffer[3]=_bus_read(1); //ACK:���Ύ��x3,�������x1
		buffer[4]=_bus_read(1); //ACK:�������x2
		buffer[5]=_bus_read(1); //ACK:�������x3
		buffer[6]=_bus_read(0); //NACK:CRC
		_bus_stop();
		
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
}

float my_i2c::ReadOPT(uint8_t add)
{
	uint8_t buffer[2];
	
	_bus_start();
	_bus_write(add + 1); //OPTxxxx�̃A�h���X�ɁARead���߂̂��߂�+1����
	_delay_us(50); //�ҋ@���ׂ����Ԃ͕s��//AutoScale��10ms�~�����W�ύX�񐔂̎��Ԃ��K�v�̖͗l�B�����W��12�i�K�Ȃ̂ōő��110ms+�����H
	buffer[0] = _bus_read(1); //ACK
	buffer[1] = _bus_read(0); //NACK
	_bus_stop();
	
	//Lux�ɕϊ�
	int expnt = (0b11110000 & buffer[0]) >> 4; //���4bit�������W��\��
	int val = ((0b00001111 & buffer[0]) << 8) + buffer[1]; //����12bit�͒l��\��
	float lux = 0.01 * pow(2, expnt) * val;
	
	if(83865.60 < lux) return 0; //�G���[����0�Ƃ���
	else return lux;
}
