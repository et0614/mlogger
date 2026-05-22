/*
 * File:   w25q512.c
 */

#include "w25q512.h"
#include "mcc_generated_files/system/system.h"
#include "mcc_generated_files/spi/spi0.h"

// ==========================================
// 定数定義 (内部設定)
// ==========================================
#define PAGE_SIZE        256  // Flashのページサイズ
#define FLASH_TOTAL_SIZE 0x4000000 // 64MB (フラッシュの総容量 (Byte))
#define DATA_AREA_SIZE   (FLASH_TOTAL_SIZE - DATA_START_ADDR) // データ領域の総容量 = 総容量 - 予約領域(4KB)
#define MAX_RECORD_COUNT ((DATA_AREA_SIZE / PAGE_SIZE) * RECS_PER_PAGE) // 保存可能な最大レコード数

// ==========================================
// 設定：CSピンのマクロ
// ==========================================
#define SPI_CS_SetLow()   FLASH_CS_SetLow() /* CSをOFFにする	*/
#define SPI_CS_SetHigh()  FLASH_CS_SetHigh() /* CSをONにする */

// ==========================================
// コマンド定義
// ==========================================
#define CMD_WRITE_ENABLE      0x06
#define CMD_READ_STATUS_REG1  0x05
#define CMD_SECTOR_ERASE_4B   0x21
#define CMD_PAGE_PROGRAM_4B   0x12
#define CMD_READ_DATA_4B      0x13

// ==========================================
// 内部ヘルパー関数 (static)
// ==========================================

// アドレスチェック用ヘルパー関数
static bool isValidRange(uint32_t addr, uint32_t len) {
    if ((addr + len) > FLASH_TOTAL_SIZE) return false; // 範囲外エラー
    return true;
}

// 書き込み許可 (WELビットセット)
static void W25_WriteEnable(void) {
    SPI_CS_SetLow();
    SPI0_ByteExchange(CMD_WRITE_ENABLE);
    SPI_CS_SetHigh();
}

// ビジー状態の確認 (WIPビットが0になるまで待機)
static bool W25_WaitForReady(void) {
    uint8_t status;
    
    // タイムアウト用カウンタ
    // Flashの書き込みは最大3ms程度かかる
    uint32_t timeout = 1000000;
    
    do {
        SPI_CS_SetLow();
        SPI0_ByteExchange(CMD_READ_STATUS_REG1);
        status = SPI0_ByteExchange(0x00); // ダミーを送ってステータスを読む
        SPI_CS_SetHigh();
        
        // タイムアウト判定
        if (timeout-- == 0)  return false; // エラー発生
    } while (status & 0x01); // Bit0 (BUSY) が1の間待つ
    
    return true;
}

// データの書き込み (4バイトアドレス指定)
static bool W25_WriteData(uint32_t address, uint8_t *data, uint16_t len) {
    if (!isValidRange(address, len)) return false; 
    
    W25_WriteEnable();

    SPI_CS_SetLow();
    // コマンドとアドレス
    SPI0_ByteExchange(CMD_PAGE_PROGRAM_4B);
    SPI0_ByteExchange((address >> 24) & 0xFF);
    SPI0_ByteExchange((address >> 16) & 0xFF);
    SPI0_ByteExchange((address >> 8)  & 0xFF);
    SPI0_ByteExchange(address         & 0xFF);

    // データ本体を一括送信
    SPI0_BufferWrite(data, len);
    
    SPI_CS_SetHigh();

    return W25_WaitForReady();
}

// ==========================================
// 公開関数
// ==========================================

uint32_t W25_GetAddressFromRecordIndex(uint32_t index) {    
    // 最大レコード数を超える場合は逸脱したアドレスを出力
    if (MAX_RECORD_COUNT <= index) return FLASH_TOTAL_SIZE + 1;

    uint32_t pageNum = index / RECS_PER_PAGE;      // 何ページ目か
    uint32_t offset  = index % RECS_PER_PAGE;      // そのページの何番目か
    
    // (ページ番号 * 256) + (ページ内オフセット * 20) + 開始オフセット
    return (pageNum * PAGE_SIZE) + (offset * RECORD_SIZE) + DATA_START_ADDR;
}

// 4KBセクタ消去 (4バイトアドレス指定)
void W25_SectorErase(uint32_t address) {
    W25_WriteEnable();

    SPI_CS_SetLow();
    SPI0_ByteExchange(CMD_SECTOR_ERASE_4B);
    SPI0_ByteExchange((address >> 24) & 0xFF);
    SPI0_ByteExchange((address >> 16) & 0xFF);
    SPI0_ByteExchange((address >> 8)  & 0xFF);
    SPI0_ByteExchange(address         & 0xFF);
    SPI_CS_SetHigh();

    W25_WaitForReady();
}

// データの読み出し (4バイトアドレス指定)
bool W25_ReadData(uint32_t address, uint8_t *buffer, uint16_t len) {
    if (!isValidRange(address, len)) return false;
    
    SPI_CS_SetLow();
    
    // コマンドとアドレス
    SPI0_ByteExchange(CMD_READ_DATA_4B);
    SPI0_ByteExchange((address >> 24) & 0xFF);
    SPI0_ByteExchange((address >> 16) & 0xFF);
    SPI0_ByteExchange((address >> 8)  & 0xFF);
    SPI0_ByteExchange(address         & 0xFF);

    // データ本体を一括受信
    SPI0_BufferRead(buffer, len);
    
    SPI_CS_SetHigh();
    
    return true;
}

// レコード単位の書き込み
bool W25_WriteRecord(uint32_t recordIndex, SensorData_t *data) {
    uint32_t addr = W25_GetAddressFromRecordIndex(recordIndex);
    
    // セクタの先頭（4096の倍数）かどうかチェック。W25Q512は一旦消去しないと適切に上書きできない
    if ((addr % 4096) == 0)
    {
        W25_SectorErase(addr);
        if (!W25_WaitForReady()) return false; // 約45msの消去時間
    }
    
    return W25_WriteData(addr, (uint8_t*)data, sizeof(SensorData_t));
}

// レコード単位の読み出し
bool W25_ReadRecord(uint32_t recordIndex, SensorData_t *data) {
    uint32_t addr = W25_GetAddressFromRecordIndex(recordIndex);
    
    return W25_ReadData(addr, (uint8_t*)data, sizeof(SensorData_t));
}

// ページ単位の読み出し
bool W25_ReadOnePage(uint32_t pageIndex, uint8_t *buffer)
{
    // アドレス計算: (ページ番号 * 256) + データ開始オフセット(4096)
    // ビットシフトを使うとさらに高速: (pageIndex << 8) + 4096
    uint32_t addr = (pageIndex * PAGE_SIZE) + DATA_START_ADDR;
    
    // 242バイト (11レコード分) を一括リード
    return W25_ReadData(addr, buffer, RECS_PER_PAGE * RECORD_SIZE);
}

/**
 * @brief 指定 index の record の timestamp (uint32 UNIX 秒) を読み出す。
 *        SensorData_t は packed: gen(1) + timestamp(4) + ... なので offset 1 から 4 byte。
 */
static bool W25_ReadTimestamp(uint32_t recordIndex, uint32_t *ts)
{
    uint32_t addr = W25_GetAddressFromRecordIndex(recordIndex);
    uint8_t buf[4];
    if (!W25_ReadData(addr + 1, buf, 4)) return false;
    // AVR/Cortex は little-endian、SensorData_t も packed LE なので素直に組み立て
    *ts = (uint32_t)buf[0]
        | ((uint32_t)buf[1] << 8)
        | ((uint32_t)buf[2] << 16)
        | ((uint32_t)buf[3] << 24);
    return true;
}

/**
 * @brief Ring buffer が一周し終わったあと (全レコードが target_gen) に、
 *        timestamp の rotation point (= 次に書き込む index、最古の record の位置) を
 *        二分探索で見つける。
 *        timestamp は通常 単調増加 ＋ 一周境界で急減するので、古典的な
 *        "rotated sorted array" の最小値探索と同型。
 *        全体が単調増加 (rotation 無し) なら 0 を返す = 一周直後の最古位置。
 */
static uint32_t W25_FindRotationPoint(void)
{
    uint32_t low  = 0;
    uint32_t high = MAX_RECORD_COUNT - 1;

    uint32_t ts_low, ts_high;
    if (!W25_ReadTimestamp(low,  &ts_low))  return 0;
    if (!W25_ReadTimestamp(high, &ts_high)) return 0;

    // 全体が単調増加なら rotation 無し (= ちょうど末尾まで書き終わった瞬間)。
    // 次に書き込む位置は wrap して index 0。
    if (ts_low <= ts_high) return 0;

    while (low < high) {
        uint32_t mid = low + (high - low) / 2;
        uint32_t ts_mid;
        if (!W25_ReadTimestamp(mid, &ts_mid)) return low;

        if (ts_mid > ts_high) {
            // [low, mid] は ascending 部分 → rotation point は (mid, high] の中
            low = mid + 1;
        } else {
            // mid は降順切替後の側 (= 最古を含む右半分) → rotation point は [low, mid] の中
            high = mid;
        }
    }
    return low;
}

/**
 * @brief 指定された世代番号 (target_gen) を持つレコードの個数を返す。
 *        起動時に logger_control から呼ばれ、戻り値は rec_latest (= 次の書き込み index) に
 *        そのまま入る。
 *
 * 対応ケース:
 *   1) target_gen のレコードが 1 件も無い (clear_data 直後など) → 0
 *   2) target_gen のレコードが flash 先頭から連続している (一周未満) → 二分探索で末尾を見つけて count
 *   3) ring buffer 一周済み (全 record が target_gen) → timestamp の rotation point を返す
 *      (rec_latest はその位置に上書き再開すれば、最古 record を消して時系列を保てる)
 */
uint32_t W25_Count_Record(uint8_t target_gen) {
    uint8_t read_gen;

    // --- ケース 1: 先頭が target_gen でなければ 0 件 ---
    W25_ReadData(W25_GetAddressFromRecordIndex(0), &read_gen, 1);
    if (read_gen != target_gen) return 0;

    // --- ケース 3: 末尾も target_gen なら ring 一周 → rotation point を探す ---
    W25_ReadData(W25_GetAddressFromRecordIndex(MAX_RECORD_COUNT - 1), &read_gen, 1);
    if (read_gen == target_gen) {
        return W25_FindRotationPoint();
    }

    // --- ケース 2: 末尾は別世代 (or 空) → 先頭から連続する target_gen の最大 index + 1 ---
    uint32_t low  = 0;
    uint32_t high = MAX_RECORD_COUNT - 1;
    uint32_t last_valid_index = 0;
    bool     found_any = false;

    while (low <= high) {
        uint32_t mid = low + (high - low) / 2;
        W25_ReadData(W25_GetAddressFromRecordIndex(mid), &read_gen, 1);

        if (read_gen == target_gen) {
            found_any = true;
            last_valid_index = mid;
            low = mid + 1;
        } else {
            if (mid == 0) break;     // underflow 防止
            high = mid - 1;
        }
    }

    return found_any ? (last_valid_index + 1) : 0;
}