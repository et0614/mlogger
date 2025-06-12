/**
 * @file my_eeprom.h
 * @brief AVR(ATMega328)��EEPROM����������
 * @author E.Togashi
 * @date 2021/12/19
 */

#ifndef MY_EEPROM_H_
#define MY_EEPROM_H_

// �␳�W��
struct CorrectionFactors {
	uint16_t version; //�o�[�W����
	float dbtA; //�������xa
	float dbtB; //�������xb
	float hmdA; //���Ύ��xa
	float hmdB; //���Ύ��xb
	float glbA; //�O���[�u���xa
	float glbB; //�O���[�u���xb
	float luxA; //�Ɠxa
	float luxB; //�Ɠxb
	float velA; //����a
	float velB; //����b
	float vel0; //������
	uint8_t crc; //CRC
};

// ���������W��
struct VelocityCharacteristicCoefficients{
	uint16_t version; //�o�[�W����
	float ccA;
	float ccB;
	float ccC;
	uint8_t crc; //CRC
};

//�v���ݒ�
struct MeasurementSettings{
	uint16_t version; //�o�[�W����
	bool start_auto; //��������J�n
	bool measure_th; //�������x�̌v���^�U
	bool measure_glb; //�O���[�u���x�̌v���^�U
	bool measure_vel; //�����̌v���^�U
	bool measure_ill; //�Ɠx�̌v���^�U
	bool measure_AD1; //�ėpAD1�̌v���^�U
	bool measure_AD2; //�ėpAD2�̌v���^�U
	bool measure_AD3; //�ėpAD3�̌v���^�U
	bool measure_Prox; //�ߐڃZ���T�̌v���^�U
	bool measure_co2; //CO2�̌v���^�U
	unsigned int interval_th; //�������x�̌v���Ԋu[sec]
	unsigned int interval_glb; //�O���[�u���x�̌v���Ԋu[sec]
	unsigned int interval_vel; //�����̌v���Ԋu[sec]
	unsigned int interval_ill; //�Ɠx�̌v���Ԋu[sec]
	unsigned int interval_AD1; //�ėpAD1�̌v���Ԋu[sec]
	unsigned int interval_AD2; //�ėpAD2�̌v���Ԋu[sec]
	unsigned int interval_AD3; //�ėpAD3�̌v���Ԋu[sec]
	unsigned int interval_Prox; //�ߐڃZ���T�̌v���Ԋu[sec]
	unsigned int interval_co2; //CO2�̌v���Ԋu[sec]
	uint32_t start_dt;	//�v���J�n����
	uint8_t crc; //CRC
};

class my_eeprom
{
	public:
		//�␳�W��
		static CorrectionFactors cFactors;

		//���������W��
		static VelocityCharacteristicCoefficients vcCoefficients;
		
		//�v���ݒ�
		static MeasurementSettings mSettings;
		
		//����
		static char mlName[21];

		//�␳�W������������
		static void SetCorrectionFactor(const char * data);
		
		//�␳�W������������
		static void SetCorrectionFactor();

		//�␳�W����\����������쐬����
		static void MakeCorrectionFactorString(char * txbuff, const char * command);

		//�����̓����W������������
		static void SetVelocityCharacteristics(const char * data);

		//�����̓����W������������
		static void SetVelocityCharacteristics();
		
		//�����̓����W����\����������쐬����
		static void MakeVelocityCharateristicsString(char * txbuff, const char * command);
		
		//EEPROM��ǂݍ���
		static void LoadEEPROM();
		
		//�v���ݒ����������
		static void SetMeasurementSetting();
		
		//���̂���������
		static void SaveName();
		
		//XBee���������ς��ۂ����擾����
		static bool IsXBeeInitialized();
		
		//XBee���������L�^����
		static void XBeeInitialized();
};

#endif /* MY_EEPROM_H_ */