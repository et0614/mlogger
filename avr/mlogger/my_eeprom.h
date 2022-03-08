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
		
		//�v���^�U  th:�����x, glb:�O���[�u���x, vel:������, ill:�Ɠx
		volatile static bool measure_th, measure_glb, measure_vel, measure_ill;
		
		//�v���Ԋu  th:�����x, glb:�O���[�u���x, vel:������, ill:�Ɠx
		volatile static unsigned int interval_th, interval_glb, interval_vel, interval_ill;
		
		//�␳�W����ǂݍ���
		static void LoadCorrectionFactor();
			
		//�␳�W������������
		static void SetCorrectionFactor(const char * data);

		//�␳�W����\����������쐬����
		static void MakeCorrectionFactorString(char * txbuff, const char * command);
		
		//�v���ݒ��ǂݍ���
		static void LoadMeasurementSetting();
		
		//�v���ݒ����������
		static void SetMeasurementSetting();
		
};

#endif /* MY_EEPROM_H_ */