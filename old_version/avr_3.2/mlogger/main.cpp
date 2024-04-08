/**
 * @file main.cpp
 * @brief AVR(AVRxxDB32)���g�p�����v���f�[�^���W�E���M�v���O����
 * @author E.Togashi
 * @date 2022/3/11
 */

/**XBee�[���̐ݒ�****************************************
 * �E�e�@�q�@����
 *   1) Firmware�́AProduct family:�uXB3-24�v,Function set:�uDigi XBee3 Zigbee 3.0�v,Firmware version:�u1010�v
 *   2) PAN ID�𓯂��l�ɂ���
 *   3) SP:Cyclic Sleep Period = 0x64�i=1000 msec�j,SN:Number of Cyclic Sleep Periods = 3600
 *      �����3600�~3=3hour�̓l�b�g���[�N����O��Ȃ�
 *   4) AP:API Mode Without Escapes[1]
 * �E�e�@�̂�
 *   1) CE:Coordinator Enable = Enabled
 *   2) SM:Sleep Mode = No sleep
 *   3) AR:many-to-one routing = 0
 *   4) NJ:Node Join Time = FF�i���Ԗ������Ƀl�b�g���[�N�Q���\�j
 * �E�q�@�̂�
 *   1) CE:Coordinator Enable = Join Network [0]
 *   2) SM:Sleep Mode = Pin Hibernate [1]�iATMega����̎w�߂ŃX���[�v�������邽�߁j
 *   �ȉ���Bluetooth�Ή��̏ꍇ�̂�
 *   3) BT:Bluetooth Enable = Enabled [1]
 *   4) BI:Bluetooth Identifier = "MLogger_xxxx"�ixxxx�͓K���Ȍ����̐�����ID�Ƃ��Ďg���j
 *   5) Bluetooth Authentication�Ɂuml_pass�v
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

//���t����p
#include <time.h>

#include "main.h"
#include "my_eeprom.h"	//EEPROM����
#include "my_i2c.h"		//I2C�ʐM
#include "my_xbee.h"	//XBee�ʐM
#include "RecursiveLeastSquares.h"

//FatFs�֘A
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//�萔�錾***********************************************************
const char VERSION_NUMBER[] = "VER:3.2.16\r";

//�M���������v�̗����グ�ɕK�v�Ȏ���[sec]
const uint8_t V_WAKEUP_TIME = 20;

//�O���[�u���x�v���p���W���[���̃^�C�v
const bool IS_MCP9700 = false; //MCP9701�Ȃ��false

//P3T1750DP���g�����ۂ�
const bool USE_P3T1750DP = false;

//���s���̃f�[�^���ꎞ�ۑ����邩
const int N_LINE_BUFF = 30;

//�L��ϐ���`********************************************************
//�����֘A
volatile static time_t currentTime = UNIX_OFFSET; //���ݎ����iUNIX����,UTC����0��2000/1/1 00:00:00�j

//�v�������ۂ�
volatile static bool logging = false;

//�d�r�͑���Ă��邩
volatile static unsigned int lowBatteryTime = 0;

//�e�@�Ƃ̒ʐM�֘A
static bool readingFrame = false; //�t���[���Ǎ������ۂ�
static uint8_t framePosition = 0; //�t���[���ǂݍ��݈ʒu
static uint8_t frameSize = 0; //�t���[���T�C�Y
volatile static char frameBuff[my_xbee::MAX_CMD_CHAR]; //�Ǎ����̃t���[��
volatile static char cmdBuff[my_xbee::MAX_CMD_CHAR]; //�R�}���h�o�b�t�@
static uint8_t xbeeOffset=14; //��Mframe type�ɉ������I�t�Z�b�g
volatile static bool outputToBLE=false; //Bluetooth�ڑ��ɏ����o�����ۂ�
volatile static bool outputToXBee=true; //XBee�ڑ��ɏ����o�����ۂ�
volatile static bool outputToSDCard=false; //SD�J�[�h�ɏ����o�����ۂ�

//�v���̐���ƌv�����ԊԊu
//th:�����x, glb:�O���[�u���x, vel:������, ill:�Ɠx
volatile static unsigned int pass_th = 0;
volatile static unsigned int pass_glb = 0;
volatile static unsigned int pass_vel = 0;
volatile static unsigned int pass_ill = 0;
volatile static unsigned int pass_ad1 = 0;

//WFC�𑗐M����܂ł̎c�莞��[sec]
static uint8_t wc_time = 0;

//SD�J�[�h�֘A
volatile bool initSD = false; //SD�J�[�h�������t���O
static FATFS* fSystem;
static char lineBuff[my_xbee::MAX_CMD_CHAR * N_LINE_BUFF + 1]; //�ꎞ�ۑ������z��i������null������ǉ��j
static uint8_t buffNumber = 0; //�ꎞ�ۑ���
static tm lastSavedTime; //�Ō�ɕۑ����������iUNIX���ԁj
static uint8_t blinkCount = 0; //SD�����o������LED�_�Ŏ��ԊԊu�ێ��ϐ�

//���Z�b�g�����p
volatile static unsigned int resetTime = 0;

//�ėp�̕�����z��
static char charBuff[my_xbee::MAX_CMD_CHAR];

//�����v�����Z�������p
static bool autCalibratingVSensor= false;
static bool calibratingVelocityVoltage = false;
static unsigned int vSensorInitTimer = 0;
static unsigned long vSensorTuningTime = 300; //5��
static float vSensorInitVoltage;

//���x�v�����Z�������p
static bool autCalibratingTSensor= false;
static unsigned int tSensorInitTimer = 0;
static unsigned long tSensorTuningTime = 86400; //1��

//�}�N����`********************************************************
#define ARRAY_LENGTH(array) (sizeof(array) / sizeof(array[0]))

int main(void)
{
	//EEPROM
	my_eeprom::LoadEEPROM();
		
	//���o�̓|�[�g��������
	initialize_port();

	//��U�A���ׂĂ̊��荞�݋֎~
	cli();
		
	//AD�ϊ��̐ݒ�
	ADC0.CTRLA |= (ADC_ENABLE_bm | ADC_RESSEL_10BIT_gc); //ADC�L����, 10bit����
	ADC0.CTRLB |= ADC_SAMPNUM_ACC64_gc; //64�񕽋ω�, 16, 32, 64����ݒ�ł���
	ADC0.CTRLC |= ADC_PRESC_DIV128_gc; //128�����Ōv���i�傫���������x�͍����A���Ԃ͂�����͗l�j
	VREF.ADC0REF = VREF_REFSEL_VREFA_gc; //��d����VREFA(2.0V)�ɐݒ�

	//�d�r�c�ʊm�F
	if(isLowBattery()) showError(1);
	
	//�X�C�b�`���荞�ݐݒ�
	PORTA.PIN2CTRL |= PORT_ISC_BOTHEDGES_gc; //�d���㏸�E�~������
	
	//����������
	my_i2c::InitializeI2C(); //I2C�ʐM
	my_i2c::InitializeAHT20(); //�����x�v
	my_i2c::InitializeVCNL4030(); //�Ɠx�v
	if(USE_P3T1750DP) my_i2c::InitializeP3T1750DP(); //���x�v
	my_xbee::Initialize();  //XBee�iUART�j
	
	//�^�C�}������
	initialize_timer();
		
	//XBee�X���[�v����
	wakeup_xbee();
		
	fSystem = (FATFS*) malloc(sizeof(FATFS));
	
	//�����ݒ肪�I������班���ҋ@
	_delay_ms(500);
	
	//XBee�ݒ�m�F
	if(!my_xbee::xbee_setting_initialized()) showError(2);

	//���荞�ݍĊJ
	sei();

	//�ʐM�ĊJ�t���O���m�F
	if(my_eeprom::startAuto)
	{
		logging = true;
		outputToXBee = true;
		outputToBLE = outputToSDCard = false;
	}
	
	//10�b�ȏ�d���s�����Ԃ��p��������I��
    while (lowBatteryTime <= 10)
    {		
		//�}�E���g�ł��Ă��Ȃ���΂Ƃɂ����}�E���g
		if(!initSD) 
			initSD = (f_mount(fSystem, "", 1) == FR_OK);
		
		//�X���[�v���[�h�ݒ�
		if(logging)
		{
			//XBee�ʐM���܂��͓d���ڑ���݃��[�h����IDLE�X���[�v
			if(outputToBLE || my_eeprom::startAuto)
			{
				set_sleep_mode(SLEEP_MODE_IDLE);
				wakeup_xbee();
			}
			//�d�r�ɂ��Zigbee�ʐM���͏ȃG�l�d���Ńp���[�_�E��
			else
			{
				set_sleep_mode(SLEEP_MODE_PWR_DOWN);
				sleep_xbee();
			}
		}
		else set_sleep_mode(SLEEP_MODE_IDLE); //���M���O�J�n�O��UART�ʐM���ł���悤��IDLE�ŃX���[�v
		
		//�}�C�R�����X���[�v������
		sleep_mode();
    }
	
	//�d�r���s�����̏���
	if(initSD) f_mount(NULL, "", 1); //SD�J�[�h���A���}�E���g
	cli(); //���荞�ݏI��
	showError(1); //LED�\��
}

static void initialize_port(void)
{
	//SPI�ʐM�̂��߂̏���������mmc.c��power_on()�֐���
	//***
	
	//�o�̓|�[�g
	PORTD.DIRSET = PIN6_bm; //LED�o��
	PORTF.DIRSET = PIN5_bm; //XBee�X���[�v����
	PORTA.DIRSET = PIN5_bm; //�������v�����[
	PORTA.DIRSET = PIN4_bm; //�������v5V����	
	//PORTA.DIRSET = PIN2_bm; //PORTA PIN2:���Z�b�g�p���荞��:2023.05.04�R�����g�A�E�g�i�O���Ńv���A�b�v���Ă��邽�ߏo�͂ɂ���K�v�Ȃ��j
	sleep_anemo();   //�������v�͓d�r�������̂ŁA�����ɃX���[�v����

	//���̓|�[�g
	PORTD.DIRCLR = PIN4_bm; //�O���[�u���x�Z���TAD�ϊ�
	PORTD.DIRCLR = PIN2_bm; //�����Z���TAD�ϊ�
	PORTF.DIRCLR = PIN4_bm; //�ėpAD�ϊ�1
	
	//�v���A�b�v/�_�E��
	PORTA.OUTSET = PIN2_bm; //PORTA PIN2(Interrupt)�F�A�b�v
}

//�^�C�}������
static void initialize_timer( void )
{
	//Timer 1 //0.01sec�^�C�}************************
	TCA0.SINGLE.INTCTRL = TCA_SINGLE_OVF_bm; //�^�C�}��ꊄ�荞�ݗL����
	TCA0.SINGLE.CTRLB = TCA_SINGLE_WGMODE_NORMAL_gc; 
	TCA0.SINGLE.EVCTRL &= ~(TCA_SINGLE_CNTAEI_bm);	
	TCA0.SINGLE.PER = 0.01 * F_CPU / 2 - 1;  //0.01sec, 4MHz, 2����
	TCA0.SINGLE.CTRLA = TCA_SINGLE_CLKSEL_DIV2_gc | TCA_SINGLE_ENABLE_bm;
		
	//�O���N���X�^���ɂ��1sec�^�C�}*******************
	//**�O���N���X�^���̗L��������**
	//���U��֎~
	_PROTECTED_WRITE(CLKCTRL.XOSC32KCTRLA, ~CLKCTRL_ENABLE_bm);
	while(CLKCTRL.MCLKSTATUS & CLKCTRL_XOSC32KS_bm); //XOSC32KS��0�ɂȂ�܂őҋ@

	//XTAL1��XTAL2�ɐڑ����ꂽ�O���N���X�^�����g�p
	_PROTECTED_WRITE(CLKCTRL.XOSC32KCTRLA, ~CLKCTRL_SEL_bm);
	
	//���U�틖��
	_PROTECTED_WRITE(CLKCTRL.XOSC32KCTRLA, CLKCTRL_ENABLE_bm);

	while (RTC.STATUS > 0); //�S���W�X�^�����������܂őҋ@
	//**�L�������������܂�*********

	RTC.CLKSEL = RTC_CLKSEL_XOSC32K_gc;	  //32.768kHz�O���N���X�^���p���U�� (XOSC32K)��I��
	RTC.DBGCTRL |= RTC_DBGRUN_bm;         //�f�o�b�O�ő��s������
	RTC.PITINTCTRL = RTC_PI_bm;           //���������L���ɂ���
	RTC.PITCTRLA = RTC_PERIOD_CYC32768_gc //RTC������32768
				| RTC_PITEN_bm;           //��������p�^�C�}��L���ɂ���
	
	//POWER DOWN���ɂ��^�C�}������L���ɂ���
	SLPCTRL.CTRLA |= SLPCTRL_SMODE_PDOWN_gc; 
	SLPCTRL.CTRLA |= SLPCTRL_SEN_bm;
}

//UART��M���̊��荞�ݏ���
ISR(USART0_RXC_vect)
{
	char dat = USART0.RXDATAL;	//�ǂݏo��
	
	//�J�n�R�[�h�u~�v�������珉�����B�{����Escape���������邪�A�u~�v�̓R�}���h�Ŏg��Ȃ��̂ŗǂ����낤
	if(dat == 0x7E)
	{
		framePosition = 1;
		readingFrame = true;
	}
	//�t���[���Ǎ���
	else if(readingFrame)
	{
		if(framePosition == 1); //�f�[�^����ʃo�C�g�i�p�P�b�g�̐�������K��0�j
		else if(framePosition == 2) //�f�[�^�����ʃo�C�g
			frameSize = (int)dat + 3;
		else if(framePosition == 3) //�R�}���hID
		{
			if(dat == 0x90) xbeeOffset = 14; //ZigBee Recieve Packet�̏ꍇ�̃I�t�Z�b�g
			else if(dat == 0xAD) xbeeOffset = 4; //User Data Relay (Bluetooth)�̏ꍇ�̃I�t�Z�b�g
		}
		else if(framePosition <= xbeeOffset); //��M�I�v�V�����Ȃǂ͖���
		else
		{
			if(frameSize <= framePosition) //�`�F�b�N�T���ɓ��B
			{
				frameBuff[framePosition - (xbeeOffset + 1)] = '\0';
				readingFrame = false;
				frameSize = 0;
				framePosition = 0;
				
				//�R�}���h�֒ǉ�
				append_command();
			}
			else if(framePosition < frameSize) //�f�[�^���o�b�t�@�Ɋi�[
				frameBuff[framePosition - (xbeeOffset + 1)] = dat;
		}
		
		framePosition++;
	}	
}

static void append_command(void)
{
	//null�܂Ői��
	unsigned int pos1 = 0;
	unsigned int cbLength = ARRAY_LENGTH(cmdBuff);
	while(cmdBuff[pos1] != '\0' && pos1 < cbLength) pos1++;
	if(pos1 == cbLength - 1) pos1 = 0; //�o�b�t�@�̍Ō�܂Ői��ł�NULL���Ȃ���΍ŏ��ɖ߂�
	
	unsigned int pos2 = 0;
	unsigned int fbLength =  ARRAY_LENGTH(frameBuff);
	while (frameBuff[pos2] != '\0' && pos2 < fbLength)
	{
		if(pos2 == fbLength - 1) break;
		
		char nxtC = frameBuff[pos2];
		cmdBuff[pos1] = nxtC;
		pos1++;
		pos2++;
		//���s�R�[�h���\�ꂽ��R�}���h���s
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
	
	//�`���[�j���O���͎w�߂��󂯎��Ȃ�
	if(autCalibratingVSensor || autCalibratingTSensor) return;
	
	//�o�[�W����
	if (strncmp(command, "VER", 3) == 0) 
		my_xbee::bltx_chars(VERSION_NUMBER);
	//���M���O�J�n
	else if (strncmp(command, "STL", 3) == 0)
	{
		//���ݎ�����ݒ�
		char num[11];
		num[10] = '\0';
		strncpy(num, command + 3, 10);
		currentTime = atol(num);
		//�ŏI�v������=���ݎ����Ƃ���
		time_t ct = currentTime - UNIX_OFFSET;
		gmtime_r(&ct, &lastSavedTime);
		
		//Bluetooth�ڑ����ۂ�(x��xbee,b��bluetooth)
		outputToXBee = (command[13]=='t' || command[13]=='e'); //XBee�Őe�@�ɏ����o�����ۂ�
		outputToBLE = (command[14]=='t'); //Bluetooth�ŏ����o�����ۂ�
		outputToSDCard = (command[15]=='t'); //SD�J�[�h�ɏ����o�����ۂ�
		
		//����̂悢�b�Ōv���J�n
		pass_th	= getNormTime(lastSavedTime, my_eeprom::interval_th);
		pass_glb = getNormTime(lastSavedTime, my_eeprom::interval_glb);
		pass_vel = getNormTime(lastSavedTime, my_eeprom::interval_vel);
		pass_ill = getNormTime(lastSavedTime, my_eeprom::interval_ill);
		pass_ad1 = getNormTime(lastSavedTime, my_eeprom::interval_AD1);
		
		//���M���O�ݒ��EEPROM�ɕۑ�
		my_eeprom::startAuto = command[13]=='e'; //Endless���M���O
		my_eeprom::SetMeasurementSetting();
		
		//���M���O�J�n
		my_xbee::bltx_chars("STL\r");
		_delay_ms(100);
		logging = true;	
	}
	//Change Measurement Settings
	else if(strncmp(command, "CMS", 3) == 0)
	{
		//�ݒ��ύX����ꍇ�ɂ̓��M���O���~������
		logging = false;
		
		//����̐���
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
		//�o�[�W�������Ⴂ�ꍇ�̏���
		else
		{
			my_eeprom::measure_AD1 = false;
			my_eeprom::measure_AD2 = false;
			my_eeprom::measure_AD3 = false;
		}
		
		//���莞�ԊԊu
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
		//�o�[�W�������Ⴂ�ꍇ�̏���
		else
		{
			my_eeprom::interval_AD1 = 60;
			my_eeprom::interval_AD2 = 60;
			my_eeprom::interval_AD3 = 60;
		}
		
		//�ߐڃZ���T�̗L������
		if(37 < strlen(command)) my_eeprom::measure_Prox = (command[55] == 't');
		//�o�[�W�������Ⴂ�ꍇ�̏���
		else my_eeprom::measure_Prox = false;
		
		//�v���J�n����
		char num2[11];
		num2[10] = '\0';
		strncpy(num2, command + 27, 10);
		my_eeprom::start_dt = atol(num2);
		
		//���M���O�ݒ��EEPROM�ɕۑ�
		my_eeprom::SetMeasurementSetting();
		
		//ACK
		sprintf(charBuff, "CMS:%d,%u,%d,%u,%d,%u,%d,%u,%ld,%d,%u,%d,%u,%d,%u,%d\r",
			my_eeprom::measure_th, my_eeprom::interval_th, 
			my_eeprom::measure_glb, my_eeprom::interval_glb, 
			my_eeprom::measure_vel, my_eeprom::interval_vel, 
			my_eeprom::measure_ill, my_eeprom::interval_ill, 
			my_eeprom::start_dt,
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
			my_eeprom::start_dt,
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
		strncpy(my_eeprom::mlName, command + 3, 21);
		my_eeprom::SaveName();
		
		//ACK
		char ack[22 + 4];
		sprintf(ack, "CLN:%s\r", my_eeprom::mlName);		
		my_xbee::bltx_chars(ack);
	}
	//Load Logger Name
	else if(strncmp(command, "LLN", 3) == 0)
	{
		char name[22 + 4];
		sprintf(name, "LLN:%s\r", my_eeprom::mlName);
		my_xbee::bltx_chars(name);
	}
	//�����v�̎����Z��
	else if(strncmp(command, "CBV", 3) == 0)
	{
		char buff[11];
		buff[5] = '\0';
		strncpy(buff, command + 3, 5);
		vSensorTuningTime = atol(buff);
		if(vSensorTuningTime < 60) vSensorTuningTime = 60;
		if(86400 < vSensorTuningTime)vSensorTuningTime = 3600;
		sprintf(buff, "CBV:%lo\r", vSensorTuningTime);
		my_xbee::bltx_chars(buff);
		
		autCalibratingVSensor = true;
		vSensorInitTimer = 0;
		vSensorInitVoltage = 0;
		wakeup_anemo();
	}
	//���x�v�̎����Z��
	else if(strncmp(command, "CBT", 3) == 0)
	{
		char buff[11];
		buff[5] = '\0';
		strncpy(buff, command + 3, 5);
		tSensorTuningTime = atol(buff);
		if(tSensorTuningTime < 60) tSensorTuningTime = 60;
		if(86400 < tSensorTuningTime)tSensorTuningTime = 3600;
		sprintf(buff, "CBT:%lo\r", tSensorTuningTime);
		my_xbee::bltx_chars(buff);

		RecursiveLeastSquares::Initialized = false;
		autCalibratingTSensor = true;
		tSensorInitTimer = 0;
	}
	//�����̎蓮�Z���J�n
	else if(strncmp(command, "SCV", 3) == 0) 
	{
		wakeup_anemo();
		calibratingVelocityVoltage = true;
	}
	//�����̎蓮�Z���I��
	else if(strncmp(command, "ECV", 3) == 0)
	{
		sleep_anemo();
		calibratingVelocityVoltage = false;
		my_xbee::bltx_chars("ECV\r");
	}
	//���ݎ����̍X�V
	else if (strncmp(command, "UCT", 3) == 0)
	{
		//���ݎ�����ݒ�
		char num[11];
		num[10] = '\0';
		strncpy(num, command + 3, 10);
		currentTime = atol(num);
		my_xbee::bltx_chars("UCT\r");
	}
	
	//�R�}���h���폜
	cmdBuff[0] = '\0';
}

// Timer1���荞��//FatFs�iSD�J�[�h���o�͒ʐM�j�p
ISR(TCA0_OVF_vect)
{
	disk_timerproc();	/* Drive timer procedure of low level disk I/O module */
	
	TCA0.SINGLE.INTFLAGS = TCA_SINGLE_OVF_bm; //���荞�݉���
}

//���M���O�p��1�b���̏���
ISR(RTC_PIT_vect)
{
	//���荞�ݗv���t���O����
	RTC.PITINTFLAGS = RTC_PI_bm;
	
	currentTime++; //1�b�i�߂�
	
	//�d���m�F
	if(isLowBattery()) lowBatteryTime++;
	else lowBatteryTime = 0;

	//���Z�b�g�{�^���������݊m�F********************************
	if(!(PORTA.IN & PIN2_bm))
	{
		resetTime++;
		if(resetTime == 3)
		{
			//Endless���M���O������
			my_eeprom::startAuto = false;
			my_eeprom::SetMeasurementSetting();
			
			logging=false;	//���M���O��~
			initSD = false;	//SD�J�[�h�ă}�E���g
			sleep_anemo();	//�����Z���T���~
			blinkLED(3);	//LED�_��
			return;
		}
	}
	else resetTime = 0; //Reset�{�^���������ݎ��Ԃ�0�ɖ߂�
	
	//�����v�Z������*******************************************
	if(calibratingVelocityVoltage) calibrateVelocityVoltage();
	
	//�����v�����Z������*******************************************
	else if(autCalibratingVSensor) autoCalibrateVelocitySensor();
	
	//���x�v�����Z������*******************************************
	else if(autCalibratingTSensor) autoCalibrateTemperatureSensor();
	
	//���M���O���ł����****************************************
	else if(logging) execLogging();
	
	//�ҋ@���ł����****************************************
	else
	{
		sleep_anemo(); //�����v���~ 2023.01.09 Bugfix
		wakeup_xbee(); //XBee�X���[�v����
		_delay_ms(1); //�X���[�v�������̗����グ��50us=0.05ms���x������炵��
		
		//����I�ɃR�}���h�Ҏ��Ԃ𑗐M
		if(wc_time <= 0)
		{
			my_xbee::bltx_chars("WFC\r"); //Waiting for command.
			wc_time = 6;
		}
		wc_time--;
		
		//����
		if((PORTA.IN & PIN2_bm))  //Reset�������ݒ��͖��Œ�~
			blinkLED(initSD ? 2 : 1);
	}
}

static void execLogging()
{
	//�����J�n�ݒ�ł͂Ȃ��A�v���J�n�����̑O�Ȃ�ΏI��
	if(!my_eeprom::startAuto && currentTime < my_eeprom::start_dt) return;
	
	//���M���O����5�b���Ƃɓ_��
	blinkCount++;
	if(5 <= blinkCount)
	{
		//SD�J�[�h�����o���Ń}�E���g�ł��Ă��Ȃ��ꍇ�ɂ͓_�����Ȃ�
		if(!(outputToSDCard && !initSD)) blinkLED(1);
		blinkCount = 0;		
	}
	
	//�v����WAKEUP_TIME[sec]�O����M���������v��H�̃X���[�v���������ĉ��M�J�n
	if(my_eeprom::measure_vel && my_eeprom::interval_vel - pass_vel < V_WAKEUP_TIME) wakeup_anemo();
	
	bool hasNewData = false;
	char tmpS[7] = "n/a"; //-10.00 ~ 50.00//6����+\r
	char hmdS[7] = "n/a"; //0.00 ~ 100.00//6����+\r
	char glbTS[7] = "n/a"; //-10.00 ~ 50.00//6����+\r
	char glbVS[7] = "n/a";
	char velS[7] = "n/a"; //0.0000 ~ 1.5000//6����+\r
	char velVS[7] = "n/a";
	char illS[9] = "n/a"; //0.01~83865.60
	char adV1S[7] = "n/a";
	
	//����������************
	pass_vel++;
	if(my_eeprom::measure_vel && my_eeprom::interval_vel <= pass_vel)
	{
		double velV = readVelVoltage(); //AD�ϊ�
		dtostrf(velV,6,4,velVS);
		
		float bff = max(0, velV / my_eeprom::Cf_vel0 - 1.0);
		float vel = bff * (2.3595 + bff * (-12.029 + bff * 79.744)); //�d��-�������Z��
		dtostrf(vel,6,4,velS);
		
		pass_vel = 0;
		hasNewData = true;
		//���̋N���������N���ɕK�v�Ȏ��Ԃ�����̏ꍇ�ɂ͔������v��H���X���[�v
		if(V_WAKEUP_TIME <= my_eeprom::interval_vel) sleep_anemo();
	}
	
	//�����x����************
	pass_th++;
	if(my_eeprom::measure_th && my_eeprom::interval_th <= pass_th)
	{
		float tmp_f = 0;
		float hmd_f = 0;
		if(my_i2c::ReadAHT20(&tmp_f, &hmd_f))
		{
			tmp_f = max(-10,min(50,my_eeprom::Cf_dbtA *(tmp_f) + my_eeprom::Cf_dbtB));
			hmd_f = max(0,min(100,my_eeprom::Cf_hmdA *(hmd_f) + my_eeprom::Cf_hmdB));
			dtostrf(tmp_f,6,2,tmpS);
			dtostrf(hmd_f,6,2,hmdS);
		}
		pass_th = 0;
		hasNewData = true;
	}
	
	//�O���[�u���x����************
	pass_glb++;
	if(my_eeprom::measure_glb && my_eeprom::interval_glb <= pass_glb)
	{
		if(USE_P3T1750DP){
			float glbT = 0;
			if(my_i2c::ReadP3T1750DP(&glbT))
			{
				glbT = max(-10,min(50,my_eeprom::Cf_glbA * glbT + my_eeprom::Cf_glbB));
				dtostrf(glbT,6,2,glbTS);
			}
		}
		else{			
			float glbV = readGlbVoltage(); //AD�ϊ�
			dtostrf(glbV,6,4,glbVS);
			
			float glbT = (glbV - (IS_MCP9700 ? 0.5 : 0.4)) / (IS_MCP9700 ? 0.0100 : 0.0195);
			glbT = max(-10,min(50,my_eeprom::Cf_glbA * glbT + my_eeprom::Cf_glbB));
			dtostrf(glbT,6,2,glbTS);			
		}
		
		pass_glb = 0;
		hasNewData = true;
	}
	
	//�Ɠx�Z���T����**************
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
	
	//�ėpAD�ϊ�����1
	pass_ad1++;
	if(my_eeprom::measure_AD1 && my_eeprom::interval_AD1 <= pass_ad1)
	{
		float adV = readVoltage(1); //AD�ϊ�
		dtostrf(adV,6,4,adV1S);
		pass_ad1 = 0;
		hasNewData = true;
	}
	
	//�V�K�f�[�^������ꍇ�͑��M
	if(hasNewData)
	{
		if(outputToXBee || outputToBLE)
		{
			wakeup_xbee(); //XBee�X���[�v����
			_delay_ms(1); //�X���[�v�������̗����グ��50us=0.05ms���x������炵��
		}
		
		//�󔒍폜���č��l��
		alignLeft(tmpS);
		alignLeft(hmdS);
		alignLeft(glbTS);
		alignLeft(glbVS);
		alignLeft(velS);
		alignLeft(velVS);
		alignLeft(illS);
		alignLeft(adV1S);		
		
		//�������쐬
		time_t ct = currentTime - UNIX_OFFSET;
		tm dtNow;
		gmtime_r(&ct, &dtNow);
		
		//�����o����������쐬
		snprintf(charBuff, sizeof(charBuff), "DTT:%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s,%s,n/a,n/a\r",
		dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
		tmpS, hmdS, glbTS, velS, illS, glbVS, velVS, adV1S);
		
		//������I�[�o�[�ɔ����čŌ�ɏI���R�[�h'\r\0'�����Ă���
		charBuff[my_xbee::MAX_CMD_CHAR-2]='\r';
		charBuff[my_xbee::MAX_CMD_CHAR-1]= '\0';

		if(outputToXBee) my_xbee::tx_chars(charBuff); //XBee Zigbee�o��
		if(outputToBLE) my_xbee::bl_chars(charBuff); //XBee Bluetooth�o��
		if(outputToSDCard)  //SD card�o��
		{
			//�f�[�^���\���ɗ��܂邩�A1min�ȏ�̎��ԊԊu���������珑���o���B1h�Ԋu�̏����o���̂��߂�3�s�ڂ��K�v
			if(N_LINE_BUFF <= buffNumber
				|| lastSavedTime.tm_min != dtNow.tm_min
				|| lastSavedTime.tm_hour != dtNow.tm_hour)
			{
				writeSDcard(lastSavedTime, lineBuff); //SD card�o��
				buffNumber = 0;
				lineBuff[0] = '\0';
				lastSavedTime = dtNow;
			}
			
			//SD�J�[�h�����o�����͖`����DTT���s�v�B���������L���C�ȃv���O�����ɂł����������B�B�B
			snprintf(charBuff, sizeof(charBuff),
			"%04d/%02d/%02d %02d:%02d:%02d,%04d/%02d/%02d %02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s,%s\r",
			dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
			dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
			tmpS, hmdS, glbTS, velS, illS, glbVS, velVS, adV1S);
			//������I�[�o�[�ɔ����čŌ�ɏI���R�[�h'\r\0'�����Ă���
			charBuff[my_xbee::MAX_CMD_CHAR-2]='\r';
			charBuff[my_xbee::MAX_CMD_CHAR-1]= '\0';
			//�ꎞ�ۑ�������̖����ɑ���
			strncat(lineBuff, charBuff, sizeof(charBuff));
			buffNumber++;
		}
	}
	
	//UART���M���I�������10msec�҂���XBee���X���[�v������(XBee���̑��M���I���܂ő҂������̂�)
	//�{���A������CTS���g���Ď�M�\�ɂȂ����^�C�~���O�ŃX���[�v���H�t���[�R���g���[���������B
	//while(! my_uart::tx_done());
	_delay_ms(10); //���̃X���[�v��XBee�̒ʐM�I���҂��ړI�B���s����ŗp�ӂ����l�Ȃ̂ŁA�������B���B�������������ł͂Ȃ��悤�ɂ��v��	
}

static void calibrateVelocityVoltage()
{
	char velVS[7] = "n/a";	
	double velV = readVelVoltage(); //AD�ϊ�
	dtostrf(velV,6,4,velVS);	
	snprintf(charBuff, sizeof(charBuff), "SCV:%s\r", velVS);
	my_xbee::bltx_chars(charBuff);
}

static void autoCalibrateVelocitySensor()
{
	const unsigned int VS_INIT_WAIT = 60; //�����Z���J�n�܂ł̑҂�����[sec]
	
	//LED�_��
	blinkLED(3);
	
	vSensorInitTimer++;
	
	//�c�莞�Ԃ�ʒm
	char buff[11];
	sprintf(buff, "TNV:%lo\r", vSensorTuningTime + VS_INIT_WAIT - vSensorInitTimer);
	my_xbee::bltx_chars(buff);
	
	if(VS_INIT_WAIT < vSensorInitTimer)
		vSensorInitVoltage += readVelVoltage(); //AD�ϊ�
	if(vSensorTuningTime + VS_INIT_WAIT < vSensorInitTimer)
	{
		vSensorInitVoltage /= vSensorTuningTime;
		if(1.4 < vSensorInitVoltage && vSensorInitVoltage < 1.55)
		{
			my_eeprom::Cf_vel0 = vSensorInitVoltage;
			my_eeprom::SetCorrectionFactor();
		}
		
		sleep_anemo();
		autCalibratingVSensor = false; //�Z���I��
	}
}

static void autoCalibrateTemperatureSensor()
{
	const unsigned int TS_INIT_WAIT = 60; //���x�Z���J�n�܂ł̑҂�����[sec]
	
	//LED�_��
	blinkLED(3);
	
	tSensorInitTimer++;
	
	//�c�莞�Ԃ�ʒm
	char buff[11];
	sprintf(buff, "TNV:%lo\r", tSensorTuningTime + TS_INIT_WAIT - tSensorInitTimer);
	my_xbee::bltx_chars(buff);
	
	if(TS_INIT_WAIT < tSensorInitTimer)
	{
		//�����x����
		float tmp_f = 0;
		float hmd_f = 0;
		if(my_i2c::ReadAHT20(&tmp_f, &hmd_f))
			tmp_f = max(-10,min(50,my_eeprom::Cf_dbtA *(tmp_f) + my_eeprom::Cf_dbtB));
		else return;
		
		//�O���[�u���x
		float glb_f = 0;
		if(USE_P3T1750DP){
			my_i2c::ReadP3T1750DP(&glb_f);
		}
		else{
			float glbV = readGlbVoltage(); //AD�ϊ�
			glb_f = (glbV - (IS_MCP9700 ? 0.5 : 0.4)) / (IS_MCP9700 ? 0.0100 : 0.0195);	
		}
		
		//�ɒ[�Ɍ덷���傫���Ȃ���Ή�A�W�����X�V
		if(abs(tmp_f - glb_f) < 3)
		{
			if(RecursiveLeastSquares::Initialized) 
				RecursiveLeastSquares::UpdateCoefficients(glb_f, tmp_f);			
			else 
			{
				//��U�A�L��y=x�ŏ���������ƈ��肷��
				RecursiveLeastSquares::InitializeCoefficients(0, 0);
				RecursiveLeastSquares::UpdateCoefficients(10, 10);
				RecursiveLeastSquares::UpdateCoefficients(20, 20);
				RecursiveLeastSquares::UpdateCoefficients(30, 30);
				RecursiveLeastSquares::UpdateCoefficients(40, 40);
				RecursiveLeastSquares::UpdateCoefficients(glb_f, tmp_f);
			}
		}
		
		if(tSensorTuningTime + TS_INIT_WAIT < tSensorInitTimer)
		{
			//���܂荓���␳�͍̗p���Ȃ�
			if(0.7 < RecursiveLeastSquares::coefA && RecursiveLeastSquares::coefA < 1.3 
			&& -3 < RecursiveLeastSquares::coefB && RecursiveLeastSquares::coefB < 3)
			{
				my_eeprom::Cf_glbA = RecursiveLeastSquares::coefA;
				my_eeprom::Cf_glbB = RecursiveLeastSquares::coefB;
				my_eeprom::SetCorrectionFactor();
			}
			autCalibratingTSensor = false; //�Z���I��
		}
	}
}

static void writeSDcard(const tm dtNow, const char write_chars[])
{
	//�}�E���g�������Ȃ�ΏI��
	if(!initSD) return;
	
	char fileName[13]={}; //yyyymmdd.csv
	snprintf(fileName, sizeof(fileName), "%04d%02d%02d.csv", dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday);
	
	//SD�J�[�h�L�^�p���t�X�V
	myRTC.year=dtNow.tm_year+1900;
	myRTC.month=dtNow.tm_mon+1;
	myRTC.mday=dtNow.tm_mday;
	myRTC.hour=dtNow.tm_hour;
	myRTC.min=dtNow.tm_min;
	myRTC.sec=dtNow.tm_sec;
	
	FIL* fl = (FIL*)malloc(sizeof(FIL));	
	if(f_open(fl, fileName, FA_OPEN_APPEND | FA_WRITE) == FR_OK)
	{
		f_puts(write_chars, fl);
		f_close(fl);
	}
	else initSD = false; //�G���[���͍ă}�E���g�����݂�
	
	free(fl);
}

//PORTA PIN2���荞��
ISR(PORTA_PORT_vect)
{
	// ���荞�݃t���O����
	PORTA.INTFLAGS = PIN2_bm;
	
	//Push:LED�_��, None:LED����
	if(PORTA.IN & PIN2_bm) PORTD.OUTCLR = PIN6_bm;
	else PORTD.OUTSET = PIN6_bm;
}

//�O���[�u���x�̓d����ǂݎ��
static float readGlbVoltage(void)
{
	//AI4���v��
	ADC0.MUXPOS = ADC_MUXPOS_AIN4_gc;
	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //�ϊ��J�n
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //�ϊ��I���҂�
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64�񕽋�)
}

//�������̓d����ǂݎ��
static float readVelVoltage(void)
{
	//AI2���v��
	ADC0.MUXPOS = ADC_MUXPOS_AIN2_gc;
	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //�ϊ��J�n
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //�ϊ��I���҂�
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64�񕽋�)
}

//AD1~3�̓d����ǂݎ��
static float readVoltage(unsigned int adNumber)
{
	if(adNumber == 1) ADC0.MUXPOS = ADC_MUXPOS_AIN20_gc; //AD1
	else return 0.0; //AD2, AD3�p�~

	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //�ϊ��J�n
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //�ϊ��I���҂�
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64�񕽋�)
}

//�d�r�c�ʂ��������Ȃ������ۂ�
//�����d����3.3V�ɏ������Ă��邪��d����2.0V�̓��M�����[�^�ō���Ă��邽�߁A�d���~�����ɂ͌�҂݂̂��s�����邱�Ƃ𗘗p
static bool isLowBattery(void)
{
	ADC0.MUXPOS = 0x44; //VDDDIV10: VDD divided by 10�i0.33V���x�j
	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //�ϊ��J�n
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //�ϊ��I���҂�
	volatile float vdd = 10.0 * 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64�񕽋�)
	
	//��d�����Ⴍ�Ȃ邽�߁A3.3V���傫�߂Ɍv�������B1�����ƂȂ����Ƃ��ɓd�͕s���Ɣ���
	return 3.3 * 1.1 < vdd;
}

//�G���[�\��
static void showError(short int errNum)
{
	switch(errNum){
		case 1: //�d�r�s��
		while(true)
		{
			turnOnLED(); //�_��
			_delay_ms(1000);
			turnOffLED(); //����
			_delay_ms(1000);
		}
		case 2: //XBee�ݒ�G���[
		while(true)
		{
			blinkLED(2);
			turnOnLED(); //�_��
			_delay_ms(1000);
			turnOffLED(); //����
			_delay_ms(1000);
		}
	}
}

static void alignLeft(char *str) {
	// ������NULL�E��łȂ�
	if (str != NULL && *str != '\0')
	{
		int len = strlen(str);

		//�󔒂��J�E���g
		int i;
		for (i = 0; i < len && str[i] == ' '; i++);

		// ����������l�߂Ɉړ�����
		if (i > 0)
		memmove(str, str + i, len - i + 1);
	}
}

//����̗ǂ������ɂȂ�悤�ɍŏ��̌v�����ԊԊu�𒲐�����
static int getNormTime(tm time, unsigned int interval)
{
	if(interval <= 5) return interval - (5 - time.tm_sec % 5);
	else if(interval <= 10) return interval - (10 - time.tm_sec % 10);
	else if(interval <= 30) return interval - (30 - time.tm_sec % 30);
	else return interval - (60 - time.tm_sec % 60);
}

//�ȉ���inline�֐�************************************

inline static void sleep_anemo(void)
{
	PORTA.OUTCLR = PIN5_bm; //�����[�Ւf
	PORTA.OUTCLR = PIN4_bm; //5V������~
}

inline static void wakeup_anemo(void)
{
	PORTA.OUTSET = PIN5_bm; //�����[�ʓd
	PORTA.OUTSET = PIN4_bm; //5V�����J�n
}

inline static void sleep_xbee(void)
{
	PORTF.OUTSET = PIN5_bm;
}

inline static void wakeup_xbee(void)
{
	PORTF.OUTCLR = PIN5_bm;
}

inline static void turnOnLED(void)
{
	PORTD.OUTSET = PIN6_bm; //�_��
}

inline static void turnOffLED(void)
{
	PORTD.OUTCLR = PIN6_bm; //����
}

inline static void toggleLED(void)
{
	PORTD.OUTTGL = PIN6_bm; //���]
}

inline static void blinkLED(int iterNum)
{
	if(iterNum < 1) return;

	//����
	turnOffLED(); //��U�K����������
	//�_��
	for(int i=0;i<iterNum;i++)
	{
		_delay_ms(100);
		turnOnLED(); //�_��
		_delay_ms(25);
		turnOffLED(); //����
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