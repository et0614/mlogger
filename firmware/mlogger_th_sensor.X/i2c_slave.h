/*
 * File:   i2c_slave.h
 * Author: e.togashi
 *
 * 子機側 I2C スレーブ (TWI0 client) 機能。
 * PoEM-pod 親機からの共通レジスタアクセスを受け付ける。
 */

#ifndef I2C_SLAVE_H
#define	I2C_SLAVE_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

extern volatile bool     I2C_Is_Busy;
extern volatile uint16_t I2C_KeepAlive_Ticks;

// 拡張領域 (補正係数) 書き換えリクエストフラグ
extern volatile bool     I2C_Config_Update_Requested;

void I2C_Slave_Init(void);

#ifdef	__cplusplus
}
#endif

#endif	/* I2C_SLAVE_H */
