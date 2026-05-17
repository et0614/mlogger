#include "mcc_generated_files/i2c_client/twi0.h"

#include <stdbool.h>
#include <avr/sleep.h>

#include "i2c_slave.h"
#include "i2c_shared_data.h"
#include "eeprom_manager.h"

#define KEEP_ALIVE_DURATION (1000) // 猶予期間[msec]
#define I2C_ADD_UNLOCK_KEY (0xA5)

static bool cnfg_update_flag = false;
static bool coef_update_flag = false;

volatile bool I2C_Config_Update_Requested = false;
volatile bool I2C_Coefficient_Update_Requested = false;

volatile bool I2C_Is_Busy = false;       // 現在通信中か
volatile uint16_t I2C_KeepAlive_Ticks = 0; // 通信終了後の起きてる時間

volatile I2C_Map_t SharedMemory;
volatile uint8_t reg_pointer = 0;   // 読み書きする場所（インデックス）
volatile bool is_address_phase = true; // 「次はレジスタ番号が来る」ことを示すフラグ

// コールバック関数: TWI0ドライバからイベント発生時に呼ばれる
bool I2C_Slave_Callback(i2c_client_transfer_event_t event)
{
    switch(event)
    {
        // アドレス一致*****************************
        case I2C_CLIENT_TRANSFER_EVENT_ADDR_MATCH:
            set_sleep_mode(SLEEP_MODE_IDLE); //スリープモード変更
            I2C_Is_Busy = true; //通信開始フラグOn
            I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION; //接続猶予時間リセット
            
            // Write要求（マスタ→スレーブ）の場合、最初の1バイトは「レジスタ番号」を意味する
            // Read要求（スレーブ→マスタ）の場合、現在の reg_pointer の場所からデータを返す
            if (TWI0_TransferDirGet() == I2C_CLIENT_TRANSFER_DIR_WRITE) 
            {
                is_address_phase = true;
                cnfg_update_flag = false;
                coef_update_flag = false;
            }
            break;

        // データ受信*******************************
        case I2C_CLIENT_TRANSFER_EVENT_RX_READY:
            {
                uint8_t rxData = TWI0_ReadByte();
                I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION; //接続猶予時間リセット

                if (is_address_phase)
                {
                    // 最初の1バイト目は「データ」ではなく「レジスタ番号（ポインタ）」
                    reg_pointer = rxData;
                    
                    // 範囲外チェック（安全のため）
                    if (reg_pointer >= sizeof(SharedMemory.bytes)) reg_pointer = 0; 
                    
                    is_address_phase = false; // 次からはデータ本体として扱う
                }
                else
                {
                    // 書き込みフェーズ
                    if (reg_pointer < sizeof(SharedMemory.bytes))
                    {
                        // Read/Write Area（enable以降）のアドレスのみ書き込みを許可
                        if (reg_pointer >= offsetof(SensorData_t, enable)) 
                        {
                            SharedMemory.bytes[reg_pointer] = rxData;

                            // TC Type または Filter に書き込まれたらフラグを立てる
                            if (reg_pointer == offsetof(SensorData_t, enable) || reg_pointer == offsetof(SensorData_t, filter_n))
                                cnfg_update_flag = true;

                            //補正係数の範囲内であれば仮フラグを立てる
                            if ((reg_pointer >= offsetof(SensorData_t, coefficientA) && 
                                reg_pointer <  offsetof(SensorData_t, crc_coefA)) ||
                               (reg_pointer >= offsetof(SensorData_t, coefficientB) && 
                                reg_pointer <  offsetof(SensorData_t, crc_coefB)))
                                coef_update_flag = true;
                        }
                    }
                    
                    // 書き込んだら自動的に次のアドレスへ進める（インクリメント）
                    reg_pointer++;
                }
            }
            break;
        
        // 送信要求**************************************
        case I2C_CLIENT_TRANSFER_EVENT_TX_READY:
            {
                I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION; //接続猶予時間リセット
                // 現在のポインタの場所にあるデータを送る
                if (reg_pointer < sizeof(SharedMemory.bytes)) {
                    TWI0_WriteByte(SharedMemory.bytes[reg_pointer]);
                    reg_pointer++; // 送ったら次へ進める
                } 
                else TWI0_WriteByte(0xFF); // 範囲外ならダミー
            }
            break;

        // 通信終了（Stop Bit）************************************
        case I2C_CLIENT_TRANSFER_EVENT_STOP_BIT_RECEIVED:
            // I2C設定変更ロックが解除されている場合にはEEPROMに反映して次回起動時に新規アドレスを反映
            if (SharedMemory.reg.i2c_addr_unlock == I2C_ADD_UNLOCK_KEY) EM_updateEEPROM();
            // 再ロック
            SharedMemory.reg.i2c_addr_unlock = 0x00;            
            //breakせずにEVENT_ERRORに続ける
            
        case I2C_CLIENT_TRANSFER_EVENT_ERROR:
            I2C_Is_Busy = false; //通信開始フラグOff
            I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION; //接続猶予時間リセット
            //reg_pointer = 0; // 次回の通信のためにインデックスを0に戻す
            I2C_Config_Update_Requested |= cnfg_update_flag;
            I2C_Coefficient_Update_Requested |= coef_update_flag;
            cnfg_update_flag = false;
            coef_update_flag = false;
            break;

        default:
            break;
    }
    
    return true; // 処理継続
}

// 初期化関数
void I2C_Slave_Init(void)
{    
    // コールバックを登録
    TWI0_CallbackRegister(I2C_Slave_Callback);
}