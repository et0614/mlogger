/**
 * @file my_xbee.h
 * @brief AVR(ATMega328)でXBeeと通信する
 * @author E.Togashi
 * @date 2021/11/28
 */

#include "my_xbee.h"
#include "my_uart.h"

void my_xbee::Initialize(void)
{
	my_uart::Initialize();	
}

//コーディネータに対して文字配列を送信
void my_xbee::tx_chars(const char data[])
{
	int chkSum = 0;
	int cl = get_char_length(data);
	
	my_uart::send_char(0x7E); //APIフレーム開始コード
	my_uart::send_char((char)(((cl + 14) >> 8) & 0xff));	//データ長の上位バイト
	my_uart::send_char((char)((cl + 14) & 0xff));			//データ長の下位バイト
	
	//ここからチェックサム加算*************
	my_uart::send_char(0x10); //コマンドID（データ送信は0x10）
	chkSum = add_csum(chkSum, 0x10);
	
	my_uart::send_char(0x00); //フレームID（任意）//0以外だとACKが戻ってくる。いらないので0
	
	for(int i=0;i<8;i++) //64bit送信先アドレスはコーディネータへの送信なのですべて0でチェックサムは不変
		my_uart::send_char(0x00);
		
	my_uart::send_char(0xFF); //16bit送信先アドレス_M
	chkSum = add_csum(chkSum, 0xFF);
	
	my_uart::send_char(0xFE); //16bit送信先アドレス_L
	chkSum = add_csum(chkSum, 0xFE);
	
	my_uart::send_char(0x00); //ブロードキャスト半径（ユニキャストなので0でチェックサムは不変）

	my_uart::send_char(0x00); //送信オプションは0でチェックサムは不変
	
	//送信データ
	for(int i=0;i<cl;i++)
	{
		my_uart::send_char(data[i]);		
		chkSum = add_csum(chkSum, data[i]);
	}
	
	my_uart::send_char((char)(0xff - chkSum)); //Checksum送信
}

int my_xbee::get_char_length(const char data[])
{
	int length = 0;
	for(length = 0; data[length]; length++);
	return length;
}

int my_xbee::add_csum(int csum, char nbyte)
{
	csum += (int)nbyte;
	csum = csum & 0x00ff;
	return csum;
}

//Bluetooth接続先に対して文字配列を送信
void my_xbee::bl_chars(const char data[])
{
	int chkSum = 0;
	int cl = get_char_length(data);
	
	my_uart::send_char(0x7E); //APIフレーム開始コード
	my_uart::send_char((char)(((cl + 3) >> 8) & 0xff));	//データ長の上位バイト
	my_uart::send_char((char)((cl + 3) & 0xff));			//データ長の下位バイト
	
	//ここからチェックサム加算*************
	my_uart::send_char(0x2D); //フレーム type（DataRelayは0x2D）
	chkSum = add_csum(chkSum, 0x2D);
	
	my_uart::send_char(0x00); //フレームID
	
	my_uart::send_char(0x01); //Source interface（Bluetoothは0x01）
	chkSum = add_csum(chkSum, 0x01);
		
	//送信データ
	for(int i=0;i<cl;i++)
	{
		my_uart::send_char(data[i]);
		chkSum = add_csum(chkSum, data[i]);
	}
	
	my_uart::send_char((char)(0xff - chkSum)); //Checksum送信
}

void my_xbee::bltx_chars(const char data[])
{
	tx_chars(data);
	bl_chars(data);	
}

