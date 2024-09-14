/**
 * @file my_eeprom.h
 * @brief AVR(ATMega328)��EEPROM����������
 * @author E.Togashi
 * @date 2021/12/19
 */

#ifndef MY_EEPROM_H_
#define MY_EEPROM_H_

class my_eeprom
{
	public:
		//�����ʐM�J�n�ݒ�
		volatile static bool startAuto;
	
		//�␳�W��
		volatile static float Cf_dbtA, Cf_dbtB, Cf_hmdA, Cf_hmdB, Cf_glbA, Cf_glbB, Cf_luxA, Cf_luxB, Cf_velA, Cf_velB, Cf_vel0;
		
		//�����v�����W��
		volatile static float VelCC_A, VelCC_B, VelCC_C;
		
		//�v���^�U  th:�����x, glb:�O���[�u���x, vel:������, ill:�Ɠx
		volatile static bool measure_th, measure_glb, measure_vel, measure_ill, measure_AD1, measure_AD2, measure_AD3, measure_Prox, measure_co2;
		
		//�v���Ԋu  th:�����x, glb:�O���[�u���x, vel:������, ill:�Ɠx
		volatile static unsigned int interval_th, interval_glb, interval_vel, interval_ill, interval_AD1, interval_AD2, interval_AD3, interval_co2;
		
		//�v���J�n����
		volatile static uint32_t start_dt;
		
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