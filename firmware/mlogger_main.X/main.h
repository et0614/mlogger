/* 
 * File:   main.h
 * Author: e.togashi
 *
 * Created on 2025/12/13, 10:13
 */

#ifndef MAIN_H
#define	MAIN_H

#ifdef	__cplusplus
extern "C" {
#endif
    
#include <stdbool.h>
#include <stdint.h>
#include <time.h>
#include "w25q256.h" //内蔵フラッシュ

uint16_t getBatteryVoltage_mV(void);

bool isLowBattery(void);
    
void showError(short int errNum);

void resetButtonHandler(void);

void oneSecHandler(void);

void msecHandler(void);

void executeSecondlyTask(void);

void genDummyData(void);

// 起動以来のスタック使用の最小余裕 [byte] (未使用 RAM の塗りつぶし検査による)
uint16_t MAIN_GetStackFreeMin(void);

// 起動時のリセット要因 (RSTCTRL.RSTFR のビット列。bit0 PORF, 1 BORF, 2 EXTRF, 3 WDRF, 4 SWRF, 5 UPDIRF)
uint8_t MAIN_GetResetFlags(void);

#ifdef	__cplusplus
}
#endif

#endif	/* MAIN_H */

