/**
 * @file my_eeprom.h
 * @brief AVR(ATMega328)��EEPROM����������
 * @author E.Togashi
 * @date 2021/12/19
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <avr/eeprom.h>

#include "my_eeprom.h"

//EEPROM�̏������t���O�B�R���p�C����ŏ��̌Ăяo���̂ݏ���������
static uint8_t EEMEM EEP_INITFLAG;

//�������x�␳�W��A,B
static float EEMEM EEP_DBTCF_A;
static float EEMEM EEP_DBTCF_B;

//���Ύ��x�␳�W��A,B
static float EEMEM EEP_HMDCF_A;
static float EEMEM EEP_HMDCF_B;

//�O���[�u���x�␳�W��A,B
static float EEMEM EEP_GLBCF_A;
static float EEMEM EEP_GLBCF_B;

//�Ɠx�␳�W��A,B
static float EEMEM EEP_LUXCF_A;
static float EEMEM EEP_LUXCF_B;

//�����␳�W��A,B,�����d��
static float EEMEM EEP_VELCF_A;
static float EEMEM EEP_VELCF_B;
static float EEMEM EEP_VEL0;

//�v���^�U
static uint8_t EEMEM EEP_MES_TH;
static uint8_t EEMEM EEP_MES_GLB;
static uint8_t EEMEM EEP_MES_VEL;
static uint8_t EEMEM EEP_MES_ILL;
static uint8_t EEMEM EEP_MES_AD1;
static uint8_t EEMEM EEP_MES_AD2;
static uint8_t EEMEM EEP_MES_AD3;
static uint8_t EEMEM EEP_MES_PRX;

//�v���Ԋu
static unsigned int EEMEM EEP_STP_TH;
static unsigned int EEMEM EEP_STP_GLB;
static unsigned int EEMEM EEP_STP_VEL;
static unsigned int EEMEM EEP_STP_ILL;
static unsigned int EEMEM EEP_STP_AD1;
static unsigned int EEMEM EEP_STP_AD2;
static unsigned int EEMEM EEP_STP_AD3;

//�����ʐM�J�n�ݒ�
static uint8_t EEMEM EEP_START_AUTO;

//���K�[����
static char EEMEM EEP_NAME[21];

//�␳�W��
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

//�v���^�U
volatile bool my_eeprom::measure_th = true;
volatile bool my_eeprom::measure_glb = true;
volatile bool my_eeprom::measure_vel = true;
volatile bool my_eeprom::measure_ill = true;
volatile bool my_eeprom::measure_AD1 = false;
volatile bool my_eeprom::measure_AD2 = false;
volatile bool my_eeprom::measure_AD3 = false;
volatile bool my_eeprom::measure_Prox = false;

//�v���Ԋu
volatile unsigned int my_eeprom::interval_th = 1;
volatile unsigned int my_eeprom::interval_glb = 1;
volatile unsigned int my_eeprom::interval_vel = 1;
volatile unsigned int my_eeprom::interval_ill = 1;
volatile unsigned int my_eeprom::interval_AD1 = 1;
volatile unsigned int my_eeprom::interval_AD2 = 1;
volatile unsigned int my_eeprom::interval_AD3 = 1;

//�����ʐM�J�n�ݒ�
volatile bool my_eeprom::startAuto = false;

//���K�[����
char my_eeprom::mlName[20];

//������������������
void initMemory()
{
	//�������x�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_DBTCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_DBTCF_B, 0.000);
	
	//���Ύ��x�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_HMDCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_HMDCF_B, 0.000);
	
	//�O���[�u���x�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_GLBCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_GLBCF_B, 0.000);
	
	//�Ɠx�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float(&EEP_LUXCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_LUXCF_B, 0.000);
	
	//�����␳�W��A,B,�����d��
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCF_A, 1.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VELCF_B, 0.000);
	eeprom_busy_wait();
	eeprom_update_float(&EEP_VEL0, 1.450);
	
	//�v���^�U
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
	
	//�v���Ԋu
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
	
	//�����ʐM�J�n�ݒ�
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_START_AUTO,'F');
	
	//���O
	eeprom_busy_wait();
	eeprom_update_block("Anonymous", EEP_NAME, 20);
	
	//�������t���O
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_INITFLAG,'T');
}

//�␳�W������������
void my_eeprom::SetCorrectionFactor(const char data[])
{
	float buff;
	char num[5];
	num[4] = '\0';
	
	//�������x�␳�W��A
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::Cf_dbtA = buff;
	//�������x�␳�W��B
	strncpy(num, data + 7, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		my_eeprom::Cf_dbtB = buff;
	
	//���Ύ��x�␳�W��A
	strncpy(num, data + 11, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::Cf_hmdA = buff;
	//���Ύ��x�␳�W��B
	strncpy(num, data + 15, 4);
	buff = 0.01 * atol(num);
	if(-9.99 <= buff && buff <= 9.99)
		my_eeprom::Cf_hmdB = buff;
	
	//�O���[�u���x�␳�W��A
	strncpy(num, data + 19, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
	{
		my_eeprom::Cf_glbA = buff;
	}
	//�O���[�u���x�␳�W��B
	strncpy(num, data + 23, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		my_eeprom::Cf_glbB = buff;
	
	//�Ɠx�␳�W��A
	strncpy(num, data + 27, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
	{
		my_eeprom::Cf_luxA = buff;
	}
	//�Ɠx�␳�W��B
	strncpy(num, data + 31, 4);
	buff = atol(num);
	if(-999 <= buff && buff <= 999)
		my_eeprom::Cf_luxB = buff;
	
	//�����␳�W��A
	strncpy(num, data + 35, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
	{
		my_eeprom::Cf_velA = buff;
	}
	//�����␳�W��B
	strncpy(num, data + 39, 4);
	buff = 0.001 * atol(num);
	if(-0.5 <= buff && buff <= 0.5)
		my_eeprom::Cf_velB = buff;
	//���������d��
	strncpy(num, data + 43, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.50)
		my_eeprom::Cf_vel0 = buff;
	
	SetCorrectionFactor();
}

//�␳�W������������
void my_eeprom::SetCorrectionFactor()
{
	//�������x�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_DBTCF_A, my_eeprom::Cf_dbtA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_DBTCF_B, my_eeprom::Cf_dbtB);
		
	//���Ύ��x�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_HMDCF_A, my_eeprom::Cf_hmdA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_HMDCF_B, my_eeprom::Cf_hmdB);
	
	//�O���[�u���x�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_GLBCF_A, my_eeprom::Cf_glbA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_GLBCF_B, my_eeprom::Cf_glbB);
	
	//�Ɠx�␳�W��A,B
	eeprom_busy_wait();
	eeprom_update_float (&EEP_LUXCF_A, my_eeprom::Cf_luxA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_LUXCF_B, my_eeprom::Cf_luxB);
	
	//�����␳�W��A,B,�����d��
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCF_A, my_eeprom::Cf_velA);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VELCF_B, my_eeprom::Cf_velB);
	eeprom_busy_wait();
	eeprom_update_float (&EEP_VEL0, my_eeprom::Cf_vel0);
}

//�␳�W����\����������쐬����
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

//�v���ݒ����������
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
}

//���̂���������
void my_eeprom::SaveName()
{
	eeprom_busy_wait();	
	eeprom_update_block(mlName, EEP_NAME, 20);
}

//�␳�W����ǂݍ���
void LoadCorrectionFactor()
{
	//�␳�W����ǂݍ���
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

//�v���ݒ��ǂݍ���
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
}

//���̂�ǂݍ���
void LoadName()
{
	eeprom_busy_wait();
	eeprom_read_block(my_eeprom::mlName, EEP_NAME, sizeof(my_eeprom::mlName));
}

//�ݒ��ǂݍ���
void my_eeprom::LoadEEPROM()
{
	//��������������
	//eeprom_busy_wait();
	//eeprom_update_byte(&EEP_INITFLAG, 'F');
	
	//�����������̏ꍇ�͕␳�W��������������
	//https://scienceprog.com/tip-on-storing-initial-values-in-eeprom-of-avr-microcontroller/
	//���̕��@���ƁA���܂���EEPROM�́uEEP_INITFLAG�v�ɁuT�v���ݒ肳��Ă����ꍇ�ɏ������j�]���邪�ǂ��̂��낤���E�E�E
	eeprom_busy_wait(); //EEPROM�ǂݏ����\�܂őҋ@
	if (eeprom_read_byte(&EEP_INITFLAG) != 'T') initMemory();
	
	LoadCorrectionFactor();
	LoadMeasurementSetting();
	LoadName();
}