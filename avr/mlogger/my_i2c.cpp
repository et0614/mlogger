/**
 * @file my_i2c.cpp
 * @brief AVR(ATMega328)でI2C通信を行う（AM2320とOPT3001）
 *  参考1：http://cjtsx.blogspot.jp/2016/07/am2320-library-for-avrs-without.html
 *  参考2：https://www.avrfreaks.net/forum/aht20-sensor-and-i2c?skey=aht20
 * @author E.Togashi
 * @date 2020/12/25
 */

#include "my_i2c.h"
#include <util/delay.h>
 
/************************************************************************/
/* Port functions                                                       */
/************************************************************************/
 
//AVRマイコンのI2C関連のピン情報を設定
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

//I2C通信の開始
static void _bus_start(void)
{
	//SCL-H + SDA-Lで開始
	SCL_HIGH;
	SDA_LOW;
	//待機してSCLをLに
	_bus_delay();
	SCL_SetLow();
}

//I2C通信の終了
static void _bus_stop(void)
{
	//SCL-HのときにSDAがLからHに変更で終了
	SDA_LOW;
	_bus_delay();
	SCL_HIGH;
	_bus_q_delay();
	SDA_HIGH;
	_bus_delay();
}

//Write処理（マスタからスレーブへの送信）
static uint8_t _bus_write(uint8_t data)
{
	//最上位bit（MSB）から順に送信
	for (uint8_t i = 0; i<8; i++) {
		SCL_SetLow(); //SCLを一旦下げて。。。
 
		//最上位bitの1,0判定
		if (data & 0x80) SDA_HIGH;
		else SDA_LOW;
		_bus_delay();
 
		SCL_SetHigh(); //用意ができたらSCLを上げる
		SCL_WAIT_HIGH;
		
		//最上位bitをシフト（次のbitへ）
		data <<= 1;
	} 
	SCL_SetLow();
	SDA_HIGH;
	_bus_delay();
 
	//スレーブのACKを待つ
	SCL_HIGH;
	SCL_WAIT_HIGH; // wait for slave ack
	_bus_delay();
	bool ack = !(I2C_PIN & _BV(I2C_SDA_BIT));
	SCL_SetLow();
 
	return (uint8_t)ack;
}

//Read処理（スレーブからマスタへの送信）
static uint8_t _bus_read(bool sendAck)
{
	uint8_t data = 0;
 
	//最上位bit（MSB）から順に受信
	for (uint8_t i=0; i<8; i++) {
		SCL_SetLow();
		SCL_SetHigh();
 
		SCL_WAIT_HIGH;
 
		if (I2C_PIN & _BV(I2C_SDA_BIT)) data |= (0x80 >> i);
	}
	SCL_SetLow();
 
	//ACK or NACKを送信
	if (sendAck) SDA_LOW;
	else SDA_HIGH;
	_bus_delay();
 
	SCL_SetHigh();
	SCL_SetLow();
 
	SDA_HIGH;
	_bus_q_delay();
 
	return data;
}
 
//巡回冗長検査値を生成
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

//初期化処理OPTxxx
void my_i2c::InitializeOPT(uint8_t add)
{
	//Configuration
	_bus_start();
	_bus_write(add + 0); //OPTxxxxのアドレス
	_bus_write(0x01); //Configuration要求
	_bus_write(0b11001110); //0b 1100 1 11 0 //automatic full-scale, 800ms, continuous conversions, read only field
	_bus_write(0b00000000); //0b 0 0 0 0 0 00//read only field * 3, hysteresis-style,
	_bus_stop();
	_delay_ms(1); //必要な待機時間は技術資料から読み取れず
	
	//OPTxxxxのResister Address をResultに設定
	//再設定するまで維持されるため、初期化時に設定してしまう
	_bus_start();
	_bus_write(add + 0); //OPTxxxxのアドレス
	_bus_write(0x00); //Result要求
	_bus_stop();
	_delay_ms(1); //必要な待機時間は技術資料から読み取れず
}

uint8_t my_i2c::ReadAM2320(float* tempValue, float* humiValue)
{
	uint8_t buffer[8];
	
	//スリープ状態から起こす。ACKは取得できない
	_bus_start();
	_bus_write(0xB8); //AM2320のアドレス（10111000）	
	_delay_ms(1);
	_bus_stop();
	
	//コマンド送信前処理// SLA + address (0xB8) + starting address(0x00) + register length(0x04)
	_bus_start();
	_bus_write(0xB8);
	_bus_write(0x03);
	_bus_write(0x00);
	_bus_write(0x04);
	_bus_stop();
	_delay_ms(1); //1.5ms以上の待機！！！
	
	_bus_start();
	_bus_write(0xB8 + 1); //Read命令（address + 1）
	_delay_us(50); //30us以上の待機
	for (uint8_t i = 0; i<7; i++) {
		buffer[i] = _bus_read(1); //読んでACK
	}
	buffer[7] = _bus_read(0); //読んでNACK
	_bus_stop();
	
	//CRC16をチェック
	uint16_t Rcrc = ((uint16_t)buffer[7] << 8)+buffer[6];
	if (Rcrc == crc16(buffer, 6)) {
		//温湿度データを復元
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
		//CRC不整合の場合
		*humiValue = -99.0;
		*tempValue = -99.0;
		return 0;
	}
}

uint8_t my_i2c::ReadAHT20(float* tempValue, float* humiValue)
{
	uint8_t buffer[7];
	
	_bus_start();
	_bus_write(0x70); //AHT20のアドレス（0x70,0b01110000）+ write(0)
	_bus_write(0xBE); //初期化
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
	_bus_write(0x70); //AHT20のアドレス（0x70,0b01110000）+ write(0)
	_bus_write(0xAC); //測定命令
	_bus_write(0x33);
	_bus_write(0x00);
	_bus_stop();
	_delay_ms(100);
		
	//測定値を受信
	_bus_start();
	_bus_write(0x70 + 1); //AHT20のアドレス（0x70,0b01110000）+ read(1)
	buffer[0]=_bus_read(1); //ACK:状態
	if(buffer[0] & (1<<7))
	{
		//計測未完了	
		*humiValue = -99;
		*tempValue = -99;
		_bus_stop();
		return 0;
	}
	else
	{
		buffer[1]=_bus_read(1); //ACK:相対湿度1
		buffer[2]=_bus_read(1); //ACK:相対湿度2
		buffer[3]=_bus_read(1); //ACK:相対湿度3,乾球温度1
		buffer[4]=_bus_read(1); //ACK:乾球温度2
		buffer[5]=_bus_read(1); //ACK:乾球温度3
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
	_bus_write(add + 1); //OPTxxxxのアドレスに、Read命令のために+1する
	_delay_us(50); //待機すべき時間は不明//AutoScaleで10ms×レンジ変更回数の時間が必要の模様。レンジは12段階なので最大で110ms+αか？
	buffer[0] = _bus_read(1); //ACK
	buffer[1] = _bus_read(0); //NACK
	_bus_stop();
	
	//Luxに変換
	int expnt = (0b11110000 & buffer[0]) >> 4; //上位4bitがレンジを表す
	int val = ((0b00001111 & buffer[0]) << 8) + buffer[1]; //下位12bitは値を表す
	float lux = 0.01 * pow(2, expnt) * val;
	
	if(83865.60 < lux) return 0; //エラー時は0とする
	else return lux;
}
