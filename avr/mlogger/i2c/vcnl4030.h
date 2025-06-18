/*
 * Vcnl4030.h
 *
 * Created: 2025/06/16 12:31:34
 *  Author: e.togashi
 */ 


#ifndef VCNL4030_H_
#define VCNL4030_H_

#include "I2cDriver.h"

class Vcnl4030{
	public:
		/**
		 * @brief �Z���T�[���o�X��ɑ��݂��邩���m�F����
		 * @return �Z���T�[�����������true
		 */
		static bool isConnected();
		
		/**
		 * @fn
		 * VCNL4030������������
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool initialize();
		
		/**
		 * @fn
		 * VCNL4030����Ɠx(Ambient Light Sensor)��ǂݎ��
		 * @param (als) �Ɠx[lx]
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool readALS(float * als);
		
		/**
		 * @fn
		 * VCNL4030���狗��[mm]��ǂݎ��
		 * @param (ps) ����[mm]
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool readPS(float * ps);		
};


#endif /* VCNL4030_H_ */