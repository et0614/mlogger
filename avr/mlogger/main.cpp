/**
 * @file main.cpp
 * @brief AVR(ATMega328)���g�p�����v���f�[�^���W�E���M�v���O����
 * @author E.Togashi
 * @date 2020/7/14
 *
 * version����
 * 2.3.0	EEPROM���g���ĕ␳�W�����Ǘ���������ɕύX,�N�����𒼐ڂɏo��,MicroSD�����o���Ή��J�n
 * 2.3.2    Reset�X�C�b�`�̓��͂ɑΉ��B
 * 2.3.3    AD�ϊ����艻�̂��߂ɔ����񐔑����B10��35��BUART�ʐM,XBee�ʐM��F�X�Ɖ��ǁBMicroSD�����o���Ή��A�ꉞ�o���オ��B
 * 2.3.4    XBee�ʐM��CTS����𓱓�
 * 2.3.5	�����O�o�b�t�@�ɂ��UART�ʐM�������
 * 2.4.1	AHT20�ɑΉ��BAD�ϊ���d���F�����̊��AVCC, �O���[�u���x�̊�����1.1V�ɕύX
 * 2.4.2	EEPROM�̏������o�O�C��
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
#include <util/delay.h>

//���t����p
#include <time.h>

#include "main.h"
#include "mlerr.h" //�G���[�R�[�h
#include "my_eeprom.h" //EEPROM����
#include "my_uart.h" //UART�ʐM
#include "my_i2c.h"  //I2C�ʐM
#include "my_xbee.h" //XBee�ʐM

//FatFs�֘A
#include "ff/ff.h"
#include "ff/diskio.h"
#include "ff/rtc.h"

//�萔�錾***********************************************************
//AD�ϊ��̈��艻�̂��߂̌J��Ԃ���
const uint8_t AD_ITER = 35;

//�M���������v�̗����グ�ɕK�v�Ȏ���[sec]
const uint8_t V_WAKEUP_TIME = 20;

//�Ɠx�Z���T�iOPTxxxx�j�̃A�h���X
const char OPT_ADDRESS = 0x88; //OPT3001��0x88, OPT3007��0x8A, ���g�p�B

//�O���[�u���x�v���p���W���[���̃^�C�v
const bool IS_MCP9700 = true;

//AM2320��AHT20��
const bool IS_AM2320 = false;

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

//WFC�𑗐M����܂ł̎c�莞��[sec]
static uint8_t wc_time = 0;

//SD�J�[�h�֘A
volatile bool initSD = false; //SD�J�[�h�������t���O
static FATFS* fSystem;

//�ėp�̕�����z��
static char charBuff[my_xbee::MAX_CMD_CHAR];

//�}�N����`********************************************************
#define cbi(addr, bit) addr &= ~(1 << bit) // addr��bit�ڂ�'0'�ɂ���B
#define sbi(addr, bit) addr |= (1 << bit)  // addr��bit�ڂ�'1'�ɂ���B
#define ARRAY_LENGTH(array) (sizeof(array) / sizeof(array[0]))

int main(void)
{
	//EEPROM
	my_eeprom::LoadCorrectionFactor();
	my_eeprom::LoadMeasurementSetting();
	
	//���o�̓|�[�g��������
	initialize_port();

	//��U�A���ׂĂ̊��荞�݋֎~
	cli();
		
	//AD�ϊ��L����
	ADCSRA = 0b10000111; //128�����Ōv���i�傫���������x�͍����A���Ԃ͂�����͗l�j
	
	//INT0���荞�ݐݒ�
	sbi(MCUSR, ISC01);
	sbi(EIMSK, INT0);
		
	//�ʐM��������
	my_i2c::InitializeI2C(); //I2C
	my_i2c::InitializeOPT(OPT_ADDRESS);  //OPTxxxx
	my_uart::Initialize();  //XBee�iUART�j
	
	//�^�C�}������
	initialize_timer();
	
	//XBee�X���[�v����
	wakeup_xbee();
	
	//FatFS����̂��߂̃������m��
	fSystem = (FATFS *)malloc(sizeof(FATFS));
	
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
		if(logging && !outputToBLE) set_sleep_mode(SLEEP_MODE_PWR_SAVE);
		else set_sleep_mode(SLEEP_MODE_IDLE); //���M���O�J�n�O��UART�ʐM���ł���悤��IDLE�ŃX���[�v
											
		//�X���[�v
		sleep_mode();
    }
}

static void initialize_port(void)
{
	//�o�̓|�[�g
	sbi(DDRD, DDD3); //SPI�ʐM�iCS�j
	sbi(DDRD, DDD5); //LED�o��
	sbi(DDRD, DDD1); //RXD:UART�����o��
	sbi(DDRD, DDD7); //XBee�X���[�v����
	sbi(DDRC, DDC1); //�������v�����[
	sbi(DDRD, DDB0); //�������v5V����
	sleep_anemo();   //�������v�͓d�r�������̂ŁA�����ɃX���[�v����

	//���̓|�[�g
	cbi(DDRC, DDC0); //�O���[�u���x�Z���TAD�ϊ�
	cbi(DDRC, DDC2); //�����Z���TAD�ϊ�
	cbi(DDRD, DDD0); //RXD:UART�ǂݍ���
	cbi(DDRD, DDD2); //INT0:���Z�b�g�p���荞��
	sbi(PORTD, PORTD2); //INT0���v���A�b�v
}

//�^�C�}������
static void initialize_timer( void )
{
	//Timer 1 //0.01sec�^�C�}
	OCR1A = (uint16_t)( ( F_CPU / 8L ) / 100L );	// �J�E���g��8MHz/8/100(100Hz)
	TCNT1 = 0;	//������
	TCCR1A = 0b00000000;			// CTC����
	TCCR1B = (0x02 | _BV(WGM12));	// 8����
	TIMSK1 |= _BV(OCIE1A);			// ���荞�݋���
	
	//Timer 2 //1sec�^�C�}
	//�O���N���X�^���쓮�ݒ�
	ASSR |= (1<<AS2);
	TCNT2 = 0;	//������
	TCCR2A = 0b00000000;	// �W���i�I�[�o�[�t���[�j����
	TCCR2B = 0b10000101;	// 128����
	TIMSK2 |= _BV(TOIE2);	// ���荞�݋���
}

//UART��M���̊��荞�ݏ���
ISR(USART_RX_vect)
{
	char dat = UDR0;	//�ǂݏo��
	
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
		my_xbee::bltx_chars("VER:2.4.2\r");
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
		
		//�v���J�n����
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
	
	//�R�}���h���폜
	cmdBuff[0] = '\0';
}

// Timer1���荞��//FatFs�iSD�J�[�h���o�͒ʐM�j�p
ISR(TIMER1_COMPA_vect)
{
	disk_timerproc();	/* Drive timer procedure of low level disk I/O module */
}

// Timer2���荞�݁F���M���O�p��1�b���̏���
ISR(TIMER2_OVF_vect)
{
	currentTime++; //1�b�i�߂�
				
	//���M���O���ł����
	if(logging)
	{
		//�v���J�n�����̑O�Ȃ�ΏI��
		if(currentTime < startTime) return;
		
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
		
		//����������************
		pass_vel++;
		if(my_eeprom::measure_vel && my_eeprom::interval_vel <= pass_vel)
		{
			double velV = readVelVoltage(); //AD�ϊ�
			dtostrf(velV,6,4,velVS);
			
			float bff = max(0, velV / my_eeprom::Cf_vel0 - 1.0);
			float vel = bff * (2.3595 + bff * (-12.029 + bff * 79.744));
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
			float ill_d = my_i2c::ReadOPT(OPT_ADDRESS);
			ill_d = max(0,min(99999.99,my_eeprom::Cf_luxA * ill_d + my_eeprom::Cf_luxB));
			dtostrf(ill_d,8,2,illS);
			pass_ill = 0;
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
			snprintf(charBuff, sizeof(charBuff), "%s%04d,%02d/%02d,%02d:%02d:%02d,%s,%s,%s,%s,%s,%s,%s\r",
			(outputToSDCard ? "" : "DTT:"), dtNow.tm_year + 1900, dtNow.tm_mon + 1, dtNow.tm_mday, dtNow.tm_hour, dtNow.tm_min, dtNow.tm_sec,
			tmpS, hmdS, glbTS, velS, illS, glbVS, velVS);
			
			//������I�[�o�[�ɔ����čŌ�ɏI���R�[�h'\r\0'�����Ă���
			charBuff[my_xbee::MAX_CMD_CHAR-2]='\r';
			charBuff[my_xbee::MAX_CMD_CHAR-1]='\0';

			if(outputToXBee) my_xbee::tx_chars(charBuff); //XBee Zigbee�o��
			if(outputToBLE) my_xbee::bl_chars(charBuff); //XBee Bluetooth�o��
			if(outputToSDCard) writeSDcard(dtNow, charBuff); //SD card�o��
		}
				
		//UART���M���I�������10msec�҂���XBee���X���[�v������(XBee���̑��M���I���܂ő҂������̂�)
		//�{���A������CTS���g���Ď�M�\�ɂȂ����^�C�~���O�ŃX���[�v���H�t���[�R���g���[���������B
		//while(! my_uart::tx_done()); //�~�܂�

		_delay_ms(10);
		//Bluetooth�ʐM�łȂ���΃X���[�v�ɓ���iXBee�̎d�l��ABluetooth���[�h�̃X���[�v�͕s�j
		if(!outputToBLE) sleep_xbee();
		if(outputToSDCard) blinkLED(1); //SD�J�[�h�L�^���͖��bLED�_��
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
		blinkLED(initSD ? 2 : 1);
	}
}

//static void writeSDcard(const tm dtNow, const char write_chars[])
static void writeSDcard(const tm dtNow, const char* write_chars)
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

//INT0���荞�݁F�v�����f����
ISR(INT0_vect)
{
	//���M���O��~
	logging=false;
	
	//SD�J�[�h�ă}�E���g
	initSD = false;
		
	//�����Z���T���~
	sleep_anemo();
	
	//LED�_��
	blinkLED(3);
}

//�O���[�u���x�̓d����ǂݎ��
static float readGlbVoltage(void)
{
	long int refV = 0;
	long int adV = 0;
	for(int i=0;i<AD_ITER;i++)
	{
		//�o��
		//�O���[�u���x�̓d����0.7V���x�Ȃ̂ŁAAREF��AVCC��ؒf���āA�����d���i1.1V�j��Ōv�����ׂ�
		
		//�1.1V���v��
		ADMUX = 0b11101110;
		_delay_ms(5);
		ADCSRA = 0b11000111; //�ϊ��J�n
		while(ADCSRA & 0b01000000); //�ϊ��I���҂�
		refV += ADC;

		//AD0���v��
		ADMUX = 0b11100000;
		_delay_ms(5);
		ADCSRA = 0b11000111; //�ϊ��J�n
		while(ADCSRA & 0b01000000); //�ϊ��I���҂�
		adV += ADC;
	}
	return (float)adV / (float)refV * 1.1;
}

//�������̓d����ǂݎ��
static float readVelVoltage(void)
{
	//���ϒl���o�͂��Ĉ��艻
	long int refV = 0;
	long int adV = 0;
	for(int i=0;i<AD_ITER;i++)
	{
		//�1.1V���v��
		ADMUX = 0b01001110;
		_delay_ms(5);
		ADCSRA = 0b11000111; //�ϊ��J�n
		while(ADCSRA & 0b01000000); //�ϊ��I���҂�
		refV += ADC;
	
		//AD2(Velocity)���v��
		ADMUX = 0b01000010;
		_delay_ms(5);
		ADCSRA = 0b11000111; //�ϊ��J�n
		while(ADCSRA & 0b01000000); //�ϊ��I���҂�
		adV += ADC;
	}
	return (float)adV / (float)refV * 1.1;
}

static void sleep_anemo(void)
{
	cbi(PORTC, PORTC1); //�����[�Ւf
	cbi(PORTB, PORTB0); //5V������~
}

//�ȉ���inline�֐�************************************

inline static void wakeup_anemo(void)
{
	sbi(PORTC, PORTC1); //�����[�ʓd
	sbi(PORTB, PORTB0); //5V�����J�n
}

inline static void sleep_xbee(void)
{
	sbi(PORTD, PORTD7);
}

inline static void wakeup_xbee(void)
{
	cbi(PORTD, PORTD7);
}

//LED��_�ł�����
inline static void blinkLED(int iterNum)
{
	if(iterNum < 1) return;

	//����
	sbi(PORTD, PORTD5);
	_delay_ms(25);
	cbi(PORTD, PORTD5);
	
	//2��ڈȍ~�͎��Ԃ��󂯂ē_��
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