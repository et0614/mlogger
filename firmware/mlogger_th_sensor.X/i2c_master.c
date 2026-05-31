#include "i2c_master.h"
#include "mcc_generated_files/i2c_host/twi1.h"

// I2C バス完了待ちの上限。TWI0_IsBusy/Tasks を繰り返し回すループの反復回数で
// 表現する。F_CPU = 24 MHz、1 反復あたり 100〜200 cycle 程度として概ね 0.8〜1.6 秒
// 相当。この上限を超えたら通信を諦めてエラー復帰し、呼び出し側にリトライを委ねる。
// WDT (~4s) より十分短く設定していること。
#define I2C_WAIT_MAX_ITER  200000UL

// 内部関数: バスをアイドル状態に強制復帰させる
static void i2c_recover_bus(void)
{
    // TWI を再初期化。ハング状態のバス状態機械を IDLE に戻す。
    TWI1_Deinitialize();
    TWI1_Initialize();
}

// 内部関数: 転送完了を待ち、エラー状態を確認する
// タイムアウトした場合はバスを強制リセットして false を返す
static bool wait_for_completion(void)
{
    uint32_t iter = 0;

    while (TWI1_IsBusy())
    {
        TWI1_Tasks();
        if (++iter > I2C_WAIT_MAX_ITER)
        {
            // ハング疑い。バスを強制復帰して失敗を返す
            i2c_recover_bus();
            return false;
        }
    }

    // エラー状態を確認
    i2c_host_error_t error = TWI1_ErrorGet();

    // エラーがなければ成功 (I2C_ERROR_NONE == 0)
    return (error == I2C_ERROR_NONE);
}

bool I2C_IsConnected(uint8_t address)
{
   // ダミーデータ（送信はされないがポインタとして必要）
    uint8_t dummy = 0;

    // 長さ0で書き込みを試みる (アドレス送信 -> ACKチェック -> Stop)
    // Melodyのバージョンによっては、長さ0だと何もせずtrueを返す場合があるため要確認
    if (!TWI1_Write(address, &dummy, 0)) return false;

    return wait_for_completion();
}

bool I2C_Write(uint8_t address, const uint8_t *data, size_t len)
{
    // 転送開始要求
    if (!TWI1_Write(address, (uint8_t *)data, len)) return false; // 他の転送が進行中

    // 完了待ち
    return wait_for_completion();
}

bool I2C_Read(uint8_t address, uint8_t *buffer, size_t len)
{
    if (len == 0) return true;

    // 転送開始要求
    if (!TWI1_Read(address, buffer, len)) return false;

    // 完了待ち
    return wait_for_completion();
}

bool I2C_WriteRead(uint8_t address, const uint8_t *writeData, size_t writeLen, uint8_t *readBuffer, size_t readLen)
{
    // MelodyのWriteRead機能を使用
    if (!TWI1_WriteRead(address, (uint8_t *)writeData, writeLen, readBuffer, readLen)) return false;

    // 完了待ち
    return wait_for_completion();
}

bool I2C_WriteByteAndStop(uint8_t address, uint8_t data)
{
    // Write関数で1バイト送信するのと同義
    return I2C_Write(address, &data, 1);
}
