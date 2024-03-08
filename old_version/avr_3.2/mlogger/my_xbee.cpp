/**
 * @file my_xbee.h
 * @brief AVR(ATMega328)でXBeeと通信する
 * @author E.Togashi
 * @date 2021/11/28
 */

#include "my_xbee.h"
#include "my_uart.h"

#include <string.h>
#include <avr/io.h>

const size_t MBUFF_LENGTH = 20;

bool my_xbee::IsAPMode = true;

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
	my_uart::send_char(0x2D); //コマンドID（DataRelayは0x2D）
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

void my_xbee::send_atcmd(const char data[]){
	int cl = get_char_length(data);
	//送信データ
	for(int i=0;i<cl;i++)
		my_uart::send_char(data[i]);
}

bool my_xbee::xbee_setting_initialized(){
	bool hasChanged = false;
	char message[MBUFF_LENGTH];
	memset(message, 0, sizeof(message));

	//ATモードへ
	my_uart::send_chars("+++");
	my_xbee::receive_message(message);
	//ATモードへ行けない場合にはすでにAPI Enabledになっているということで正常と判断。あまり良くない処理。
	if(strcmp(message, "OK") != 0) return 1;
	
	//PAN IDは19800614
	my_uart::send_chars("atid\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "19800614") != 0) {
		my_uart::send_chars("atid19800614\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//SPは1000msec=0x64
	my_uart::send_chars("atsp\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "64") != 0) {
		my_uart::send_chars("atsp64\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//SNは3600sec
	my_uart::send_chars("atsn\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "3600") != 0) {
		my_uart::send_chars("atsn3600\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//CEはend device(0)
	my_uart::send_chars("atce\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "0") != 0) {
		my_uart::send_chars("atce0\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//SMはPin Hibernate(1)
	my_uart::send_chars("atsm\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "1") != 0) {
		my_uart::send_chars("atsm1\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//d5はZigbee通信状況のLED表示フラグOff(0)
	my_uart::send_chars("atd5\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "0") != 0) {
		my_uart::send_chars("atd50\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//Bluetooth有効/無効+password
	my_uart::send_chars("atbt\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "0") == 0) {		
		my_uart::send_chars("at$S28513497\r"); //salt
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$V6567694B0CA9ADCED8D5B2B0015718D1E2637B86E3E178E029936A078926C2B0\r"); //$V
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$W259F6833E1E1932E4485F48865FB6B76EC6E847A7272C77A8C27DD7DF94E44DC\r"); //$W
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$XA99A6E3937FBB8D05BBB4E4A8C4CB221C14D15CD004139C77B6FE0C8AF2932D8\r"); //$X
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$Y6D4411D507FC52AFD5877D6E8529AEE7FB931F10944BC0D058FB246D0DE071DB\r"); //$Y
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("atbt1\r"); //Bluetooth 有効化
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//biはBluetooth identifier
	my_uart::send_chars("atbi\r");
	my_xbee::receive_message(message);
	if(strncmp(message, "MLogger_", 8) != 0) {
		my_uart::send_chars("atbiMLogger_xxxx\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
				
	//AP
	my_uart::send_chars("atap\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "1") != 0) {
		my_uart::send_chars("atap1\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
		
	//変更があった場合にはメモリに書き込む
	if(hasChanged){
		my_uart::send_chars("atwr\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
	}
		
	return 1;
}

void my_xbee::receive_message(char message[]){
	uint8_t index = 0;
	while (1) {
		//1文字読む	
		while (!(USART0.STATUS & USART_RXCIF_bm)){;}		
		char c = USART0.RXDATAL;  
		 
		if(c !='\n' && c != '\r'){ 
			message[index++] = c;
			if(MBUFF_LENGTH < index) index = 0;
		}
		if(c == '\r'){
			message[index] = '\0';
			return;
		}
	}
}
