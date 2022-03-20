/**
 * @file main.cpp
 * @brief AVR(ATMega328)を使用した計測データ収集・送信プログラム
 * @author E.Togashi
 * @date 2020/7/14
 *
 * version履歴
 * 2.3.0	EEPROMを使って補正係数を管理する方式に変更,年月日を直接に出力,MicroSD書き出し対応開始
 * 2.3.2    Resetスイッチの入力に対応。
 * 2.3.3    AD変換安定化のために反復回数増加。10回→35回。UART通信,XBee通信を色々と改良。MicroSD書き出し対応、一応出来上がり。
 * 2.3.4    XBee通信にCTS制御を導入
 * 2.3.5	リングバッファによるUART通信を取りやめ
 * 2.4.1	AHT20に対応。AD変換基準電圧：風速の基準をAVCC, グローブ温度の基準を内部1.1Vに変更
 * 2.4.2	EEPROMの初期化バグ修正
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
#include <util/delay.h>

//日付操作用
#include <time.h>

#include "main.h"
#include "mlerr.h" //エラーコード
#include "my_eeprom.h" //EEPROM処理
#include "my_uart.h" //UART通信
#include "my_i2c.h"  //I2C通信
#include "my_xbee.h" //XBee通信

//FatFs関連
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//定数宣言***********************************************************
//AD変換の安定化のための繰り返し回数
const uint8_t AD_ITER = 35;

//熱線式風速計の立ち上げに必要な時間[sec]
const uint8_t V_WAKEUP_TIME = 20;

//照度センサ（OPTxxxx）のアドレス
const char OPT_ADDRESS = 0x88; //OPT3001は0x88, OPT3007は0x8A, を使用。

//グローブ温度計測用モジュールのタイプ
const bool IS_MCP9700 = true;

//AM2320かAHT20か
const bool IS_AM2320 = false;

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

//WFCを送信するまでの残り時間[sec]
static uint8_t wc_time = 0;

//SDカード関連
volatile bool initSD = false; //SDカード初期化フラグ
static FATFS* fSystem;

//汎用の文字列配列
static char charBuff[my_xbee::MAX_CMD_CHAR];

//マクロ定義********************************************************
#define cbi(addr, bit) addr &= ~(1 << bit) // addrのbit目を'0'にする。
#define sbi(addr, bit) addr |= (1 << bit)  // addrのbit目を'1'にする。
#define ARRAY_LENGTH(array) (sizeof(array) / sizeof(array[0]))

int main(void)
{
	//EEPROM
	my_eeprom::LoadCorrectionFactor();
	my_eeprom::LoadMeasurementSetting();
	
	//入出力ポートを初期化
	initialize_port();

	//一旦、すべての割り込み禁止
	cli();
		
	//AD変換有効化
	ADCSRA = 0b10000111; //128分周で計測（大きい方が精度は高く、時間はかかる模様）
	
	//INT0割り込み設定
	sbi(MCUSR, ISC01);
	sbi(EIMSK, INT0);
		
	//通信を初期化
	my_i2c::InitializeI2C(); //I2C
	my_i2c::InitializeOPT(OPT_ADDRESS);  //OPTxxxx
	my_uart::Initialize();  //XBee（UART）
	
	//タイマ初期化
	initialize_timer();
	
	//XBeeスリープ解除
	wakeup_xbee();
	
	//FatFS操作のためのメモリ確保
	fSystem = (FATFS *)malloc(sizeof(FATFS));
	
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
		if(logging && !outputToBLE) set_sleep_mode(SLEEP_MODE_PWR_SAVE);
		else set_sleep_mode(SLEEP_MODE_IDLE); //ロギング開始前はUART通信ができるようにIDLEでスリープ
											
		//スリープ
		sleep_mode();
    }
}

static void initialize_port(void)
{
	//出力ポート
	sbi(DDRD, DDD3); //SPI通信（CS）
	sbi(DDRD, DDD5); //LED出力
	sbi(DDRD, DDD1); //RXD:UART書き出し
	sbi(DDRD, DDD7); //XBeeスリープ制御
	sbi(DDRC, DDC1); //微風速計リレー
	sbi(DDRD, DDB0); //微風速計5V昇圧
	sleep_anemo();   //微風速計は電池を消費するので、すぐにスリープする

	//入力ポート
	cbi(DDRC, DDC0); //グローブ温度センサAD変換
	cbi(DDRC, DDC2); //風速センサAD変換
	cbi(DDRD, DDD0); //RXD:UART読み込み
	cbi(DDRD, DDD2); //INT0:リセット用割り込み
	sbi(PORTD, PORTD2); //INT0をプルアップ
}

//タイマ初期化
static void initialize_timer( void )
{
	//Timer 1 //0.01secタイマ
	OCR1A = (uint16_t)( ( F_CPU / 8L ) / 100L );	// カウントは8MHz/8/100(100Hz)
	TCNT1 = 0;	//初期化
	TCCR1A = 0b00000000;			// CTC動作
	TCCR1B = (0x02 | _BV(WGM12));	// 8分周
	TIMSK1 |= _BV(OCIE1A);			// 割り込み許可
	
	//Timer 2 //1secタイマ
	//外部クリスタル駆動設定
	ASSR |= (1<<AS2);
	TCNT2 = 0;	//初期化
	TCCR2A = 0b00000000;	// 標準（オーバーフロー）動作
	TCCR2B = 0b10000101;	// 128分周
	TIMSK2 |= _BV(TOIE2);	// 割り込み許可
}

//UART受信時の割り込み処理
ISR(USART_RX_vect)
{
	char dat = UDR0;	//読み出し
	
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
		my_xbee::bltx_chars("VER:2.4.2\r");
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
		
		//計測開始時刻
		char num2[11];
		num2[10] = '\0';
		strncpy(num2, command + 27, 10);
		startTime = atol(num2);
		
		//ACK
		sprintf(charBuff, "CMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld\r",
			my_eeprom::measure_th, my_eeprom::interval_th, 
			my_eeprom::measure_glb, my_eeprom::interval_glb, 
			my_eeprom::measure_vel, my_eeprom::interval_vel, 
			my_eeprom::measure_ill, my_eeprom::interval_ill, startTime);
		my_xbee::bltx_chars(charBuff);
	}
	//Load Measurement Settings
	else if(strncmp(command, "LMS", 3) == 0)
	{
		sprintf(charBuff, "LMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld\r",
			my_eeprom::measure_th, my_eeprom::interval_th, 
			my_eeprom::measure_glb, my_eeprom::interval_glb, 
			my_eeprom::measure_vel, my_eeprom::interval_vel, 
			my_eeprom::measure_ill, my_eeprom::interval_ill, startTime);
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
	
	//コマンドを削除
	cmdBuff[0] = '\0';
}

// Timer1割り込み//FatFs（SDカード入出力通信）用
ISR(TIMER1_COMPA_vect)
{
	disk_timerproc();	/* Drive timer procedure of low level disk I/O module */
}

// Timer2割り込み：ロギング用の1秒毎の処理
ISR(TIMER2_OVF_vect)
{
	currentTime++; //1秒進める
				
	//ロギング中であれば
	if(logging)
	{
		//計測開始時刻の前ならば終了
		if(currentTime < startTime) return;
		
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
		
		//微風速測定************
		pass_vel++;
		if(my_eeprom::measure_vel && my_eeprom::interval_vel <= pass_vel)
		{
			double velV = readVelVoltage(); //AD変換
			dtostrf(velV,6,4,velVS);
			
			float bff = max(0, velV / my_eeprom::Cf_vel0 - 1.0);
			float vel = bff * (2.3595 + bff * (-12.029 + bff * 79.744));
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
			float ill_d = my_i2c::ReadOPT(OPT_ADDRESS);
			ill_d = max(0,min(99999.99,my_eeprom::Cf_luxA * ill_d + my_eeprom::Cf_luxB));
			dtostrf(ill_d,8,2,illS);
			pass_ill = 0;
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
			snprintf(charBuff, sizeof(charBuff), "%s%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s\r",
			(outputToSDCard ? "" : "DTT:"), dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
			tmpS, hmdS, glbTS, velS, illS, glbVS, velVS);
			
			//文字列オーバーに備えて最後に終了コード'\r\0'を入れておく
			charBuff[my_xbee::MAX_CMD_CHAR-2]='\r';
			charBuff[my_xbee::MAX_CMD_CHAR-1]='\0';

			if(outputToXBee) my_xbee::tx_chars(charBuff); //XBee Zigbee出力
			if(outputToBLE) my_xbee::bl_chars(charBuff); //XBee Bluetooth出力
			if(outputToSDCard) writeSDcard(dtNow, charBuff); //SD card出力
		}
				
		//UART送信が終わったら10msec待ってXBeeをスリープさせる(XBee側の送信が終わるまで待ちたいので)
		//本来、ここはCTSを使って受信可能になったタイミングでスリープか？フローコントロールを検討。
		//while(! my_uart::tx_done()); //止まる

		_delay_ms(10);
		//Bluetooth通信でなければスリープに入る（XBeeの仕様上、Bluetoothモードのスリープは不可）
		if(!outputToBLE) sleep_xbee();
		if(outputToSDCard) blinkLED(1); //SDカード記録中は毎秒LED点滅
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
		blinkLED(initSD ? 2 : 1);
	}
}

//static void writeSDcard(const tm dtNow, const char write_chars[])
static void writeSDcard(const tm dtNow, const char* write_chars)
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

//INT0割り込み：計測中断処理
ISR(INT0_vect)
{
	//ロギング停止
	logging=false;
	
	//SDカード再マウント
	initSD = false;
		
	//風速センサを停止
	sleep_anemo();
	
	//LED点滅
	blinkLED(3);
}

//グローブ温度の電圧を読み取る
static float readGlbVoltage(void)
{
	long int refV = 0;
	long int adV = 0;
	for(int i=0;i<AD_ITER;i++)
	{
		//覚書
		//グローブ温度の電圧は0.7V程度なので、AREFとAVCCを切断して、内部電圧（1.1V）基準で計測すべき
		
		//基準1.1Vを計測
		ADMUX = 0b11101110;
		_delay_ms(5);
		ADCSRA = 0b11000111; //変換開始
		while(ADCSRA & 0b01000000); //変換終了待ち
		refV += ADC;

		//AD0を計測
		ADMUX = 0b11100000;
		_delay_ms(5);
		ADCSRA = 0b11000111; //変換開始
		while(ADCSRA & 0b01000000); //変換終了待ち
		adV += ADC;
	}
	return (float)adV / (float)refV * 1.1;
}

//微風速の電圧を読み取る
static float readVelVoltage(void)
{
	//平均値を出力して安定化
	long int refV = 0;
	long int adV = 0;
	for(int i=0;i<AD_ITER;i++)
	{
		//基準1.1Vを計測
		ADMUX = 0b01001110;
		_delay_ms(5);
		ADCSRA = 0b11000111; //変換開始
		while(ADCSRA & 0b01000000); //変換終了待ち
		refV += ADC;
	
		//AD2(Velocity)を計測
		ADMUX = 0b01000010;
		_delay_ms(5);
		ADCSRA = 0b11000111; //変換開始
		while(ADCSRA & 0b01000000); //変換終了待ち
		adV += ADC;
	}
	return (float)adV / (float)refV * 1.1;
}

static void sleep_anemo(void)
{
	cbi(PORTC, PORTC1); //リレー遮断
	cbi(PORTB, PORTB0); //5V昇圧停止
}

//以下はinline関数************************************

inline static void wakeup_anemo(void)
{
	sbi(PORTC, PORTC1); //リレー通電
	sbi(PORTB, PORTB0); //5V昇圧開始
}

inline static void sleep_xbee(void)
{
	sbi(PORTD, PORTD7);
}

inline static void wakeup_xbee(void)
{
	cbi(PORTD, PORTD7);
}

//LEDを点滅させる
inline static void blinkLED(int iterNum)
{
	if(iterNum < 1) return;

	//初回
	sbi(PORTD, PORTD5);
	_delay_ms(25);
	cbi(PORTD, PORTD5);
	
	//2回目以降は時間を空けて点滅
	for(int i=1;i<iterNum;i++)
	{
		_delay_ms(100);
		sbi(PORTD, PORTD5);
		_delay_ms(25);
		cbi(PORTD, PORTD5);
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