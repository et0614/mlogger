/*
 * i2c_driver.h
 *
 * Created: 2025/06/16 9:33:19
 *  Author: e.togashi
 */ 

#ifndef I2C_DRIVER_H_
#define I2C_DRIVER_H_

#include <avr/io.h>

class i2c_driver
{
	public:
	
		/**
		 * @brief I2C�ʐM�̏�Ԃ������񋓌^
		 */
		enum I2C_Status
		{
			I2C_INIT = 0,
			I2C_ACKED,
			I2C_NACKED,
			I2C_READY,
			I2C_ERROR,
			I2C_SUCCESS
		};
		
		/**
		 * @brief �Z���T�[���o�X��ɑ��݂��邩���m�F����
		 * @return �Z���T�[�����������true
		 */
		static bool IsConnected(uint8_t address);
		
		/**
		 * @brief I2C�o�X�����������܂�
		 */
		static void Initialize();

		/**
		 * @brief �w��A�h���X�Ƀf�[�^���������݂܂�
		 * @return ���������ꍇ��true
		 */
		static bool Write(uint8_t address, const uint8_t* data, uint8_t length);

		/**
		 * @brief �w��A�h���X����f�[�^��ǂݍ��݂܂�
		 * @return ���������ꍇ��true
		 */
		static bool Read(uint8_t address, uint8_t* buffer, uint8_t length);

		/**
		 * @brief �f�[�^���������񂾌�A�����ăf�[�^��ǂݍ��݂܂��iRepeated Start���g�p�j
		 * @return ���������ꍇ��true
		 */
		static bool WriteRead(uint8_t address, const uint8_t* writeData, uint8_t writeLength, uint8_t* readBuffer, uint8_t readLength);

		/**
		 * @brief 1�o�C�g�𑗐M���AACK/NACK���C�ɂ����I������ (����ȕ��A�V�[�P���X�p)
		 */
		static bool WriteByteAndStop(uint8_t address, uint8_t data);

	private:
		
		//�������ݏI����҂�
		static uint8_t _i2c_WaitW(void);

		//�ǂݍ��ݏI����҂�
		static uint8_t _i2c_WaitR(void);

		//I2C�ʐM�i�������݁j�J�n
		static uint8_t _start_writing(uint8_t address);

		//I2C�ʐM�i�ǂݍ��݁j�J�n
		static uint8_t _start_reading(uint8_t address);

		//I2C�ʐM�̏I��
		static void _bus_stop(void);

		//Write�����i�}�X�^����X���[�u�ւ̑��M�j
		static uint8_t _bus_write(uint8_t data);

		//Read�����i�X���[�u����}�X�^�ւ̑��M�j
		static uint8_t _bus_read(bool sendAck, bool withStopCondition, uint8_t* data);	
};



#endif /* I2C_DRIVER_H_ */