/*
 * File:   eeprom_manager.h
 * Author: e.togashi
 *
 * EEPROM への永続化機能。
 * I2C アドレス、4 計測値 (T, RH, CO2, T_glb) ぶんの線形補正係数 (a/b 各 2 値)、
 * 装置ラベルを保存する。
 */

#ifndef EEPROM_MANAGER_H
#define	EEPROM_MANAGER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

// EEPROM を読み込み、SharedMemory に反映する。
// 未初期化 EEPROM (init_flag 不一致) の場合は EM_resetEEPROM を内部で呼んでから読み戻す。
void EM_loadEEPROM(void);

// SharedMemory の内容を EEPROM に書き戻す (差分のみ)
void EM_updateEEPROM(void);

// EEPROM をデフォルト値で初期化する (I2C address = 0x10, 全係数 a=1.0/b=0.0, name=空)
void EM_resetEEPROM(void);

#ifdef	__cplusplus
}
#endif

#endif	/* EEPROM_MANAGER_H */
