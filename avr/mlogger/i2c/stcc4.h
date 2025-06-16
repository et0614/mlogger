/*
 * stcc4.h
 *
 * Created: 2025/06/16 17:05:49
 *  Author: etoga
 */ 


#ifndef STCC4_H_
#define STCC4_H_

#include "i2c_driver.h"

class stcc4{
	public:
		/**
		 * @brief �Z���T�[���o�X��ɑ��݂��邩���m�F����
		 * @return �Z���T�[�����������true
		 */
		static bool IsConnected();
	
		/**
		 * @fn
		 * ����������
		 * @return ������true�A���s��false
		 */
		static bool Initialize();
		
		/**
		 * @fn
		 * �X���[�v������
		 * @return ������true�A���s��false
		 */
		static bool EnterSleep();
		
		/**
		 * @fn
		 * �X���[�v��������
		 * @return ������true�A���s��false
		 */
		static bool ExitSleep();
		
		/**
		 * @fn
		 * 1�񑪒肷��
		 * @return ������true�A���s��false
		 */
		static bool MeasureSingleShot();
		
		/**
		 * @fn
		 * �v�����ʂ�ǂ�
		 * @return ������true�A���s��false
		 */
		static bool ReadMeasurement(uint16_t * co2, float * temperature, float * humidity);
		
		/**
		 * @fn
		 * �����p�����x��ݒ肷��
		 * @return ������true�A���s��false
		 */
		static bool SetRHTCompensation(float temperature, float humidity);
		
	private:
		/**
		 * @brief 16bit�̃R�}���h���Z���T�[�ɑ��M����
		 * @param command ���M����16bit�R�}���h
		 * @return ���������ꍇ��true
		 */
		static bool sendCommand(uint16_t command);
		
		/**
		 * @brief 16bit�R�}���h�ƕ����̈����f�[�^�𑗐M����
		 * @brief �e����(16bit)�̌�ɂ͎�����CRC8���t�^�����
		 * @param command ���M����16bit�R�}���h
		 * @param args ���M����16bit�����̔z��
		 * @param numArgs �����̐�
		 * @return ���������ꍇ��true
		 */
		static bool sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs);
};


#endif /* STCC4_H_ */