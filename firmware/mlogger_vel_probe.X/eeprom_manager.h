/* 
 * File:   eeprom_manager.h
 * Author: e.togashi
 *
 * Created on 2026/01/18, 16:23
 */

#ifndef EEPROM_MANAGER_H
#define	EEPROM_MANAGER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

//EEPROMを読み込む
void EM_loadEEPROM();

//設定を保存する
void EM_updateEEPROM();

void EM_resetEEPROM();


#ifdef	__cplusplus
}
#endif

#endif	/* EEPROM_MANAGER_H */

