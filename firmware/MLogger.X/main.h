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
#include <time.h>
#include "w25q512.h" //内蔵フラッシュ
    
bool isLowBattery(void);
    
void showError(short int errNum);

void resetButtonHandler(void);

void oneSecHandler(void);

void msecHandler(void);

void executeSecondlyTask(void);

void genDummyData(void);

#ifdef	__cplusplus
}
#endif

#endif	/* MAIN_H */

