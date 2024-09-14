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

//AHT20のアドレス (0x38=0b00111000; 0x70=0b01110000)
const uint8_t AHT20_ADD = 0x38 << 1;

//P3T1750DPのアドレス（0x24=0b10010000; 0x48=0b01001000, A0=A1=A2=GND）
const uint8_t P3T1750DP_ADD = 0x48 << 1;

//STC31Cのアドレス
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
 
//巡回冗長検査値を生成
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
	float tRise = 0.000000001 * (IS_STANDARD_MODE ? 1000 : 250); //Rise time [sec]
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
}

void my_i2c::InitializeVCNL4030(void){
	//照度計・距離計を無効にする//この処理は不要かもしれない
	//照度計測
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return; } //照度計測設定コマンド
	if(_bus_write(0b00010001) != I2C_ACKED) { _bus_stop(); return; } //000 1 00 0 1: 計測レベル, ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測無効
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return; } //000000 0 1: reserved, 感度1倍, White channel無効（この機能はよくわからん）
	_bus_stop();
	
	//距離計測
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return; } //距離計測設定コマンド1(PS_CONF1, PS_CONF2)
	if(_bus_write(0b11001111) != I2C_ACKED) { _bus_stop(); return; } //11 00 111 1: Duty ratio=1/320, 割込回数は毎回, Integration time=8T(400us), 距離計測無効
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();
}

float my_i2c::ReadVCNL4030_ALS(void)
{	
	//照度センサを有効にする
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; } //照度計測設定コマンド
	if(_bus_write(0b00010000) != I2C_ACKED) { _bus_stop(); return 0; } //000 1 00 0 0: 計測レベル(50ms), ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測有効
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return 0; } //000000 0 1: reserved, 感度1倍, White channel無効（この機能はよくわからん）
	_bus_stop();
	_delay_ms(100); //計測レベル50msなので、安全を見てその倍、待機
	
	//照度読み取り
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x0B) != I2C_ACKED) { _bus_stop(); return 0; } //照度計測コマンド
	//stopせずに続けて送信
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
	uint16_t data = (buffer[1] << 8) + buffer[0];
	
	//照度センサを無効にする
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; } //照度計測設定コマンド
	if(_bus_write(0b00010001) != I2C_ACKED) { _bus_stop(); return 0; } //000 1 00 0 1: 計測レベル(50ms), ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測無効
	if(_bus_write(0b00000011) != I2C_ACKED) { _bus_stop(); return 0; } //000000 0 1: reserved, 感度1倍, White channel無効（この機能はよくわからん）
	_bus_stop();
	
	return 0.064 * 4 * data; //ダイナミックレンジ2倍、感度1倍設定のため:2/(1/2)=4
}

float my_i2c::ReadVCNL4030_PS(void)
{
	//1回のみの読み取りのため、Active Force Modeにする
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x04) != I2C_ACKED) { _bus_stop(); return 0; } //距離計測設定コマンド
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 1 0 0 0
	if(_bus_write(0b00000000) != I2C_ACKED) { _bus_stop(); return 0; } //0 00 0 0 000
	_bus_stop();
	//距離計測を有効にする
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return 0; } //距離計測設定コマンド1(PS_CONF1, PS_CONF2)
	if(_bus_write(0b11001110) != I2C_ACKED) { _bus_stop(); return 0; } //11 00 111 0: Duty ratio=1/320, 割込回数は毎回, Integration time=8T(400us), 距離計測有効
	if(_bus_write(0b00001000) != I2C_ACKED) { _bus_stop(); return 0; } //00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	_bus_stop();
	_delay_ms(150); //技術資料によると8Tの場合には128msのようだが。。。
	
	//距離読み取り
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x08) != I2C_ACKED) { _bus_stop(); return 0; } //距離計測コマンド
	//stopせずに続けて送信
	if(_start_reading(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	uint8_t buffer[2];
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACK
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACK
	uint16_t data = (buffer[1] << 8) + buffer[0];
	
	//距離計測を無効にする
	if(_start_writing(VCNL_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x03) != I2C_ACKED) { _bus_stop(); return 0; } //距離計測設定コマンド1(PS_CONF1, PS_CONF2)
	if(_bus_write(0b11001111) != I2C_ACKED) { _bus_stop(); return 0; } //11 00 111 1: Duty ratio=1/320, 割込回数は毎回, Integration time=8T(400us), 距離計測無効
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
			//debug用
			//volatile uint8_t xxx = client_address;
		}
		_bus_stop();
		_delay_ms(10);
	}
}

void my_i2c::InitializeAHT20(void)
{
	//Statusが0x18以外の場合にはリセット
	if((ReadAHT20Status()&0x18)!=0x18)
	{
		//レジスタ初期化
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
	
	//初期化コマンド(送信後10ms待つ)
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
	
	if((ReadAHT20Status()&0x18)!=0x18) //Statusが0x18以外の場合にはリセット
	{
		//レジスタ初期化
		ResetAHT20(0x1b);
		ResetAHT20(0x1c);
		ResetAHT20(0x1e);
		_delay_ms(10);
	}
	
	//測定命令(計測終了まで80ms必要)
	if(_start_writing(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0xAC) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x33) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; }
	_bus_stop();
	_delay_ms(80);
	
	uint16_t cnt = 0;
	while(((ReadAHT20Status()&0x80)==0x80)) //bit[7]=1の間はbusy
	{
		_delay_ms(2);
		if(cnt++>=100) break;
	}
	
	//測定値を受信
	if(_start_reading(AHT20_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
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
	if(_bus_write(0b00000001) != I2C_ACKED) { _bus_stop(); return; } //Configuration registerを操作するためのポインタレジスタ（01）を書き込む
	if(_bus_write(0b00101001) != I2C_ACKED) { _bus_stop(); return; } //Shutdownモードとする。その他のビットはデフォルト（55msで計測）
	_bus_stop();
}

uint8_t my_i2c::ReadP3T1750DP(float *tempValue)
{
	*tempValue = -99;
	
	//Shutdownモードから起こして1回のみ計測させる
	if(_start_writing(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0b00000001) != I2C_ACKED) { _bus_stop(); return 0; } //Configuration registerを操作するためのポインタレジスタ（01）を書き込む
	if(_bus_write(0b10101001) != I2C_ACKED) { _bus_stop(); return 0; } //One-Shot計測
	_bus_stop();
	
	_delay_ms(55); //計測のために55msのお休み
	
	//温度を読み取る
	uint8_t buffer[2];
	if(_start_writing(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0b00000000) != I2C_ACKED) { _bus_stop(); return 0; } //Temperature registerを操作するためのポインタレジスタ（00）を書き込む
	if(_start_reading(P3T1750DP_ADD) != I2C_ACKED) { _bus_stop(); return 0; } //Restart
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //ACKで継続
	if(_bus_read(0, 1, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //NACKで終了
	
	//MSBの最上位が1の場合（マイナス）
	if(buffer[0] & 0b10000000)
	{
		uint16_t data = ~((buffer[0] << 4) + (buffer[1] >> 4)) + 0b0000001;
		*tempValue = -0.0625 * data;
	}
	//その他（プラス）
	else
	{
		uint16_t data = (buffer[0] << 4) + (buffer[1] >> 4);
		*tempValue = 0.0625 * data;
	}
	return 1; //成功
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
	//スリープさせる
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return; } //スリープコマンド1
	if(_bus_write(0x77) != I2C_ACKED) { _bus_stop(); return; } //スリープコマンド2
	_bus_stop();
}

uint8_t my_i2c::ReadSTC31C(float temperature, float relatvieHumid, uint16_t *co2Level)
{
	*co2Level = 0;
	
	//プローブ抜き差し時には初期化されてしまうため、念の為、毎回、ガス設定をする
	uint8_t buffer[2];
	buffer[0] = 0x00;
	buffer[1] = 0x13;
	uint8_t rcrc = crc8((uint8_t*)buffer, 2);
	
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //測定ガス種別設定コマンド1
	if(_bus_write(0x15) != I2C_ACKED) { _bus_stop(); return 0; } //測定ガス種別設定コマンド2
	if(_bus_write(0x00) != I2C_ACKED) { _bus_stop(); return 0; } //引数1：空気中の二酸化炭素(40%=400000ppmまで)
	if(_bus_write(0x13) != I2C_ACKED) { _bus_stop(); return 0; } //引数2：空気中の二酸化炭素(40%=400000ppmまで)
	if(_bus_write(rcrc) != I2C_ACKED) { _bus_stop(); return 0; } //CRC
	_bus_stop();
	
	//補正用温度をキャスト
	uint16_t tmp = (temperature * 200.0f);
	uint8_t tmps[2];
	tmps[0] = (uint8_t)(tmp >> 8);
	tmps[1] = (uint8_t)(tmp & 0xFF);
	rcrc = crc8((uint8_t*)tmps, 2);
	
	//温度を送信
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //補正用温度送信コマンド1
	if(_bus_write(0x1E) != I2C_ACKED) { _bus_stop(); return 0; } //補正用温度送信コマンド2
	if(_bus_write(tmps[0]) != I2C_ACKED) { _bus_stop(); return 0; } //引数1：乾球温度（上位ビット）
	if(_bus_write(tmps[1]) != I2C_ACKED) { _bus_stop(); return 0; } //引数2：乾球温度（下位ビット）
	if(_bus_write(rcrc) != I2C_ACKED) { _bus_stop(); return 0; } //CRC
	_bus_stop();
	
	//補正用湿度をキャスト
	uint16_t hmd = (relatvieHumid * 65535 / 100);
	uint8_t hmds[2];
	hmds[0] = (uint8_t)(hmd >> 8);
	hmds[1] = (uint8_t)(hmd & 0xFF);
	rcrc = crc8((uint8_t*)hmds, 2);
	
	//湿度を送信
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //補正用湿度送信コマンド1
	if(_bus_write(0x24) != I2C_ACKED) { _bus_stop(); return 0; } //補正用湿度送信コマンド2
	if(_bus_write(hmds[0]) != I2C_ACKED) { _bus_stop(); return 0; } //引数1：相対湿度（上位ビット）
	if(_bus_write(hmds[1]) != I2C_ACKED) { _bus_stop(); return 0; } //引数2：相対湿度（下位ビット）
	if(_bus_write(rcrc) != I2C_ACKED) { _bus_stop(); return 0; } //CRC
	_bus_stop();
	
	//気圧補正もあるが計測していないため、デフォルトの101.3kPaとなる
	//***
	
	//計測コマンド送信
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //計測コマンド1
	if(_bus_write(0x39) != I2C_ACKED) { _bus_stop(); return 0; } //計測コマンド2
		
	//計測には最大で110ms必要
	_delay_ms(110);
	
	//計測値受信
	if(_start_reading(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; } //計測値がない場合にはNACKを受信
	if(_bus_read(1, 0, &buffer[0]) != I2C_SUCCESS) { _bus_stop(); return 0; } //計測値上位ビット
	if(_bus_read(1, 0, &buffer[1]) != I2C_SUCCESS) { _bus_stop(); return 0; } //計測値下位ビット
	if(_bus_read(0, 1, &rcrc) != I2C_SUCCESS) { _bus_stop(); return 0; } //CRC, NACKで受信を終了させる
	
	//CRCチェック
	if(crc8((uint8_t*)buffer, 2) != rcrc) return 0;

	// 16ビットの整数値に再構成
	*co2Level = (uint16_t)(buffer[0] << 8) | buffer[1];
	*co2Level = (uint16_t)(((float)(*co2Level - 16384) / 32768.0f) * 1000000.0f);

	//スリープさせる
	if(_start_writing(STC31C_ADD) != I2C_ACKED) { _bus_stop(); return 0; }
	if(_bus_write(0x36) != I2C_ACKED) { _bus_stop(); return 0; } //スリープコマンド1
	if(_bus_write(0x77) != I2C_ACKED) { _bus_stop(); return 0; } //スリープコマンド2
	_bus_stop();

	return 1;
}