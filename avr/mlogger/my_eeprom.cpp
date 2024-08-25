/**
 * @file my_eeprom.h
 * @brief AVR(AVRxxDB32)のEEPROMを処理する
 * @author E.Togashi
 * @date 2021/12/19
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <avr/eeprom.h>

#include "my_eeprom.h"
#include "parameters.h"
//#include "global_variables.h"

//EEPROMの初期化フラグ。コンパイル後最初の呼び出しのみ初期化する
static uint8_t EEMEM EEP_INITFLAG;

//XBEEの初期化フラグ
static uint8_t EEMEM EEP_XB_INITFLAG;

//乾球温度補正係数A,B
static float EEMEM EEP_DBTCF_A;
static float EEMEM EEP_DBTCF_B;

//相対湿度補正係数A,B
static float EEMEM EEP_HMDCF_A;
static float EEMEM EEP_HMDCF_B;

//グローブ温度補正係数A,B
static float EEMEM EEP_GLBCF_A;
static float EEMEM EEP_GLBCF_B;

//照度補正係数A,B
static float EEMEM EEP_LUXCF_A;
static float EEMEM EEP_LUXCF_B;

//風速補正係数A,B,無風電圧
static float EEMEM EEP_VELCF_A;
static float EEMEM EEP_VELCF_B;
static float EEMEM EEP_VEL0;

//風速計特性係数
static float EEMEM EEP_VELCC_A;
static float EEMEM EEP_VELCC_B;
static float EEMEM EEP_VELCC_C;

//計測真偽
static uint8_t EEMEM EEP_MES_TH;
static uint8_t EEMEM EEP_MES_GLB;
static uint8_t EEMEM EEP_MES_VEL;
static uint8_t EEMEM EEP_MES_ILL;
static uint8_t EEMEM EEP_MES_AD1;
static uint8_t EEMEM EEP_MES_AD2;
static uint8_t EEMEM EEP_MES_AD3;
static uint8_t EEMEM EEP_MES_PRX;

//計測間隔
static unsigned int EEMEM EEP_STP_TH;
static unsigned int EEMEM EEP_STP_GLB;
static unsigned int EEMEM EEP_STP_VEL;
static unsigned int EEMEM EEP_STP_ILL;
static unsigned int EEMEM EEP_STP_AD1;
static unsigned int EEMEM EEP_STP_AD2;
static unsigned int EEMEM EEP_STP_AD3;

//計測開始日時
static uint32_t EEMEM EEP_START_DT;

//自動通信開始設定
static uint8_t EEMEM EEP_START_AUTO;

//ロガー名称
static char EEMEM EEP_NAME[21];

//補正係数
volatile float my_eeprom::Cf_dbtA = 1.0;
volatile float my_eeprom::Cf_dbtB = 0.0;
volatile float my_eeprom::Cf_hmdA = 1.0;
volatile float my_eeprom::Cf_hmdB = 0.0;
volatile float my_eeprom::Cf_glbA = 1.0;
volatile float my_eeprom::Cf_glbB = 0.0;
volatile float my_eeprom::Cf_luxA = 1.0;
volatile float my_eeprom::Cf_luxB = 0.0;
volatile float my_eeprom::Cf_velA = 1.0;
volatile float my_eeprom::Cf_velB = 0.0;
volatile float my_eeprom::Cf_vel0 = 1.45;

//風速計特性係数
volatile float my_eeprom::VelCC_A = 79.744;
volatile float my_eeprom::VelCC_B = -12.029;
volatile float my_eeprom::VelCC_C = 2.356;

//計測真偽
volatile bool my_eeprom::measure_th = true;
volatile bool my_eeprom::measure_glb = true;
volatile bool my_eeprom::measure_vel = true;
volatile bool my_eeprom::measure_ill = true;
volatile bool my_eeprom::measure_AD1 = false;
volatile bool my_eeprom::measure_AD2 = false;
volatile bool my_eeprom::measure_AD3 = false;
volatile bool my_eeprom::measure_Prox = false;

//計測間隔
volatile unsigned int my_eeprom::interval_th = 1;
volatile unsigned int my_eeprom::interval_glb = 1;
volatile unsigned int my_eeprom::interval_vel = 1;
volatile unsigned int my_eeprom::interval_ill = 1;
volatile unsigned int my_eeprom::interval_AD1 = 1;
volatile unsigned int my_eeprom::interval_AD2 = 1;
volatile unsigned int my_eeprom::interval_AD3 = 1;

//計測開始日時
volatile uint32_t my_eeprom::start_dt = 1609459200;

//自動通信開始設定
volatile bool my_eeprom::startAuto = false;

//ロガー名称
char my_eeprom::mlName[21];

//メモリを初期化する
void initMemory()
{
	//乾球温度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_DBTCF_A, DBT_COEF_A);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_DBTCF_B, DBT_COEF_B);
	
	//相対湿度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_HMDCF_A, HMD_COEF_A);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_HMDCF_B, HMD_COEF_B);
	
	//グローブ温度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_GLBCF_A, GLB_COEF_A);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_GLBCF_B, GLB_COEF_B);
	
	//照度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_LUXCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_LUXCF_B, 0.000);
	
	//風速補正係数A,B,無風電圧
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCF_B, 0.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VEL0, 1.450);
	
	//風速計特性係数
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCC_A, 79.744);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCC_B, -12.029);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCC_C, 2.356);
		
	//計測真偽
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_TH,'T');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_GLB,'T');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_VEL,'T');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_ILL,'T');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_AD1,'F');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_AD2,'F');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_AD3,'F');
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_PRX,'F');
	
	//計測間隔
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_TH, 1);
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_GLB, 1);
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_VEL, 1);
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_ILL, 1);
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_AD1, 1);
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_AD2, 1);
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_AD3, 1);
	
	//計測開始日時
	eeprom_busy_wait();
	eeprom_update_dword(&EEP_START_DT, 1609459200); //UNIX時間,UTC時差0で2021/1/1 00:00:00
	
	//自動通信開始設定
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_START_AUTO,'F');
	
	//名前
	eeprom_busy_wait();
	eeprom_update_block((const void *)ML_NAME, (void *)EEP_NAME, sizeof(my_eeprom::mlName));
	
	//XBee初期化フラグ
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_XB_INITFLAG,'F');
	
	//初期化フラグ
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_INITFLAG,'T');
}

//補正係数を書き込む
void my_eeprom::SetCorrectionFactor(const char data[])
{
	float buff;
	char num[5];
	num[4] = '\0';
	
	//乾球温度補正係数A
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::Cf_dbtA = buff;
	//乾球温度補正係数B
	strncpy(num, data + 7, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		my_eeprom::Cf_dbtB = buff;
	
	//相対湿度補正係数A
	strncpy(num, data + 11, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::Cf_hmdA = buff;
	//相対湿度補正係数B
	strncpy(num, data + 15, 4);
	buff = 0.01 * atol(num);
	if(-9.99 <= buff && buff <= 9.99)
		my_eeprom::Cf_hmdB = buff;
	
	//グローブ温度補正係数A
	strncpy(num, data + 19, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
	{
		my_eeprom::Cf_glbA = buff;
	}
	//グローブ温度補正係数B
	strncpy(num, data + 23, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		my_eeprom::Cf_glbB = buff;
	
	//照度補正係数A
	strncpy(num, data + 27, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
	{
		my_eeprom::Cf_luxA = buff;
	}
	//照度補正係数B
	strncpy(num, data + 31, 4);
	buff = atol(num);
	if(-999 <= buff && buff <= 999)
		my_eeprom::Cf_luxB = buff;
	
	//風速補正係数A
	strncpy(num, data + 35, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
	{
		my_eeprom::Cf_velA = buff;
	}
	//風速補正係数B
	strncpy(num, data + 39, 4);
	buff = 0.001 * atol(num);
	if(-0.5 <= buff && buff <= 0.5)
		my_eeprom::Cf_velB = buff;
	//風速無風電圧
	strncpy(num, data + 43, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.50)
		my_eeprom::Cf_vel0 = buff;
	
	SetCorrectionFactor();
}

//補正係数を書き込む
void my_eeprom::SetCorrectionFactor()
{
	//乾球温度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_DBTCF_A, my_eeprom::Cf_dbtA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_DBTCF_B, my_eeprom::Cf_dbtB);
		
	//相対湿度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_HMDCF_A, my_eeprom::Cf_hmdA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_HMDCF_B, my_eeprom::Cf_hmdB);
	
	//グローブ温度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_GLBCF_A, my_eeprom::Cf_glbA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_GLBCF_B, my_eeprom::Cf_glbB);
	
	//照度補正係数A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_LUXCF_A, my_eeprom::Cf_luxA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_LUXCF_B, my_eeprom::Cf_luxB);
	
	//風速補正係数A,B,無風電圧
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCF_A, my_eeprom::Cf_velA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCF_B, my_eeprom::Cf_velB);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VEL0, my_eeprom::Cf_vel0);
}

//補正係数を表す文字列を作成する
void my_eeprom::MakeCorrectionFactorString(char * txbuff, const char * command)
{
	char dbtA[6],dbtB[6],hmdA[6],hmdB[6],glbA[6],glbB[6],luxA[6],luxB[5],velA[6],velB[7],vel0[6];
	
	dtostrf(my_eeprom::Cf_dbtA,5,3,dbtA);
	dtostrf(my_eeprom::Cf_dbtB,5,2,dbtB);
	
	dtostrf(my_eeprom::Cf_hmdA,5,3,hmdA);
	dtostrf(my_eeprom::Cf_hmdB,5,2,hmdB);
	
	dtostrf(my_eeprom::Cf_glbA,5,3,glbA);
	dtostrf(my_eeprom::Cf_glbB,5,2,glbB);
	
	dtostrf(my_eeprom::Cf_luxA,5,3,luxA);
	dtostrf(my_eeprom::Cf_luxB,4,0,luxB);
	
	dtostrf(my_eeprom::Cf_velA,5,3,velA);
	dtostrf(my_eeprom::Cf_velB,6,3,velB);
	dtostrf(my_eeprom::Cf_vel0,5,3,vel0);
	
	sprintf(txbuff, "%s:%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\r",
		command, dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, vel0);
}

//風速の特性係数を書き込む
void my_eeprom::SetVelocityCharacteristics(const char data[])
{
	float buff;
	char num[8];
	
	//無風電圧
	num[4] = '\0';
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.50)
	my_eeprom::Cf_vel0 = buff;
	
	//特性A
	num[7] = '\0';
	strncpy(num, data + 7, 7);
	buff = 0.001 * atol(num);
	my_eeprom::VelCC_A = buff;
	
	//特性B
	strncpy(num, data + 14, 7);
	buff = 0.001 * atol(num);
	my_eeprom::VelCC_B = buff;
	
	//特性C
	strncpy(num, data + 21, 7);
	buff = 0.001 * atol(num);
	my_eeprom::VelCC_C = buff;

	SetVelocityCharacteristics();	
}

//風速の特性係数を書き込む
void my_eeprom::SetVelocityCharacteristics()
{
	//無風電圧
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VEL0, my_eeprom::Cf_vel0);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCC_A, my_eeprom::VelCC_A);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCC_B, my_eeprom::VelCC_B);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCC_C, my_eeprom::VelCC_C);
}

//風速の特性係数を表す文字列を作成する
void my_eeprom::MakeVelocityCharateristicsString(char * txbuff, const char * command)
{
	char vel0[6],vccA[9],vccB[9],vccC[9];
	
	dtostrf(my_eeprom::Cf_vel0,5,3,vel0);
	dtostrf(my_eeprom::VelCC_A,8,3,vccA);
	dtostrf(my_eeprom::VelCC_B,8,3,vccB);
	dtostrf(my_eeprom::VelCC_C,8,3,vccC);
	
	sprintf(txbuff, "%s:%s,%s,%s,%s\r",
	command, vel0, vccA, vccB, vccC);
}

//計測設定を書き込む
void my_eeprom::SetMeasurementSetting()
{
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_START_AUTO,my_eeprom::startAuto ? 'T' : 'F');
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_TH,my_eeprom::measure_th ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_TH,my_eeprom::interval_th);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_GLB,my_eeprom::measure_glb ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_GLB,my_eeprom::interval_glb);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_VEL,my_eeprom::measure_vel ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_VEL,my_eeprom::interval_vel);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_ILL,my_eeprom::measure_ill ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_ILL,my_eeprom::interval_ill);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_AD1,my_eeprom::measure_AD1 ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_AD1,my_eeprom::interval_AD1);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_AD2,my_eeprom::measure_AD2 ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_AD2,my_eeprom::interval_AD2);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_AD3,my_eeprom::measure_AD3 ? 'T' : 'F');
	eeprom_busy_wait();
	eeprom_update_word(&EEP_STP_AD3,my_eeprom::interval_AD3);
	
	eeprom_busy_wait();
	eeprom_update_byte(&EEP_MES_PRX,my_eeprom::measure_Prox ? 'T' : 'F');

	eeprom_busy_wait();
	eeprom_update_dword(&EEP_START_DT,my_eeprom::start_dt); //uint32_t
}

//名称を書き込む
void my_eeprom::SaveName()
{
	eeprom_busy_wait();	
	eeprom_update_block((const void *)mlName, (void *)EEP_NAME, sizeof(my_eeprom::mlName));
}

//補正係数を読み込む
void LoadCorrectionFactor()
{
	//補正係数を読み込む
	eeprom_busy_wait();
	my_eeprom::Cf_dbtA = eeprom_read_float (&EEP_DBTCF_A);
	eeprom_busy_wait();
	my_eeprom::Cf_dbtB = eeprom_read_float (&EEP_DBTCF_B);
	eeprom_busy_wait();
	my_eeprom::Cf_hmdA = eeprom_read_float (&EEP_HMDCF_A);
	eeprom_busy_wait();
	my_eeprom::Cf_hmdB = eeprom_read_float (&EEP_HMDCF_B);
	eeprom_busy_wait();
	my_eeprom::Cf_glbA = eeprom_read_float (&EEP_GLBCF_A);
	eeprom_busy_wait();
	my_eeprom::Cf_glbB = eeprom_read_float (&EEP_GLBCF_B);
	eeprom_busy_wait();
	my_eeprom::Cf_luxA = eeprom_read_float (&EEP_LUXCF_A);
	eeprom_busy_wait();
	my_eeprom::Cf_luxB = eeprom_read_float (&EEP_LUXCF_B);
	eeprom_busy_wait();
	my_eeprom::Cf_velA = eeprom_read_float (&EEP_VELCF_A);
	eeprom_busy_wait();
	my_eeprom::Cf_velB = eeprom_read_float (&EEP_VELCF_B);
	eeprom_busy_wait();
	my_eeprom::Cf_vel0 = eeprom_read_float (&EEP_VEL0);
}

//風速の特性係数を読み込む
void LoadVelocityCharateristics()
{
	eeprom_busy_wait();
	my_eeprom::Cf_vel0 = eeprom_read_float (&EEP_VEL0);
	eeprom_busy_wait();
	my_eeprom::VelCC_A = eeprom_read_float (&EEP_VELCC_A);
	eeprom_busy_wait();
	my_eeprom::VelCC_B = eeprom_read_float (&EEP_VELCC_B);
	eeprom_busy_wait();
	my_eeprom::VelCC_C = eeprom_read_float (&EEP_VELCC_C);
}

//計測設定を読み込む
void LoadMeasurementSetting()
{
	eeprom_busy_wait();
	my_eeprom::startAuto = (eeprom_read_byte(&EEP_START_AUTO) == 'T');
	
	eeprom_busy_wait();
	my_eeprom::measure_th = (eeprom_read_byte(&EEP_MES_TH) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_th = eeprom_read_word(&EEP_STP_TH);
	
	eeprom_busy_wait();
	my_eeprom::measure_glb = (eeprom_read_byte(&EEP_MES_GLB) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_glb = eeprom_read_word(&EEP_STP_GLB);
	
	eeprom_busy_wait();
	my_eeprom::measure_vel = (eeprom_read_byte(&EEP_MES_VEL) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_vel = eeprom_read_word(&EEP_STP_VEL);
	
	eeprom_busy_wait();
	my_eeprom::measure_ill = (eeprom_read_byte(&EEP_MES_ILL) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_ill = eeprom_read_word(&EEP_STP_ILL);
	
	eeprom_busy_wait();
	my_eeprom::measure_AD1 = (eeprom_read_byte(&EEP_MES_AD1) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_AD1 = eeprom_read_word(&EEP_STP_AD1);
	
	eeprom_busy_wait();
	my_eeprom::measure_AD2 = (eeprom_read_byte(&EEP_MES_AD2) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_AD2 = eeprom_read_word(&EEP_STP_AD2);
	
	eeprom_busy_wait();
	my_eeprom::measure_AD3 = (eeprom_read_byte(&EEP_MES_AD3) == 'T');
	eeprom_busy_wait();
	my_eeprom::interval_AD3 = eeprom_read_word(&EEP_STP_AD3);
	
	eeprom_busy_wait();
	my_eeprom::measure_Prox = (eeprom_read_byte(&EEP_MES_PRX) == 'T');
	
	eeprom_busy_wait();
	my_eeprom::start_dt = eeprom_read_dword(&EEP_START_DT); //uint32_t用
}

//名称を読み込む
void LoadName()
{
	eeprom_busy_wait();
	eeprom_read_block((void *)my_eeprom::mlName, (const void *)EEP_NAME, sizeof(my_eeprom::mlName));
}

//設定を読み込む
void my_eeprom::LoadEEPROM()
{
	//強制初期化処理
	//eeprom_busy_wait();
	//eeprom_update_byte(&EEP_INITFLAG, 'F');
	
	//初期化未了の場合は補正係数を初期化する
	//https://scienceprog.com/tip-on-storing-initial-values-in-eeprom-of-avr-microcontroller/
	//この方法だと、たまたまEEPROMの「EEP_INITFLAG」に「T」が設定されていた場合に処理が破綻するが良いのだろうか・・・
	eeprom_busy_wait(); //EEPROM読み書き可能まで待機
	if (eeprom_read_byte(&EEP_INITFLAG) != 'T') initMemory();
	
	LoadCorrectionFactor();
	LoadVelocityCharateristics();
	LoadMeasurementSetting();
	LoadName();
}

//XBeeが初期化済か否かを取得する
bool my_eeprom::IsXBeeInitialized(){
	eeprom_busy_wait(); //EEPROM読み書き可能まで待機
	return eeprom_read_byte(&EEP_XB_INITFLAG) == 'T';
}

//XBee初期化を記録する
void my_eeprom::XBeeInitialized(){
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_XB_INITFLAG,'T');	
}