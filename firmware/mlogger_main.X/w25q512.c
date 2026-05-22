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
 * @brief 指定された世代番号 (target_gen) を持つレコードの個数を返す。
 *        起動時に logger_control から呼ばれ、戻り値は rec_latest (= 次の書き込み index) に
 *        そのまま入る。
 *
 *        書き込みは ring buffer 方式ではなく「満杯停止」方式 (logger_control.c 参照)。
 *        よって target_gen の record は常に flash 先頭から連続して並ぶ前提で OK。
 *        二分探索で「先頭から target_gen が並んでいる最大 index」+ 1 を返す。
 */
uint32_t W25_Count_Record(uint8_t target_gen) {
    uint32_t low = 0;
    uint32_t high = MAX_RECORD_COUNT - 1;

    uint32_t last_valid_index = 0; // 見つかった有効なインデックスの最大値
    bool found_any = false;        // 1つでも見つかったかフラグ
    uint8_t read_gen; // 世代保持用の1バイトバッファ

    // --- データが1件もない場合の高速チェック ---
    // 先頭データの世代番号を読む
    W25_ReadData(W25_GetAddressFromRecordIndex(0), &read_gen, 1);

    // 先頭がターゲット世代でないなら、データ数は0
    if (read_gen != target_gen) return 0;

    // --- 二分探索 (Binary Search) ---
    while (low <= high) {
        // 中間地点のインデックスを計算
        uint32_t mid = low + (high - low) / 2;

        // 世代番号を読む
        W25_ReadData(W25_GetAddressFromRecordIndex(mid), &read_gen, 1);

        // 有効データの条件:
        bool is_valid = (read_gen == target_gen);

        if (is_valid) {
            // 一致した！ -> 有効データは「ここ」か、もっと「右(後ろ)」にあるはず
            found_any = true;
            last_valid_index = mid; // 暫定値として記録
            low = mid + 1;          // 右側を探索
        } else {
            // 一致しない（古いデータ or 空き領域） -> 有効データはもっと「左(前)」にあるはず
            if (mid == 0) break;    // underflow防止
            high = mid - 1;         // 左側を探索
        }
    }

    if (found_any) {
        return last_valid_index + 1; // インデックスは0始まりなので、個数は +1
    } else {
        return 0;
    }
}