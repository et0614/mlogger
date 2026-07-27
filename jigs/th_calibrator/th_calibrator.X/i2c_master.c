#include "i2c_master.h"
#include "mcc_generated_files/i2c_host/twi0.h"

// I2C完了待ちのタイムアウト上限 (ループ回数)。SDA固着やデバイス無応答で永久に
// ビジーが解けない場合に、ここで打ち切ってシステムのハングを防ぐ。
// 通常のI2C転送は1ms未満で完了するため、十分大きな値にしてある(~0.5-1s相当)。
#define I2C_BUSY_TIMEOUT  1000000UL

// 内部関数: 転送完了を待ち、エラー状態を確認する
static bool wait_for_completion(void)
{
    // ビジー状態である間、タスクを回して処理を進める (タイムアウト付き)
    uint32_t timeout = I2C_BUSY_TIMEOUT;
    while (TWI0_IsBusy())
    {
        TWI0_Tasks();
        if (timeout-- == 0) return false; // タイムアウト: I2C詰まり等
    }

    // エラー状態を確認
    i2c_host_error_t error = TWI0_ErrorGet();

    // エラーがなければ成功 (I2C_ERROR_NONE == 0 と仮定)
    return (error == I2C_ERROR_NONE);
}

bool I2C_IsConnected(uint8_t address)
{
   // ダミーデータ（送信はされないがポインタとして必要）
    uint8_t dummy = 0;
    
    // 長さ0で書き込みを試みる (アドレス送信 -> ACKチェック -> Stop)
    // Melodyのバージョンによっては、長さ0だと何もせずtrueを返す場合があるため要確認
    if (!TWI0_Write(address, &dummy, 0)) return false;

    uint32_t timeout = I2C_BUSY_TIMEOUT;
    while (TWI0_IsBusy())
    {
        TWI0_Tasks();
        if (timeout-- == 0) return false; // タイムアウト
    }

    return (TWI0_ErrorGet() == I2C_ERROR_NONE);
}

bool I2C_Write(uint8_t address, const uint8_t *data, size_t len)
{
    // 転送開始要求
    if (!TWI0_Write(address, (uint8_t *)data, len)) return false; // 他の転送が進行中

    // 完了待ち
    return wait_for_completion();
}

bool I2C_Read(uint8_t address, uint8_t *buffer, size_t len)
{
    if (len == 0) return true;

    // 転送開始要求
    if (!TWI0_Read(address, buffer, len)) return false;
 
    // 完了待ち
    return wait_for_completion();
}

bool I2C_WriteRead(uint8_t address, const uint8_t *writeData, size_t writeLen, uint8_t *readBuffer, size_t readLen)
{
    // MelodyのWriteRead機能を使用
    if (!TWI0_WriteRead(address, (uint8_t *)writeData, writeLen, readBuffer, readLen)) return false;

    // 完了待ち
    return wait_for_completion();
}

bool I2C_WriteByteAndStop(uint8_t address, uint8_t data)
{
    // Write関数で1バイト送信するのと同義
    return I2C_Write(address, &data, 1); 
}