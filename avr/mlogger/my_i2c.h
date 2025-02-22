/**
 * @file my_i2c.h
 * @brief AVRxx�V���[�Y��I2C�ʐM����
 *  �Q�l�Fhttp://cjtsx.blogspot.jp/2016/07/am2320-library-for-avrs-without.html
 * @author E.Togashi
 * @date 2020/12/25
 */
 
#ifndef MY_I2C_H_
#define MY_I2C_H_
 
#include <avr/io.h>

class my_i2c
{	
	public:
		static void InitializeI2C(void);

		/**
		 * @fn
		 * AHT20������������
		 */
		static void InitializeAHT20();
				
		/**
		 * @fn
		 * AHT20���犣�����x�Ƒ��Ύ��x��ǂݎ��
		 * @param (tempValue) �������x
		 * @param (humiValue) ���Ύ��x
		 * @return �ǎ搬����1�A���s��0
		 */
		static uint8_t ReadAHT20(float * tempValue, float * humiValue);
		
		/**
		 * @fn
		 * AHT20�����Z�b�g����
		 * @param (code) ���Z�b�g����R�[�h�i0x1B or 0x1C or 0x1E�j
		 */
		static void ResetAHT20(uint8_t code);
		
		/**
		 * @fn
		 * AHT20�̏�Ԃ�ǂݎ��
		 * @return ��Ԃ�\���o�C�g
		 */
		static uint8_t ReadAHT20Status();
		
		/**
		 * @fn
		 * VCNL4030������������
		 */
		static void InitializeVCNL4030();
		
		/**
		 * @fn
		 * VCNL4030����Ɠx��ǂݎ��
		 * @return �Ɠx[lx]
		 */
		static float ReadVCNL4030_ALS(void);
		
		/**
		 * @fn
		 * VCNL4030���狗����ǂݎ��
		 * @return ����[mm]
		 */
		static float ReadVCNL4030_PS(void);
		
		static void ScanAddress(uint8_t minAddress, uint8_t maxAddress);
		
		/**
		 * @fn
		 * P3T1750DP������������
		 */
		static void InitializeP3T1750DP();

		/**
		 * @fn
		 * P3T1750DP���牷�x��ǂݎ��
		 * @param (tempValue) �������x
		 * @return �ǎ搬����1�A���s��0
		 */
		static uint8_t ReadP3T1750DP(float * tempValue);
		
		/**
		 * @fn
		 * STC31C�����邩�ۂ�
		 * @return �����1�A�Ȃ����0
		 */
		static uint8_t HasSTC31C();
		
		/**
		 * @fn
		 * STC31C������������
		 */
		static void InitializeSTC31C();
		
		/**
		 * @fn
		 * STC31C����CO2�Z�x[ppm]��ǂݎ��
		 * @param (temperature) �␳�p�̊������x[C]
		 * @param (relatvieHumid) �␳�p�̑��Ύ��x[%]
		 * @param (co2Level) CO2�Z�x[ppm]
		 * @return �ǎ搬����1�A���s��0
		 */
		static uint8_t ReadSTC31C(float temperature, float relatvieHumid, uint16_t *co2Level);
		
		/**
		 * @fn
		 * SHT4X-AD������������
		 * @param (isAD) SHT4X-AD���ۂ��i�ۂ̏ꍇ�ɂ�SHT4X-BD�j
		 */
		static void InitializeSHT4X(bool isAD);
				
		/**
		 * @fn
		 * SHT4X-AD���犣�����x�Ƒ��Ύ��x��ǂݎ��
		 * @param (tempValue) �������x
		 * @param (humiValue) ���Ύ��x
		 * @param (isAD) SHT4X-AD���ۂ��i�ۂ̏ꍇ�ɂ�SHT4X-BD�j
		 * @return �ǎ搬����1�A���s��0
		 */
		static uint8_t ReadSHT4X(float * tempValue, float * humiValue, bool isAD);
};
  
#endif /* MY_I2C_H_ */