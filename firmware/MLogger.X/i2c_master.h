/* 
 * File:   i2c_master.h
 * Author: e.togashi
 *
 * Created on 2025/12/14, 13:52
 */

#ifndef I2C_MASTER_H
#define	I2C_MASTER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

// 接続確認 (Address NACK チェック)
bool I2C_IsConnected(uint8_t address);

// 書き込み (完了待ち機能付き)
bool I2C_Write(uint8_t address, const uint8_t *data, size_t len);

// 読み込み (完了待ち機能付き)
bool I2C_Read(uint8_t address, uint8_t *buffer, size_t len);

// 書き込み後に読み込み (Repeated Start 対応)
bool I2C_WriteRead(uint8_t address, const uint8_t *writeData, size_t writeLen, uint8_t *readBuffer, size_t readLen);

// 1バイト書いてStop (特殊用途)
bool I2C_WriteByteAndStop(uint8_t address, uint8_t data);

#ifdef	__cplusplus
}
#endif

#endif	/* I2C_MASTER_H */

