/* * File: w25q64.h
 * Author: e.togashi
 *
 * Created on December 11, 2025
 *
 * Summary:
 * Winbond W25Q64 (64M-bit / 8MB) Flashメモリ制御ドライバ
 * SPI通信を用いてセンサデータの保存・読み出しを行う
 * 満杯停止方式 (古いデータは上書きしない) で管理する
 *
 * アドレッシング: W25Q64 は容量 8MB (24bit = 16MB 以内) なので 3バイトアドレスのみ。
 * (W25Q256/512 のような 4バイトアドレスモードは非搭載。standard 3-byte address
 *  コマンド 0x03/0x02/0x20 を使用する。)
 */

#ifndef W25Q64_H
#define	W25Q64_H

#ifdef	__cplusplus
extern "C" {
#endif


#include <stdint.h>
#include <stdbool.h>

// ==========================================
// 定数定義 (内部設定)
// ==========================================
#define RECORD_SIZE      sizeof(SensorData_t) // 6バイト
#define RECS_PER_PAGE    (PAGE_SIZE / RECORD_SIZE) // 1ページのレコード数 (256/6 = 42 余り 4)
#define DATA_START_ADDR  4096 // データ領域の開始位置 (最初の4KBは管理領域として飛ばす)
#define PAGE_SIZE        256  // Flashのページサイズ
#define FLASH_TOTAL_SIZE 0x800000 // 8MB (W25Q64 の総容量 (Byte))
#define DATA_AREA_SIZE   (FLASH_TOTAL_SIZE - DATA_START_ADDR) // データ領域の総容量 = 総容量 - 予約領域(4KB)
#define MAX_RECORD_COUNT ((DATA_AREA_SIZE / PAGE_SIZE) * RECS_PER_PAGE) // 保存可能な最大レコード数 (≒ 1.37M)

// データ構造体の定義 (6 bytes, packed)
//
// タイムスタンプは保持しない。計測開始日時(start_dt)と計測間隔(interval)は
// EEPROM(EM_mSettings)に別途記録し、各レコードの時刻は
//   時刻 = start_dt + (レコードindex) * interval
// で復元する。
//
// 空き(未書き込み)レコードの判定: 消去後のflashは全bit=1(0xFF)。humidity の実値は
// 0..10000(=0x2710 = 100.00%) なので 0xFFFF(=655.35%) は物理的にあり得ない。
// よって humidity == 0xFFFF を「空きレコード」の指標として使う (W25_Count_Record)。
typedef struct __attribute__((packed)) {
    int16_t  temperature;   // 温度     (単位: ℃ * 100)
    uint16_t humidity;      // 相対湿度 (単位: % * 100)  ※空き判定に使用 (0xFFFF=空き)
    uint16_t co2_ppm;       // CO2濃度  (単位: ppm)。CO2_INVALID なら欠測(計測失敗)レコード
} SensorData_t;

// 欠測(計測失敗)マーカー: co2_ppm がこの値のレコードは無効。実在しないppm値を使う。
// 計測失敗時もこの値で1レコード書き込むことで「時刻 = start_dt + index*interval」の
// index と時刻の対応をズラさない (1区間=1レコードを保つ)。humidity は 0xFFFF(空き判定値)
// 以外(=0)にして件数にカウントされるようにする。
#define CO2_INVALID  0xFFFFu

/**
 * @brief レコード番号をもとにアドレスを出力する
 * @param index レコード番号
 * @return アドレス
 */
uint32_t W25_GetAddressFromRecordIndex(uint32_t index);

/**
 * @brief 指定した物理アドレスの4KBセクタを消去する
 * @param address 物理アドレス (4096の倍数)
 */
void W25_SectorErase(uint32_t address);

/**
 * @brief チップ全体を消去する (Chip Erase, 0xC7)。
 *        BUSY ビットが 0 になるまで blocking で待つため、typ 約 20 秒〜最大 100 秒戻らない。
 *        通常運用では呼ばない (erase_flash コマンドからのみ呼ばれる)。
 */
void W25_ChipErase(void);

/**
 * @brief 指定したアドレスから任意のバイト数を読み出す (生データ読み出し)
 * @param address 物理アドレス
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


/**
 * @brief JEDEC ID (0x9F) を読み出す。id[0]=メーカ, id[1]=メモリタイプ, id[2]=容量。
 *        Winbond は id[0]==0xEF。SPI疎通とチップ存在の確認用(非破壊)。
 * @return true:応答あり(0x00/0xFF以外), false:未接続/異常
 */
bool W25_ReadJEDECID(uint8_t id[3]);

/**
 * @brief フラッシュの write/read/verify セルフテスト。予約領域(先頭4KB, データ領域外)を
 *        スクラッチに使うので保存データは消さない。消去→パターン書込→読出→照合。
 * @return true:一致(書込/読出/消去OK), false:不一致/失敗
 */
bool W25_SelfTest(void);

/**
 * @brief 保存済み(有効)レコードの件数を返す。
 *        書き込みは index 0 から連番の満杯停止方式なので、有効レコードは flash 先頭から
 *        連続して並ぶ。「最初に空き(humidity==0xFFFF)になる index」を二分探索で求め、
 *        それを件数として返す (= 次に書き込むべき index)。
 * @return 有効レコード数 (0 〜 MAX_RECORD_COUNT)
 */
uint32_t W25_Count_Record(void);

#ifdef	__cplusplus
}
#endif

#endif	/* W25Q64_H */

