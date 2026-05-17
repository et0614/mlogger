/* 
 * File:   i2c_shared_data.h
 * Author: E. Togashi
 *
 * Created on January 14, 2026, 7:57 AM
 */

#ifndef I2C_SHARED_DATA_H
#define	I2C_SHARED_DATA_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stddef.h>
    
// 構造体のパッキング（コンパイラの隙間埋めを禁止してアドレスを詰める）
#pragma pack(push, 1) 

typedef struct {
    // [Read Only Area] -------------------------
    // 電圧[mV] (0-2000)
    uint8_t voltage_high;
    uint8_t voltage_low;
    uint8_t voltage_crc;
    
    // 風速[mm/s] (0-10000)
    uint8_t velocity_high;
    uint8_t velocity_low;
    uint8_t velocity_crc;
    
    // マイコン温度[10C]
    uint8_t mcu_temp_high;
    uint8_t mcu_temp_low;
    uint8_t mcu_temp_crc;
    
    //予熱中か否か
    uint8_t status;
    
    //バージョン
    uint8_t version;
    
    //製造ID
    uint8_t id[16];
    uint8_t id_crc;
    uint8_t id_hash[4];
    uint8_t id_hash_crc;
    
    // [Read/Write Area] ------------------------
    uint8_t enable;
    uint8_t filter_n;
    uint8_t updated;
    
    //電圧→風速換算用係数
    float coefficientA[5];
    uint8_t crc_coefA;
    float coefficientB[5];
    uint8_t crc_coefB;
    
    //I2Cアドレス変更用のロック
    uint8_t i2c_addr_unlock; 
    // I2Cアドレス
    uint8_t i2c_address;

} SensorData_t;

// 共用体の定義
typedef union {
    SensorData_t reg;       // 構造体としてアクセス (プログラム内で便利)
    uint8_t      bytes[sizeof(SensorData_t)]; // 配列としてアクセス
} I2C_Map_t;

#pragma pack(pop)

// 実体の宣言（i2c_slave.c で定義）
extern volatile I2C_Map_t SharedMemory;

// --- ステータスレジスタのビット定義 ---
#define STATUS_HEATING_BIT    (0) // Bit 0: 予熱中フラグ
#define STATUS_HEATING_MASK   (1 << STATUS_HEATING_BIT)

#ifdef	__cplusplus
}
#endif

#endif	/* I2C_SHARED_DATA_H */

