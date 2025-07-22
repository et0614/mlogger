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
 *   3) SP:Cyclic Sleep Period = 0x64�i=1000 msec�j
 *      SN:Number of Cyclic Sleep Periods = 3600
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
#include <util/atomic.h>

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
#include "EepromManager.h"	//EEPROM����
#include "i2c/I2cDriver.h"  //I2C�ʐM
#include "i2c/sht4x.h"      //SHT4X(�����x�Z���T)
#include "i2c/stcc4.h"      //STCC4(CO2�Z���T)
#include "i2c/vcnl4030.h"	//VCNL4030(�Ɠx�Z���T)
#include "UartDriver.h"		//UART�ʐM
#include "XbeeController.h" //XBee�ʐM

//FatFs�֘A
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//�萔�錾***********************************************************
const char VERSION_NUMBER[] = "VER:3.4.0\r";

//�R�}���h�̕�����
const uint8_t CMD_LENGTH = 3;

//�R�}���h�̍ő啶����
const int MAX_CMD_CHAR = 150;

//�M���������v�̗����グ�ɕK�v�Ȏ���[sec]
const uint8_t V_WAKEUP_TIME = 20;

//���s���̃f�[�^���ꎞ�ۑ����邩
const int N_LINE_BUFF = 30;

//�Ɠx�v�J�o�[�A�N�����̓��ߗ�
const double TRANSMITTANCE = 0.60;

//����������version2���ۂ�
const bool IS_VEL_FNC2 = true;

//�L��ϐ���`********************************************************
//�����֘A
volatile static time_t currentTime = UNIX_OFFSET; //���ݎ����iUNIX����,UTC����0��2000/1/1 00:00:00�j

//1�b���̏������s�t���O
volatile bool process_logging_flag = false;

//�v�������ۂ�
static bool logging = false;

//�d�r�͑���Ă��邩
static unsigned int lowBatteryTime = 0;

//�R�}���h
static char xbee_payload_buffer[MAX_CMD_CHAR]; // process_xbee_byte����y�C���[�h���󂯎�邽�߂̈ꎞ�I�ȃo�b�t�@
static char cmdBuff[MAX_CMD_CHAR]; // �����̃y�C���[�h�ɂ܂�����R�}���h��g�ݗ��Ă邽�߂̃o�b�t�@
static bool outputToBLE=false; //Bluetooth�ڑ��ɏ����o�����ۂ�
static bool outputToXBee=true; //XBee�ڑ��ɏ����o�����ۂ�
static bool outputToFM=false; //Flash Memory�ɏ����o�����ۂ�

//�v�����ԊԊu
static MeasurementPassCounters pass_counters = {0};

//WFC�𑗐M����܂ł̎c�莞��[sec]
static uint8_t wc_time = 0;

//�ڑ��ێ��p��p�P�b�g����[sec]
static uint8_t slp_time = 0;

//Flash�������֘A
static FATFS fSystem;
static bool initFM = false; //Flash Memory�������t���O
static char lineBuff[MAX_CMD_CHAR * N_LINE_BUFF + 1]; //�ꎞ�ۑ������z��i������null������ǉ��j
static uint8_t buffNumber = 0; //�ꎞ�ۑ���
static tm lastSavedTime; //�Ō�ɕۑ����������iUNIX���ԁj
static uint8_t blinkCount = 0; //FM�����o������LED�_�Ŏ��ԊԊu�ێ��ϐ�

//���Z�b�g�����p
static unsigned int resetTime = 0;

//CO2�Z���T�֘A
static bool hasCO2Sensor = false;

//�ėp�̕�����z��
static char charBuff[MAX_CMD_CHAR];

//�����v�����Z�������p
static bool calibratingVelocityVoltage = false;

int main(void)
{	
	//EEPROM
	EepromManager::loadEEPROM();
		
	//���o�̓|�[�g��������
	initializePort();

	//��U�A���ׂĂ̊��荞�݋֎~
	cli();
		
	//AD�ϊ��̐ݒ�
	ADC0.CTRLA |= (ADC_ENABLE_bm | ADC_RESSEL_10BIT_gc); //ADC�L����, 10bit����
	ADC0.CTRLB |= ADC_SAMPNUM_ACC64_gc; //64�񕽋ω�, 16, 32, 64����ݒ�ł���
	ADC0.CTRLC |= ADC_PRESC_DIV128_gc; //128�����Ōv���i�傫���������x�͍����A���Ԃ͂�����͗l�j
	VREF.ADC0REF = VREF_REFSEL_VREFA_gc; //��d����VREFA(2.0V)�ɐݒ�

	//��d����1�b�p��������d�r�c�ʃG���[
	int count = 0;
	while(isLowBattery()){
		count++;
		if(10 <= count)	showError(1);
		_delay_ms(100);
	}
	
	//�X�C�b�`���荞�ݐݒ�
	PORTA.PIN2CTRL |= PORT_ISC_BOTHEDGES_gc; //�d���㏸�E�~������
	
	//����������
	I2cDriver::initialize(); //I2C�ʐM
	hasCO2Sensor = Stcc4::isConnected();
	if(hasCO2Sensor) Stcc4::initialize(); //CO2�Z���T
	Sht4x::initialize(Sht4x::SHT4_AD); //�����x�Z���T
	Sht4x::initialize(Sht4x::SHT4_BD); //�O���[�u���x�Z���T
	Vcnl4030::initialize(); //�Ɠx�v
	XbeeController::initialize();  //XBee�iUART�j
	
	//�^�C�}������
	initializeTimer();
		
	//XBee�X���[�v����
	wakeupXbee();
	
	//�����ݒ肪�I������班���ҋ@
	_delay_ms(500);

	//XBee�ݒ�m�F
	if(!XbeeController::xbeeSettingInitialized()) showError(2);

	//���荞�ݍĊJ
	sei();

	//�ʐM�ĊJ�t���O���m�F
	if(EepromManager::mSettings.start_auto)
	{
		logging = true;
		outputToXBee = true;
		outputToBLE = outputToFM = false;
	}
	
	//10�b�ȏ�d���s�����Ԃ��p��������I��
    while (lowBatteryTime <= 10)
    {
		bool work_done_this_cycle = false;
		
		//�}�E���g�ł��Ă��Ȃ���΂Ƃɂ����}�E���g
		if(!initFM)
			initFM = (f_mount(&fSystem, "", 1) == FR_OK);

		// UART�����O�o�b�t�@�Ƀf�[�^������΁A���ׂď�������
		while (UartDriver::uartRingBufferHasData())
		{
			work_done_this_cycle = true;			
			char received_byte = UartDriver::uartRingBufferGet();
			
			// 1�o�C�g���p�[�T�[�ɓn���A�߂�l��true�Ȃ�Xbee�t���[���̎�M������
			if (XbeeController::processXbeeByte(received_byte, xbee_payload_buffer, sizeof(xbee_payload_buffer)))
				appendCommand(xbee_payload_buffer);
		}
		
		//1�b���̏���
		if(process_logging_flag){
			process_logging_flag = false;
			executeSecondlyTask();
		}
		
		//������������΃X���[�v������
		if(!work_done_this_cycle){
			//�X���[�v���[�h�ݒ�
			if(logging)
			{
				//XBee�ʐM���܂��͓d���ڑ���݃��[�h����IDLE�X���[�v
				if(outputToBLE || EepromManager::mSettings.start_auto) 
				{
					set_sleep_mode(SLEEP_MODE_IDLE);
					wakeupXbee();
				}
				//�d�r�ɂ��Zigbee�ʐM���͏ȃG�l�d���Ńp���[�_�E��
				else 
				{
					set_sleep_mode(SLEEP_MODE_PWR_DOWN);
					sleepXbee();
				}
			}
			else set_sleep_mode(SLEEP_MODE_IDLE); //���M���O�J�n�O��UART�ʐM���ł���悤��IDLE�ŃX���[�v
		
			//�}�C�R�����X���[�v������
			sleep_mode();
		}
    }
	
	//�d�r���s�������ꍇ�̂݁A�����܂ł��ǂ蒅��
	if(initFM) f_mount(NULL, "", 1); //FM���A���}�E���g
	cli(); //���荞�ݏI��
	showError(1); //LED�\��
}

static void initializePort(void)
{
	//SPI�ʐM�̂��߂̏���������mmc.c��power_on()�֐���
	//***
	
	//�o�̓|�[�g
	PORTA.DIRSET = PIN6_bm; //��LED�o��
	PORTA.DIRSET = PIN7_bm; //��LED�o��
	PORTF.DIRSET = PIN5_bm; //XBee�X���[�v����
	PORTA.DIRSET = PIN4_bm; //�������v5V����
	PORTD.DIRSET = PIN6_bm; //UART RTS�iLow�Ŏ�M�\�j
	sleepAnemo();   //�������v�͓d�r�������̂ŁA�����ɃX���[�v����

	//���̓|�[�g
	PORTD.DIRCLR = PIN2_bm; //�����Z���TAD�ϊ�
	PORTF.DIRCLR = PIN4_bm; //�ėpAD�ϊ�1
	
	//�v���A�b�v/�_�E��
	PORTA.OUTSET = PIN2_bm; //PORTA PIN2(Interrupt)�F�A�b�v
	PORTD.OUTCLR = PIN6_bm; //UART RTS�iLow�Ŏ�M�\�j�F�_�E��
	
	//���g�p�|�[�g
	PORTA.PIN5CTRL = PORT_ISC_INPUT_DISABLE_gc; // PA5
	PORTD.PIN1CTRL = PORT_ISC_INPUT_DISABLE_gc; // PD1
	PORTD.PIN3CTRL = PORT_ISC_INPUT_DISABLE_gc; // PD3
	PORTD.PIN4CTRL = PORT_ISC_INPUT_DISABLE_gc; // PD4
}

//�^�C�}������
static void initializeTimer( void )
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

static void appendCommand(const char* payload)
{
	// cmdBuff�ɒǋL���Ă������߂̌��݈ʒu(pos1)��T���i�t���[�����܂����ŃR�}���h����������Ă���ꍇ�ɑΉ����邽�߁j
	unsigned int pos1 = 0;
	while(cmdBuff[pos1] != '\0' && pos1 < MAX_CMD_CHAR) pos1++;

	// �V�����y�C���[�h(payload)��1�������ǂ݂Ȃ���AcmdBuff�ɒǋL���Ă���
	unsigned int pos2 = 0;
	while (payload[pos2] != '\0' && pos1 < MAX_CMD_CHAR - 1)
	{
		char nxtC = payload[pos2];
		cmdBuff[pos1] = nxtC;

		// ���s�R�[�h(\r)������������A�R�}���h�����������Ƃ݂Ȃ���������
		if (nxtC == '\r')
		{
			cmdBuff[pos1 + 1] = '\0';      // ��������I�[������
			solveCommand(cmdBuff);        // �R�}���h���s
			cmdBuff[0] = '\0';             // ���s��A�g���p�o�b�t�@���N���A
			pos1 = 0;                      // �g���p�o�b�t�@�̈ʒu��擪�Ƀ��Z�b�g
		}
		else pos1++; // ���̈ʒu��
		
		pos2++; // �y�C���[�h�̎��̕�����
	}

	// �y�C���[�h�̍Ō��\r�������A�R�}���h�����r���[�Ȍ`�ŏI������ꍇ
	// �i���̃t���[���ɑ����ꍇ�j�ɔ����āAcmdBuff��NULL�I�[���Ă���
	cmdBuff[pos1] = '\0';
}

static void solveCommand(const char *command)
{	
	//�o�[�W����
	if (strncmp(command, "VER", 3) == 0) 
		XbeeController::bltxChars(VERSION_NUMBER);
	//���M���O�J�n
	else if (strncmp(command, "STL", 3) == 0)
	{
		//���ݎ�����ݒ�i�ŏI�v������=���ݎ����Ƃ���j
		time_t ct; //�ŏI�v������
		char num[11];
		num[10] = '\0';
		strncpy(num, command + CMD_LENGTH, 10);
		ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
			currentTime = atol(num);
			ct = currentTime - UNIX_OFFSET; //�ŏI�v������=���ݎ����Ƃ���
		}
		gmtime_r(&ct, &lastSavedTime);
		
		//Bluetooth�ڑ����ۂ�(x��xbee,b��bluetooth)
		outputToXBee = (command[13]=='t' || command[13]=='e'); //XBee�Őe�@�ɏ����o�����ۂ�
		outputToBLE = (command[14]=='t'); //Bluetooth�ŏ����o�����ۂ�
		outputToFM = (command[15]=='t'); //FM�ɏ����o�����ۂ�
		
		//����̂悢�b�Ōv���J�n
		pass_counters.th = getNormTime(lastSavedTime, EepromManager::mSettings.interval_th);
		pass_counters.glb = getNormTime(lastSavedTime, EepromManager::mSettings.interval_glb);
		pass_counters.vel = getNormTime(lastSavedTime, EepromManager::mSettings.interval_vel);
		pass_counters.ill = getNormTime(lastSavedTime, EepromManager::mSettings.interval_ill);
		pass_counters.ad1 = getNormTime(lastSavedTime, EepromManager::mSettings.interval_AD1);
		pass_counters.co2 = getNormTime(lastSavedTime, EepromManager::mSettings.interval_co2);
		
		//���M���O�ݒ��EEPROM�ɕۑ�
		EepromManager::mSettings.start_auto = command[13]=='e'; //Endless���M���O
		EepromManager::setMeasurementSetting();
		
		//���M���O�J�n
		XbeeController::bltxChars("STL\r");
		_delay_ms(100);
		logging = true;	
	}
	//Change Measurement Settings
	else if(strncmp(command, "CMS", 3) == 0)
	{
		//�ݒ��ύX����ꍇ�ɂ̓��M���O���~������
		logging = false;
			
		//����̐���
		EepromManager::mSettings.measure_th = (command[3] == 't');
		EepromManager::mSettings.measure_glb = (command[9] == 't');
		EepromManager::mSettings.measure_vel = (command[15] == 't');
		EepromManager::mSettings.measure_ill = (command[21] == 't');
		EepromManager::mSettings.measure_AD1 = (command[37] == 't');
		EepromManager::mSettings.measure_AD2 = (command[43] == 't');
		EepromManager::mSettings.measure_AD3 = (command[49] == 't');
		EepromManager::mSettings.measure_Prox = (command[55] == 't');
		//�o�[�W�������Ⴂ�ꍇ�̏���
		if(56 < strlen(command)) 
			EepromManager::mSettings.measure_co2 = (command[56] == 't');
		else EepromManager::mSettings.measure_co2 = false;
		
		//���莞�ԊԊu
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
		//�o�[�W�������Ⴂ�ꍇ�̏���
		if(56 < strlen(command))
		{
			strncpy(num, command + 57,5);
			EepromManager::mSettings.interval_co2 = atoi(num);
		}
		else EepromManager::mSettings.interval_co2 = 60;

		//�v���J�n����
		char num2[11];
		num2[10] = '\0';
		strncpy(num2, command + 27, 10);
		EepromManager::mSettings.start_dt = atol(num2);
		
		//���M���O�ݒ��EEPROM�ɕۑ�
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
	//�����̎蓮�Z���J�n
	else if(strncmp(command, "SCV", 3) == 0) 
	{
		wakeupAnemo();
		calibratingVelocityVoltage = true;
		//�ȍ~�A���b"SCV:�d��"�����M�����
	}
	//�����̎蓮�Z���I��
	else if(strncmp(command, "ECV", 3) == 0)
	{
		sleepAnemo();
		calibratingVelocityVoltage = false;
		XbeeController::bltxChars("ECV\r");
	}
	//CO2�Z���T�̗L��
	else if(strncmp(command, "HCS", 3) == 0)
	{
		XbeeController::bltxChars(hasCO2Sensor ? "HCS:1\r" : "HCS:0\r");
	}
	//���ݎ����̍X�V
	else if (strncmp(command, "UCT", 3) == 0)
	{
		//���ݎ�����ݒ�
		char num[11];
		num[10] = '\0';
		strncpy(num, command + CMD_LENGTH, 10);
		ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
			currentTime = atol(num);
		}
		XbeeController::bltxChars("UCT\r");
	}
}

// Timer1���荞��//FatFs�iFM���o�͒ʐM�j�p
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
	
	ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
		currentTime++; //1�b�i�߂�
	}
	
	//1�b���̏����t���O�𗧂Ă�
	process_logging_flag = true;
}

static void executeSecondlyTask(){
	//�d���m�F�i���E�܂Ŏg����Flash�������̃f�[�^���j�����邱�Ƃ�����j
	if(isLowBattery()) lowBatteryTime++;
	else lowBatteryTime = 0;

	//���Z�b�g�{�^���������݊m�F********************************
	if(!(PORTA.IN & PIN2_bm))
	{
		resetTime++;
		if(resetTime == 3)
		{
			//Endless���M���O������
			EepromManager::mSettings.start_auto = false;
			EepromManager::setMeasurementSetting();
			
			logging=false;	//���M���O��~
			initFM = false;	//FM���ă}�E���g
			sleepAnemo();	//�����Z���T���~
			blinkRedLED(3);	//��LED�_��
			return;
		}
	}
	else resetTime = 0; //Reset�{�^���������ݎ��Ԃ�0�ɖ߂�
	
	//�����v�Z������*******************************************
	if(calibratingVelocityVoltage) calibrateVelocityVoltage();
	
	//���M���O���ł����****************************************
	else if(logging) execLogging();
	
	//�ҋ@���ł����****************************************
	else
	{
		sleepAnemo(); //�����v���~ 2023.01.09 Bugfix
		wakeupXbee(); //XBee�X���[�v����
		_delay_ms(1); //�X���[�v�������̗����グ��50us=0.05ms���x������炵��
		
		//����I�ɃR�}���h�Ҏ��Ԃ𑗐M
		if(wc_time <= 0)
		{
			XbeeController::bltxChars("WFC\r"); //Waiting for command.
			wc_time = 6;
		}
		wc_time--;
		
		//����
		if((PORTA.IN & PIN2_bm))  //Reset�������ݒ��͖��Œ�~
		blinkGreenLED(initFM ? 2 : 1);
	}
}

static void execLogging()
{
	//FM�����o���Ń}�E���g�O�̏ꍇ�ɂ͐�LED�ŃA���[�g
	if(outputToFM && !initFM) blinkRedLED(1);
	
	//�����v���J�n��Off�Ōv���J�n�����̑O�Ȃ�ΏI��
	time_t current_snapshot;
	ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
		current_snapshot = currentTime;
	}
	if(!EepromManager::mSettings.start_auto && current_snapshot < EepromManager::mSettings.start_dt) return;
	
	//���M���O����5�b���Ƃɓ_��
	blinkCount++;
	if(5 <= blinkCount)
	{
		//FM�����o���Ń}�E���g�ł��Ă��Ȃ��ꍇ�ɂ͓_�����Ȃ�
		if(!(outputToFM && !initFM)) blinkGreenLED(1);
		blinkCount = 0;
	}
	
	//�v����WAKEUP_TIME[sec]�O����M���������v��H�̃X���[�v���������ĉ��M�J�n
	if(EepromManager::mSettings.measure_vel && EepromManager::mSettings.interval_vel - pass_counters.vel < V_WAKEUP_TIME) wakeupAnemo();
	
	bool hasNewData = false;
	char tmpS[7] = "n/a"; //-10.00 ~ 50.00//6����+\r
	char hmdS[7] = "n/a"; //0.00 ~ 100.00//6����+\r
	char glbTS[7] = "n/a"; //-10.00 ~ 50.00//6����+\r
	char glbVS[7] = "n/a";
	char velS[7] = "n/a"; //0.0000 ~ 1.5000//6����+\r
	char velVS[7] = "n/a";
	char illS[9] = "n/a"; //0.01~83865.60
	char adV1S[7] = "n/a";
	char co2S[6] = "n/a"; //0~65535//5����+\r
	
	//����������************
	pass_counters.vel++;
	if(EepromManager::mSettings.measure_vel && (int)EepromManager::mSettings.interval_vel <= pass_counters.vel)
	{
		double velV = readVelVoltage(); //AD�ϊ�
		dtostrf(velV,6,4,velVS);
				
		float bff = max(0, velV / EepromManager::cFactors.vel0 - 1.0);
		float vel = 0;
		if(IS_VEL_FNC2)
			vel = EepromManager::vcCoefficients.ccB * pow(bff,EepromManager::vcCoefficients.ccA);
		else
			vel = bff * (EepromManager::vcCoefficients.ccC + bff * (EepromManager::vcCoefficients.ccB + bff * EepromManager::vcCoefficients.ccA)); //�d��-�������Z��
		dtostrf(vel,6,4,velS);
		
		pass_counters.vel = 0;
		hasNewData = true;
		//���̋N���������N���ɕK�v�Ȏ��Ԃ�����̏ꍇ�ɂ͔������v��H���X���[�v
		if(V_WAKEUP_TIME <= EepromManager::mSettings.interval_vel) sleepAnemo();
	}
	
	//CO2����************	
	pass_counters.co2++;	
	if(EepromManager::mSettings.measure_co2 && (int)EepromManager::mSettings.interval_co2 <= pass_counters.co2)
	{
		uint16_t co2_u = 0;
		float tmp_f = 0;
		float hmd_f = 0;
		if(Stcc4::readMeasurement(&co2_u, &tmp_f, &hmd_f)) sprintf(co2S, "%u\n", co2_u);
		Stcc4::enterSleep(); //�X���[�v
		pass_counters.co2 = 0;
	}
	
	//�����x����************
	pass_counters.th++;
	//CO2�v������ꍇ�ɂ�1�b�O�ɉ����x��ʒm���Čv���w�߂��o���K�v������
	bool mesCo2m1 = EepromManager::mSettings.measure_co2 && (int)EepromManager::mSettings.interval_co2 - 1 <= pass_counters.co2;
	bool mesTH = EepromManager::mSettings.measure_th && (int)EepromManager::mSettings.interval_th <= pass_counters.th;
	if(mesCo2m1 || mesTH)
	{
		float tmp_f = 0;
		float hmd_f = 0;
		if(Sht4x::readValue(&tmp_f, &hmd_f, Sht4x::SHT4_BD))
		{
			if(mesCo2m1){
				Stcc4::exitSleep(); //�N����
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
	
	//�O���[�u���x����************
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
	
	//�Ɠx�Z���T����**************
	pass_counters.ill++;
	if(EepromManager::mSettings.measure_ill && (int)EepromManager::mSettings.interval_ill <= pass_counters.ill)
	{
		//�ߐڌv
		if(EepromManager::mSettings.measure_Prox)
		{
			float ill_d;
			Vcnl4030::readPS(&ill_d);
			dtostrf(ill_d,8,2,illS);
		}
		//�Ɠx�v
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
	
	//�ėpAD�ϊ�����1
	pass_counters.ad1++;
	if(EepromManager::mSettings.measure_AD1 && (int)EepromManager::mSettings.interval_AD1 <= pass_counters.ad1)
	{
		float adV = readVoltage(1); //AD�ϊ�
		dtostrf(adV,6,4,adV1S);
		pass_counters.ad1 = 0;
		hasNewData = true;
	}
	
	//�V�K�f�[�^������ꍇ�͑��M
	if(hasNewData)
	{		
		if(outputToXBee || outputToBLE)
		{
			slp_time=0; //��p�P�b�g�܂ł̎��Ԃ�������
			wakeupXbee(); //XBee�X���[�v����
			_delay_ms(1); //�X���[�v�������̗����グ��50us=0.05ms���x������炵���B�Z������ƃR���f���T�̉e�����\���ɗ����オ��Ȃ��B�B�B
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
		time_t ct;
		ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
			ct = currentTime - UNIX_OFFSET;
		}
		tm dtNow;
		gmtime_r(&ct, &dtNow);
		
		//�����o����������쐬
		snprintf(charBuff, sizeof(charBuff), "DTT:%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s,%s,n/a,n/a,%s\r",
		dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
		tmpS, hmdS, glbTS, velS, illS, "n/a", velVS, adV1S, co2S);
		
		//������I�[�o�[�ɔ����čŌ�ɏI���R�[�h'\r\0'�����Ă���
		charBuff[MAX_CMD_CHAR-2]='\r';
		charBuff[MAX_CMD_CHAR-1]='\0';

		if(outputToXBee) 
			XbeeController::txChars(charBuff); //XBee Zigbee�o��
		if(outputToBLE) 
			XbeeController::blChars(charBuff); //XBee Bluetooth�o��
		if(outputToFM)  //FM�o��
		{
			//�f�[�^���\���ɗ��܂邩�A1min�ȏ�̎��ԊԊu���������珑���o���B1h�Ԋu�̏����o���̂��߂�3�s�ڂ��K�v
			if(N_LINE_BUFF <= buffNumber 
				|| lastSavedTime.tm_min != dtNow.tm_min 
				|| lastSavedTime.tm_hour != dtNow.tm_hour)
			{
				writeFlashMemory(lastSavedTime, lineBuff); //FM�o��
				buffNumber = 0;
				lineBuff[0] = '\0';
				lastSavedTime = dtNow;
			}
			
			//FM�����o�����͖`����DTT���s�v�B
			char *trmChar = charBuff + 4;
			strcat(lineBuff, trmChar);
			buffNumber++;
		}
	}
	slp_time++;
	//���̏����͈ӊO�ɓd�r�����Ղ���̂Ŏ��ԊԊu�𑝂₵XBee�g�p���݂̂Ƃ����i2024.07.22�j
	if(3500 <= slp_time && (outputToXBee || outputToBLE)){
		wakeupXbee(); //XBee�X���[�v����
		_delay_ms(1);  //�X���[�v�������̗����グ��50us=0.05ms���x������炵���B
		XbeeController::txChars("\r"); //�l�b�g���[�N�ؒf���p�̋�p�P�b�g�𑗐M�i���̏����͈����j
		slp_time = 0;
	}
	
	//UART���M���I�������10msec�҂���XBee���X���[�v������(XBee���̑��M���I���܂ő҂������̂�)
	//�{���A������CTS���g���Ď�M�\�ɂȂ����^�C�~���O�ŃX���[�v���H�t���[�R���g���[���������B
	_delay_ms(10); //���̃X���[�v��XBee�̒ʐM�I���҂��ړI�B���s����ŗp�ӂ����l�Ȃ̂ŁA�������B���B�������������ł͂Ȃ��悤�ɂ��v��	
}

static void calibrateVelocityVoltage()
{
	//LED�_��
	blinkGreenAndRedLED(1);
	
	char velVS[7] = "n/a";
	double velV = readVelVoltage(); //AD�ϊ�
	dtostrf(velV,6,4,velVS);	
	snprintf(charBuff, sizeof(charBuff), "SCV:%s\r", velVS);
	XbeeController::bltxChars(charBuff);
}

static void writeFlashMemory(const tm dtNow, const char write_chars[])
{
	//�}�E���g�������Ȃ�ΏI��
	if(!initFM) return;
	
	char fileName[13]={}; //yyyymmdd.csv
	snprintf(fileName, sizeof(fileName), "%04d%02d%02d.csv", dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday);
	
	//FM�L�^�p���t�X�V
	myRTC.year=dtNow.tm_year+1900;
	myRTC.month=dtNow.tm_mon+1;
	myRTC.mday=dtNow.tm_mday;
	myRTC.hour=dtNow.tm_hour;
	myRTC.min=dtNow.tm_min;
	myRTC.sec=dtNow.tm_sec;
	
	// FIL�I�u�W�F�N�g���[�J���ϐ��Ƃ��ăX�^�b�N�Ɋm��
	FIL fmFile;
	if(f_open(&fmFile, fileName, FA_OPEN_APPEND | FA_WRITE) == FR_OK)
	{
		f_puts(write_chars, &fmFile);
		f_close(&fmFile);
	}
	else initFM = false; // �G���[���͍ă}�E���g�����݂�
}

//PORTA PIN2���荞�݁i���Z�b�g�X�C�b�`�������݁j
ISR(PORTA_PORT_vect)
{
	// ���荞�݃t���O����
	PORTA.INTFLAGS = PIN2_bm;
	
	//Push:LED�_��, None:LED����
	if(PORTA.IN & PIN2_bm) turnOffRedLED();
	else turnOnRedLED();
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
				blinkRedLED(1);
				_delay_ms(3000);
			}
		case 2: //XBee�ݒ�G���[
			while(true)
			{
				blinkRedLED(2);
				_delay_ms(3000);
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
	if(interval == 1) return interval; //1sec�̏ꍇ�ɂ͒����Ɍv��
	if(interval <= 5) return interval - (5 - time.tm_sec % 5);
	else if(interval <= 10) return interval - (10 - time.tm_sec % 10);
	else if(interval <= 30) return interval - (30 - time.tm_sec % 30);
	else return interval - (60 - time.tm_sec % 60);
}

//�ȉ���inline�֐�************************************

inline static void sleepAnemo(void)
{
	PORTA.OUTCLR = PIN4_bm; //5V������~
}

inline static void wakeupAnemo(void)
{
	PORTA.OUTSET = PIN4_bm; //5V�����J�n
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

	// �_�ł̑O�Ɉ�x����
	PORTA.OUTCLR = pin_mask;

	// �w��񐔓_��
	for(int i=0; i < iterNum; i++)
	{
		_delay_ms(100);
		PORTA.OUTSET = pin_mask; // �_��
		_delay_ms(25);
		PORTA.OUTCLR = pin_mask; // ����
	}
}

inline static void blinkGreenAndRedLED(int iterNum)
{
	blinkLED(iterNum, PIN6_bm | PIN7_bm);
}

inline static void turnOnGreenLED(void)
{
	PORTA.OUTSET = PIN7_bm; //�_��
}

inline static void turnOffGreenLED(void)
{
	PORTA.OUTCLR = PIN7_bm; //����
}

inline static void toggleGreenLED(void)
{
	PORTA.OUTTGL = PIN7_bm; //���]
}

inline static void blinkGreenLED(int iterNum)
{
	blinkLED(iterNum, PIN7_bm);
}

inline static void turnOnRedLED(void)
{
	PORTA.OUTSET = PIN6_bm; //�_��
}

inline static void turnOffRedLED(void)
{
	PORTA.OUTCLR = PIN6_bm; //����
}

inline static void toggleRedLED(void)
{
	PORTA.OUTTGL = PIN6_bm; //���]
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