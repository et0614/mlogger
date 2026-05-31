#include "mcc_generated_files/i2c_client/twi0.h"

#include <stdbool.h>
#include <stddef.h>
#include <avr/sleep.h>

#include "i2c_slave.h"
#include "i2c_shared_data.h"
#include "eeprom_manager.h"

#define KEEP_ALIVE_DURATION (1000) // 通信終了後の起きてる時間 [msec]

static bool cnfg_update_flag = false;
static bool addr_changed_flag = false;

volatile bool I2C_Config_Update_Requested = false;

volatile bool I2C_Is_Busy = false;
volatile uint16_t I2C_KeepAlive_Ticks = 0;

volatile I2C_Map_t SharedMemory;
static volatile uint8_t reg_pointer = 0;
static volatile bool is_address_phase = true;

// 指定レジスタアドレスへの書き込みが許可されているか判定。
// - 0x04 Addr.Key       : 常に許可
// - 0x05 New Addr.      : Addr.Key が ADDR_KEY_UNLOCK のときのみ許可
// - 0x18-0x27 Name      : 常に許可 (装置ラベル、鍵不要)
// - 0x29 Status2        : 親機の ACK 用に常に許可
// - 0x4C 以降 拡張領域  : 常に許可 (補正係数)
// その他は読み出し専用
static bool isWritable(uint8_t addr)
{
    if (addr == REG_ADDR_KEY) return true;
    if (addr == REG_NEW_ADDR) return (SharedMemory.reg.addr_key == ADDR_KEY_UNLOCK);
    if (addr >= REG_NAME && addr < REG_NAME + NODE_NAME_LEN) return true;
    if (addr == REG_STATUS2) return true;
    if (addr >= REG_EXTENSION && addr < sizeof(SharedMemory.bytes)) return true;
    return false;
}

bool I2C_Slave_Callback(i2c_client_transfer_event_t event)
{
    switch(event)
    {
        // アドレス一致*****************************
        case I2C_CLIENT_TRANSFER_EVENT_ADDR_MATCH:
            set_sleep_mode(SLEEP_MODE_IDLE);
            I2C_Is_Busy = true;
            I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION;

            if (TWI0_TransferDirGet() == I2C_CLIENT_TRANSFER_DIR_WRITE)
            {
                is_address_phase = true;
                cnfg_update_flag = false;
                addr_changed_flag = false;
            }
            break;

        // データ受信*******************************
        case I2C_CLIENT_TRANSFER_EVENT_RX_READY:
            {
                uint8_t rxData = TWI0_ReadByte();
                I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION;

                if (is_address_phase)
                {
                    reg_pointer = rxData;
                    if (reg_pointer >= sizeof(SharedMemory.bytes)) reg_pointer = 0;
                    is_address_phase = false;
                }
                else
                {
                    if (isWritable(reg_pointer))
                    {
                        SharedMemory.bytes[reg_pointer] = rxData;

                        // アドレス変更を検知 (STOP 受信時に EEPROM 反映)
                        if (reg_pointer == REG_NEW_ADDR) addr_changed_flag = true;

                        // Name 領域への書き込みは EEPROM 反映トリガ
                        if (reg_pointer >= REG_NAME && reg_pointer < REG_NAME + NODE_NAME_LEN)
                            cnfg_update_flag = true;

                        // 拡張領域 (補正係数) への書き込みも EEPROM 反映トリガ
                        if (reg_pointer >= REG_EXTENSION) cnfg_update_flag = true;
                    }
                    reg_pointer++;
                }
            }
            break;

        // 送信要求**************************************
        case I2C_CLIENT_TRANSFER_EVENT_TX_READY:
            {
                I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION;
                if (reg_pointer < sizeof(SharedMemory.bytes)) {
                    TWI0_WriteByte(SharedMemory.bytes[reg_pointer]);
                    reg_pointer++;
                }
                else TWI0_WriteByte(0xFF);
            }
            break;

        // 通信終了 (Stop Bit)***************************
        case I2C_CLIENT_TRANSFER_EVENT_STOP_BIT_RECEIVED:
            // アドレス変更が確定 (鍵 + 新アドレス書込) されていれば EEPROM 永続化 +
            // TWI0.SADDR 即時反映。次のトランザクションから新アドレスで応答する。
            // (リセット不要のシームレス切替)
            if (addr_changed_flag && SharedMemory.reg.addr_key == ADDR_KEY_UNLOCK) {
                EM_updateEEPROM();
                TWI0.SADDR = SharedMemory.reg.new_addr << 1;
            }
            // 鍵をクリア
            SharedMemory.reg.addr_key = 0x00;
            // フォールスルー

        case I2C_CLIENT_TRANSFER_EVENT_ERROR:
            I2C_Is_Busy = false;
            I2C_KeepAlive_Ticks = KEEP_ALIVE_DURATION;
            I2C_Config_Update_Requested |= cnfg_update_flag;
            cnfg_update_flag = false;
            addr_changed_flag = false;
            break;

        default:
            break;
    }

    return true;
}

void I2C_Slave_Init(void)
{
    TWI0_CallbackRegister(I2C_Slave_Callback);
}
