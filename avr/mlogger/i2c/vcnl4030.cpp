/*
 * vcnl4030.cpp
 *
 * Created: 2025/06/16 12:31:44
 *  Author: etoga
 */ 

#include "vcnl4030.h"
#include <math.h>
#include <util/delay.h>

//VCNL4030のアドレス。同部品には4種のアドレスがあるので型番に注意。
const uint8_t VCNL_ADD = 0x60;

// VCNL4030のコマンド/レジスタアドレス
const uint8_t CMD_ALS_CONF   = 0x00; // 照度センサー設定
const uint8_t CMD_PS_CONF    = 0x03; // 近接センサー設定(CONF1, CONF2)
const uint8_t CMD_PS_CONF3   = 0x04; // 近接センサー設定(CONF3, MS)
const uint8_t CMD_PS_DATA    = 0x08; // 近接センサーデータ
const uint8_t CMD_ALS_DATA   = 0x0B; // 照度センサーデータ

bool vcnl4030::Initialize(void){
	// 照度センサー(ALS)の設定を書き込む
	const uint8_t als_config[] = {
		CMD_ALS_CONF,
		0b00010001, // ALS_CONF1: 000 1 00 0 1: 計測レベル, ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測無効
		0b00000011  // ALS_CONF2: 感度設定
	};
	if (!i2c_driver::Write(VCNL_ADD, als_config, sizeof(als_config)))
		return false; // 通信失敗

	// 近接センサー(PS)の設定を書き込む
	const uint8_t ps_config[] = {
		CMD_PS_CONF,
		0b11001111, // PS_CONF1: 11 00 111 1: Duty ratio=1/320, 割込回数は毎回, Integration time=8T(400us), 距離計測無効
		0b00001000  // PS_CONF2: 00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	};
	if (!i2c_driver::Write(VCNL_ADD, ps_config, sizeof(ps_config)))
		return false; // 通信失敗

	return true; // 成功
}

bool vcnl4030::ReadALS(float * als)
{
	//照度センサを有効にする
	const uint8_t enable_als[] = {
		CMD_ALS_CONF,	//照度計測設定コマンド
		0b00010000,		//ALS_CONF1: 000 1 00 0 0: 計測レベル(50ms), ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測有効
		0b00000011		//ALS_CONF2: 000000 1 1: reserved, 感度1倍, White channel無効（この機能はよくわからん）
	};
	if (!i2c_driver::Write(VCNL_ADD, enable_als, sizeof(enable_als)))
		return false; // 通信失敗
	
	_delay_ms(100); //計測レベル50msなので、安全を見てその倍、待機
	
	// 照度データレジスタ(0x0B)を指定し、2バイトのデータを読み込む
	const uint8_t read_command = CMD_ALS_DATA;
	uint8_t buffer[2];
	if (!i2c_driver::WriteRead(VCNL_ADD, &read_command, 1, buffer, sizeof(buffer))) 
		return false; // 通信失敗
	
	// 照度センサーを無効に戻す
	const uint8_t disable_als[] = { 
		CMD_ALS_CONF,	//照度計測設定コマンド
		0b00010001,		//ALS_CONF1: 000 1 00 0 1: 計測レベル(50ms), ダイナミックレンジ2倍, 割込回数は毎回, 割込無効, 照度計測無効
		0b00000011		//ALS_CONF2:000000 1 1: reserved, 感度1倍, White channel無効（この機能はよくわからん）
	};
	if (!i2c_driver::Write(VCNL_ADD, disable_als, sizeof(disable_als)))
		return false; // 通信失敗

	// 5. 読み取ったデータを照度[lx]に変換
	uint16_t data = (buffer[1] << 8) | buffer[0];
	*als = 0.064f * 4.0f * data; // ダイナミックレンジ2倍、感度1倍設定のため: 2/(1/2)=4

	return true;
}

bool vcnl4030::ReadPS(float * ps)
{
	*ps = 0; // 初期化

	// 1回のみの測定のため、Active Force Modeに設定する (Write API)
	const uint8_t force_mode[] = { 
		CMD_PS_CONF3,	//距離計測設定コマンド
		0b00001000,		//PS_CONF1:0 00 0 1 0 0 0
		0b00000000		//PS_CONF2:0 00 0 0 000
	};
	if (!i2c_driver::Write(VCNL_ADD, force_mode, sizeof(force_mode)))
		return false; // 通信失敗
	
	// 距離計測を有効にする (Write API)
	const uint8_t enable_ps[] = { 
		CMD_PS_CONF,	//距離計測設定コマンド
		0b11001110,		//PS_CONF1:11 00 111 0: Duty ratio=1/320, 割込回数は毎回, Integration time=8T(400us), 距離計測有効
		0b00001000		//PS_CONF2:00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	};
	if (!i2c_driver::Write(VCNL_ADD, enable_ps, sizeof(enable_ps)))
		return false; // 通信失敗

	// 測定時間を待つ: 技術資料によると8Tの場合には128usのようだが。。。
	_delay_ms(150);

	// 距離データレジスタ(0x08)を指定し、2バイトのデータを読み込む
	const uint8_t read_command = CMD_PS_DATA;
	uint8_t buffer[2];
	if (!i2c_driver::WriteRead(VCNL_ADD, &read_command, 1, buffer, sizeof(buffer)))
		return false; // 通信失敗

	// 距離計測を無効に戻す (Write API)
	const uint8_t disable_ps[] = { 
		CMD_PS_CONF,	//距離計測設定コマンド
		0b11001111,		//PS_CONF1:11 00 111 0: Duty ratio=1/320, 割込回数は毎回, Integration time=8T(400us), 距離計測無効
		0b00001000		//PS_CONF2:00 00 1 0 00: reserved, two-step mode, 16bit, typical sensitivity, no interrupt
	};
	if (!i2c_driver::Write(VCNL_ADD, disable_ps, sizeof(disable_ps))) 
		return false; // 通信失敗
	
	// 読み取ったデータを距離[mm]に変換
	uint16_t data = (buffer[1] << 8) | buffer[0];
	if (data < 1) data = 1;
	float ld = log(data);
	*ps = exp((-0.018 * ld - 0.234) * ld + 6.564);

	return true;
}
