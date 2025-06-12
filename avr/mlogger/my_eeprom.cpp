/**
 * @file my_eeprom.h
 * @brief AVR(AVRxxDB32)��EEPROM����������
 * @author E.Togashi
 * @date 2021/12/19
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <avr/eeprom.h>

#include "parameters.h"
#include "my_eeprom.h"

//EEPROM�̏������t���O�B�R���p�C����ŏ��̌Ăяo���̂ݏ���������
static uint8_t EEMEM EEP_INITFLAG;

//XBEE�̏������t���O
static uint8_t EEMEM EEP_XB_INITFLAG;

//�␳�W��
static CorrectionFactors EEMEM EEP_CFACTORS;

//���������W��
static VelocityCharacteristicCoefficients EEMEM EEP_VCCOEFS;

//�v���ݒ�
static MeasurementSettings EEMEM EEP_MSETTINGS;

//���K�[����
static char EEMEM EEP_NAME[21];

//�␳�W��
CorrectionFactors my_eeprom::cFactors;

//���������W��
VelocityCharacteristicCoefficients my_eeprom::vcCoefficients;

//�v���ݒ�
MeasurementSettings my_eeprom::mSettings;

//���K�[����
char my_eeprom::mlName[21];

//����璷�����l�𐶐�
static uint8_t crc8(uint8_t *ptr, uint8_t len)
{
	uint8_t crc = 0xFF;
	for(int i = 0; i < len; i++) {
		crc ^= *ptr++;
		for(uint8_t bit = 8; bit > 0; --bit) {
			if(crc & 0x80) {
				crc = (crc << 1) ^ 0x31u;
				} else {
				crc = (crc << 1);
			}
		}
	}
	return crc;
}

void initCFactors(){
	my_eeprom::cFactors = {
		1, //�o�[�W����
		DBT_COEF_A, //�������xa
		DBT_COEF_B, //�������xb
		HMD_COEF_A, //���Ύ��xa
		HMD_COEF_B, //���Ύ��xb
		GLB_COEF_A, //�O���[�u���xa
		GLB_COEF_B, //�O���[�u���xb
		1.0, //�Ɠxa
		0.0, //�Ɠxb
		1.0, //����a
		0.0, //����b
		VEL_VEL0, //������
		0 //CRC�i��U0�ŏ������j
	};
	// CRC���v�Z
	my_eeprom::cFactors.crc = crc8(
		(uint8_t*)&my_eeprom::cFactors,
		sizeof(CorrectionFactors) - sizeof(my_eeprom::cFactors.crc) //crc�����o�[���g�̃T�C�Y�͌v�Z�͈͂��珜�O����
	);
}

void initVCCoefficients(){
	my_eeprom::vcCoefficients = {
		1,		//�o�[�W����
		VEL_COEF_A,		//�W��A
		VEL_COEF_B, //�W��B
		0,	//�W��C
		0		//CRC�i��U0�ŏ������j
	};
	// CRC���v�Z
	my_eeprom::cFactors.crc = crc8(
		(uint8_t*)&my_eeprom::cFactors,
		sizeof(CorrectionFactors) - sizeof(my_eeprom::cFactors.crc) //crc�����o�[���g�̃T�C�Y�͌v�Z�͈͂��珜�O����
	);
}

void initMSettings(){
	my_eeprom::mSettings = {
		1,		//�o�[�W����
		false, //��������J�n
		true, //�������x�̌v���^�U
		true, //�O���[�u���x�̌v���^�U
		true, //�����̌v���^�U
		true, //�Ɠx�̌v���^�U
		false, //�ėpAD1�̌v���^�U
		false, //�ėpAD2�̌v���^�U
		false, //�ėpAD3�̌v���^�U
		false, //�ߐڃZ���T�̌v���^�U
		false, //CO2�̌v���^�U
		1, //�������x�̌v���Ԋu[sec]
		1, //�O���[�u���x�̌v���Ԋu[sec]
		1, //�����̌v���Ԋu[sec]
		1, //�Ɠx�̌v���Ԋu[sec]
		1, //�ėpAD1�̌v���Ԋu[sec]
		1, //�ėpAD2�̌v���Ԋu[sec]
		1, //�ėpAD3�̌v���Ԋu[sec]
		1, //�ߐڃZ���T�̌v���Ԋu[sec]
		1, //CO2�̌v���Ԋu[sec]
		1609459200,	//�v���J�n���� (UNIX����,UTC����0��2021/1/1 00:00:00)
		0		//CRC�i��U0�ŏ������j
	};
	// CRC���v�Z
	my_eeprom::mSettings.crc = crc8(
		(uint8_t*)&my_eeprom::mSettings,
		sizeof(MeasurementSettings) - sizeof(my_eeprom::mSettings.crc) //crc�����o�[���g�̃T�C�Y�͌v�Z�͈͂��珜�O����
	);
}

//�␳�W������������
void writeCFactors()
{
	eeprom_busy_wait();
	eeprom_update_block(&my_eeprom::cFactors, &EEP_CFACTORS, sizeof(CorrectionFactors));
}

//���������W������������
void writeVCCoefficients()
{
	eeprom_busy_wait();
	eeprom_update_block(&my_eeprom::vcCoefficients, &EEP_VCCOEFS, sizeof(VelocityCharacteristicCoefficients));
}

//�v���ݒ����������
void writeMSettings()
{
	eeprom_busy_wait();
	eeprom_update_block(&my_eeprom::mSettings, &EEP_MSETTINGS, sizeof(MeasurementSettings));
}

//������������������
void initMemory()
{
	//�␳�W��
	initCFactors();
	writeCFactors();

	//�����v�����W��
	initVCCoefficients();
	writeVCCoefficients();
	
	//�v���ݒ�
	initMSettings();
	writeMSettings();
	
	//���O
	eeprom_busy_wait();
	eeprom_update_block((const void *)ML_NAME, (void *)EEP_NAME, sizeof(my_eeprom::mlName));
	
	//XBee�������t���O
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_XB_INITFLAG,'F');
	
	//�������t���O
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_INITFLAG,'T');
}

//�␳�W����ݒ肷��
void my_eeprom::SetCorrectionFactor(const char data[])
{
	float buff;
	char num[5];
	num[4] = '\0';
	
	//�������x�␳�W��A
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::cFactors.dbtA = buff;
	//�������x�␳�W��B
	strncpy(num, data + 7, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		my_eeprom::cFactors.dbtB = buff;
	
	//���Ύ��x�␳�W��A
	strncpy(num, data + 11, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::cFactors.hmdA = buff;
	//���Ύ��x�␳�W��B
	strncpy(num, data + 15, 4);
	buff = 0.01 * atol(num);
	if(-9.99 <= buff && buff <= 9.99)
		my_eeprom::cFactors.hmdB = buff;
	
	//�O���[�u���x�␳�W��A
	strncpy(num, data + 19, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::cFactors.glbA = buff;
	//�O���[�u���x�␳�W��B
	strncpy(num, data + 23, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		my_eeprom::cFactors.dbtB = buff;
	
	//�Ɠx�␳�W��A
	strncpy(num, data + 27, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::cFactors.luxA = buff;
	//�Ɠx�␳�W��B
	strncpy(num, data + 31, 4);
	buff = atol(num);
	if(-999 <= buff && buff <= 999)
		my_eeprom::cFactors.luxB = buff;
	
	//�����␳�W��A
	strncpy(num, data + 35, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		my_eeprom::cFactors.velA = buff;
	//�����␳�W��B
	strncpy(num, data + 39, 4);
	buff = 0.001 * atol(num);
	if(-0.5 <= buff && buff <= 0.5)
		my_eeprom::cFactors.velB = buff;
	//���������d��
	strncpy(num, data + 43, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.50)
		my_eeprom::cFactors.vel0 = buff;
	
	//EEPROM�ɏ�������
	writeCFactors();
}

//�␳�W����\����������쐬����
void my_eeprom::MakeCorrectionFactorString(char * txbuff, const char * command)
{
	char dbtA[6],dbtB[6],hmdA[6],hmdB[6],glbA[6],glbB[6],luxA[6],luxB[5],velA[6],velB[7],vel0[6];
	
	dtostrf(my_eeprom::cFactors.dbtA,5,3,dbtA);
	dtostrf(my_eeprom::cFactors.dbtB,5,2,dbtB);
	
	dtostrf(my_eeprom::cFactors.hmdA,5,3,hmdA);
	dtostrf(my_eeprom::cFactors.hmdB,5,2,hmdB);
	
	dtostrf(my_eeprom::cFactors.glbA,5,3,glbA);
	dtostrf(my_eeprom::cFactors.glbB,5,2,glbB);
	
	dtostrf(my_eeprom::cFactors.luxA,5,3,luxA);
	dtostrf(my_eeprom::cFactors.luxB,4,0,luxB);
	
	dtostrf(my_eeprom::cFactors.velA,5,3,velA);
	dtostrf(my_eeprom::cFactors.velB,6,3,velB);
	dtostrf(my_eeprom::cFactors.vel0,5,3,vel0);
	
	sprintf(txbuff, "%s:%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\r",
		command, dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, vel0);
}

//�����̓����W����ݒ肷��
void my_eeprom::SetVelocityCharacteristics(const char data[])
{
	float buff;
	char num[8];
	
	//�����d��
	num[4] = '\0';
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.60)
	my_eeprom::cFactors.vel0 = buff;
	
	//����A
	num[7] = '\0';
	strncpy(num, data + 7, 7);
	buff = 0.001 * atol(num);
	my_eeprom::vcCoefficients.ccA = buff;
	
	//����B
	strncpy(num, data + 14, 7);
	buff = 0.001 * atol(num);
	my_eeprom::vcCoefficients.ccB = buff;
	
	//����C
	strncpy(num, data + 21, 7);
	buff = 0.001 * atol(num);
	my_eeprom::vcCoefficients.ccC = buff;

	writeVCCoefficients();
}

//�����̓����W����\����������쐬����
void my_eeprom::MakeVelocityCharateristicsString(char * txbuff, const char * command)
{
	char vel0[6],vccA[9],vccB[9],vccC[9];
	
	dtostrf(my_eeprom::cFactors.vel0,5,3,vel0);
	dtostrf(my_eeprom::vcCoefficients.ccA,8,3,vccA);
	dtostrf(my_eeprom::vcCoefficients.ccB,8,3,vccB);
	dtostrf(my_eeprom::vcCoefficients.ccC,8,3,vccC);
	
	sprintf(txbuff, "%s:%s,%s,%s,%s\r",
	command, vel0, vccA, vccB, vccC);
}

//�v���ݒ��ݒ肷��
void my_eeprom::SetMeasurementSetting()
{
	writeMSettings();	
}

//���̂���������
void my_eeprom::SaveName()
{
	eeprom_busy_wait();	
	eeprom_update_block((const void *)mlName, (void *)EEP_NAME, sizeof(my_eeprom::mlName));
}

//�␳�W����ǂݍ���
void LoadCorrectionFactor()
{
	eeprom_busy_wait();	
	eeprom_read_block(&my_eeprom::cFactors, &EEP_CFACTORS, sizeof(CorrectionFactors));

	// �ǂݍ��񂾃f�[�^��CRC������
	uint8_t expected_crc = my_eeprom::cFactors.crc;
	uint8_t actual_crc = crc8(
		(uint8_t*)&my_eeprom::cFactors,
		sizeof(CorrectionFactors) - sizeof(my_eeprom::cFactors.crc)
	);

	// CRC����v���Ȃ��i�f�[�^�j���j�ꍇ�ɂ̓f�t�H���g�l�ōď�����
	if (expected_crc != actual_crc) initCFactors();
}

//�����̓����W����ǂݍ���
void LoadVelocityCharateristics()
{
	eeprom_busy_wait();
	eeprom_read_block(&my_eeprom::vcCoefficients, &EEP_VCCOEFS, sizeof(VelocityCharacteristicCoefficients));

	// �ǂݍ��񂾃f�[�^��CRC������
	uint8_t expected_crc = my_eeprom::vcCoefficients.crc;
	uint8_t actual_crc = crc8(
		(uint8_t*)&my_eeprom::vcCoefficients,
		sizeof(VelocityCharacteristicCoefficients) - sizeof(my_eeprom::vcCoefficients.crc)
	);

	// CRC����v���Ȃ��i�f�[�^�j���j�ꍇ�ɂ̓f�t�H���g�l�ōď�����
	if (expected_crc != actual_crc) initVCCoefficients();
}

//�v���ݒ��ǂݍ���
void LoadMeasurementSetting()
{
	eeprom_busy_wait();
	eeprom_read_block(&my_eeprom::mSettings, &EEP_MSETTINGS, sizeof(MeasurementSettings));

	// �ǂݍ��񂾃f�[�^��CRC������
	uint8_t expected_crc = my_eeprom::mSettings.crc;
	uint8_t actual_crc = crc8(
		(uint8_t*)&my_eeprom::mSettings,
		sizeof(MeasurementSettings) - sizeof(my_eeprom::mSettings.crc)
	);

	// CRC����v���Ȃ��i�f�[�^�j���j�ꍇ�ɂ̓f�t�H���g�l�ōď�����
	if (expected_crc != actual_crc) initMSettings();
}

//���̂�ǂݍ���
void LoadName()
{
	eeprom_busy_wait();
	eeprom_read_block((void *)my_eeprom::mlName, (const void *)EEP_NAME, sizeof(my_eeprom::mlName));
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
	LoadVelocityCharateristics();
	LoadMeasurementSetting();
	LoadName();
}

//XBee���������ς��ۂ����擾����
bool my_eeprom::IsXBeeInitialized(){
	eeprom_busy_wait(); //EEPROM�ǂݏ����\�܂őҋ@
	return eeprom_read_byte(&EEP_XB_INITFLAG) == 'T';
}

//XBee���������L�^����
void my_eeprom::XBeeInitialized(){
	eeprom_busy_wait();
	eeprom_write_byte(&EEP_XB_INITFLAG,'T');	
}