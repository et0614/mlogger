/* 
 * File:   sht4x.h
 * Author: e.togashi
 *
 * Created on 2025/12/14, 13:54
 */

#ifndef SHT4X_H
#define	SHT4X_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

// SHT4Xの種類（アドレス）
typedef enum {
    SHT4_AD = 0x44,
    SHT4_BD = 0x45,
    SHT4_CD = 0x46
} SHT4XType;

bool SHT4x_IsConnected(SHT4XType type);
bool SHT4x_Initialize(SHT4XType type);
bool SHT4x_ReadValue(float *tempValue, float *humiValue, SHT4XType type);
bool SHT4x_ReadSerial(uint32_t *serialNumber, SHT4XType type);
    
#ifdef	__cplusplus
}
#endif

#endif	/* SHT4X_H */

