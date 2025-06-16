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
		 * @brief I2C通信の状態を示す列挙型
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
		 * @brief センサーがバス上に存在するかを確認する
		 * @return センサーが応答すればtrue
		 */
		static bool IsConnected(uint8_t address);
		
		/**
		 * @brief I2Cバスを初期化します
		 */
		static void Initialize();

		/**
		 * @brief 指定アドレスにデータを書き込みます
		 * @return 成功した場合にtrue
		 */
		static bool Write(uint8_t address, const uint8_t* data, uint8_t length);

		/**
		 * @brief 指定アドレスからデータを読み込みます
		 * @return 成功した場合にtrue
		 */
		static bool Read(uint8_t address, uint8_t* buffer, uint8_t length);

		/**
		 * @brief データを書き込んだ後、続けてデータを読み込みます（Repeated Startを使用）
		 * @return 成功した場合にtrue
		 */
		static bool WriteRead(uint8_t address, const uint8_t* writeData, uint8_t writeLength, uint8_t* readBuffer, uint8_t readLength);

		/**
		 * @brief 1バイトを送信し、ACK/NACKを気にせず終了する (特殊な復帰シーケンス用)
		 */
		static bool WriteByteAndStop(uint8_t address, uint8_t data);

	private:
		
		//書き込み終了を待つ
		static uint8_t _i2c_WaitW(void);

		//読み込み終了を待つ
		static uint8_t _i2c_WaitR(void);

		//I2C通信（書き込み）開始
		static uint8_t _start_writing(uint8_t address);

		//I2C通信（読み込み）開始
		static uint8_t _start_reading(uint8_t address);

		//I2C通信の終了
		static void _bus_stop(void);

		//Write処理（マスタからスレーブへの送信）
		static uint8_t _bus_write(uint8_t data);

		//Read処理（スレーブからマスタへの送信）
		static uint8_t _bus_read(bool sendAck, bool withStopCondition, uint8_t* data);	
};



#endif /* I2C_DRIVER_H_ */