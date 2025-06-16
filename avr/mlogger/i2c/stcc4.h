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
		
	private:
		/**
		 * @brief 16bit�̃R�}���h���Z���T�[�ɑ��M����
		 * @param command ���M����16bit�R�}���h
		 * @return ���������ꍇ��true
		 */
		static bool sendCommand(uint16_t command);
};


#endif /* STCC4_H_ */