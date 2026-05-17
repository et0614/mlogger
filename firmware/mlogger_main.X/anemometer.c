#include "anemometer.h"
#include "i2c_master.h"
#include "crc.h"

#define ANEMO_ADDRESS 0x10
#define REG_VOLTAGE 0x00
#define REG_VELOCITY 0x03
#define REG_ENABLE 0xF6
#define REG_FILTER 0x09
#define REG_UPDATED 0x0A

// <editor-fold defaultstate="collapsed" desc="公開関数の実装">

void Anemometer_Init(Anemometer_t* anemo) {
    // 構造体をクリア
    anemo->adc_value = 0;
    anemo->wind_speed_mps = 0.0f;
    
    // 平滑化フィルタを設定
    uint8_t writeBuffer[2];
    writeBuffer[0] = REG_FILTER; // レジスタアドレス
    writeBuffer[1] = 6;   // 書き込む値
	I2C_Write(ANEMO_ADDRESS, writeBuffer, 2);
}

void Anemometer_Update(Anemometer_t* anemo) {
    // I2C通信バッファ
	uint8_t buffer[3];
    
    // 電圧[mV]を読み取る
    const uint8_t volCmd = REG_VOLTAGE;
    I2C_WriteRead(ANEMO_ADDRESS, &volCmd, 1, buffer, 3);
	if (CRC_calc8(buffer, 2) != buffer[2]) return; // CRCエラー
    anemo->adc_value = (int16_t)((buffer[0] << 8) | buffer[1]);
    
    // 風速[mm/s]を読み取る
    const uint8_t velCmd = REG_VELOCITY;
    I2C_WriteRead(ANEMO_ADDRESS, &velCmd, 1, buffer, 3);
	if (CRC_calc8(buffer, 2) != buffer[2]) return; // CRCエラー
    anemo->wind_speed_mps = 0.001 * (float)((int16_t)((buffer[0] << 8) | buffer[1]));
}

// 起動する
void Anemometer_Wakeup(){
    uint8_t writeBuffer[2];
    writeBuffer[0] = REG_ENABLE; // レジスタアドレス
    writeBuffer[1] = 1;   // 書き込む値
	I2C_Write(ANEMO_ADDRESS, writeBuffer, 2);
}

// 休止する
void Anemometer_Sleep(){
    uint8_t writeBuffer[2];
    writeBuffer[0] = REG_ENABLE; // レジスタアドレス
    writeBuffer[1] = 0;   // 書き込む値
	I2C_Write(ANEMO_ADDRESS, writeBuffer, 2);
}

// </editor-fold>