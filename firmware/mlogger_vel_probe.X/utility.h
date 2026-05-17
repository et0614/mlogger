/* 
 * File:   utility.h
 * Author: e.togashi
 *
 * Created on 2026/01/18, 16:07
 */

#ifndef CRC8_H
#define	CRC8_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>

 /**
 * @brief CRC8計算関数（多項式 0x31）
 * @param data CRC8を作成するuint8_t型配列
 * @param len 配列要素数
 * @return CRC8
 */
uint8_t calc_crc8(const uint8_t *data, uint8_t len);

 /**
 * @brief floatの上下バイトを入れ替えてエンディアンを変換する
 * @param f float型変数
 */
void swap_float(float* f);

 /**
 * @brief FNV-1aでハッシュを計算する
 * @param data ハッシュを作成するuint8_t型配列
 * @param len 配列要素数
 * @return ハッシュ
 */
uint32_t fnv1a_32(const uint8_t* data, size_t len);


#ifdef	__cplusplus
}
#endif

#endif	/* CRC8_H */

