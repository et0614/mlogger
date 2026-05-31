#include "mcc_generated_files/nvm/nvm.h"
#include "eeprom_manager.h"
#include "i2c_shared_data.h"

#include <string.h>
#include <util/atomic.h>

// AVR128DB32 EEPROM 開始アドレス
#define EEPROM_BASE_ADDR  0x1400

// EEPROM レイアウト (native LE で保存)。
// init_flag = 'U' は本レイアウト (4 計測値・8 係数版) を示すマジック。
// 旧版 'T' (T/RH 2 計測値・4 係数) で書かれた EEPROM は EM_loadEEPROM で
// 不一致 → EM_resetEEPROM が走り、新レイアウトのデフォルトで上書きされる。
typedef struct {
    uint8_t init_flag;                  // 'U' なら本レイアウトで初期化済み
    uint8_t i2c_addr;                   // I2C アドレス
    uint8_t reserved[2];                // 4byte alignment
    float   t_coef_a;
    float   t_coef_b;
    float   rh_coef_a;
    float   rh_coef_b;
    float   co2_coef_a;
    float   co2_coef_b;
    float   t_glb_coef_a;
    float   t_glb_coef_b;
    char    name[NODE_NAME_LEN];        // 装置ラベル (NUL 終端)
} EepromMap;

#define ADDR_INIT_FLAG       (EEPROM_BASE_ADDR + offsetof(EepromMap, init_flag))
#define ADDR_I2C_ADDR        (EEPROM_BASE_ADDR + offsetof(EepromMap, i2c_addr))
#define ADDR_T_COEF_A        (EEPROM_BASE_ADDR + offsetof(EepromMap, t_coef_a))
#define ADDR_T_COEF_B        (EEPROM_BASE_ADDR + offsetof(EepromMap, t_coef_b))
#define ADDR_RH_COEF_A       (EEPROM_BASE_ADDR + offsetof(EepromMap, rh_coef_a))
#define ADDR_RH_COEF_B       (EEPROM_BASE_ADDR + offsetof(EepromMap, rh_coef_b))
#define ADDR_CO2_COEF_A      (EEPROM_BASE_ADDR + offsetof(EepromMap, co2_coef_a))
#define ADDR_CO2_COEF_B      (EEPROM_BASE_ADDR + offsetof(EepromMap, co2_coef_b))
#define ADDR_T_GLB_COEF_A    (EEPROM_BASE_ADDR + offsetof(EepromMap, t_glb_coef_a))
#define ADDR_T_GLB_COEF_B    (EEPROM_BASE_ADDR + offsetof(EepromMap, t_glb_coef_b))
#define ADDR_NAME            (EEPROM_BASE_ADDR + offsetof(EepromMap, name))

// マジック値 (本レイアウト)
#define INIT_FLAG_MAGIC      ('U')

// デフォルト値
// 0x10 は風速プローブが使うので、温湿度+CO2+グローブ温度プローブは 0x11 に割り当てる。
// (mlogger_main 側 th_probe.c の TH_PROBE_ADDRESS と必ず同期させること)
#define DEFAULT_I2C_ADDRESS  (0x11)
#define DEFAULT_COEF_A       (1.0f)
#define DEFAULT_COEF_B       (0.0f)

// 補正係数を a/b ペアで扱うための index (差分検出ループの簡略化用)
typedef struct {
    float    a;
    float    b;
    uint16_t addr_a;
    uint16_t addr_b;
} CoefEntry;

#define COEF_COUNT  4   // T, RH, CO2, T_glb

// 直近書き込み値 (差分書き込み判定用)
static uint8_t   last_i2c_address;
static CoefEntry last_coefs[COEF_COUNT];
static char      last_name[NODE_NAME_LEN];

// <editor-fold defaultstate="collapsed" desc="EEPROM ブロック読み書き">

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

// <editor-fold defaultstate="collapsed" desc="補正係数の SharedMemory ↔ ローカルキャッシュ橋渡し">

// SharedMemory.reg の各補正係数フィールドの読み書きヘルパ。
// SensorData_t が個別 float メンバなのでアクセスはインデックスで分岐する。
static float get_coef_from_shared(uint8_t idx, bool is_b)
{
    float v = 0.0f;
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        switch (idx) {
            case 0: v = is_b ? SharedMemory.reg.t_coef_b      : SharedMemory.reg.t_coef_a;      break;
            case 1: v = is_b ? SharedMemory.reg.rh_coef_b     : SharedMemory.reg.rh_coef_a;     break;
            case 2: v = is_b ? SharedMemory.reg.co2_coef_b    : SharedMemory.reg.co2_coef_a;    break;
            case 3: v = is_b ? SharedMemory.reg.t_glb_coef_b  : SharedMemory.reg.t_glb_coef_a;  break;
        }
    }
    return v;
}

static void set_coef_to_shared(uint8_t idx, bool is_b, float v)
{
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        switch (idx) {
            case 0: if (is_b) SharedMemory.reg.t_coef_b      = v; else SharedMemory.reg.t_coef_a      = v; break;
            case 1: if (is_b) SharedMemory.reg.rh_coef_b     = v; else SharedMemory.reg.rh_coef_a     = v; break;
            case 2: if (is_b) SharedMemory.reg.co2_coef_b    = v; else SharedMemory.reg.co2_coef_a    = v; break;
            case 3: if (is_b) SharedMemory.reg.t_glb_coef_b  = v; else SharedMemory.reg.t_glb_coef_a  = v; break;
        }
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数">

void EM_loadEEPROM(void)
{
    if (EEPROM_Read(ADDR_INIT_FLAG) != INIT_FLAG_MAGIC)
    {
        EM_resetEEPROM();
        while (EEPROM_IsBusy());
        EEPROM_Write(ADDR_INIT_FLAG, INIT_FLAG_MAGIC);
    }

    // 補正係数アドレステーブル (load / update の両方で使う)
    const uint16_t addr_a[COEF_COUNT] = {
        ADDR_T_COEF_A,     ADDR_RH_COEF_A,    ADDR_CO2_COEF_A,   ADDR_T_GLB_COEF_A
    };
    const uint16_t addr_b[COEF_COUNT] = {
        ADDR_T_COEF_B,     ADDR_RH_COEF_B,    ADDR_CO2_COEF_B,   ADDR_T_GLB_COEF_B
    };

    // I2C アドレス
    while (EEPROM_IsBusy());
    SharedMemory.reg.new_addr = EEPROM_Read(ADDR_I2C_ADDR);
    last_i2c_address = SharedMemory.reg.new_addr;

    // 全補正係数を EEPROM から読み、SharedMemory に反映 + ローカルキャッシュ更新
    for (uint8_t i = 0; i < COEF_COUNT; i++) {
        last_coefs[i].addr_a = addr_a[i];
        last_coefs[i].addr_b = addr_b[i];
        read_eep_block(&last_coefs[i].a, addr_a[i], sizeof(float));
        read_eep_block(&last_coefs[i].b, addr_b[i], sizeof(float));
        set_coef_to_shared(i, false, last_coefs[i].a);
        set_coef_to_shared(i, true,  last_coefs[i].b);
    }

    // 装置ラベル (旧バージョンからの upgrade で未初期化 = 0xFF が読まれる対策)
    read_eep_block(last_name, ADDR_NAME, NODE_NAME_LEN);
    if ((unsigned char)last_name[0] == 0xFF) {
        memset(last_name, 0, NODE_NAME_LEN);
    }
    last_name[NODE_NAME_LEN - 1] = '\0';
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        memcpy((void*)SharedMemory.reg.name, last_name, NODE_NAME_LEN);
    }
}

void EM_updateEEPROM(void)
{
    // I2C アドレス
    if (last_i2c_address != SharedMemory.reg.new_addr)
    {
        last_i2c_address = SharedMemory.reg.new_addr;
        while (EEPROM_IsBusy());
        EEPROM_Write(ADDR_I2C_ADDR, SharedMemory.reg.new_addr);
    }

    // 全補正係数を差分書き込み
    for (uint8_t i = 0; i < COEF_COUNT; i++) {
        float cur_a = get_coef_from_shared(i, false);
        float cur_b = get_coef_from_shared(i, true);
        if (memcmp(&last_coefs[i].a, &cur_a, sizeof(float)) != 0) {
            last_coefs[i].a = cur_a;
            write_eep_block(&last_coefs[i].a, last_coefs[i].addr_a, sizeof(float));
        }
        if (memcmp(&last_coefs[i].b, &cur_b, sizeof(float)) != 0) {
            last_coefs[i].b = cur_b;
            write_eep_block(&last_coefs[i].b, last_coefs[i].addr_b, sizeof(float));
        }
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

void EM_resetEEPROM(void)
{
    // I2C アドレス
    while (EEPROM_IsBusy());
    EEPROM_Write(ADDR_I2C_ADDR, DEFAULT_I2C_ADDRESS);

    // 全補正係数を恒等変換 (a=1.0, b=0.0) で初期化
    const float def_a = DEFAULT_COEF_A;
    const float def_b = DEFAULT_COEF_B;
    const uint16_t addr_a[COEF_COUNT] = {
        ADDR_T_COEF_A, ADDR_RH_COEF_A, ADDR_CO2_COEF_A, ADDR_T_GLB_COEF_A
    };
    const uint16_t addr_b[COEF_COUNT] = {
        ADDR_T_COEF_B, ADDR_RH_COEF_B, ADDR_CO2_COEF_B, ADDR_T_GLB_COEF_B
    };
    for (uint8_t i = 0; i < COEF_COUNT; i++) {
        write_eep_block(&def_a, addr_a[i], sizeof(float));
        write_eep_block(&def_b, addr_b[i], sizeof(float));
    }

    // 装置ラベル: 空文字 (NUL 16 byte)
    char empty_name[NODE_NAME_LEN] = {0};
    write_eep_block(empty_name, ADDR_NAME, NODE_NAME_LEN);
}

// </editor-fold>
