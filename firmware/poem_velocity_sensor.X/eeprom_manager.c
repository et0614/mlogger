#include "mcc_generated_files/nvm/nvm.h"
#include "eeprom_manager.h"
#include "i2c_shared_data.h"
#include "utility.h"

#include <string.h>
#include <util/atomic.h>

// AVR128DB32 EEPROM 開始アドレス
#define EEPROM_BASE_ADDR  0x1400

// EEPROM レイアウト (native LE で保存)
// Note: 既存 EEPROM (init_flag='T') との後方互換のため、name を末尾に追加。
//       upgrade 時 name は 0xFF で埋まっているので、EM_loadEEPROM でサニタイズする。
typedef struct {
    uint8_t init_flag;
    uint8_t reserved1;
    uint8_t filter_number;
    float   coefficientA[5];
    float   coefficientB[5];
    uint8_t i2c_addr;
    char    name[NODE_NAME_LEN];        // 装置ラベル (NUL 終端)
} EepromMap;

#define ADDR_INIT_FLAG      (EEPROM_BASE_ADDR + offsetof(EepromMap, init_flag))
#define ADDR_FILTER_NUMBER  (EEPROM_BASE_ADDR + offsetof(EepromMap, filter_number))
#define ADDR_COEF_A         (EEPROM_BASE_ADDR + offsetof(EepromMap, coefficientA))
#define ADDR_COEF_B         (EEPROM_BASE_ADDR + offsetof(EepromMap, coefficientB))
#define ADDR_I2C_ADDR       (EEPROM_BASE_ADDR + offsetof(EepromMap, i2c_addr))
#define ADDR_NAME           (EEPROM_BASE_ADDR + offsetof(EepromMap, name))

// デフォルト設定
#define DEFAULT_FILTER_CONFIG  (6)
#define DEFAULT_I2C_ADDRESS    (0x10)
static const float DEFAULT_COEF_A[5] = {0.462f, 0.970f, 0.183f, 1.584f, 0.865f};
static const float DEFAULT_COEF_B[5] = {0.410f, 0.0f, 0.0f, 0.0f, 0.0f};

// 直近書き込み値 (差分書き込み判定用)
static uint8_t last_filter_config;
static uint8_t last_i2c_address;
static float   last_coefficientA[5];
static float   last_coefficientB[5];
static char    last_name[NODE_NAME_LEN];

// <editor-fold defaultstate="collapsed" desc="EEPROMブロック読み書き">

static void write_eep_block(const void* src, uint16_t dst_addr, size_t size)
{
    const uint8_t* pSrc = (const uint8_t*)src;
    for (size_t i = 0; i < size; i++) {
        while (EEPROM_IsBusy());
        if (EEPROM_Read(dst_addr + i) != pSrc[i]) {
            while (EEPROM_IsBusy());
            ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
                EEPROM_Write(dst_addr + i, pSrc[i]);
            }
        }
    }
    while (EEPROM_IsBusy());
}

static void read_eep_block(void* dst, uint16_t src_addr, size_t size)
{
    uint8_t* pDst = (uint8_t*)dst;
    for (size_t i = 0; i < size; i++) {
        pDst[i] = EEPROM_Read(src_addr + i);
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数">

void EM_loadEEPROM()
{
    // 初期化未了の場合、デフォルト書き込み
    if (EEPROM_Read(ADDR_INIT_FLAG) != 'T')
    {
        EM_resetEEPROM();
        while (EEPROM_IsBusy());
        EEPROM_Write(ADDR_INIT_FLAG, 'T');
    }

    // フィルタ
    while (EEPROM_IsBusy());
    SharedMemory.reg.filter_n = EEPROM_Read(ADDR_FILTER_NUMBER);
    last_filter_config = SharedMemory.reg.filter_n;

    // 係数A (native LE)
    read_eep_block(last_coefficientA, ADDR_COEF_A, sizeof(last_coefficientA));
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy((void*)SharedMemory.reg.coefficientA, last_coefficientA, sizeof(last_coefficientA));
    }

    // 係数B (native LE)
    read_eep_block(last_coefficientB, ADDR_COEF_B, sizeof(last_coefficientB));
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy((void*)SharedMemory.reg.coefficientB, last_coefficientB, sizeof(last_coefficientB));
    }

    // I2C アドレス -> new_addr に反映
    while (EEPROM_IsBusy());
    SharedMemory.reg.new_addr = EEPROM_Read(ADDR_I2C_ADDR);
    last_i2c_address = SharedMemory.reg.new_addr;

    // 装置ラベル (旧バージョンからの upgrade では未初期化 = 0xFF が読まれる)
    read_eep_block(last_name, ADDR_NAME, NODE_NAME_LEN);
    if ((unsigned char)last_name[0] == 0xFF) {
        memset(last_name, 0, NODE_NAME_LEN);
    }
    last_name[NODE_NAME_LEN - 1] = '\0';
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy((void*)SharedMemory.reg.name, last_name, NODE_NAME_LEN);
    }
}

void EM_updateEEPROM()
{
    // フィルタ
    if (last_filter_config != SharedMemory.reg.filter_n)
    {
        last_filter_config = SharedMemory.reg.filter_n;
        while (EEPROM_IsBusy());
        EEPROM_Write(ADDR_FILTER_NUMBER, SharedMemory.reg.filter_n);
    }

    // 係数A
    float currentA[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(currentA, (const void*)SharedMemory.reg.coefficientA, sizeof(currentA));
    }
    if (memcmp(last_coefficientA, currentA, sizeof(last_coefficientA)) != 0)
    {
        memcpy(last_coefficientA, currentA, sizeof(currentA));
        write_eep_block(last_coefficientA, ADDR_COEF_A, sizeof(last_coefficientA));
    }

    // 係数B
    float currentB[5];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(currentB, (const void*)SharedMemory.reg.coefficientB, sizeof(currentB));
    }
    if (memcmp(last_coefficientB, currentB, sizeof(last_coefficientB)) != 0)
    {
        memcpy(last_coefficientB, currentB, sizeof(currentB));
        write_eep_block(last_coefficientB, ADDR_COEF_B, sizeof(last_coefficientB));
    }

    // I2C アドレス
    if (last_i2c_address != SharedMemory.reg.new_addr)
    {
        last_i2c_address = SharedMemory.reg.new_addr;
        while (EEPROM_IsBusy());
        EEPROM_Write(ADDR_I2C_ADDR, SharedMemory.reg.new_addr);
    }

    // 装置ラベル
    char currentName[NODE_NAME_LEN];
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy(currentName, (const void*)SharedMemory.reg.name, NODE_NAME_LEN);
    }
    if (memcmp(last_name, currentName, NODE_NAME_LEN) != 0) {
        memcpy(last_name, currentName, NODE_NAME_LEN);
        write_eep_block(last_name, ADDR_NAME, NODE_NAME_LEN);
    }
}

void EM_resetEEPROM()
{
    // フィルタ
    while (EEPROM_IsBusy());
    EEPROM_Write(ADDR_FILTER_NUMBER, DEFAULT_FILTER_CONFIG);

    // 係数A (native LE)
    write_eep_block(DEFAULT_COEF_A, ADDR_COEF_A, sizeof(DEFAULT_COEF_A));

    // 係数B (native LE)
    write_eep_block(DEFAULT_COEF_B, ADDR_COEF_B, sizeof(DEFAULT_COEF_B));

    // I2C アドレス
    while (EEPROM_IsBusy());
    EEPROM_Write(ADDR_I2C_ADDR, DEFAULT_I2C_ADDRESS);

    // 装置ラベル: 空文字 (NUL 16 byte)
    char empty_name[NODE_NAME_LEN] = {0};
    write_eep_block(empty_name, ADDR_NAME, NODE_NAME_LEN);
}

// </editor-fold>
