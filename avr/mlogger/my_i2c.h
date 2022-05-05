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
		
		static void InitializeOPT(uint8_t add);
		
		/**
		 * @fn
		 * AM2320���犣�����x�Ƒ��Ύ��x��ǂݎ��
		 * @param (tempValue) �������x
		 * @param (humiValue) ���Ύ��x
		 * @return �ǎ搬����1�A���s��0
		 */
		static uint8_t ReadAM2320(float * tempValue, float * humiValue);
		
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
		 * OPTxxx����Ɠx��ǂݎ��
		 * @param (add) �A�h���X
		 * @return �Ɠx[lx]
		 */
		static float ReadOPT(uint8_t add);
		
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
};
  
#endif /* MY_I2C_H_ */