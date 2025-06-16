/*
 * vcnl4030.h
 *
 * Created: 2025/06/16 12:31:34
 *  Author: etoga
 */ 


#ifndef VCNL4030_H_
#define VCNL4030_H_

#include "i2c_driver.h"

class vcnl4030{
	public:
		/**
		 * @brief �Z���T�[���o�X��ɑ��݂��邩���m�F����
		 * @return �Z���T�[�����������true
		 */
		static bool IsConnected();
		
		/**
		 * @fn
		 * VCNL4030������������
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool Initialize();
		
		/**
		 * @fn
		 * VCNL4030����Ɠx(Ambient Light Sensor)��ǂݎ��
		 * @param (als) �Ɠx[lx]
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool ReadALS(float * als);
		
		/**
		 * @fn
		 * VCNL4030���狗��[mm]��ǂݎ��
		 * @param (ps) ����[mm]
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool ReadPS(float * ps);		
};


#endif /* VCNL4030_H_ */