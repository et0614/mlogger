/**
 * @file main.cpp
 * @brief AVR(AVRxxDB32)を使用した計測データ収集・送信プログラム
 * @author E.Togashi
 * @date 2022/3/11
 *
 * version履歴
 * 3.0.0	AVRxxDB32シリーズ用
 * 3.0.1	Reset処理を3秒長押しで有効に。短時間押し込みは電池確認のための点灯に変更。
 * 3.0.2	ADCバグ修正
 * 3.0.3	ADC基準電圧を2.0Vに変更
 * 3.0.4	CMSコマンド実行時にもEEPROMに設定を保存するように変更
 * 3.0.5	機器名称関連のコマンド（LLN,CLN）を実装
 * 3.0.6	AHT20のエラー時のリセット処理を追加
 * 3.0.7	SDカード書き出しの省電力化
 * 3.0.8	SDカード書き出し時のLED点灯バグ修正
 */

/**XBee端末の設定****************************************
 * ・親機子機共通
 *   1) FirmwareはZIGBEE TH Reg version 4061, XB3-24,Digi XBee3 Zigbee 3.0 TH 100D
 *   2) PAN IDを同じ値にする
 *   3) SP:Cyclic Sleep Period = 0x64（=1000 msec）,SN:Number of Cyclic Sleep Periods = 3600
 *      これで3600×3=3hourはネットワークから外れない
 *   4) AP:API Enable = API enabled
 * ・親機のみ
 *   1) CE:Coordinator Enable = Enabled
 *   2) SM:Sleep Mode = No sleep
 *   3) AR:many-to-one routing = 0
 *   4) NJ:Node Join Time = FF（時間無制限にネットワーク参加可能）
 * ・子機のみ
 *   1) CE:Coordinator Enable = Disabled
 *   2) SM:Sleep Mode = Pin Hibernate（ATMegaからの指令でスリープ解除するため）
 *   以下はBluetooth対応の場合のみ
 *   3) BT:Bluetooth Enable = Enabled
 *   4) BI:Bluetooth Identifier = "MLogger_xxx"（xxxは適当な文字で良い）
 *   5) Bluetooth Authenticationに「ml_pass」
*********************************************************/

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <avr/io.h>
#include <avr/interrupt.h>
#include <avr/sleep.h>

#include <avr/xmega.h>

#ifdef __cplusplus
extern "C"{
#endif
	#include <avr/cpufunc.h>
#ifdef __cplusplus
} // extern "C"
#endif

#include <util/delay.h>

//日付操作用
#include <time.h>

#include "main.h"
#include "my_eeprom.h"	//EEPROM処理
#include "my_i2c.h"		//I2C通信
#include "my_xbee.h"	//XBee通信

//FatFs関連
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//定数宣言***********************************************************
//熱線式風速計の立ち上げに必要な時間[sec]
const uint8_t V_WAKEUP_TIME = 20;

//照度センサ（OPTxxxx）のアドレス
const char OPT_ADDRESS = 0x88; //OPT3001は0x88, OPT3007は0x8A, を使用。

//グローブ温度計測用モジュールのタイプ
const bool IS_MCP9700 = false; //MCP9701ならばfalse

//AM2320かAHT20か
const bool IS_AM2320 = false;

//何行分のデータを一時保存するか
const int N_LINE_BUFF = 45;

//広域変数定義********************************************************
//日時関連
volatile static time_t currentTime = 0; //現在時刻（UNIX時間）
volatile static time_t startTime = 1609459200;   //計測開始時刻（UNIX時間,UTC時差0で2021/1/1 00:00:00）

//計測中か否か
volatile static bool logging = false;

//親機との通信関連
static bool readingFrame = false; //フレーム読込中か否か
static uint8_t framePosition = 0; //フレーム読み込み位置
static uint8_t frameSize = 0; //フレームサイズ
volatile static char frameBuff[my_xbee::MAX_CMD_CHAR]; //読込中のフレーム
volatile static char cmdBuff[my_xbee::MAX_CMD_CHAR]; //コマンドバッファ
static uint8_t xbeeOffset=14; //受信frame typeに応じたオフセット
volatile static bool outputToBLE=false; //Bluetooth接続に書き出すか否か
volatile static bool outputToXBee=true; //XBee接続に書き出すか否か
volatile static bool outputToSDCard=false; //SDカードに書き出すか否か

//計測の是非と計測時間間隔
//th:温湿度, glb:グローブ温度, vel:微風速, ill:照度
volatile static unsigned int pass_th = 0;
volatile static unsigned int pass_glb = 0;
volatile static unsigned int pass_vel = 0;
volatile static unsigned int pass_ill = 0;
volatile static unsigned int pass_ad1 = 0;
volatile static unsigned int pass_ad2 = 0;
volatile static unsigned int pass_ad3 = 0;

//WFCを送信するまでの残り時間[sec]
static uint8_t wc_time = 0;

//SDカード関連
volatile bool initSD = false; //SDカード初期化フラグ
static FATFS* fSystem;
static char lineBuff[my_xbee::MAX_CMD_CHAR * N_LINE_BUFF + 1]; //一時保存文字配列（末尾にnull文字を追加）
static uint8_t buffNumber = 0; //一時保存回数
static uint8_t lastSavedMinute = 0; //最後に保存した分
static uint8_t blinkCount = 0; //SD書き出し時のLED点滅時間間隔保持変数

//リセット処理用
volatile static unsigned int resetTime = 0;

//汎用の文字列配列
static char charBuff[my_xbee::MAX_CMD_CHAR];

//マクロ定義********************************************************
#define ARRAY_LENGTH(array) (sizeof(array) / sizeof(array[0]))

int main(void)
{
	//EEPROM
	my_eeprom::LoadEEPROM();
		
	//入出力ポートを初期化
	initialize_port();

	//一旦、すべての割り込み禁止
	cli();
		
	//AD変換の設定
	ADC0.CTRLA |= (ADC_ENABLE_bm | ADC_RESSEL_10BIT_gc); //ADC有効化, 10bit分解
	ADC0.CTRLB |= ADC_SAMPNUM_ACC64_gc; //64回平均化, 16, 32, 64から設定できる
	ADC0.CTRLC |= ADC_PRESC_DIV128_gc; //128分周で計測（大きい方が精度は高く、時間はかかる模様）
	VREF.ADC0REF = VREF_REFSEL_VREFA_gc; //基準電圧をVREFA(2.0V)に設定
	
	//スイッチ割り込み設定
	PORTA.PIN2CTRL |= PORT_ISC_BOTHEDGES_gc; //電圧上昇・降下割込
		
	//通信を初期化
	my_i2c::InitializeI2C(); //I2C
	//my_i2c::InitializeOPT(OPT_ADDRESS);  //照度センサとしてOPTxxxxを使う場合
	my_xbee::Initialize();  //XBee（UART）
	my_i2c::InitializeAHT20();
	
	//タイマ初期化
	initialize_timer();
	
	//XBeeスリープ解除
	wakeup_xbee();
	
	fSystem = (FATFS*) malloc(sizeof(FATFS));
	
	//初期設定が終わったら少し待機
	_delay_ms(500);

	//割り込み再開
	sei();

	//通信再開フラグを確認
	if(my_eeprom::startAuto)
	{
		logging = true;
		outputToXBee = true;
		outputToBLE = outputToSDCard = false;
		startTime = currentTime;
	}
	
    while (1) 
    {
		//Logging中でなければSDカードマウントを試みる
		if(!logging && !initSD)
			if(f_mount(fSystem, "", 1) == FR_OK) initSD = true;
		
		//スリープモード設定		
		if(logging && !outputToBLE) set_sleep_mode(SLEEP_MODE_PWR_DOWN); //ATmega328PではPWR_SAVE
		else set_sleep_mode(SLEEP_MODE_IDLE); //ロギング開始前はUART通信ができるようにIDLEでスリープ

		//Bluetooth通信を除き、ロギング中はXBeeをスリープさせる（XBeeの仕様上、Bluetoothモードのスリープは不可）
		if(logging && !outputToBLE) sleep_xbee();
		
		//マイコンをスリープさせる
		sleep_mode();
    }
}

static void initialize_port(void)
{
	//SPI通信のための初期処理はmmc.cのpower_on()関数内
	//***
	
	//出力ポート
	PORTD.DIRSET = PIN6_bm; //LED出力
	PORTF.DIRSET = PIN5_bm; //XBeeスリープ制御
	PORTA.DIRSET = PIN5_bm; //微風速計リレー
	PORTA.DIRSET = PIN4_bm; //微風速計5V昇圧	
	PORTA.DIRSET = PIN2_bm; //PORTA PIN2:リセット用割り込み
	sleep_anemo();   //微風速計は電池を消費するので、すぐにスリープする

	//入力ポート
	PORTD.DIRCLR = PIN4_bm; //グローブ温度センサAD変換
	PORTD.DIRCLR = PIN2_bm; //風速センサAD変換
	PORTF.DIRCLR = PIN4_bm; //汎用AD変換1
	PORTD.DIRCLR = PIN5_bm; //汎用AD変換2
	PORTD.DIRCLR = PIN3_bm; //汎用AD変換3
	PORTA.DIRCLR = PIN6_bm; //汎用IO1
	PORTA.DIRCLR = PIN7_bm; //汎用IO2
	
	//プルアップ/ダウン
	PORTA.OUTSET = PIN2_bm; //PORTA PIN2(Interrupt)：アップ
	PORTA.OUTSET = PIN6_bm; //汎用IO1：アップ
	PORTA.OUTSET = PIN7_bm; //汎用IO2：アップ
	
	//PORTD.OUTCLR = PIN4_bm; //グローブ温度センサ：ダウン 2022.04.19 Pullup/downはADCではそもそも無効。
	//PORTD.OUTCLR = PIN2_bm; //風速センサ：ダウン
	//PORTF.OUTCLR = PIN4_bm; //汎用AD変換1：ダウン
	//PORTD.OUTCLR = PIN5_bm; //汎用AD変換2：ダウン
	//PORTD.OUTCLR = PIN3_bm; //汎用AD変換3：ダウン
}

//タイマ初期化
static void initialize_timer( void )
{
	//Timer 1 //0.01secタイマ************************
	TCA0.SINGLE.INTCTRL = TCA_SINGLE_OVF_bm; //タイマ溢れ割り込み有効化
	TCA0.SINGLE.CTRLB = TCA_SINGLE_WGMODE_NORMAL_gc; 
	TCA0.SINGLE.EVCTRL &= ~(TCA_SINGLE_CNTAEI_bm);	
	TCA0.SINGLE.PER = 0.01 * F_CPU / 2 - 1;  //0.01sec, 4MHz, 2分周
	TCA0.SINGLE.CTRLA = TCA_SINGLE_CLKSEL_DIV2_gc | TCA_SINGLE_ENABLE_bm;
		
	//外部クリスタルによる1secタイマ*******************
	//**外部クリスタルの有効化処理**
	//発振器禁止
	_PROTECTED_WRITE(CLKCTRL.XOSC32KCTRLA, ~CLKCTRL_ENABLE_bm);
	while(CLKCTRL.MCLKSTATUS & CLKCTRL_XOSC32KS_bm); //XOSC32KSが0になるまで待機

	//XTAL1とXTAL2に接続された外部クリスタルを使用
	_PROTECTED_WRITE(CLKCTRL.XOSC32KCTRLA, ~CLKCTRL_SEL_bm);
	
	//発振器許可
	_PROTECTED_WRITE(CLKCTRL.XOSC32KCTRLA, CLKCTRL_ENABLE_bm);

	while (RTC.STATUS > 0); //全レジスタが同期されるまで待機
	//**有効化処理ここまで*********

	RTC.CLKSEL = RTC_CLKSEL_XOSC32K_gc;	  //32.768kHz外部クリスタル用発振器 (XOSC32K)を選択
	RTC.DBGCTRL |= RTC_DBGRUN_bm;         //デバッグで走行を許可
	RTC.PITINTCTRL = RTC_PI_bm;           //定期割込を有効にする
	RTC.PITCTRLA = RTC_PERIOD_CYC32768_gc //RTC周期は32768
				| RTC_PITEN_bm;           //定期割込用タイマを有効にする
	
	//POWER DOWN時にもタイマ割込を有効にする
	SLPCTRL.CTRLA |= SLPCTRL_SMODE_PDOWN_gc; 
	SLPCTRL.CTRLA |= SLPCTRL_SEN_bm;
}

//UART受信時の割り込み処理
ISR(USART0_RXC_vect)
{
	char dat = USART0.RXDATAL;	//読み出し
	
	//開始コード「~」が来たら初期化。本当はEscape処理がいるが、「~」はコマンドで使わないので良いだろう
	if(dat == 0x7E)
	{
		framePosition = 1;
		readingFrame = true;
	}
	//フレーム読込中
	else if(readingFrame)
	{
		if(framePosition == 1); //データ長上位バイト（パケットの制限から必ず0）
		else if(framePosition == 2) //データ長下位バイト
			frameSize = (int)dat + 3;
		else if(framePosition == 3 && dat != 0x90) //0x90以外のコマンドの場合には無視//この処理、問題あり
		{
			if(dat == 0x90) xbeeOffset = 14; //ZigBee Recieve Packetの場合のオフセット
			else if(dat == 0xAD) xbeeOffset = 4; //User Data Relayの場合のオフセット
		}
		else if(framePosition <= xbeeOffset); //受信オプションなどは無視
		else
		{
			if(frameSize <= framePosition) //チェックサムに到達
			{
				frameBuff[framePosition - (xbeeOffset + 1)] = '\0';
				readingFrame = false;
				frameSize = 0;
				framePosition = 0;
				
				//コマンドへ追加
				append_command();
			}
			else if(framePosition < frameSize) //データをバッファに格納
				frameBuff[framePosition - (xbeeOffset + 1)] = dat;
		}
		
		framePosition++;
	}	
}

static void append_command(void)
{
	//nullまで進む
	unsigned int pos1 = 0;
	unsigned int cbLength = ARRAY_LENGTH(cmdBuff);
	while(cmdBuff[pos1] != '\0' && pos1 < cbLength) pos1++;
	if(pos1 == cbLength - 1) pos1 = 0; //バッファの最後まで進んでもNULLがなければ最初に戻る
	
	unsigned int pos2 = 0;
	unsigned int fbLength =  ARRAY_LENGTH(frameBuff);
	while (frameBuff[pos2] != '\0' && pos2 < fbLength)
	{
		if(pos2 == fbLength - 1) break;
		
		char nxtC = frameBuff[pos2];
		cmdBuff[pos1] = nxtC;
		pos1++;
		pos2++;
		//改行コードが表れたらコマンド実行
		if(nxtC == '\r')
		{
			cmdBuff[pos1] = '\0';
			solve_command();
			pos1 = 0;
		}		
	}	
}

static void solve_command(void)
{
	char* command = (char*)cmdBuff;
	
	//バージョン
	if (strncmp(command, "VER", 3) == 0) 
		my_xbee::bltx_chars("VER:3.0.8\r");
	//ロギング開始
	else if (strncmp(command, "STL", 3) == 0)
	{
		//現在時刻を設定
		char num[11];
		num[10] = '\0';
		strncpy(num, command + 3, 10);
		currentTime = atol(num);
		
		//Bluetooth接続か否か(xはxbee,bはbluetooth)
		outputToXBee = (command[13]=='t'); //XBeeで親機に書き出すか否か
		outputToBLE = (command[14]=='t'); //Bluetoothで書き出すか否か
		outputToSDCard = (command[15]=='t'); //SDカードに書き出すか否か
		
		//0秒時点で直ちに一回は計測する。微風速はおかしな値になるが。。。
		pass_th	= my_eeprom::interval_th;
		pass_glb = my_eeprom::interval_glb;
		pass_vel = my_eeprom::interval_vel;
		pass_ill = my_eeprom::interval_ill;
		pass_ad1 = my_eeprom::interval_AD1;
		pass_ad2 = my_eeprom::interval_AD2;
		pass_ad3 = my_eeprom::interval_AD3;
		
		//ロギング設定をEEPROMに保存
		//my_eeprom::startAuto = outputToXBee; //リセットスイッチを持つ基盤が用意できたらコメントアウトする
		my_eeprom::SetMeasurementSetting();
		
		//ロギング開始
		my_xbee::bltx_chars("STL\r");
		_delay_ms(100);
		logging = true;	
	}
	//Change Measurement Settings
	else if(strncmp(command, "CMS", 3) == 0)
	{
		//設定を変更する場合にはロギングを停止させる
		logging = false;
		
		//測定の是非
		my_eeprom::measure_th = (command[3] == 't');
		my_eeprom::measure_glb = (command[9] == 't');
		my_eeprom::measure_vel = (command[15] == 't');
		my_eeprom::measure_ill = (command[21] == 't');
		if(37 < strlen(command))
		{
			my_eeprom::measure_AD1 = (command[37] == 't');
			my_eeprom::measure_AD2 = (command[43] == 't');
			my_eeprom::measure_AD3 = (command[49] == 't');
		}
		//バージョンが低い場合の処理
		else
		{
			my_eeprom::measure_AD1 = false;
			my_eeprom::measure_AD2 = false;
			my_eeprom::measure_AD3 = false;
		}
		
		//測定時間間隔
		char num[6];
		num[5] = '\0';
		strncpy(num, command + 4, 5);
		my_eeprom::interval_th = atoi(num);
		strncpy(num, command + 10, 5);
		my_eeprom::interval_glb = atoi(num);
		strncpy(num, command + 16, 5);
		my_eeprom::interval_vel = atoi(num);
		strncpy(num, command + 22, 5);
		my_eeprom::interval_ill = atoi(num);
		if(37 < strlen(command))
		{
			strncpy(num, command + 38, 5);
			my_eeprom::interval_AD1 = atoi(num);
			strncpy(num, command + 44, 5);
			my_eeprom::interval_AD2 = atoi(num);
			strncpy(num, command + 50, 5);
			my_eeprom::interval_AD3 = atoi(num);
		}
		//バージョンが低い場合の処理
		else
		{
			my_eeprom::interval_AD1 = 60;
			my_eeprom::interval_AD2 = 60;
			my_eeprom::interval_AD3 = 60;
		}
		
		//近接センサの有効無効
		if(37 < strlen(command)) my_eeprom::measure_Prox = (command[55] == 't');
		//バージョンが低い場合の処理
		else my_eeprom::measure_Prox = false;
		
		//計測開始時刻
		char num2[11];
		num2[10] = '\0';
		strncpy(num2, command + 27, 10);
		startTime = atol(num2);
		
		//ロギング設定をEEPROMに保存
		my_eeprom::SetMeasurementSetting();
		
		//ACK
		sprintf(charBuff, "CMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld,%d,%u,%d,%u,%d,%u,%d\r",
			my_eeprom::measure_th, my_eeprom::interval_th, 
			my_eeprom::measure_glb, my_eeprom::interval_glb, 
			my_eeprom::measure_vel, my_eeprom::interval_vel, 
			my_eeprom::measure_ill, my_eeprom::interval_ill, 
			startTime,
			my_eeprom::measure_AD1, my_eeprom::interval_AD1, 
			my_eeprom::measure_AD2, my_eeprom::interval_AD2, 
			my_eeprom::measure_AD3, my_eeprom::interval_AD3,
			my_eeprom::measure_Prox);
		my_xbee::bltx_chars(charBuff);
	}
	//Load Measurement Settings
	else if(strncmp(command, "LMS", 3) == 0)
	{
		sprintf(charBuff, "LMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld,%d,%u,%d,%u,%d,%u,%d\r",
			my_eeprom::measure_th, my_eeprom::interval_th,
			my_eeprom::measure_glb, my_eeprom::interval_glb,
			my_eeprom::measure_vel, my_eeprom::interval_vel,
			my_eeprom::measure_ill, my_eeprom::interval_ill, 
			startTime,
			my_eeprom::measure_AD1, my_eeprom::interval_AD1,
			my_eeprom::measure_AD2, my_eeprom::interval_AD2,
			my_eeprom::measure_AD3, my_eeprom::interval_AD3,
			my_eeprom::measure_Prox);
		my_xbee::bltx_chars(charBuff);
	}
	//End Logging
	else if(strncmp(command, "ENL", 3) == 0)
	{
		logging = false;
		my_xbee::bltx_chars("ENL\r");
	}
	//Set Correction Factor
	else if(strncmp(command, "SCF", 3) == 0)
	{
		my_eeprom::SetCorrectionFactor(command);
		my_eeprom::MakeCorrectionFactorString(charBuff, "SCF");
		my_xbee::bltx_chars(charBuff);
	}
	//Load Correction Factor
	else if(strncmp(command, "LCF", 3) == 0)
	{
		my_eeprom::MakeCorrectionFactorString(charBuff, "LCF");
		my_xbee::bltx_chars(charBuff);
	}
	//Change Logger Name
	else if(strncmp(command, "CLN", 3) == 0)
	{
		strncpy(my_eeprom::mlName, command + 3, 20);
		my_eeprom::SaveName();
		
		//ACK
		char ack[21 + 4];
		sprintf(ack, "CLN:%s\r", my_eeprom::mlName);		
		my_xbee::bltx_chars(ack);
	}
	//Load Logger Name
	else if(strncmp(command, "LLN", 3) == 0)
	{
		char name[21+4];
		sprintf(name, "LLN:%s\r", my_eeprom::mlName);
		my_xbee::bltx_chars(name);
	}
	
	//コマンドを削除
	cmdBuff[0] = '\0';
}

// Timer1割り込み//FatFs（SDカード入出力通信）用
ISR(TCA0_OVF_vect)
{
	disk_timerproc();	/* Drive timer procedure of low level disk I/O module */
	
	TCA0.SINGLE.INTFLAGS = TCA_SINGLE_OVF_bm; //割り込み解除
}

//ロギング用の1秒毎の処理
ISR(RTC_PIT_vect)
{
	//割り込み要求フラグ解除
	RTC.PITINTFLAGS = RTC_PI_bm;
	
	currentTime++; //1秒進める
	
	//リセットボタン押し込み確認
	if(!(PORTA.IN & PIN2_bm))
	{
		resetTime++;
		if(3 < resetTime)
		{
			logging=false;	//ロギング停止
			initSD = false;	//SDカード再マウント
			sleep_anemo();	//風速センサを停止
			blinkLED(3);	//LED点滅
			return;
		}
	}
	else resetTime = 0; //Resetボタン押し込み時間を0に戻す
				
	//ロギング中であれば
	if(logging)
	{
		//計測開始時刻の前ならば終了
		if(currentTime < startTime) return;
		
		//SDカードロギング中は5秒ごとに点灯
		if(outputToSDCard)  //SD card出力
		{
			blinkCount++;
			if(5 <= blinkCount)
			{
				blinkCount = 0;
				blinkLED(1);
			}
		}
		
		//計測のWAKEUP_TIME[sec]前から熱線式風速計回路のスリープを解除して加熱開始
		if(my_eeprom::measure_vel && my_eeprom::interval_vel - pass_vel < V_WAKEUP_TIME) wakeup_anemo();
		
		bool hasNewData = false;
		char tmpS[7] = "n/a"; //-10.00 ~ 50.00//6文字+\r
		char hmdS[7] = "n/a"; //0.00 ~ 100.00//6文字+\r
		char glbTS[7] = "n/a"; //-10.00 ~ 50.00//6文字+\r
		char glbVS[7] = "n/a";
		char velS[7] = "n/a"; //0.0000 ~ 1.5000//6文字+\r
		char velVS[7] = "n/a";
		char illS[9] = "n/a"; //0.01~83865.60
		char adV1S[7] = "n/a";
		char adV2S[7] = "n/a";
		char adV3S[7] = "n/a";
		
		//微風速測定************
		pass_vel++;
		if(my_eeprom::measure_vel && my_eeprom::interval_vel <= pass_vel)
		{
			double velV = readVelVoltage(); //AD変換
			dtostrf(velV,6,4,velVS);
			
			float bff = max(0, velV / my_eeprom::Cf_vel0 - 1.0);
			float vel = bff * (2.3595 + bff * (-12.029 + bff * 79.744)); //電圧-風速換算式
			dtostrf(vel,6,4,velS);
			
			pass_vel = 0;
			hasNewData = true;
			//次の起動時刻が起動に必要な時間よりも後の場合には微風速計回路をスリープ
			if(V_WAKEUP_TIME <= my_eeprom::interval_vel) sleep_anemo();
		}
		
		//温湿度測定************
		pass_th++;
		if(my_eeprom::measure_th && my_eeprom::interval_th <= pass_th)
		{
			float tmp_f = 0;
			float hmd_f = 0;
			if((IS_AM2320 && my_i2c::ReadAM2320(&tmp_f, &hmd_f)) || (!IS_AM2320  &&  my_i2c::ReadAHT20(&tmp_f, &hmd_f)))
			{
				tmp_f = max(-10,min(50,my_eeprom::Cf_dbtA *(tmp_f) + my_eeprom::Cf_dbtB));
				hmd_f = max(0,min(100,my_eeprom::Cf_hmdA *(hmd_f) + my_eeprom::Cf_hmdB));
				dtostrf(tmp_f,6,2,tmpS);
				dtostrf(hmd_f,6,2,hmdS);
			}
			pass_th = 0;
			hasNewData = true;
		}
	
		//グローブ温度測定************
		pass_glb++;
		if(my_eeprom::measure_glb && my_eeprom::interval_glb <= pass_glb)
		{
			float glbV = readGlbVoltage(); //AD変換
			dtostrf(glbV,6,4,glbVS);
			
			float glbT = (glbV - (IS_MCP9700 ? 0.5 : 0.4)) / (IS_MCP9700 ? 0.0100 : 0.0195);
			glbT = max(-10,min(50,my_eeprom::Cf_glbA * glbT + my_eeprom::Cf_glbB));
			dtostrf(glbT,6,2,glbTS);
			
			pass_glb = 0;
			hasNewData = true;			
		}
		
		//照度センサ測定**************
		pass_ill++;
		if(my_eeprom::measure_ill && my_eeprom::interval_ill <= pass_ill)
		{
			
			if(my_eeprom::measure_Prox) 
			{
				float ill_d = my_i2c::ReadVCNL4030_PS();
				dtostrf(ill_d,8,2,illS);
			}
			else
			{
				float ill_d = my_i2c::ReadVCNL4030_ALS();
				ill_d = max(0,min(99999.99,my_eeprom::Cf_luxA * ill_d + my_eeprom::Cf_luxB));
				dtostrf(ill_d,8,2,illS);
			}
			pass_ill = 0;
			hasNewData = true;
		}
		
		//汎用AD変換測定1
		pass_ad1++;
		if(my_eeprom::measure_AD1 && my_eeprom::interval_AD1 <= pass_ad1)
		{
			float adV = readVoltage(1); //AD変換
			dtostrf(adV,6,4,adV1S);
			pass_ad1 = 0;
			hasNewData = true;
		}
		
		//汎用AD変換測定2
		pass_ad2++;
		if(my_eeprom::measure_AD2 && my_eeprom::interval_AD2 <= pass_ad2)
		{
			float adV = readVoltage(2); //AD変換
			dtostrf(adV,6,4,adV2S);
			pass_ad2 = 0;
			hasNewData = true;
		}
		
		//汎用AD変換測定3
		pass_ad3++;
		if(my_eeprom::measure_AD3 && my_eeprom::interval_AD3 <= pass_ad3)
		{
			float adV = readVoltage(3); //AD変換
			dtostrf(adV,6,4,adV3S);
			pass_ad3 = 0;
			hasNewData = true;
		}
		
		//新規データがある場合は送信
		if(hasNewData)
		{
			if(outputToXBee || outputToBLE) 
			{
				wakeup_xbee(); //XBeeスリープ解除
				_delay_ms(1); //スリープ解除時の立ち上げは50us=0.05ms程度かかるらしい
			}
			
			//日時を作成
			time_t ct = currentTime - UNIX_OFFSET;
			tm dtNow;
			gmtime_r(&ct, &dtNow);
			
			//書き出し文字列を作成
			snprintf(charBuff, sizeof(charBuff), "%s%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\r",
			(outputToSDCard ? "" : "DTT:"), dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
			tmpS, hmdS, glbTS, velS, illS, glbVS, velVS, adV1S, adV2S, adV3S);
			
			//文字列オーバーに備えて最後に終了コード'\r\0'を入れておく
			charBuff[my_xbee::MAX_CMD_CHAR-2]='\r';
			charBuff[my_xbee::MAX_CMD_CHAR-1]= '\0';

			if(outputToXBee) my_xbee::tx_chars(charBuff); //XBee Zigbee出力
			if(outputToBLE) my_xbee::bl_chars(charBuff); //XBee Bluetooth出力
			if(outputToSDCard)  //SD card出力
			{
				//データが十分に溜まるか、1min以上の時間間隔があいたら書き出す
				if(N_LINE_BUFF <= buffNumber || lastSavedMinute != dtNow.tm_min)
				{
					writeSDcard(dtNow, lineBuff); //SD card出力
					buffNumber = 0;
					lineBuff[0] = '\0';
					lastSavedMinute = dtNow.tm_min;
				}
				//一時保存文字列の末尾に足す
				strncat(lineBuff, charBuff, sizeof(charBuff));
				buffNumber++;
			}
		}
				
		//UART送信が終わったら10msec待ってXBeeをスリープさせる(XBee側の送信が終わるまで待ちたいので)
		//本来、ここはCTSを使って受信可能になったタイミングでスリープか？フローコントロールを検討。
		//while(! my_uart::tx_done());
		_delay_ms(10); //このスリープはXBeeの通信終了待ち目的。試行錯誤で用意した値なので、根拠が曖昧。そもそもここではないようにも思う
	}
	else
	{
		wakeup_xbee(); //XBeeスリープ解除
		_delay_ms(1); //スリープ解除時の立ち上げは50us=0.05ms程度かかるらしい
		
		//定期的にコマンド待受状態を送信
		if(wc_time <= 0)
		{
			my_xbee::bltx_chars("WFC\r"); //Waiting for command.
			wc_time = 6;
		}
		wc_time--;
		
		//明滅
		if((PORTA.IN & PIN2_bm))  //Reset押し込み中は明滅停止
			blinkLED(initSD ? 2 : 1);
	}
}

static void writeSDcard(const tm dtNow, const char write_chars[])
{
	//マウント未完了ならば終了
	if(!initSD) return;
	
	char fileName[13]={}; //yyyymmdd.csv
	snprintf(fileName, sizeof(fileName), "%04d%02d%02d.txt", dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday);
	
	//SDカード記録用日付更新
	myRTC.year=dtNow.tm_year+1900;
	myRTC.month=dtNow.tm_mon+1;
	myRTC.mday=dtNow.tm_mday;
	myRTC.hour=dtNow.tm_hour;
	myRTC.min=dtNow.tm_min;
	myRTC.sec=dtNow.tm_sec;
	
	FIL* fl = (FIL*)malloc(sizeof(FIL));	
	if(f_open(fl, fileName, FA_OPEN_APPEND | FA_WRITE) == FR_OK){
		f_puts(write_chars, fl);
		f_close(fl);
	}
	
	free(fl);
}

//PORTA PIN2割り込み
ISR(PORTA_PORT_vect)
{
	// 割り込みフラグ解除
	PORTA.INTFLAGS = PIN2_bm;
	
	//Push:LED点灯, None:LED消灯
	if(PORTA.IN & PIN2_bm) PORTD.OUTCLR = PIN6_bm;
	else PORTD.OUTSET = PIN6_bm;
}

//グローブ温度の電圧を読み取る
static float readGlbVoltage(void)
{
	//AI4を計測
	ADC0.MUXPOS = ADC_MUXPOS_AIN4_gc;
	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //変換開始
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //変換終了待ち
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64回平均)
}

//微風速の電圧を読み取る
static float readVelVoltage(void)
{
	//AI2を計測
	ADC0.MUXPOS = ADC_MUXPOS_AIN2_gc;
	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //変換開始
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //変換終了待ち
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64回平均)
}

//AD1~3の電圧を読み取る
static float readVoltage(unsigned int adNumber)
{
	if(adNumber == 1) ADC0.MUXPOS = ADC_MUXPOS_AIN20_gc; //AD1
	else if(adNumber == 2) ADC0.MUXPOS = ADC_MUXPOS_AIN5_gc; //AD2
	else ADC0.MUXPOS = ADC_MUXPOS_AIN3_gc; //AD3

	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //変換開始
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //変換終了待ち
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64回平均)
}

static void sleep_anemo(void)
{
	PORTA.OUTCLR = PIN5_bm; //リレー遮断
	PORTA.OUTCLR = PIN4_bm; //5V昇圧停止
}

//以下はinline関数************************************

inline static void wakeup_anemo(void)
{
	PORTA.OUTSET = PIN5_bm; //リレー通電
	PORTA.OUTSET = PIN4_bm; //5V昇圧開始
}

inline static void sleep_xbee(void)
{
	PORTF.OUTSET = PIN5_bm;
}

inline static void wakeup_xbee(void)
{
	PORTF.OUTCLR = PIN5_bm;
}

inline static void blinkLED(int iterNum)
{
	if(iterNum < 1) return;

	//初回
	PORTD.OUTCLR = PIN6_bm; //一旦必ず消灯して
	//点滅
	for(int i=0;i<iterNum;i++)
	{
		_delay_ms(100);
		PORTD.OUTSET = PIN6_bm; //点灯
		_delay_ms(25);
		PORTD.OUTCLR = PIN6_bm; //消灯
	}
}

inline static float max(float x, float y)
{
	return (x > y) ? x : y;
}

inline static float min(float x, float y)
{
	return (x < y) ? x : y;
}