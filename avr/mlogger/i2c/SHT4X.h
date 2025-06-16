/*
 * sht4x.h
 *
 * Created: 2025/06/16 9:53:19
 *  Author: e.togashi
 */ 


#ifndef SHT4X_H_
#define SHT4X_H_

#include <stdint.h>
#include "sht4x.h"

class sht4x{
	public:
		/**
		 * @fn
		 * SHT4X-AD������������
		 * @param (isAD) SHT4X-AD���ۂ��i�ۂ̏ꍇ�ɂ�SHT4X-BD�j
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool Initialize(bool isAD);
				
		/**
		 * @fn
		 * SHT4X-AD���犣�����x�Ƒ��Ύ��x��ǂݎ��
		 * @param (tempValue) �������x
		 * @param (humiValue) ���Ύ��x
		 * @param (isAD) SHT4X-AD���ۂ��i�ۂ̏ꍇ�ɂ�SHT4X-BD�j
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool ReadValue(float * tempValue, float * humiValue, bool isAD);
		
		/**
		* @fn
		* SHT4X����V���A���ԍ���ǂݎ��
		* @param (serialNumber) 32�r�b�g�̃V���A���ԍ����i�[����|�C���^
		* @param (isAD) SHT4X-AD���ۂ��i�ۂ̏ꍇ�ɂ�SHT4X-BD�j
		* @return �ǎ搬����true�A���s��false
		*/
		static bool ReadSerial(uint32_t* serialNumber, bool isAD);
	
	};


#endif /* SHT4X_H_ */