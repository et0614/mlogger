/*
 * utilities.h
 *
 * Created: 2025/06/16 9:19:14
 *  Author: e.togashi
 */ 

#ifndef UTILITIES_H_
#define UTILITIES_H_

#include <stdint.h>

class Utilities
{	
	public:
		/**
		 * @fn
		 * CRC-8の巡回冗長検査値を生成する
		 * @brief SHT4xやAHT20などのセンサーで利用されるCRC-8チェックサムを計算する。
		 * 多項式: 0x31 (x^8 + x^5 + x^4 + 1)
		 * 初期値: 0xFF
		 * @param (ptr) チェックサムを計算するデータ配列へのポインタ
		 * @param (len) データ長（バイト）
		 * @return 8ビットのCRCチェックサム値
		 */
		static uint8_t crc8(uint8_t *ptr, uint8_t len);
		
		/**
		 * @fn
		 * CRC-16/CCITT-FALSEの巡回冗長検査値を生成する
		 * @brief XMODEMなどで利用される標準的なCRC-16チェックサムを計算する。
		 * 多項式: 0x1021 (x^16 + x^12 + x^5 + 1)
		 * 初期値: 0xFFFF
		 * @param (ptr) チェックサムを計算するデータ配列へのポインタ
		 * @param (len) データ長（バイト）
		 * @return 16ビットのCRCチェックサム値
		 */
		 static uint16_t crc16(uint8_t *ptr, uint8_t len);

};

#endif /* UTILITIES_H_ */