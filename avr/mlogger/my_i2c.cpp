/**
 * @file my_i2c.cpp
 * @brief AVRxxシリーズでI2C通信する
 *  参考1: https://github.com/microchip-pic-avr-examples/avr128da48-cnano-i2c-send-receive-mplabx
 *  参考2: https://www.avrfreaks.net/forum/aht20-sensor-and-i2c?skey=aht20
 * @author E.Togashi
 * @date 2020/12/25
 */

#include "my_i2c.h"
#include <avr/io.h>
#include <util/delay.h>

//VCNLのアドレス。同部品には4種のアドレスがあるので型番に注意。
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
 
//書き込み終了を待つ
static uint8_t _i2c_WaitW(void)
 {
	 uint8_t state = I2C_INIT;
	 do
	 {
		 //書き込みもしくは読み込み完了フラグを監視
		 if(TWI1.MSTATUS & (TWI_WIF_bm | TWI_RIF_bm))
		 //if(TWI1.MSTATUS & TWI_WIF_bm)
		 {
			 //ACKを受け取った場合
			 if(!(TWI1.MSTATUS & TWI_RXACK_bm)) state = I2C_ACKED;
			 //ACKを受け取らなかった場合
			 else state = I2C_NACKED;
		 }
		 //エラー発生フラグを監視
		 else if(TWI1.MSTATUS & (TWI_BUSERR_bm | TWI_ARBLOST_bm)) state = I2C_ERROR;
	 } while(!state);
	 
	 return state;
 }

//読み込み終了を待つ
static uint8_t _i2c_WaitR(void)
 {
	 uint8_t state = I2C_INIT;
	 do
	 {
		 //書き込みもしくは読み込み完了フラグを監視
		 if(TWI1.MSTATUS & (TWI_WIF_bm | TWI_RIF_bm)) state = I2C_READY;
		 //if(TWI1.MSTATUS & TWI_RIF_bm) state = I2C_READY;
		 //エラー発生フラグを監視
		 else if(TWI1.MSTATUS & (TWI_BUSERR_bm | TWI_ARBLOST_bm)) state = I2C_ERROR;
	 } while(!state);
	 
	 return state;
 }
 
//I2C通信（書き込み）開始
static uint8_t _start_writing(uint8_t address)
{
	TWI1.MADDR = address & ~0x01; //Write動作の場合、1桁目は0
	
	//while(TWI_RXACK_bm & TWI1.MSTATUS);
	//return 1;
	return _i2c_WaitW();
}

//I2C通信（読み込み）開始
static uint8_t _start_reading(uint8_t address)
{
	TWI1.MADDR = address | 0x01; //Read動作の場合、1桁目は1
	
	//while(TWI_RXACK_bm & TWI1.MSTATUS);
	//return 1;
	return _i2c_WaitW();
}

//I2C通信の終了
static void _bus_stop(void)
{
	TWI1.MCTRLB = TWI_ACKACT_bm | TWI_MCMD_STOP_gc; //NACK
}

//Write処理（マスタからスレーブへの送信）
static uint8_t _bus_write(uint8_t data)
{
	TWI1.MDATA = data;
	return _i2c_WaitW();
}

//Read処理（スレーブからマスタへの送信）
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
	//エラー処理
	else return rslt;
}
 
//巡回冗長検査値を生成1
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

//巡回冗長検査値を生成2
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
 
//以下、publicメソッド*************************************

void my_i2c::InitializeI2C(void)
{
	// TWI通信のPIN設定 : SDA->PF2, SCL->PF3
	PORTMUX.TWIROUTEA = 0x00;
	
	// 動作モードの設定
	TWI1.CTRLA &= ~TWI_FMPEN_bm; //デフォルト（Standard もしくは Fast）
	
	// SDA hold time（SCLがLowになった後、どれだけSDA信号を維持するか）
	TWI1.CTRLA |= TWI_SDAHOLD_50NS_gc; //50ns
	
	// ボーレートの設定（周波数が決まる）
	//AVR32DB32のデータシート:Clock Generationより
	const bool IS_STANDARD_MODE = true;
	float fScl = IS_STANDARD_MODE ? 100000 : 400000; //周波数[Hz]
	float tRise = 0.000000001 * (IS_STANDARD_MODE ? 1000 : 300); //Rise time [sec]
	float tOf = 0.000000001 * 250;
	float baud = (uint8_t)(((float)F_CPU / (2 * fScl)) - 5 - ((float)F_CPU * tRise / 2));
	float tLow = (baud + 5) / F_CPU - tOf;
	float tLowM = 0.000000001 * (IS_STANDARD_MODE ? 4700 : 1300);
	if(tLow < tLowM) baud = F_CPU * (tLowM + tOf) - 5;
	TWI1.MBAUD = baud;
	
	// アドレスレジスタ、データレジスタを初期化
	TWI1.MADDR = 0x00;
	TWI1.MDATA = 0x00;

	TWI1.MCTRLA |= TWI_ENABLE_bm		// TWIの有効化
				| TWI_TIMEOUT_200US_gc; //200uSの通信不良でSkip	

	TWI1.MSTATUS = TWI_BUSSTATE_IDLE_gc; //バスをIDLE状態にする
	TWI1.MSTATUS |= TWI_WIF_bm | TWI_CLKHOLD_bm; //フラグクリア

	TWI1.MCTRLB |= TWI_FLUSH_bm; //通信状態を初期化
	
	//照度計測設定//設定は変えないので初期化時のみ呼び出し
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; } //照度計測設定コマンド
	if(_bus_write(0b00010000) != I2C_ACKED) { _bus_stop(); return; } //000 1 00 0 0: 計測レベル, ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測有効（常に有効で電力消費は問題ないか？）
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return; } //000000 0 1: reserved, 感度1倍, White channel無効（この機能はよくわからん）
	_bus_stop();
	
	//距離計測設定
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return; } //距離計測設定コマンド
	if(_bus_write(0b11001100) != I2C_ACKED) { _bus_stop(); return; } //11 00 111 0: Duty ratio=1/320, 割込回数は毎回, Integration time=4T(200ms), 距離計測有効（常に有効で電力消費は問題ないか？）
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();
}

void my_i2c::InitializeOPT(uint8_t add)
{
	//Configuration
	_start_writing(add); //OPTxxxxのアドレス
	_bus_write(0x01); //Configuration要求
	_bus_write(0b11001110); //0b 1100 1 11 0 //automatic full-scale, 800ms, continuous conversions, read only field
	_bus_write(0b00000000); //0b 0 0 0 0 0 00//read only field * 3, hysteresis-style,
	_bus_stop();
	_delay_ms(1); //必要な待機時間は技術資料から読み取れず
	
	//OPTxxxxのResister Address をResultに設定
	//再設定するまで維持されるため、初期化時に設定してしまう
	_start_writing(add); //OPTxxxxのアドレス
	_bus_write(0x00); //Result要求
	_bus_stop();
	_delay_ms(1); //必要な待機時間は技術資料から読み取れず
}

uint8_t my_i2c::ReadAM2320(float* tempValue, float* humiValue)
{
	const uint8_t AM_ADD = 0xB8; //AM2320のアドレス（0xB8=0b10111000）
	uint8_t buffer[8];
	
	//スリープ状態から起こす。ACKは取得できない
	_start_writing(AM_ADD);
	_delay_ms(1);
	_bus_stop();
	
	//コマンド送信前処理// SLA + address (0xB8) + starting address(0x00) + register length(0x04)	
	_start_writing(AM_ADD);
	_bus_write(0x03);
	_bus_write(0x00);
	_bus_write(0x04);
	_bus_stop();
	_delay_ms(1); //1.5ms以上の待機！！！

	_start_reading(AM_ADD);
	_delay_us(50); //30us以上の待機
	for (uint8_t i = 0; i<7; i++) {		
		_bus_read(1, 0, &buffer[i]); //読んでACK
	}
	_bus_read(0, 1, &buffer[7]); //読んでNACK
	//_bus_stop();
	
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
	*humiValue = -99;
	*tempValue = -99;
	
	const uint8_t AHT_ADD = 0x38 << 1; //AHT20のアドレス（0x38=0b00111000）
	uint8_t buffer[7];
	
	//初期化コマンド(送信後10ms待つ)
	if(_start_writing(AHT_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0xBE) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; }
	_bus_stop();
	_delay_ms(10);
	
	//測定命令(計測終了まで80ms必要)
	if(_start_writing(AHT_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0xAC) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x33) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; }
	_bus_stop();
	_delay_ms(80);
			
	//測定値を受信
	if(_start_reading(AHT_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:状態
	//Busyの場合
	if((buffer[0] & (1<<7))) { _bus_stop(); return 0; }
	else
	{
		if(_bus_read(1, 0, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:相対湿度1
		if(_bus_read(1, 0, &buffer[2]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:相対湿度2
		if(_bus_read(1, 0, &buffer[3]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:相対湿度3,乾球温度1
		if(_bus_read(1, 0, &buffer[4]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:乾球温度2
		if(_bus_read(1, 0, &buffer[5]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK:乾球温度3
		if(_bus_read(0, 1, &buffer[6]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK:CRC
		
		//CRC8をチェック
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
	
	_start_reading(add); //OPTxxxxのアドレス
	_delay_us(50); //待機すべき時間は不明//AutoScaleで10ms×レンジ変更回数の時間が必要の模様。レンジは12段階なので最大で110ms+αか？
	_bus_read(1, 0, &buffer[0]); //ACK
	_bus_read(0, 1, &buffer[1]); //NACK
	
	//Luxに変換
	int expnt = (0b11110000 & buffer[0]) >> 4; //上位4bitがレンジを表す
	int val = ((0b00001111 & buffer[0]) << 8) + buffer[1]; //下位12bitは値を表す
	float lux = 0.01 * pow(2, expnt) * val;
	
	if(83865.60 < lux) return 0; //エラー時は0とする
	else return lux;
}

float my_i2c::ReadVCNL4030_ALS(void)
{
	//照度読み取り
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x0B) != I2C_ACKED) { _bus_stop(); return 0; } //照度計測コマンド
	//stopせずに続けて送信
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
		
	uint16_t data = (buffer[1] << 8) + buffer[0];
	return 0.064 * 4 * data; //ダイナミックレンジ2倍、感度1倍設定のため:2/(1/2)=4
}

uint16_t my_i2c::ReadVCNL4030_PS(void)
{
	//1回のみの読み取りのため、Active Force Modeを使う
	/*if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x04) != I2C_ACKED) { _bus_stop(); return 0; } //距離計測設定コマンド
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 1 0 0 0
	if(_bus_write(0b00000000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 0 000
	_bus_stop();*/
	
	//距離読み取り
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return 0; } //距離計測コマンド
	//stopせずに続けて送信
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
			//debug用
			//volatile uint8_t xxx = client_address;
		}
		_bus_stop();
		_delay_ms(10);
	}	
	
}
	
