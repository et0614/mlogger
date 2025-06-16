/*
 * i2c_driver.cpp
 *
 * Created: 2025/06/16 9:33:33
 *  Author: e.togashi
 */ 

#include "i2c_driver.h"

void i2c_driver::Initialize()
{
	// TWI通信のPIN設定 : SDA->PF2, SCL->PF3
	PORTMUX.TWIROUTEA = 0x00;
	
	// 動作モードの設定
	TWI1.CTRLA &= ~TWI_FMPEN_bm; //デフォルト（Standard もしくは Fast）
	
	// SDA hold time（SCLがLowになった後、どれだけSDA信号を維持するか）
	TWI1.CTRLA |= TWI_SDAHOLD_50NS_gc; //50ns
	
	// ボーレートの設定（周波数が決まる）
	const bool IS_STANDARD_MODE = true;
	float fScl = IS_STANDARD_MODE ? 100000 : 400000; //周波数[Hz]
	float tRise = 0.000000001 * (IS_STANDARD_MODE ? 1000 : 250); //Rise time [sec]
	float tOf = 0.000000001 * 250;
	float baud = (uint8_t)(((float)F_CPU / (2 * fScl)) - 5 - ((float)F_CPU * tRise / 2));
	float tLow = (baud + 5) / F_CPU - tOf;
	float tLowM = 0.000000001 * (IS_STANDARD_MODE ? 4700 : 1300);
	if(tLow < tLowM) baud = F_CPU * (tLowM + tOf) - 5;
	TWI1.MBAUD = baud;
	
	// アドレスレジスタ、データレジスタを初期化
	TWI1.MADDR = 0x00;
	TWI1.MDATA = 0x00;

	TWI1.MCTRLA |= TWI_ENABLE_bm		// TWIの有効化
				| TWI_TIMEOUT_200US_gc; //200uSの通信不良でSkip

	TWI1.MSTATUS = TWI_BUSSTATE_IDLE_gc; //バスをIDLE状態にする
	TWI1.MSTATUS |= TWI_WIF_bm | TWI_CLKHOLD_bm; //フラグクリア

	TWI1.MCTRLB |= TWI_FLUSH_bm; //通信状態を初期化
}

bool i2c_driver::Write(uint8_t address, const uint8_t* data, uint8_t length)
{
	if (_start_writing(address) != I2C_ACKED) {
		_bus_stop();
		return false;
	}

	for (uint8_t i = 0; i < length; i++) {
		if (_bus_write(data[i]) != I2C_ACKED) {
			_bus_stop();
			return false;
		}
	}

	_bus_stop();
	return true;
}

bool i2c_driver::Read(uint8_t address, uint8_t* buffer, uint8_t length)
{
	if (length == 0) return true;

	if (_start_reading(address) != I2C_ACKED) {
		_bus_stop();
		return false;
	}

	// 最後のバイト以外はACKを返す
	for (uint8_t i = 0; i < length - 1; i++) {
		if (_bus_read(true, false, &buffer[i]) != I2C_SUCCESS) {
			_bus_stop();
			return false;
		}
	}

	// 最後のバイトはNACKを返し、Stop Conditionを発行する
	if (_bus_read(false, true, &buffer[length - 1]) != I2C_SUCCESS) {
		_bus_stop();
		return false;
	}

	return true;
}

bool i2c_driver::WriteRead(uint8_t address, const uint8_t* writeData, uint8_t writeLength, uint8_t* readBuffer, uint8_t readLength)
{
	// Write Phase
	if (_start_writing(address) != I2C_ACKED) {
		_bus_stop();
		return false;
	}

	for (uint8_t i = 0; i < writeLength; i++) {
		if (_bus_write(writeData[i]) != I2C_ACKED) {
			_bus_stop();
			return false;
		}
	}

	// Read Phase (Repeated Start)
	if (readLength > 0) {
		return Read(address, readBuffer, readLength);
	}
	
	// Readがない場合はここで終了
	_bus_stop();
	return true;
}

bool i2c_driver::WriteByteAndStop(uint8_t address, uint8_t data)
{
	if (_start_writing(address) != I2C_ACKED) {
		_bus_stop();
		return false;
	}

	// ACK/NACKの結果を問わず、次の処理へ
	_bus_write(data);

	_bus_stop();
	return true;
}

//以下はprivateメソッド

//書き込み終了を待つ
uint8_t i2c_driver::_i2c_WaitW(void)
{
	uint8_t state = I2C_INIT;
	do
	{
		//書き込みもしくは読み込み完了フラグを監視
		if(TWI1.MSTATUS & (TWI_WIF_bm | TWI_RIF_bm))
		//if(TWI1.MSTATUS & TWI_WIF_bm)
		{
			//ACKを受け取った場合
			if(!(TWI1.MSTATUS & TWI_RXACK_bm)) state = I2C_ACKED;
			//ACKを受け取らなかった場合
			else state = I2C_NACKED;
		}
		//エラー発生フラグを監視
		else if(TWI1.MSTATUS & (TWI_BUSERR_bm | TWI_ARBLOST_bm)) state = I2C_ERROR;
	} while(!state);
	
	return state;
}

//読み込み終了を待つ
uint8_t i2c_driver::_i2c_WaitR(void)
{
	uint8_t state = I2C_INIT;
	do
	{
		//書き込みもしくは読み込み完了フラグを監視
		if(TWI1.MSTATUS & (TWI_WIF_bm | TWI_RIF_bm)) state = I2C_READY;
		//if(TWI1.MSTATUS & TWI_RIF_bm) state = I2C_READY;
		//エラー発生フラグを監視
		else if(TWI1.MSTATUS & (TWI_BUSERR_bm | TWI_ARBLOST_bm)) state = I2C_ERROR;
	} while(!state);
	
	return state;
}

//I2C通信（書き込み）開始
uint8_t i2c_driver::_start_writing(uint8_t address_7bit)
{
	TWI1.MADDR = (address_7bit << 1) & ~0x01; //Write動作の場合、1桁目は0
	
	//while(TWI_RXACK_bm & TWI1.MSTATUS);
	//return 1;
	return _i2c_WaitW();
}

//I2C通信（読み込み）開始
uint8_t i2c_driver::_start_reading(uint8_t address_7bit)
{
	TWI1.MADDR = (address_7bit << 1) | 0x01; //Read動作の場合、1桁目は1
	
	//while(TWI_RXACK_bm & TWI1.MSTATUS);
	//return 1;
	return _i2c_WaitW();
}

//I2C通信の終了
void i2c_driver::_bus_stop(void)
{
	TWI1.MCTRLB = TWI_ACKACT_bm | TWI_MCMD_STOP_gc; //NACK
}

//Write処理（マスタからスレーブへの送信）
uint8_t i2c_driver::_bus_write(uint8_t data)
{
	TWI1.MDATA = data;
	return _i2c_WaitW();
}

//Read処理（スレーブからマスタへの送信）
uint8_t i2c_driver::_bus_read(bool sendAck, bool withStopCondition, uint8_t* data)
{
	uint8_t rslt = _i2c_WaitR();
	if(rslt == I2C_READY)
	{
		*data = TWI1.MDATA;
		
		if(sendAck) TWI1.MCTRLB &= ~TWI_ACKACT_bm; //ACK
		else TWI1.MCTRLB |= TWI_ACKACT_bm; //NACK
		
		if(withStopCondition) TWI1.MCTRLB |= TWI_MCMD_STOP_gc;
		else TWI1.MCTRLB |= TWI_MCMD_RECVTRANS_gc;

		return I2C_SUCCESS;
	}
	//エラー処理
	else return rslt;
}



