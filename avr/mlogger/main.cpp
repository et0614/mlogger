/**
 * @file main.cpp
 * @brief AVR(AVRxxDB32)���g�p�����v���f�[�^���W�E���M�v���O����
 * @author E.Togashi
 * @date 2022/3/11
 *
 * version����
 * 3.0.0	AVRxxDB32�V���[�Y�p
 * 3.0.1	Reset������3�b�������ŗL���ɁB�Z���ԉ������݂͓d�r�m�F�̂��߂̓_���ɕύX�B
 * 3.0.2	ADC�o�O�C��
 * 3.0.3	ADC��d����2.0V�ɕύX
 * 3.0.4	CMS�R�}���h���s���ɂ�EEPROM�ɐݒ��ۑ�����悤�ɕύX
 * 3.0.5	�@�햼�̊֘A�̃R�}���h�iLLN,CLN�j������
 * 3.0.6	AHT20�̃G���[���̃��Z�b�g������ǉ�
 * 3.0.7	SD�J�[�h�����o���̏ȓd�͉�
 * 3.0.8	SD�J�[�h�����o������LED�_���o�O�C��
 */

/**XBee�[���̐ݒ�****************************************
 * �E�e�@�q�@����
 *   1) Firmware��ZIGBEE TH Reg version 4061, XB3-24,Digi XBee3 Zigbee 3.0 TH 100D
 *   2) PAN ID�𓯂��l�ɂ���
 *   3) SP:Cyclic Sleep Period = 0x64�i=1000 msec�j,SN:Number of Cyclic Sleep Periods = 3600
 *      �����3600�~3=3hour�̓l�b�g���[�N����O��Ȃ�
 *   4) AP:API Enable = API enabled
 * �E�e�@�̂�
 *   1) CE:Coordinator Enable = Enabled
 *   2) SM:Sleep Mode = No sleep
 *   3) AR:many-to-one routing = 0
 *   4) NJ:Node Join Time = FF�i���Ԗ������Ƀl�b�g���[�N�Q���\�j
 * �E�q�@�̂�
 *   1) CE:Coordinator Enable = Disabled
 *   2) SM:Sleep Mode = Pin Hibernate�iATMega����̎w�߂ŃX���[�v�������邽�߁j
 *   �ȉ���Bluetooth�Ή��̏ꍇ�̂�
 *   3) BT:Bluetooth Enable = Enabled
 *   4) BI:Bluetooth Identifier = "MLogger_xxx"�ixxx�͓K���ȕ����ŗǂ��j
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

//FatFs�֘A
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//�萔�錾***********************************************************
//�M���������v�̗����グ�ɕK�v�Ȏ���[sec]
const uint8_t V_WAKEUP_TIME = 20;

//�Ɠx�Z���T�iOPTxxxx�j�̃A�h���X
const char OPT_ADDRESS = 0x88; //OPT3001��0x88, OPT3007��0x8A, ���g�p�B

//�O���[�u���x�v���p���W���[���̃^�C�v
const bool IS_MCP9700 = false; //MCP9701�Ȃ��false

//AM2320��AHT20��
const bool IS_AM2320 = false;

//���s���̃f�[�^���ꎞ�ۑ����邩
const int N_LINE_BUFF = 45;

//�L��ϐ���`********************************************************
//�����֘A
volatile static time_t currentTime = 0; //���ݎ����iUNIX���ԁj
volatile static time_t startTime = 1609459200;   //�v���J�n�����iUNIX����,UTC����0��2021/1/1 00:00:00�j

//�v�������ۂ�
volatile static bool logging = false;

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
volatile static unsigned int pass_ad2 = 0;
volatile static unsigned int pass_ad3 = 0;

//WFC�𑗐M����܂ł̎c�莞��[sec]
static uint8_t wc_time = 0;

//SD�J�[�h�֘A
volatile bool initSD = false; //SD�J�[�h�������t���O
static FATFS* fSystem;
static char lineBuff[my_xbee::MAX_CMD_CHAR * N_LINE_BUFF + 1]; //�ꎞ�ۑ������z��i������null������ǉ��j
static uint8_t buffNumber = 0; //�ꎞ�ۑ���
static uint8_t lastSavedMinute = 0; //�Ō�ɕۑ�������
static uint8_t blinkCount = 0; //SD�����o������LED�_�Ŏ��ԊԊu�ێ��ϐ�

//���Z�b�g�����p
volatile static unsigned int resetTime = 0;

//�ėp�̕�����z��
static char charBuff[my_xbee::MAX_CMD_CHAR];

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
	
	//�X�C�b�`���荞�ݐݒ�
	PORTA.PIN2CTRL |= PORT_ISC_BOTHEDGES_gc; //�d���㏸�E�~������
		
	//�ʐM��������
	my_i2c::InitializeI2C(); //I2C
	//my_i2c::InitializeOPT(OPT_ADDRESS);  //�Ɠx�Z���T�Ƃ���OPTxxxx���g���ꍇ
	my_xbee::Initialize();  //XBee�iUART�j
	my_i2c::InitializeAHT20();
	
	//�^�C�}������
	initialize_timer();
	
	//XBee�X���[�v����
	wakeup_xbee();
	
	fSystem = (FATFS*) malloc(sizeof(FATFS));
	
	//�����ݒ肪�I������班���ҋ@
	_delay_ms(500);

	//���荞�ݍĊJ
	sei();

	//�ʐM�ĊJ�t���O���m�F
	if(my_eeprom::startAuto)
	{
		logging = true;
		outputToXBee = true;
		outputToBLE = outputToSDCard = false;
		startTime = currentTime;
	}
	
    while (1) 
    {
		//Logging���łȂ����SD�J�[�h�}�E���g�����݂�
		if(!logging && !initSD)
			if(f_mount(fSystem, "", 1) == FR_OK) initSD = true;
		
		//�X���[�v���[�h�ݒ�		
		if(logging && !outputToBLE) set_sleep_mode(SLEEP_MODE_PWR_DOWN); //ATmega328P�ł�PWR_SAVE
		else set_sleep_mode(SLEEP_MODE_IDLE); //���M���O�J�n�O��UART�ʐM���ł���悤��IDLE�ŃX���[�v

		//Bluetooth�ʐM�������A���M���O����XBee���X���[�v������iXBee�̎d�l��ABluetooth���[�h�̃X���[�v�͕s�j
		if(logging && !outputToBLE) sleep_xbee();
		
		//�}�C�R�����X���[�v������
		sleep_mode();
    }
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
	PORTA.DIRSET = PIN2_bm; //PORTA PIN2:���Z�b�g�p���荞��
	sleep_anemo();   //�������v�͓d�r�������̂ŁA�����ɃX���[�v����

	//���̓|�[�g
	PORTD.DIRCLR = PIN4_bm; //�O���[�u���x�Z���TAD�ϊ�
	PORTD.DIRCLR = PIN2_bm; //�����Z���TAD�ϊ�
	PORTF.DIRCLR = PIN4_bm; //�ėpAD�ϊ�1
	PORTD.DIRCLR = PIN5_bm; //�ėpAD�ϊ�2
	PORTD.DIRCLR = PIN3_bm; //�ėpAD�ϊ�3
	PORTA.DIRCLR = PIN6_bm; //�ėpIO1
	PORTA.DIRCLR = PIN7_bm; //�ėpIO2
	
	//�v���A�b�v/�_�E��
	PORTA.OUTSET = PIN2_bm; //PORTA PIN2(Interrupt)�F�A�b�v
	PORTA.OUTSET = PIN6_bm; //�ėpIO1�F�A�b�v
	PORTA.OUTSET = PIN7_bm; //�ėpIO2�F�A�b�v
	
	//PORTD.OUTCLR = PIN4_bm; //�O���[�u���x�Z���T�F�_�E�� 2022.04.19 Pullup/down��ADC�ł͂������������B
	//PORTD.OUTCLR = PIN2_bm; //�����Z���T�F�_�E��
	//PORTF.OUTCLR = PIN4_bm; //�ėpAD�ϊ�1�F�_�E��
	//PORTD.OUTCLR = PIN5_bm; //�ėpAD�ϊ�2�F�_�E��
	//PORTD.OUTCLR = PIN3_bm; //�ėpAD�ϊ�3�F�_�E��
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
		else if(framePosition == 3 && dat != 0x90) //0x90�ȊO�̃R�}���h�̏ꍇ�ɂ͖���//���̏����A��肠��
		{
			if(dat == 0x90) xbeeOffset = 14; //ZigBee Recieve Packet�̏ꍇ�̃I�t�Z�b�g
			else if(dat == 0xAD) xbeeOffset = 4; //User Data Relay�̏ꍇ�̃I�t�Z�b�g
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
	
	//�o�[�W����
	if (strncmp(command, "VER", 3) == 0) 
		my_xbee::bltx_chars("VER:3.0.8\r");
	//���M���O�J�n
	else if (strncmp(command, "STL", 3) == 0)
	{
		//���ݎ�����ݒ�
		char num[11];
		num[10] = '\0';
		strncpy(num, command + 3, 10);
		currentTime = atol(num);
		
		//Bluetooth�ڑ����ۂ�(x��xbee,b��bluetooth)
		outputToXBee = (command[13]=='t'); //XBee�Őe�@�ɏ����o�����ۂ�
		outputToBLE = (command[14]=='t'); //Bluetooth�ŏ����o�����ۂ�
		outputToSDCard = (command[15]=='t'); //SD�J�[�h�ɏ����o�����ۂ�
		
		//0�b���_�Œ����Ɉ��͌v������B�������͂������Ȓl�ɂȂ邪�B�B�B
		pass_th	= my_eeprom::interval_th;
		pass_glb = my_eeprom::interval_glb;
		pass_vel = my_eeprom::interval_vel;
		pass_ill = my_eeprom::interval_ill;
		pass_ad1 = my_eeprom::interval_AD1;
		pass_ad2 = my_eeprom::interval_AD2;
		pass_ad3 = my_eeprom::interval_AD3;
		
		//���M���O�ݒ��EEPROM�ɕۑ�
		//my_eeprom::startAuto = outputToXBee; //���Z�b�g�X�C�b�`������Ղ��p�ӂł�����R�����g�A�E�g����
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
		startTime = atol(num2);
		
		//���M���O�ݒ��EEPROM�ɕۑ�
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
	
	//���Z�b�g�{�^���������݊m�F
	if(!(PORTA.IN & PIN2_bm))
	{
		resetTime++;
		if(3 < resetTime)
		{
			logging=false;	//���M���O��~
			initSD = false;	//SD�J�[�h�ă}�E���g
			sleep_anemo();	//�����Z���T���~
			blinkLED(3);	//LED�_��
			return;
		}
	}
	else resetTime = 0; //Reset�{�^���������ݎ��Ԃ�0�ɖ߂�
				
	//���M���O���ł����
	if(logging)
	{
		//�v���J�n�����̑O�Ȃ�ΏI��
		if(currentTime < startTime) return;
		
		//SD�J�[�h���M���O����5�b���Ƃɓ_��
		if(outputToSDCard)  //SD card�o��
		{
			blinkCount++;
			if(5 <= blinkCount)
			{
				blinkCount = 0;
				blinkLED(1);
			}
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
		char adV2S[7] = "n/a";
		char adV3S[7] = "n/a";
		
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
	
		//�O���[�u���x����************
		pass_glb++;
		if(my_eeprom::measure_glb && my_eeprom::interval_glb <= pass_glb)
		{
			float glbV = readGlbVoltage(); //AD�ϊ�
			dtostrf(glbV,6,4,glbVS);
			
			float glbT = (glbV - (IS_MCP9700 ? 0.5 : 0.4)) / (IS_MCP9700 ? 0.0100 : 0.0195);
			glbT = max(-10,min(50,my_eeprom::Cf_glbA * glbT + my_eeprom::Cf_glbB));
			dtostrf(glbT,6,2,glbTS);
			
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
		
		//�ėpAD�ϊ�����2
		pass_ad2++;
		if(my_eeprom::measure_AD2 && my_eeprom::interval_AD2 <= pass_ad2)
		{
			float adV = readVoltage(2); //AD�ϊ�
			dtostrf(adV,6,4,adV2S);
			pass_ad2 = 0;
			hasNewData = true;
		}
		
		//�ėpAD�ϊ�����3
		pass_ad3++;
		if(my_eeprom::measure_AD3 && my_eeprom::interval_AD3 <= pass_ad3)
		{
			float adV = readVoltage(3); //AD�ϊ�
			dtostrf(adV,6,4,adV3S);
			pass_ad3 = 0;
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
			
			//�������쐬
			time_t ct = currentTime - UNIX_OFFSET;
			tm dtNow;
			gmtime_r(&ct, &dtNow);
			
			//�����o����������쐬
			snprintf(charBuff, sizeof(charBuff), "%s%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\r",
			(outputToSDCard ? "" : "DTT:"), dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
			tmpS, hmdS, glbTS, velS, illS, glbVS, velVS, adV1S, adV2S, adV3S);
			
			//������I�[�o�[�ɔ����čŌ�ɏI���R�[�h'\r\0'�����Ă���
			charBuff[my_xbee::MAX_CMD_CHAR-2]='\r';
			charBuff[my_xbee::MAX_CMD_CHAR-1]= '\0';

			if(outputToXBee) my_xbee::tx_chars(charBuff); //XBee Zigbee�o��
			if(outputToBLE) my_xbee::bl_chars(charBuff); //XBee Bluetooth�o��
			if(outputToSDCard)  //SD card�o��
			{
				//�f�[�^���\���ɗ��܂邩�A1min�ȏ�̎��ԊԊu���������珑���o��
				if(N_LINE_BUFF <= buffNumber || lastSavedMinute != dtNow.tm_min)
				{
					writeSDcard(dtNow, lineBuff); //SD card�o��
					buffNumber = 0;
					lineBuff[0] = '\0';
					lastSavedMinute = dtNow.tm_min;
				}
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
	else
	{
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

static void writeSDcard(const tm dtNow, const char write_chars[])
{
	//�}�E���g�������Ȃ�ΏI��
	if(!initSD) return;
	
	char fileName[13]={}; //yyyymmdd.csv
	snprintf(fileName, sizeof(fileName), "%04d%02d%02d.txt", dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday);
	
	//SD�J�[�h�L�^�p���t�X�V
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
	else if(adNumber == 2) ADC0.MUXPOS = ADC_MUXPOS_AIN5_gc; //AD2
	else ADC0.MUXPOS = ADC_MUXPOS_AIN3_gc; //AD3

	_delay_ms(5);
	ADC0.COMMAND = ADC_STCONV_bm; //�ϊ��J�n
	while (!(ADC0.INTFLAGS & ADC_RESRDY_bm)) ; //�ϊ��I���҂�
	return 2.0 * (float)ADC0.RES / 65536; //1024*64 (10bit,64�񕽋�)
}

static void sleep_anemo(void)
{
	PORTA.OUTCLR = PIN5_bm; //�����[�Ւf
	PORTA.OUTCLR = PIN4_bm; //5V������~
}

//�ȉ���inline�֐�************************************

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

inline static void blinkLED(int iterNum)
{
	if(iterNum < 1) return;

	//����
	PORTD.OUTCLR = PIN6_bm; //��U�K����������
	//�_��
	for(int i=0;i<iterNum;i++)
	{
		_delay_ms(100);
		PORTD.OUTSET = PIN6_bm; //�_��
		_delay_ms(25);
		PORTD.OUTCLR = PIN6_bm; //����
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