/*
 * Stcc4.h
 *
 * Created: 2025/06/16 17:05:49
 *  Author: e.togashi
 */ 


#ifndef STCC4_H_
#define STCC4_H_

#include "I2cDriver.h"

class Stcc4{
	public:
		/**
		 * @brief �Z���T�[���o�X��ɑ��݂��邩���m�F����
		 * @return �Z���T�[�����������true
		 */
		static bool isConnected();
	
		/**
		 * @fn
		 * ����������
		 * @return ������true�A���s��false
		 */
		static bool initialize();
		
		/**
		 * @fn
		 * 3���Ԉȏ�̕s�g�p���Ȃǂ̏����������i22�b������j
		 * @return ������true�A���s��false
		 */
		static bool performConditioning();
		
		/**
		 * @fn
		 * �����Z������
		 * @param correction �␳�����Z�x[ppm]
		 * @return ������true�A���s��false
		 */
		static bool performForcedRecalibration(uint16_t co2Level, int16_t* correction);
		
		/**
		 * @fn
		 * �X���[�v������
		 * @return ������true�A���s��false
		 */
		static bool enterSleep();
		
		/**
		 * @fn
		 * �X���[�v��������
		 * @return ������true�A���s��false
		 */
		static bool exitSleep();
		
		/**
		 * @fn
		 * 1�񑪒肷��
		 * @return ������true�A���s��false
		 */
		static bool measureSingleShot();
		
		/**
		 * @fn
		 * �v�����ʂ�ǂ�
		 * @return ������true�A���s��false
		 */
		static bool readMeasurement(uint16_t * co2, float * temperature, float * humidity);
		
		/**
		 * @fn
		 * �����p�����x��ݒ肷��
		 * @return ������true�A���s��false
		 */
		static bool setRHTCompensation(float temperature, float humidity);
		
	private:
		/**
		 * @brief 16bit�̃R�}���h���Z���T�[�ɑ��M����
		 * @param command ���M����16bit�R�}���h
		 * @return ���������ꍇ��true
		 */
		static bool _sendCommand(uint16_t command);
		
		/**
		 * @brief 16bit�R�}���h�ƕ����̈����f�[�^�𑗐M����
		 * @brief �e����(16bit)�̌�ɂ͎�����CRC8���t�^�����
		 * @param command ���M����16bit�R�}���h
		 * @param args ���M����16bit�����̔z��
		 * @param numArgs �����̐�
		 * @return ���������ꍇ��true
		 */
		static bool _sendCommandWithArguments(uint16_t command, const uint16_t args[], uint8_t numArgs);
};


#endif /* STCC4_H_ */