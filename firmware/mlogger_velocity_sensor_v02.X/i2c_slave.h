/* 
 * File:   i2c_slave.h
 * Author: e.togashi
 *
 * Created on 2026/01/14, 15:19
 */

#ifndef I2C_SLAVE_H
#define	I2C_SLAVE_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

extern volatile bool I2C_Is_Busy;       // 現在通信中か
extern volatile uint16_t I2C_KeepAlive_Ticks; // 通信終了後の起きてる時間

extern volatile bool I2C_Config_Update_Requested; // 設定変更リクエストフラグ
extern volatile bool I2C_Coefficient_Update_Requested; // 係数変更リクエストフラグ

void I2C_Slave_Init(void);


#ifdef	__cplusplus
}
#endif

#endif	/* I2C_SLAVE_H */

