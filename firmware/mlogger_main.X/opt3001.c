#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/timer/delay.h"
#include "opt3001.h"
#include "i2c_master.h"
#include <math.h>

//OPT3001のアドレス
#define OPT_ADD_GND 0x44 //GND短絡:1000100
#define OPT_ADD_VDD 0x45 //VDD短絡:1000101
#define OPT_ADD_SDA 0x46 //SDA短絡:1000110
#define OPT_ADD_SCL 0x47 //SCL短絡:1000111
#define OPT_ADD OPT_ADD_SCL

#define REG_ALS 0x00
#define REG_CONFIG 0x01

bool OPT3001_Initialize(void){
    
    uint8_t writeBuffer[3];
    writeBuffer[0] = REG_CONFIG; // レジスタアドレス
    writeBuffer[1] = 0xCE;   // 設定1: 0b 1100 1 11 0 //automatic full-scale, 800ms, continuous conversions, read only field
    writeBuffer[2] = 0x00;   // 設定2: 0b 0 0 0 0 0 00//read only field * 3, hysteresis-style, 
	if(!I2C_Write(OPT_ADD, writeBuffer, 3)) return false;
    
    DELAY_milliseconds(1); //必要な待機時間は技術資料から読み取れず
    
    return true;
}

bool OPT3001_ReadALS(float *als){
    // I2C通信バッファ
	uint8_t buffer[2];
    
    const uint8_t cmd = REG_ALS;
    if(!I2C_WriteRead(OPT_ADD, &cmd, 1, buffer, 2)) return false;
    int expnt = (0b11110000 & buffer[0]) >> 4; //上位4bitがレンジを表す
	int val = ((0b00001111 & buffer[0]) << 8) + buffer[1]; //下位12bitは値を表す
	*als = 0.01 * (float)(1 << expnt) * (float)val;
    
    return true;
}