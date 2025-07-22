/*
 * Sht4x.h
 *
 * Created: 2025/06/16 9:53:19
 *  Author: e.togashi
 */ 


#ifndef SHT4X_H_
#define SHT4X_H_

#include <stdint.h>

class Sht4x{
	public:
	
		/**
		 * @brief SHT4X�̎��
		 */
		enum SHT4XType: uint8_t
		{
			SHT4_AD = 0x44,
			SHT4_BD = 0x45,
			SHT4_CD = 0x46
		};
		
		/**
		 * @brief �Z���T�[���o�X��ɑ��݂��邩���m�F����
		 * @param (sht4xType) SHT4X�̎��
		 * @return �Z���T�[�����������true
		 */
		static bool isConnected(SHT4XType sht4xType);
		
		/**
		 * @fn
		 * SHT4X-AD������������
		 * @param (sht4xType) SHT4X�̎��
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool initialize(SHT4XType sht4xType);
				
		/**
		 * @fn
		 * SHT4X-AD���犣�����x�Ƒ��Ύ��x��ǂݎ��
		 * @param (tempValue) �������x
		 * @param (humiValue) ���Ύ��x
		 * @param (sht4xType) SHT4X�̎��
		 * @return �ǎ搬����true�A���s��false
		 */
		static bool readValue(float * tempValue, float * humiValue, SHT4XType sht4xType);
		
		/**
		* @fn
		* SHT4X����V���A���ԍ���ǂݎ��
		* @param (serialNumber) 32�r�b�g�̃V���A���ԍ����i�[����|�C���^
		* @param (sht4xType) SHT4X�̎��
		* @return �ǎ搬����true�A���s��false
		*/
		static bool readSerial(uint32_t* serialNumber, SHT4XType sht4xType);
	};


#endif /* SHT4X_H_ */