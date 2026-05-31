/* 
 * File:   crc.h
 * Author: e.togashi
 *
 * Created on December 13, 2025, 10:58 AM
 */

#ifndef CRC_H
#define	CRC_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
    
/**
 * @brief  SHT4xやAHT20などのセンサーで利用されるCRC-8チェックサムを計算する。
 * @details
 * 多項式: 0x31 (x^8 + x^5 + x^4 + 1)
 * 初期値: 0xFF
 * @param[in] ptr  チェックサムを計算するデータ配列へのポインタ
 * @param[in] len  データ長（バイト）
 * @return    uint8_t 計算されたCRC-8チェックサム値
 */
static inline uint8_t CRC_calc8(uint8_t *ptr, uint8_t len)
{
	uint8_t crc = 0xFF;
	for(int i = 0; i < len; i++) {
		crc ^= *ptr++;
		for(uint8_t bit = 8; bit > 0; --bit) {
			if(crc & 0x80) {
				crc = (crc << 1) ^ 0x31u;
			} else {
				crc = (crc << 1);
			}
		}
	}
	return crc;
}

/**
* @brief 標準的なCRC-16チェックサムを計算する。
* @details
* 多項式: 0x1021 (x^16 + x^12 + x^5 + 1)
* 初期値: 0xFFFF
* @param[in] ptr チェックサムを計算するデータ配列へのポインタ
* @param[in] len データ長（バイト）
* @return uint16_t 16ビットのCRCチェックサム値
*/
static inline uint16_t CRC_calc16(uint8_t *ptr, uint8_t len)
{
	uint16_t crc = 0xFFFF;
	const uint16_t poly = 0x1021;

	for (uint8_t i = 0; i < len; i++) {
		crc ^= (uint16_t)(*ptr++) << 8;
		for (uint8_t j = 0; j < 8; j++) {
			if (crc & 0x8000) {
				crc = (crc << 1) ^ poly;
			} else {
				crc = crc << 1;
			}
		}
	}
	return crc;
}

#ifdef	__cplusplus
}
#endif

#endif	/* CRC_H */

