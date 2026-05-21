/* * File: w25q512.h
 * Author: e.togashi
 *
 * Created on December 11, 2025
 *
 * Summary:
 * Winbond W25Q512 (512M-bit / 64MB) Flashメモリ制御ドライバ
 * SPI通信を用いてセンサデータの保存・読み出しを行う
 * 4バイトアドレスモードを使用し、リングバッファとして管理する
 */

#ifndef W25Q512_H
#define	W25Q512_H

#ifdef	__cplusplus
extern "C" {
#endif


#include <stdint.h>
#include <stdbool.h>

// ==========================================
// 定数定義 (内部設定)
// ==========================================
#define RECORD_SIZE      sizeof(SensorData_t) // 22バイト
#define RECS_PER_PAGE    11   // 1ページに入るレコード数 (256 / 22 = 11 余り 14)
#define DATA_START_ADDR  4096 // データ領域の開始位置 (最初の4KBは管理領域として飛ばす)
#define PAGE_SIZE        256  // Flashのページサイズ
#define FLASH_TOTAL_SIZE 0x4000000 // 64MB (フラッシュの総容量 (Byte))
#define DATA_AREA_SIZE   (FLASH_TOTAL_SIZE - DATA_START_ADDR) // データ領域の総容量 = 総容量 - 予約領域(4KB)
#define MAX_RECORD_COUNT ((DATA_AREA_SIZE / PAGE_SIZE) * RECS_PER_PAGE) // 保存可能な最大レコード数
    
// ビットフラグの定義
#define FLAG_ILLUMINANCE (1 << 0) // 0bit目: 照度
#define FLAG_TEMP_DRY    (1 << 1) // 1bit目: 乾球温度
#define FLAG_TEMP_GLOBE  (1 << 2) // 2bit目: グローブ温度
#define FLAG_HUMIDITY    (1 << 3) // 3bit目: 相対湿度
#define FLAG_WIND_SPEED  (1 << 4) // 4bit目: 風速
#define FLAG_VOLTAGE     (1 << 5) // 5bit目: 風速推定のための電圧
#define FLAG_CO2_PPM     (1 << 6) // 6bit目: CO2濃度
    
// データ構造体の定義 (22 bytes)
typedef struct __attribute__((packed)) {
    uint8_t  generation;    // データ世代番号
    uint32_t timestamp;     // タイムスタンプ(UNIX時間)
    uint8_t  valid_flags;   // 有効データフラグ
    uint32_t illuminance;   // 照度 (単位: Lux * 10)
    int16_t  temp_dry;      // 乾球温度 (単位: ℃ * 100)
    int16_t  temp_globe;    // グローブ温度 (単位: ℃ * 100)
    uint16_t humidity;      // 相対湿度 (単位: % * 100)
    uint16_t wind_speed;    // 風速 (単位: m/s * 10000)
    uint16_t voltage;       // 風速推定のための電圧 (単位: mV)
    uint16_t co2_ppm;       // CO2濃度 (単位: ppm)
} SensorData_t;

/**
 * @brief レコード番号をもとにアドレスを出力する
 * @param index レコード番号
 * @return アドレス
 */
uint32_t W25_GetAddressFromRecordIndex(uint32_t index);

/**
 * @brief 指定した物理アドレスの4KBセクタを消去する
 * @param address 4バイト物理アドレス (4096の倍数)
 */
void W25_SectorErase(uint32_t address);

/**
 * @brief 指定したアドレスから任意のバイト数を読み出す (生データ読み出し)
 * @param address 4バイト物理アドレス
 * @param buffer  読み出し先バッファ
 * @param len     読み出しバイト数
 * @return true:成功, false:タイムアウト等のエラー
 */
bool W25_ReadData(uint32_t address, uint8_t *buffer, uint16_t len);

/**
 * @brief レコード番号(index)を指定してデータを書き込む。
 * 内部で必要に応じてセクタ消去も実行する。
 * @param recordIndex 0から始まる通し番号
 * @param data        書き込むデータ構造体へのポインタ
 * @return true:成功, false:タイムアウト等のエラー
 */
bool W25_WriteRecord(uint32_t recordIndex, SensorData_t *data);

/**
 * @brief レコード番号(index)を指定してデータを読み出す。
 * @param recordIndex 0から始まる通し番号
 * @param data        読み出しデータを格納する構造体へのポインタ
 * @return true:成功, false:タイムアウト等のエラー
 */
bool W25_ReadRecord(uint32_t recordIndex, SensorData_t *data);

/**
 * @brief Flashの1ページ(256バイト)分を直接読み出す
 * @param pageIndex ページ番号 (注意：レコード番号ではない)
 * @param buffer    読み出し先バッファ (最低256バイト必要)
 * @return true:成功, false:タイムアウト等のエラー
 */
bool W25_ReadOnePage(uint32_t pageIndex, uint8_t *buffer);


uint32_t W25_Count_Record(uint8_t target_gen);

#ifdef	__cplusplus
}
#endif

#endif	/* W25Q512_H */

