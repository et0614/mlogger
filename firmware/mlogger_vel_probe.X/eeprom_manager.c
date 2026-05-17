#include "mcc_generated_files/nvm/nvm.h"
#include "eeprom_manager.h"
#include "i2c_shared_data.h"
#include "utility.h"

#include <string.h>
#include <util/atomic.h>

// AVR128DB32 EEPROM開始アドレス (定数として定義)
#define EEPROM_BASE_ADDR  0x1400

// EEPROM全体のマップを定義（型定義のみ）
typedef struct {
    uint8_t init_flag;      // 0x0000
    uint8_t tc_type_config;
    uint8_t filter_number;
    float coefficientA[5];
    float coefficientB[5];
    uint8_t i2c_addr;
} EepromMap;

// 自動的にアドレス数値に変換
#define ADDR_INIT_FLAG      (EEPROM_BASE_ADDR +offsetof(EepromMap, init_flag))
#define ADDR_FILTER_NUMBER  (EEPROM_BASE_ADDR +offsetof(EepromMap, filter_number))
#define ADDR_COEF_A         (EEPROM_BASE_ADDR +offsetof(EepromMap, coefficientA))
#define ADDR_COEF_B         (EEPROM_BASE_ADDR +offsetof(EepromMap, coefficientB))
#define ADDR_I2C_ADDR       (EEPROM_BASE_ADDR +offsetof(EepromMap, i2c_addr))

//デフォルト設定定数
#define DEFAULT_FILTER_CONFIG  (6)
#define DEFAULT_I2C_ADDRESS    (0x10)
static const float DEFAULT_COEF_A[5] = {0.462f, 0.970f, 0.183f, 1.584f, 0.865f};
static const float DEFAULT_COEF_B[5] = {0.410f, 0.0f, 0.0f, 0.0f, 0.0f};

static uint8_t last_filter_config;
static uint8_t last_i2c_address;
static float last_coefficientA[5];
static float last_coefficientB[5];

// <editor-fold defaultstate="collapsed" desc="EEPROMブロック読み込み・書き込み処理">

//ブロック書き込み
static void write_eep_block(const void* src, uint16_t dst_addr, size_t size)
{
    const uint8_t* pSrc = (const uint8_t*)src;
    for (size_t i = 0; i < size; i++) {   
        while(EEPROM_IsBusy());
        if (EEPROM_Read(dst_addr + i) != pSrc[i])
        {
            //書き込み可能になるまで待機
            while(EEPROM_IsBusy());
            //アトミックに書き込み実行
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                EEPROM_Write(dst_addr + i, pSrc[i]);
            }
        }
    }
    
    //書き込みが完了したことを確認して処理を終える
    while(EEPROM_IsBusy());
}

// ブロック読み込み
static void read_eep_block(void* dst, uint16_t src_addr, size_t size)
{
    uint8_t* pDst = (uint8_t*)dst;
    for (size_t i = 0; i < size; i++) {
        pDst[i] = EEPROM_Read(src_addr + i);
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数">

//EEPROMを読み込む
void EM_loadEEPROM()
{
    //初期化未了の場合
    if (EEPROM_Read(ADDR_INIT_FLAG) != 'T') 
    {
        //設定を初期化
        EM_resetEEPROM();
        
        //初期化フラグ
        while(EEPROM_IsBusy());
        EEPROM_Write(ADDR_INIT_FLAG, 'T');
    }
    
    //フィルターn数
    while(EEPROM_IsBusy());
    SharedMemory.reg.filter_n = EEPROM_Read(ADDR_FILTER_NUMBER);
    last_filter_config = SharedMemory.reg.filter_n;
        
    //係数Aをロード
    read_eep_block((void*)last_coefficientA, ADDR_COEF_A, sizeof(last_coefficientA));
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy((void*)SharedMemory.reg.coefficientA, last_coefficientA, sizeof(last_coefficientA));
        SharedMemory.reg.crc_coefA = calc_crc8((void*)SharedMemory.reg.coefficientA, (uint8_t)sizeof(SharedMemory.reg.coefficientA));
    }
    
    //係数Bをロード
    read_eep_block(last_coefficientB, ADDR_COEF_B, sizeof(last_coefficientB));
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy((void*)SharedMemory.reg.coefficientB, last_coefficientB, sizeof(last_coefficientB));
        SharedMemory.reg.crc_coefB = calc_crc8((void*)SharedMemory.reg.coefficientB, (uint8_t)sizeof(SharedMemory.reg.coefficientB));
    }
    
    //I2Cアドレス
    while(EEPROM_IsBusy());
    SharedMemory.reg.i2c_address = EEPROM_Read(ADDR_I2C_ADDR);
    last_i2c_address = SharedMemory.reg.i2c_address;
}

//設定を保存する
void EM_updateEEPROM()
{
    //EEPROM寿命のため、設定変更があった場合のみ、書き込む
    //フィルタn数
    if(last_filter_config != SharedMemory.reg.filter_n)
    {
        last_filter_config = SharedMemory.reg.filter_n;
        while(EEPROM_IsBusy());
        EEPROM_Write(ADDR_FILTER_NUMBER, SharedMemory.reg.filter_n);
    }
    
    // 係数A
    float currentA[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(currentA, (void*)SharedMemory.reg.coefficientA, sizeof(currentA));
    }
    if(memcmp(last_coefficientA, (const void*)currentA, sizeof(last_coefficientA)) != 0)
    {
        memcpy(last_coefficientA, currentA, sizeof(currentA));
        write_eep_block(last_coefficientA, ADDR_COEF_A, sizeof(last_coefficientA));
        SharedMemory.reg.crc_coefA = calc_crc8((void*)last_coefficientA, (uint8_t)sizeof(last_coefficientA));
    }
    
    //係数B
    float currentB[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(currentB, (void*)SharedMemory.reg.coefficientB, sizeof(currentB));
    }
    if(memcmp(last_coefficientB, (const void*)currentB, sizeof(last_coefficientB)) != 0)
    {
        memcpy(last_coefficientB, currentB, sizeof(currentB));
        write_eep_block(last_coefficientB, ADDR_COEF_B, sizeof(last_coefficientB));
        SharedMemory.reg.crc_coefB = calc_crc8((void*)last_coefficientB, (uint8_t)sizeof(last_coefficientB));
    }
    
    //I2Cアドレス
    if(last_i2c_address != SharedMemory.reg.i2c_address)
    {
        last_i2c_address = SharedMemory.reg.i2c_address;
        while(EEPROM_IsBusy());
        EEPROM_Write(ADDR_I2C_ADDR, SharedMemory.reg.i2c_address);
    }
}

void EM_resetEEPROM()
{
    //フィルターn数
    while(EEPROM_IsBusy());
    EEPROM_Write(ADDR_FILTER_NUMBER, DEFAULT_FILTER_CONFIG);

    //係数A
    float temp_defaults[5];
    memcpy(temp_defaults, DEFAULT_COEF_A, sizeof(temp_defaults));
    for(int i=0; i<5; i++) swap_float(&temp_defaults[i]); // ビッグエンディアンに変換
    write_eep_block((const void*)temp_defaults, ADDR_COEF_A, sizeof(temp_defaults));

    //係数B
    memcpy(temp_defaults, DEFAULT_COEF_B, sizeof(temp_defaults));
    for(int i=0; i<5; i++) swap_float(&temp_defaults[i]); 
    write_eep_block((const void*)temp_defaults, ADDR_COEF_B, sizeof(temp_defaults));

    //I2Cアドレス
    while(EEPROM_IsBusy());
    EEPROM_Write(ADDR_I2C_ADDR, DEFAULT_I2C_ADDRESS);
}

// </editor-fold>
