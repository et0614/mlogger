/**
 * @file main.cpp
 * @brief AVR(AVRxxDB32)を使用した計測データ収集・送信プログラム
 * @author E.Togashi
 * @date 2022/3/11
 */

/**XBee端末の設定****************************************
 * ・親機子機共通
 *   1) Firmwareは、Product family:「XB3-24」,Function set:「Digi XBee3 Zigbee 3.0」,Firmware version:「1010」
 *   2) PAN IDを同じ値にする
 *   3) SP:Cyclic Sleep Period = 0x64（=1000 msec）
 *      SN:Number of Cyclic Sleep Periods = 3600
 *      これで3600×3=3hourはネットワークから外れない
 *   4) AP:API Mode Without Escapes[1]
 * ・親機のみ
 *   1) CE:Coordinator Enable = Enabled
 *   2) SM:Sleep Mode = No sleep
 *   3) AR:many-to-one routing = 0
 *   4) NJ:Node Join Time = FF（時間無制限にネットワーク参加可能）
 * ・子機のみ
 *   1) CE:Coordinator Enable = Join Network [0]
 *   2) SM:Sleep Mode = Pin Hibernate [1]（ATMegaからの指令でスリープ解除するため）
 *   以下はBluetooth対応の場合のみ
 *   3) BT:Bluetooth Enable = Enabled [1]
 *   4) BI:Bluetooth Identifier = "MLogger_xxxx"（xxxxは適当な桁数の数字でIDとして使う）
 *   5) Bluetooth Authenticationに「ml_pass」
*********************************************************/

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <avr/io.h>
#include <avr/interrupt.h>
#include <avr/sleep.h>
#include <avr/xmega.h>
#include <util/atomic.h>

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
#include "EepromManager.h"	//EEPROM処理
#include "i2c/I2cDriver.h"  //I2C通信
#include "i2c/sht4x.h"      //SHT4X(温湿度センサ)
#include "i2c/stcc4.h"      //STCC4(CO2センサ)
#include "i2c/vcnl4030.h"	//VCNL4030(照度センサ)
#include "UartDriver.h"		//UART通信
#include "XbeeController.h" //XBee通信

//FatFs関連
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//定数宣言***********************************************************
const char VERSION_NUMBER[] = "VER:3.4.0\r";

//コマンドの文字数
const uint8_t CMD_LENGTH = 3;

//コマンドの最大文字数
const int MAX_CMD_CHAR = 150;

//熱線式風速計の立ち上げに必要な時間[sec]
const uint8_t V_WAKEUP_TIME = 20;

//何行分のデータを一時保存するか
const int N_LINE_BUFF = 30;

//照度計カバーアクリル板の透過率
const double TRANSMITTANCE = 0.60;

//風速特性式version2か否か
const bool IS_VEL_FNC2 = true;

//広域変数定義********************************************************
//日時関連
volatile static time_t currentTime = UNIX_OFFSET; //現在時刻（UNIX時間,UTC時差0で2000/1/1 00:00:00）

//1秒毎の処理実行フラグ
volatile bool process_logging_flag = false;

//計測中か否か
static bool logging = false;

//電池は足りているか
static unsigned int lowBatteryTime = 0;

//コマンド
static char xbee_payload_buffer[MAX_CMD_CHAR]; // process_xbee_byteからペイロードを受け取るための一時的なバッファ
static char cmdBuff[MAX_CMD_CHAR]; // 複数のペイロードにまたがるコマンドを組み立てるためのバッファ
static bool outputToBLE=false; //Bluetooth接続に書き出すか否か
static bool outputToXBee=true; //XBee接続に書き出すか否か
static bool outputToFM=false; //Flash Memoryに書き出すか否か

//計測時間間隔
static MeasurementPassCounters pass_counters = {0};

//WFCを送信するまでの残り時間[sec]
static uint8_t wc_time = 0;

//接続維持用空パケット時間[sec]
static uint8_t slp_time = 0;

//Flashメモリ関連
static FATFS fSystem;
static bool initFM = false; //Flash Memory初期化フラグ
static char lineBuff[MAX_CMD_CHAR * N_LINE_BUFF + 1]; //一時保存文字配列（末尾にnull文字を追加）
static uint8_t buffNumber = 0; //一時保存回数
static tm lastSavedTime; //最後に保存した日時（UNIX時間）
static uint8_t blinkCount = 0; //FM書き出し時のLED点滅時間間隔保持変数

//リセット処理用
static unsigned int resetTime = 0;

//CO2センサ関連
static bool hasCO2Sensor = false;

//汎用の文字列配列
static char charBuff[MAX_CMD_CHAR];

//風速計自動校正処理用
static bool calibratingVelocityVoltage = false;

int main(void)
{	
	//EEPROM
	EepromManager::loadEEPROM();
		
	//入出力ポートを初期化
	initializePort();

	//一旦、すべての割り込み禁止
	cli();
		
	//AD変換の設定
	ADC0.CTRLA |= (ADC_ENABLE_bm | ADC_RESSEL_10BIT_gc); //ADC有効化, 10bit分解
	ADC0.CTRLB |= ADC_SAMPNUM_ACC64_gc; //64回平均化, 16, 32, 64から設定できる
	ADC0.CTRLC |= ADC_PRESC_DIV128_gc; //128分周で計測（大きい方が精度は高く、時間はかかる模様）
	VREF.ADC0REF = VREF_REFSEL_VREFA_gc; //基準電圧をVREFA(2.0V)に設定

	//低電圧が1秒継続したら電池残量エラー
	int count = 0;
	while(isLowBattery()){
		count++;
		if(10 <= count)	showError(1);
		_delay_ms(100);
	}
	
	//スイッチ割り込み設定
	PORTA.PIN2CTRL |= PORT_ISC_BOTHEDGES_gc; //電圧上昇・降下割込
	
	//初期化処理
	I2cDriver::initialize(); //I2C通信
	hasCO2Sensor = Stcc4::isConnected();
	if(hasCO2Sensor) Stcc4::initialize(); //CO2センサ
	Sht4x::initialize(Sht4x::SHT4_AD); //温湿度センサ
	Sht4x::initialize(Sht4x::SHT4_BD); //グローブ温度センサ
	Vcnl4030::initialize(); //照度計
	XbeeController::initialize();  //XBee（UART）
	
	//タイマ初期化
	initializeTimer();
		
	//XBeeスリープ解除
	wakeupXbee();
	
	//初期設定が終わったら少し待機
	_delay_ms(500);

	//XBee設定確認
	if(!XbeeController::xbeeSettingInitialized()) showError(2);

	//割り込み再開
	sei();

	//通信再開フラグを確認
	if(EepromManager::mSettings.start_auto)
	{
		logging = true;
		outputToXBee = true;
		outputToBLE = outputToFM = false;
	}
	
	//10秒以上電圧不足時間が継続したら終了
    while (lowBatteryTime <= 10)
    {
		bool work_done_this_cycle = false;
		
		//マウントできていなければとにかくマウント
		if(!initFM)
			initFM = (f_mount(&fSystem, "", 1) == FR_OK);

		// UARTリングバッファにデータがあれば、すべて処理する
		while (UartDriver::uartRingBufferHasData())
		{
			work_done_this_cycle = true;			
			char received_byte = UartDriver::uartRingBufferGet();
			
			// 1バイトずつパーサーに渡し、戻り値がtrueならXbeeフレームの受信が完了
			if (XbeeController::processXbeeByte(received_byte, xbee_payload_buffer, sizeof(xbee_payload_buffer)))
				appendCommand(xbee_payload_buffer);
		}
		
		//1秒毎の処理
		if(process_logging_flag){
			process_logging_flag = false;
			executeSecondlyTask();
		}
		
		//処理が無ければスリープさせる
		if(!work_done_this_cycle){
			//スリープモード設定
			if(logging)
			{
				//XBee通信時または電源接続常設モード時はIDLEスリープ
				if(outputToBLE || EepromManager::mSettings.start_auto) 
				{
					set_sleep_mode(SLEEP_MODE_IDLE);
					wakeupXbee();
				}
				//電池によるZigbee通信時は省エネ重視でパワーダウン
				else 
				{
					set_sleep_mode(SLEEP_MODE_PWR_DOWN);
					sleepXbee();
				}
			}
			else set_sleep_mode(SLEEP_MODE_IDLE); //ロギング開始前はUART通信ができるようにIDLEでスリープ
		
			//マイコンをスリープさせる
			sleep_mode();
		}
    }
	
	//電池が不足した場合のみ、ここまでたどり着く
	if(initFM) f_mount(NULL, "", 1); //FMをアンマウント
	cli(); //割り込み終了
	showError(1); //LED表示
}

static void initializePort(void)
{
	//SPI通信のための初期処理はmmc.cのpower_on()関数内
	//***
	
	//出力ポート
	PORTA.DIRSET = PIN6_bm; //緑LED出力
	PORTA.DIRSET = PIN7_bm; //赤LED出力
	PORTF.DIRSET = PIN5_bm; //XBeeスリープ制御
	PORTA.DIRSET = PIN4_bm; //微風速計5V昇圧
	PORTD.DIRSET = PIN6_bm; //UART RTS（Lowで受信可能）
	sleepAnemo();   //微風速計は電池を消費するので、すぐにスリープする

	//入力ポート
	PORTD.DIRCLR = PIN2_bm; //風速センサAD変換
	PORTF.DIRCLR = PIN4_bm; //汎用AD変換1
	
	//プルアップ/ダウン
	PORTA.OUTSET = PIN2_bm; //PORTA PIN2(Interrupt)：アップ
	PORTD.OUTCLR = PIN6_bm; //UART RTS（Lowで受信可能）：ダウン
	
	//未使用ポート
	PORTA.PIN5CTRL = PORT_ISC_INPUT_DISABLE_gc; // PA5
	PORTD.PIN1CTRL = PORT_ISC_INPUT_DISABLE_gc; // PD1
	PORTD.PIN3CTRL = PORT_ISC_INPUT_DISABLE_gc; // PD3
	PORTD.PIN4CTRL = PORT_ISC_INPUT_DISABLE_gc; // PD4
}

//タイマ初期化
static void initializeTimer( void )
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

static void appendCommand(const char* payload)
{
	// cmdBuffに追記していくための現在位置(pos1)を探す（フレームをまたいでコマンドが分割されている場合に対応するため）
	unsigned int pos1 = 0;
	while(cmdBuff[pos1] != '\0' && pos1 < MAX_CMD_CHAR) pos1++;

	// 新しいペイロード(payload)を1文字ずつ読みながら、cmdBuffに追記していく
	unsigned int pos2 = 0;
	while (payload[pos2] != '\0' && pos1 < MAX_CMD_CHAR - 1)
	{
		char nxtC = payload[pos2];
		cmdBuff[pos1] = nxtC;

		// 改行コード(\r)が見つかったら、コマンドが完成したとみなし処理する
		if (nxtC == '\r')
		{
			cmdBuff[pos1 + 1] = '\0';      // 文字列を終端させる
			solveCommand(cmdBuff);        // コマンド実行
			cmdBuff[0] = '\0';             // 実行後、組立用バッファをクリア
			pos1 = 0;                      // 組立用バッファの位置を先頭にリセット
		}
		else pos1++; // 次の位置へ
		
		pos2++; // ペイロードの次の文字へ
	}

	// ペイロードの最後に\rが無く、コマンドが中途半端な形で終わった場合
	// （次のフレームに続く場合）に備えて、cmdBuffをNULL終端しておく
	cmdBuff[pos1] = '\0';
}

static void solveCommand(const char *command)
{	
	//バージョン
	if (strncmp(command, "VER", 3) == 0) 
		XbeeController::bltxChars(VERSION_NUMBER);
	//ロギング開始
	else if (strncmp(command, "STL", 3) == 0)
	{
		//現在時刻を設定（最終計測日時=現在時刻とする）
		time_t ct; //最終計測日時
		char num[11];
		num[10] = '\0';
		strncpy(num, command + CMD_LENGTH, 10);
		ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
			currentTime = atol(num);
			ct = currentTime - UNIX_OFFSET; //最終計測日時=現在時刻とする
		}
		gmtime_r(&ct, &lastSavedTime);
		
		//Bluetooth接続か否か(xはxbee,bはbluetooth)
		outputToXBee = (command[13]=='t' || command[13]=='e'); //XBeeで親機に書き出すか否か
		outputToBLE = (command[14]=='t'); //Bluetoothで書き出すか否か
		outputToFM = (command[15]=='t'); //FMに書き出すか否か
		
		//きりのよい秒で計測開始
		pass_counters.th = getNormTime(lastSavedTime, EepromManager::mSettings.interval_th);
		pass_counters.glb = getNormTime(lastSavedTime, EepromManager::mSettings.interval_glb);
		pass_counters.vel = getNormTime(lastSavedTime, EepromManager::mSettings.interval_vel);
		pass_counters.ill = getNormTime(lastSavedTime, EepromManager::mSettings.interval_ill);
		pass_counters.ad1 = getNormTime(lastSavedTime, EepromManager::mSettings.interval_AD1);
		pass_counters.co2 = getNormTime(lastSavedTime, EepromManager::mSettings.interval_co2);
		
		//ロギング設定をEEPROMに保存
		EepromManager::mSettings.start_auto = command[13]=='e'; //Endlessロギング
		EepromManager::setMeasurementSetting();
		
		//ロギング開始
		XbeeController::bltxChars("STL\r");
		_delay_ms(100);
		logging = true;	
	}
	//Change Measurement Settings
	else if(strncmp(command, "CMS", 3) == 0)
	{
		//設定を変更する場合にはロギングを停止させる
		logging = false;
			
		//測定の是非
		EepromManager::mSettings.measure_th = (command[3] == 't');
		EepromManager::mSettings.measure_glb = (command[9] == 't');
		EepromManager::mSettings.measure_vel = (command[15] == 't');
		EepromManager::mSettings.measure_ill = (command[21] == 't');
		EepromManager::mSettings.measure_AD1 = (command[37] == 't');
		EepromManager::mSettings.measure_AD2 = (command[43] == 't');
		EepromManager::mSettings.measure_AD3 = (command[49] == 't');
		EepromManager::mSettings.measure_Prox = (command[55] == 't');
		//バージョンが低い場合の処理
		if(56 < strlen(command)) 
			EepromManager::mSettings.measure_co2 = (command[56] == 't');
		else EepromManager::mSettings.measure_co2 = false;
		
		//測定時間間隔
		char num[6];
		num[5] = '\0';
		strncpy(num, command + 4, 5);
		EepromManager::mSettings.interval_th = atoi(num);
		strncpy(num, command + 10, 5);
		EepromManager::mSettings.interval_glb = atoi(num);
		strncpy(num, command + 16, 5);
		EepromManager::mSettings.interval_vel = atoi(num);
		strncpy(num, command + 22, 5);
		EepromManager::mSettings.interval_ill = atoi(num);
		strncpy(num, command + 38, 5);
		EepromManager::mSettings.interval_AD1 = atoi(num);
		strncpy(num, command + 44, 5);
		EepromManager::mSettings.interval_AD2 = atoi(num);
		strncpy(num, command + 50, 5);
		EepromManager::mSettings.interval_AD3 = atoi(num);
		//バージョンが低い場合の処理
		if(56 < strlen(command))
		{
			strncpy(num, command + 57,5);
			EepromManager::mSettings.interval_co2 = atoi(num);
		}
		else EepromManager::mSettings.interval_co2 = 60;

		//計測開始時刻
		char num2[11];
		num2[10] = '\0';
		strncpy(num2, command + 27, 10);
		EepromManager::mSettings.start_dt = atol(num2);
		
		//ロギング設定をEEPROMに保存
		EepromManager::setMeasurementSetting();
		
		//ACK
		sprintf(charBuff, "CMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld,%d,%u,%d,%u,%d,%u,%d,%d,%u\r",
			EepromManager::mSettings.measure_th, EepromManager::mSettings.interval_th, 
			EepromManager::mSettings.measure_glb, EepromManager::mSettings.interval_glb, 
			EepromManager::mSettings.measure_vel, EepromManager::mSettings.interval_vel, 
			EepromManager::mSettings.measure_ill, EepromManager::mSettings.interval_ill, 
			EepromManager::mSettings.start_dt,
			EepromManager::mSettings.measure_AD1, EepromManager::mSettings.interval_AD1, 
			EepromManager::mSettings.measure_AD2, EepromManager::mSettings.interval_AD2, 
			EepromManager::mSettings.measure_AD3, EepromManager::mSettings.interval_AD3,
			EepromManager::mSettings.measure_Prox,
			EepromManager::mSettings.measure_co2, EepromManager::mSettings.interval_co2);
		XbeeController::bltxChars(charBuff);
	}
	//Load Measurement Settings
	else if(strncmp(command, "LMS", 3) == 0)
	{
		sprintf(charBuff, "LMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld,%d,%u,%d,%u,%d,%u,%d,%d,%u\r",
			EepromManager::mSettings.measure_th, EepromManager::mSettings.interval_th,
			EepromManager::mSettings.measure_glb, EepromManager::mSettings.interval_glb,
			EepromManager::mSettings.measure_vel, EepromManager::mSettings.interval_vel,
			EepromManager::mSettings.measure_ill, EepromManager::mSettings.interval_ill, 
			EepromManager::mSettings.start_dt,
			EepromManager::mSettings.measure_AD1, EepromManager::mSettings.interval_AD1,
			EepromManager::mSettings.measure_AD2, EepromManager::mSettings.interval_AD2,
			EepromManager::mSettings.measure_AD3, EepromManager::mSettings.interval_AD3,
			EepromManager::mSettings.measure_Prox,
			EepromManager::mSettings.measure_co2, EepromManager::mSettings.interval_co2);
		XbeeController::bltxChars(charBuff);
	}
	//End Logging
	else if(strncmp(command, "ENL", 3) == 0)
	{
		logging = false;
		XbeeController::bltxChars("ENL\r");
	}
	//Set Correction Factor
	else if(strncmp(command, "SCF", 3) == 0)
	{
		EepromManager::setCorrectionFactor(command);
		EepromManager::makeCorrectionFactorString(charBuff, "SCF");
		XbeeController::bltxChars(charBuff);
	}
	//Load Correction Factor
	else if(strncmp(command, "LCF", 3) == 0)
	{
		EepromManager::makeCorrectionFactorString(charBuff, "LCF");
		XbeeController::bltxChars(charBuff);
	}	
	//Set Velocity Characteristics
	else if(strncmp(command, "SVC", 3) == 0)
	{
		EepromManager::setVelocityCharacteristics(command);
		EepromManager::makeVelocityCharateristicsString(charBuff, "SVC");
		XbeeController::bltxChars(charBuff);
	}
	//Load Velocity Characteristics
	else if(strncmp(command, "LVC", 3) == 0)
	{
		EepromManager::makeVelocityCharateristicsString(charBuff, "LVC");
		XbeeController::bltxChars(charBuff);
	}	
	//Change Logger Name
	else if(strncmp(command, "CLN", 3) == 0)
	{
		strncpy(EepromManager::mlName, command + CMD_LENGTH, 21);
		EepromManager::saveName();
		
		//ACK
		char ack[22 + 4];
		sprintf(ack, "CLN:%s\r", EepromManager::mlName);		
		XbeeController::bltxChars(ack);
	}
	//Load Logger Name
	else if(strncmp(command, "LLN", 3) == 0)
	{
		char name[22 + 4];
		sprintf(name, "LLN:%s\r", EepromManager::mlName);
		XbeeController::bltxChars(name);
	}
	//風速の手動校正開始
	else if(strncmp(command, "SCV", 3) == 0) 
	{
		wakeupAnemo();
		calibratingVelocityVoltage = true;
		//以降、毎秒"SCV:電圧"が送信される
	}
	//風速の手動校正終了
	else if(strncmp(command, "ECV", 3) == 0)
	{
		sleepAnemo();
		calibratingVelocityVoltage = false;
		XbeeController::bltxChars("ECV\r");
	}
	//CO2センサの有無
	else if(strncmp(command, "HCS", 3) == 0)
	{
		XbeeController::bltxChars(hasCO2Sensor ? "HCS:1\r" : "HCS:0\r");
	}
	//現在時刻の更新
	else if (strncmp(command, "UCT", 3) == 0)
	{
		//現在時刻を設定
		char num[11];
		num[10] = '\0';
		strncpy(num, command + CMD_LENGTH, 10);
		ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
			currentTime = atol(num);
		}
		XbeeController::bltxChars("UCT\r");
	}
}

// Timer1割り込み//FatFs（FM入出力通信）用
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
	
	ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
		currentTime++; //1秒進める
	}
	
	//1秒毎の処理フラグを立てる
	process_logging_flag = true;
}

static void executeSecondlyTask(){
	//電圧確認（限界まで使うとFlashメモリのデータが破損することがある）
	if(isLowBattery()) lowBatteryTime++;
	else lowBatteryTime = 0;

	//リセットボタン押し込み確認********************************
	if(!(PORTA.IN & PIN2_bm))
	{
		resetTime++;
		if(resetTime == 3)
		{
			//Endlessロギングを解除
			EepromManager::mSettings.start_auto = false;
			EepromManager::setMeasurementSetting();
			
			logging=false;	//ロギング停止
			initFM = false;	//FMを再マウント
			sleepAnemo();	//風速センサを停止
			blinkRedLED(3);	//赤LED点滅
			return;
		}
	}
	else resetTime = 0; //Resetボタン押し込み時間を0に戻す
	
	//風速計校正処理*******************************************
	if(calibratingVelocityVoltage) calibrateVelocityVoltage();
	
	//ロギング中であれば****************************************
	else if(logging) execLogging();
	
	//待機中であれば****************************************
	else
	{
		sleepAnemo(); //風速計を停止 2023.01.09 Bugfix
		wakeupXbee(); //XBeeスリープ解除
		_delay_ms(1); //スリープ解除時の立ち上げは50us=0.05ms程度かかるらしい
		
		//定期的にコマンド待受状態を送信
		if(wc_time <= 0)
		{
			XbeeController::bltxChars("WFC\r"); //Waiting for command.
			wc_time = 6;
		}
		wc_time--;
		
		//明滅
		if((PORTA.IN & PIN2_bm))  //Reset押し込み中は明滅停止
		blinkGreenLED(initFM ? 2 : 1);
	}
}

static void execLogging()
{
	//FM書き出しでマウント前の場合には赤LEDでアラート
	if(outputToFM && !initFM) blinkRedLED(1);
	
	//自動計測開始がOffで計測開始時刻の前ならば終了
	time_t current_snapshot;
	ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
		current_snapshot = currentTime;
	}
	if(!EepromManager::mSettings.start_auto && current_snapshot < EepromManager::mSettings.start_dt) return;
	
	//ロギング中は5秒ごとに点灯
	blinkCount++;
	if(5 <= blinkCount)
	{
		//FM書き出しでマウントできていない場合には点灯しない
		if(!(outputToFM && !initFM)) blinkGreenLED(1);
		blinkCount = 0;
	}
	
	//計測のWAKEUP_TIME[sec]前から熱線式風速計回路のスリープを解除して加熱開始
	if(EepromManager::mSettings.measure_vel && EepromManager::mSettings.interval_vel - pass_counters.vel < V_WAKEUP_TIME) wakeupAnemo();
	
	bool hasNewData = false;
	char tmpS[7] = "n/a"; //-10.00 ~ 50.00//6文字+\r
	char hmdS[7] = "n/a"; //0.00 ~ 100.00//6文字+\r
	char glbTS[7] = "n/a"; //-10.00 ~ 50.00//6文字+\r
	char glbVS[7] = "n/a";
	char velS[7] = "n/a"; //0.0000 ~ 1.5000//6文字+\r
	char velVS[7] = "n/a";
	char illS[9] = "n/a"; //0.01~83865.60
	char adV1S[7] = "n/a";
	char co2S[6] = "n/a"; //0~65535//5文字+\r
	
	//微風速測定************
	pass_counters.vel++;
	if(EepromManager::mSettings.measure_vel && (int)EepromManager::mSettings.interval_vel <= pass_counters.vel)
	{
		double velV = readVelVoltage(); //AD変換
		dtostrf(velV,6,4,velVS);
				
		float bff = max(0, velV / EepromManager::cFactors.vel0 - 1.0);
		float vel = 0;
		if(IS_VEL_FNC2)
			vel = EepromManager::vcCoefficients.ccB * pow(bff,EepromManager::vcCoefficients.ccA);
		else
			vel = bff * (EepromManager::vcCoefficients.ccC + bff * (EepromManager::vcCoefficients.ccB + bff * EepromManager::vcCoefficients.ccA)); //電圧-風速換算式
		dtostrf(vel,6,4,velS);
		
		pass_counters.vel = 0;
		hasNewData = true;
		//次の起動時刻が起動に必要な時間よりも後の場合には微風速計回路をスリープ
		if(V_WAKEUP_TIME <= EepromManager::mSettings.interval_vel) sleepAnemo();
	}
	
	//CO2測定************	
	pass_counters.co2++;	
	if(EepromManager::mSettings.measure_co2 && (int)EepromManager::mSettings.interval_co2 <= pass_counters.co2)
	{
		uint16_t co2_u = 0;
		float tmp_f = 0;
		float hmd_f = 0;
		if(Stcc4::readMeasurement(&co2_u, &tmp_f, &hmd_f)) sprintf(co2S, "%u\n", co2_u);
		Stcc4::enterSleep(); //スリープ
		pass_counters.co2 = 0;
	}
	
	//温湿度測定************
	pass_counters.th++;
	//CO2計測する場合には1秒前に温湿度を通知して計測指令を出す必要がある
	bool mesCo2m1 = EepromManager::mSettings.measure_co2 && (int)EepromManager::mSettings.interval_co2 - 1 <= pass_counters.co2;
	bool mesTH = EepromManager::mSettings.measure_th && (int)EepromManager::mSettings.interval_th <= pass_counters.th;
	if(mesCo2m1 || mesTH)
	{
		float tmp_f = 0;
		float hmd_f = 0;
		if(Sht4x::readValue(&tmp_f, &hmd_f, Sht4x::SHT4_BD))
		{
			if(mesCo2m1){
				Stcc4::exitSleep(); //起こす
				Stcc4::setRHTCompensation(tmp_f, hmd_f);
				Stcc4::measureSingleShot();
			}

			tmp_f = max(-10,min(50,EepromManager::cFactors.dbtA *(tmp_f) + EepromManager::cFactors.dbtB));
			hmd_f = max(0,min(100,EepromManager::cFactors.hmdA *(hmd_f) + EepromManager::cFactors.hmdB));
			dtostrf(tmp_f,6,2,tmpS);
			dtostrf(hmd_f,6,2,hmdS);
		}
		if(mesTH)
		{
			pass_counters.th = 0;
			hasNewData = true;
		}
	}
	
	//グローブ温度測定************
	pass_counters.glb++;
	if(EepromManager::mSettings.measure_glb && (int)EepromManager::mSettings.interval_glb <= pass_counters.glb)
	{
		float glbT = 0;
		float glbH = 0;
		if(Sht4x::readValue(&glbT, &glbH, Sht4x::SHT4_AD))
		{
			glbT = max(-10,min(50,EepromManager::cFactors.glbA * glbT + EepromManager::cFactors.glbB));
			dtostrf(glbT,6,2,glbTS);
		}
		
		pass_counters.glb = 0;
		hasNewData = true;
	}
	
	//照度センサ測定**************
	pass_counters.ill++;
	if(EepromManager::mSettings.measure_ill && (int)EepromManager::mSettings.interval_ill <= pass_counters.ill)
	{
		//近接計
		if(EepromManager::mSettings.measure_Prox)
		{
			float ill_d;
			Vcnl4030::readPS(&ill_d);
			dtostrf(ill_d,8,2,illS);
		}
		//照度計
		else
		{
			float ill_d;
			Vcnl4030::readALS(&ill_d);
			ill_d /= TRANSMITTANCE;
			ill_d = max(0,min(99999.99,EepromManager::cFactors.luxA * ill_d + EepromManager::cFactors.luxB));
			dtostrf(ill_d,8,2,illS);
		}
		pass_counters.ill = 0;
		hasNewData = true;
	}
	
	//汎用AD変換測定1
	pass_counters.ad1++;
	if(EepromManager::mSettings.measure_AD1 && (int)EepromManager::mSettings.interval_AD1 <= pass_counters.ad1)
	{
		float adV = readVoltage(1); //AD変換
		dtostrf(adV,6,4,adV1S);
		pass_counters.ad1 = 0;
		hasNewData = true;
	}
	
	//新規データがある場合は送信
	if(hasNewData)
	{		
		if(outputToXBee || outputToBLE)
		{
			slp_time=0; //空パケットまでの時間を初期化
			wakeupXbee(); //XBeeスリープ解除
			_delay_ms(1); //スリープ解除時の立ち上げは50us=0.05ms程度かかるらしい。短すぎるとコンデンサの影響か十分に立ち上がらない。。。
		}
		
		//空白削除して左詰め
		alignLeft(tmpS);
		alignLeft(hmdS);
		alignLeft(glbTS);
		alignLeft(glbVS);
		alignLeft(velS);
		alignLeft(velVS);
		alignLeft(illS);
		alignLeft(adV1S);
		
		//日時を作成
		time_t ct;
		ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
			ct = currentTime - UNIX_OFFSET;
		}
		tm dtNow;
		gmtime_r(&ct, &dtNow);
		
		//書き出し文字列を作成
		snprintf(charBuff, sizeof(charBuff), "DTT:%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s,%s,n/a,n/a,%s\r",
		dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
		tmpS, hmdS, glbTS, velS, illS, "n/a", velVS, adV1S, co2S);
		
		//文字列オーバーに備えて最後に終了コード'\r\0'を入れておく
		charBuff[MAX_CMD_CHAR-2]='\r';
		charBuff[MAX_CMD_CHAR-1]='\0';

		if(outputToXBee) 
			XbeeController::txChars(charBuff); //XBee Zigbee出力
		if(outputToBLE) 
			XbeeController::blChars(charBuff); //XBee Bluetooth出力
		if(outputToFM)  //FM出力
		{
			//データが十分に溜まるか、1min以上の時間間隔があいたら書き出す。1h間隔の書き出しのために3行目も必要
			if(N_LINE_BUFF <= buffNumber 
				|| lastSavedTime.tm_min != dtNow.tm_min 
				|| lastSavedTime.tm_hour != dtNow.tm_hour)
			{
				writeFlashMemory(lastSavedTime, lineBuff); //FM出力
				buffNumber = 0;
				lineBuff[0] = '\0';
				lastSavedTime = dtNow;
			}
			
			//FM書き出し時は冒頭のDTTが不要。
			char *trmChar = charBuff + 4;
			strcat(lineBuff, trmChar);
			buffNumber++;
		}
	}
	slp_time++;
	//この処理は意外に電池を消耗するので時間間隔を増やしXBee使用時のみとした（2024.07.22）
	if(3500 <= slp_time && (outputToXBee || outputToBLE)){
		wakeupXbee(); //XBeeスリープ解除
		_delay_ms(1);  //スリープ解除時の立ち上げは50us=0.05ms程度かかるらしい。
		XbeeController::txChars("\r"); //ネットワーク切断回避用の空パケットを送信（この処理は悪い）
		slp_time = 0;
	}
	
	//UART送信が終わったら10msec待ってXBeeをスリープさせる(XBee側の送信が終わるまで待ちたいので)
	//本来、ここはCTSを使って受信可能になったタイミングでスリープか？フローコントロールを検討。
	_delay_ms(10); //このスリープはXBeeの通信終了待ち目的。試行錯誤で用意した値なので、根拠が曖昧。そもそもここではないようにも思う	
}

static void calibrateVelocityVoltage()
{
	//LED点灯
	blinkGreenAndRedLED(1);
	
	char velVS[7] = "n/a";
	double velV = readVelVoltage(); //AD変換
	dtostrf(velV,6,4,velVS);	
	snprintf(charBuff, sizeof(charBuff), "SCV:%s\r", velVS);
	XbeeController::bltxChars(charBuff);
}

static void writeFlashMemory(const tm dtNow, const char write_chars[])
{
	//マウント未完了ならば終了
	if(!initFM) return;
	
	char fileName[13]={}; //yyyymmdd.csv
	snprintf(fileName, sizeof(fileName), "%04d%02d%02d.csv", dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday);
	
	//FM記録用日付更新
	myRTC.year=dtNow.tm_year+1900;
	myRTC.month=dtNow.tm_mon+1;
	myRTC.mday=dtNow.tm_mday;
	myRTC.hour=dtNow.tm_hour;
	myRTC.min=dtNow.tm_min;
	myRTC.sec=dtNow.tm_sec;
	
	// FILオブジェクトローカル変数としてスタックに確保
	FIL fmFile;
	if(f_open(&fmFile, fileName, FA_OPEN_APPEND | FA_WRITE) == FR_OK)
	{
		f_puts(write_chars, &fmFile);
		f_close(&fmFile);
	}
	else initFM = false; // エラー時は再マウントを試みる
}

//PORTA PIN2割り込み（リセットスイッチ押し込み）
ISR(PORTA_PORT_vect)
{
	// 割り込みフラグ解除
	PORTA.INTFLAGS = PIN2_bm;
	
	//Push:LED点灯, None:LED消灯
	if(PORTA.IN & PIN2_bm) turnOffRedLED();
	else turnOnRedLED();
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
	else return 0.0; //AD2, AD3廃止

	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //変換開始
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //変換終了待ち
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64回平均)
}

//電池残量が小さくなったか否か
//内部電源は3.3Vに昇圧しているが基準電圧の2.0Vはレギュレータで作っているため、電圧降下時には後者のみが不足することを利用
static bool isLowBattery(void)
{
	ADC0.MUXPOS = 0x44; //VDDDIV10: VDD divided by 10（0.33V程度）
	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //変換開始
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //変換終了待ち
	volatile float vdd = 10.0 * 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64回平均)
	
	//基準電圧が低くなるため、3.3Vが大きめに計測される。1割増となったときに電力不足と判定
	return 3.3 * 1.1 < vdd;
}

//エラー表示
static void showError(short int errNum)
{	
	switch(errNum){
		case 1: //電池不足
			while(true)
			{
				blinkRedLED(1);
				_delay_ms(3000);
			}
		case 2: //XBee設定エラー
			while(true)
			{
				blinkRedLED(2);
				_delay_ms(3000);
			}
	}
}

static void alignLeft(char *str) {
	// 文字列がNULL・空でない
	if (str != NULL && *str != '\0') 
	{
		int len = strlen(str);

		//空白をカウント
		int i;
		for (i = 0; i < len && str[i] == ' '; i++);

		// 文字列を左詰めに移動する
		if (i > 0)
			memmove(str, str + i, len - i + 1);
	}
}

//きりの良い時刻になるように最初の計測時間間隔を調整する
static int getNormTime(tm time, unsigned int interval)
{	
	if(interval == 1) return interval; //1secの場合には直ちに計測
	if(interval <= 5) return interval - (5 - time.tm_sec % 5);
	else if(interval <= 10) return interval - (10 - time.tm_sec % 10);
	else if(interval <= 30) return interval - (30 - time.tm_sec % 30);
	else return interval - (60 - time.tm_sec % 60);
}

//以下はinline関数************************************

inline static void sleepAnemo(void)
{
	PORTA.OUTCLR = PIN4_bm; //5V昇圧停止
}

inline static void wakeupAnemo(void)
{
	PORTA.OUTSET = PIN4_bm; //5V昇圧開始
}

inline static void sleepXbee(void)
{
	PORTF.OUTSET = PIN5_bm;
}

inline static void wakeupXbee(void)
{
	PORTF.OUTCLR = PIN5_bm;
}

inline static void blinkLED(int iterNum, uint8_t pin_mask)
{
	if(iterNum < 1) return;

	// 点滅の前に一度消灯
	PORTA.OUTCLR = pin_mask;

	// 指定回数点滅
	for(int i=0; i < iterNum; i++)
	{
		_delay_ms(100);
		PORTA.OUTSET = pin_mask; // 点灯
		_delay_ms(25);
		PORTA.OUTCLR = pin_mask; // 消灯
	}
}

inline static void blinkGreenAndRedLED(int iterNum)
{
	blinkLED(iterNum, PIN6_bm | PIN7_bm);
}

inline static void turnOnGreenLED(void)
{
	PORTA.OUTSET = PIN7_bm; //点灯
}

inline static void turnOffGreenLED(void)
{
	PORTA.OUTCLR = PIN7_bm; //消灯
}

inline static void toggleGreenLED(void)
{
	PORTA.OUTTGL = PIN7_bm; //反転
}

inline static void blinkGreenLED(int iterNum)
{
	blinkLED(iterNum, PIN7_bm);
}

inline static void turnOnRedLED(void)
{
	PORTA.OUTSET = PIN6_bm; //点灯
}

inline static void turnOffRedLED(void)
{
	PORTA.OUTCLR = PIN6_bm; //消灯
}

inline static void toggleRedLED(void)
{
	PORTA.OUTTGL = PIN6_bm; //反転
}

inline static void blinkRedLED(int iterNum)
{
	blinkLED(iterNum, PIN6_bm);
}

inline static float max(float x, float y)
{
	return (x > y) ? x : y;
}

inline static float min(float x, float y)
{
	return (x < y) ? x : y;
}