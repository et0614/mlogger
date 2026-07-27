/*
 * File:   w25q64.c
 *
 * 容量・サイズ系定数 (PAGE_SIZE / FLASH_TOTAL_SIZE / DATA_AREA_SIZE / MAX_RECORD_COUNT)
 * は w25q64.h に集約済み。重複定義は撤去。
 *
 * アドレッシング: W25Q64 は 3バイト(24bit)アドレスのみ。standard コマンド
 * 0x03(Read) / 0x02(Page Program) / 0x20(Sector Erase) を使用する。
 */

#include "w25q64.h"
#include "mcc_generated_files/system/system.h"
#include "mcc_generated_files/spi/spi0.h"
#include <stddef.h>   // offsetof

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
#define CMD_SECTOR_ERASE      0x20   // 4KB Sector Erase (3-byte address)
#define CMD_PAGE_PROGRAM      0x02   // Page Program     (3-byte address)
#define CMD_READ_DATA         0x03   // Read Data        (3-byte address)
#define CMD_CHIP_ERASE        0xC7

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

// データの書き込み (3バイトアドレス指定)
static bool W25_WriteData(uint32_t address, uint8_t *data, uint16_t len) {
    if (!isValidRange(address, len)) return false;

    W25_WriteEnable();

    SPI_CS_SetLow();
    // コマンドとアドレス (3バイト)
    SPI0_ByteExchange(CMD_PAGE_PROGRAM);
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

// JEDEC ID (0x9F): id[0]=メーカ, id[1]=メモリタイプ, id[2]=容量。Winbond は id[0]==0xEF。
bool W25_ReadJEDECID(uint8_t id[3]) {
    SPI_CS_SetLow();
    SPI0_ByteExchange(0x9F);
    id[0] = SPI0_ByteExchange(0x00);
    id[1] = SPI0_ByteExchange(0x00);
    id[2] = SPI0_ByteExchange(0x00);
    SPI_CS_SetHigh();
    return (id[0] != 0x00 && id[0] != 0xFF); // 0x00/0xFF は未接続/バス異常
}

// write/read/verify セルフテスト。予約領域(先頭4KB, DATA_START_ADDR=4096 より前)をスクラッチに
// 使うので保存データ(index 0 以降)は消えない。消去→パターン書込→読出→照合。
bool W25_SelfTest(void) {
    const uint32_t addr = 0; // 予約領域の先頭セクタ(現状未使用)
    uint8_t wr[16], rd[16];
    for (uint8_t i = 0; i < sizeof(wr); i++) wr[i] = (uint8_t)(0xA5 ^ i);

    W25_SectorErase(addr);                          // 4KBセクタ消去 (全bit=1)
    if (!W25_WriteData(addr, wr, sizeof(wr))) return false;
    if (!W25_ReadData(addr, rd, sizeof(rd)))  return false;
    for (uint8_t i = 0; i < sizeof(wr); i++)
        if (rd[i] != wr[i]) return false;           // 照合不一致
    return true;
}

uint32_t W25_GetAddressFromRecordIndex(uint32_t index) {
    // 最大レコード数を超える場合は逸脱したアドレスを出力
    if (MAX_RECORD_COUNT <= index) return FLASH_TOTAL_SIZE + 1;

    uint32_t pageNum = index / RECS_PER_PAGE;      // 何ページ目か
    uint32_t offset  = index % RECS_PER_PAGE;      // そのページの何番目か
    
    // (ページ番号 * 256) + (ページ内オフセット * 20) + 開始オフセット
    return (pageNum * PAGE_SIZE) + (offset * RECORD_SIZE) + DATA_START_ADDR;
}

// チップ全体消去 (W25Q64 で typ 約 20 秒〜最大 100 秒の blocking)。
// 完了まで BUSY (Status Reg1 bit0) を polling し続けるためタイムアウトは設けない。
// 呼び出し側は実行前に LED 等で「処理中」を示し、終わるまで待つこと。
void W25_ChipErase(void) {
    W25_WriteEnable();

    SPI_CS_SetLow();
    SPI0_ByteExchange(CMD_CHIP_ERASE);
    SPI_CS_SetHigh();

    // chip erase の最大時間は数十秒級。W25_WaitForReady の固定タイムアウトでは足りないので
    // ここで専用の長時間 polling を行う。
    uint8_t status;
    do {
        SPI_CS_SetLow();
        SPI0_ByteExchange(CMD_READ_STATUS_REG1);
        status = SPI0_ByteExchange(0x00);
        SPI_CS_SetHigh();
    } while (status & 0x01);
}

// 4KBセクタ消去 (3バイトアドレス指定)
void W25_SectorErase(uint32_t address) {
    W25_WriteEnable();

    SPI_CS_SetLow();
    SPI0_ByteExchange(CMD_SECTOR_ERASE);
    SPI0_ByteExchange((address >> 16) & 0xFF);
    SPI0_ByteExchange((address >> 8)  & 0xFF);
    SPI0_ByteExchange(address         & 0xFF);
    SPI_CS_SetHigh();

    W25_WaitForReady();
}

// データの読み出し (3バイトアドレス指定)
bool W25_ReadData(uint32_t address, uint8_t *buffer, uint16_t len) {
    if (!isValidRange(address, len)) return false;

    SPI_CS_SetLow();

    // コマンドとアドレス (3バイト)
    SPI0_ByteExchange(CMD_READ_DATA);
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
    
    // セクタの先頭（4096の倍数）かどうかチェック。W25Q シリーズは一旦消去しないと適切に上書きできない
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

// 1レコードが「空き(未書き込み=消去状態)」かを判定する。
// 消去後のNOR flashは全bit=1(0xFF)。humidity の実値は 0..10000(=0x2710) なので
// 0xFFFF(=655.35%) は物理的にあり得ない → これを空き判定の指標に使う。
// (humidity フィールドの2バイトだけを読むので高速)
static bool W25_IsRecordEmpty(uint32_t index) {
    uint16_t humidity;
    uint32_t addr = W25_GetAddressFromRecordIndex(index)
                  + offsetof(SensorData_t, humidity);
    W25_ReadData(addr, (uint8_t*)&humidity, sizeof(humidity));
    return humidity == 0xFFFF;
}

uint32_t W25_Count_Record(void) {
    // 先頭が空きならデータ0件 (高速パス)
    if (W25_IsRecordEmpty(0)) return 0;

    // 不変条件: record[low]=有効が確定, record[high]=空きが確定。
    //   low  = 0               … 上で有効を確認済み
    //   high = MAX_RECORD_COUNT … 番兵 (満杯時はここが件数になる)
    // 「最初に空きになる index」へ low/high を挟み込む。
    uint32_t low  = 0;
    uint32_t high = MAX_RECORD_COUNT;

    while (high - low > 1) {
        uint32_t mid = low + (high - low) / 2;
        if (W25_IsRecordEmpty(mid)) high = mid; // mid は空き → 境界は mid 以下
        else                        low  = mid; // mid は有効 → 境界は mid より後
    }

    // high = 最初に空きになる index = 有効レコード数
    return high;
}