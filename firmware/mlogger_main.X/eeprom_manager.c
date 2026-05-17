#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/nvm/nvm.h" //EEPROM処理
#include "eeprom_manager.h"
#include "parameters.h"
#include "crc.h"
#include <stddef.h>
#include <string.h>
#include <stdio.h>
#include <avr/io.h>
#include <util/atomic.h>

// AVR64DU32 EEPROM開始アドレス (定数として定義)
#define EEPROM_BASE_ADDR  0x1400

// EEPROM全体のマップを定義（型定義のみ）
typedef struct {
    uint8_t init_flag;      // 0x0000
    uint8_t xb_init_flag;   // 0x0001
    CorrectionFactors cFactors; 
    VelocityCharacteristicCoefficients vcCoefs;
    MeasurementSettings mSettings;
    char name[21];
    uint8_t gen_number;
} EepromMap;

// 自動的にアドレス数値に変換
#define ADDR_INIT_FLAG   (EEPROM_BASE_ADDR +offsetof(EepromMap, init_flag))
#define ADDR_XB_INIT     (EEPROM_BASE_ADDR +offsetof(EepromMap, xb_init_flag))
#define ADDR_CFACTORS    (EEPROM_BASE_ADDR +offsetof(EepromMap, cFactors))
#define ADDR_VCCOEFS     (EEPROM_BASE_ADDR +offsetof(EepromMap, vcCoefs))
#define ADDR_MSETTINGS   (EEPROM_BASE_ADDR +offsetof(EepromMap, mSettings))
#define ADDR_BATCONFIG   (EEPROM_BASE_ADDR +offsetof(EepromMap, bConfig))
#define ADDR_NAME        (EEPROM_BASE_ADDR +offsetof(EepromMap, name))
#define ADDR_GEN_NUMBER  (EEPROM_BASE_ADDR +offsetof(EepromMap, gen_number))

//データ世代数
uint8_t EM_generationNumber = 1;

//補正係数
CorrectionFactors EM_cFactors;

//風速特性係数
VelocityCharacteristicCoefficients EM_vcCoefficients;

//計測設定
MeasurementSettings EM_mSettings;

//名称
char EM_mlName[21];

// <editor-fold defaultstate="collapsed" desc="inline関数の定義">

inline static float max(float x, float y)
{
	return (x > y) ? x : y;
}

inline static float min(float x, float y)
{
	return (x > y) ? y : x;
}

// </editor-fold>

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

// <editor-fold defaultstate="collapsed" desc="書き込み処理">

//補正係数を書き込む
static void writeCFactors()
{    
	// CRCを計算
	EM_cFactors.crc = CRC_calc8(
        (uint8_t*)&EM_cFactors,
        sizeof(CorrectionFactors) - sizeof(EM_cFactors.crc) //crcメンバー自身のサイズは計算範囲から除外する
	);
    
	write_eep_block(&EM_cFactors, ADDR_CFACTORS, sizeof(CorrectionFactors));
}

//風速特性係数を書き込む
static void writeVCCoefficients()
{
	// CRCを計算
	EM_vcCoefficients.crc = CRC_calc8(
        (uint8_t*)&EM_vcCoefficients,
        sizeof(VelocityCharacteristicCoefficients) - sizeof(EM_vcCoefficients.crc) //crcメンバー自身のサイズは計算範囲から除外する
	);
	
	write_eep_block(&EM_vcCoefficients, ADDR_VCCOEFS, sizeof(VelocityCharacteristicCoefficients));
}

//計測設定を書き込む
static void writeMSettings()
{
	// CRCを計算
	EM_mSettings.crc = CRC_calc8(
        (uint8_t*)&EM_mSettings,
        sizeof(MeasurementSettings) - sizeof(EM_mSettings.crc) //crcメンバー自身のサイズは計算範囲から除外する
	);
	
	write_eep_block(&EM_mSettings, ADDR_MSETTINGS, sizeof(MeasurementSettings));
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="初期化処理">

static void initCFactors(){
	EM_cFactors = (CorrectionFactors){
		1, //バージョン
		DBT_COEF_A, //乾球温度a
		DBT_COEF_B, //乾球温度b
		HMD_COEF_A, //相対湿度a
		HMD_COEF_B, //相対湿度b
		GLB_COEF_A, //グローブ温度a
		GLB_COEF_B, //グローブ温度b
		1.0, //照度a
		0.0, //照度b
		1.0, //風速a
		0.0, //風速b
		0 //CRC（一旦0で初期化）
	};
}

static void initVCCoefficients(){
	EM_vcCoefficients = (VelocityCharacteristicCoefficients){
		1,		//バージョン
		VOL_VEL0,       //無風電圧
		VEL_COEF_A1,	//係数A1
		VEL_COEF_B1,    //係数B1
		VEL_COEF_A2,	//係数A2
		VEL_COEF_B2,	//係数B2
        VEL_SWITCH,     //切り替え風速
		0		//CRC（一旦0で初期化）
	};
}

static void initMSettings(){
	EM_mSettings = (MeasurementSettings){
        1,		//バージョン
		false, //自動測定開始
		true, //乾球温度の計測真偽
		true, //グローブ温度の計測真偽
		true, //風速の計測真偽
		true, //照度の計測真偽
		false, //汎用AD1の計測真偽
		false, //汎用AD2の計測真偽
		false, //汎用AD3の計測真偽
		false, //近接センサの計測真偽
		false, //CO2の計測真偽
		1, //乾球温度の計測間隔[sec]
		1, //グローブ温度の計測間隔[sec]
		1, //風速の計測間隔[sec]
		1, //照度の計測間隔[sec]
		1, //汎用AD1の計測間隔[sec]
		1, //汎用AD2の計測間隔[sec]
		1, //汎用AD3の計測間隔[sec]
		1, //近接センサの計測間隔[sec]
		1, //CO2の計測間隔[sec]
		1609459200,	//計測開始日時 (UNIX時間,UTC時差0で2021/1/1 00:00:00)
		0		//CRC（一旦0で初期化）
	};
}

//メモリを初期化する
static void initMemory()
{
	//補正係数
	initCFactors();
	writeCFactors();

	//風速計特性係数
	initVCCoefficients();
	writeVCCoefficients();
	
	//計測設定
	initMSettings();
	writeMSettings();
        
	//名前
	write_eep_block((const void *)ML_NAME, ADDR_NAME, sizeof(EM_mlName));
	
    //データ世代番号
    while(EEPROM_IsBusy());
    EEPROM_Write(ADDR_GEN_NUMBER, EM_generationNumber);
    
	//XBee初期化フラグ
    while(EEPROM_IsBusy());
    EEPROM_Write(ADDR_XB_INIT, 'F');
	
	//初期化フラグ
    while(EEPROM_IsBusy());
    EEPROM_Write(ADDR_INIT_FLAG, 'T');
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="読み込み処理">

//補正係数を読み込む
static void loadCFactors()
{
    read_eep_block(&EM_cFactors, ADDR_CFACTORS, sizeof(CorrectionFactors));

	// 読み込んだデータのCRCを検証
	uint8_t expected_crc = EM_cFactors.crc;
	uint8_t actual_crc = CRC_calc8(
		(uint8_t*)&EM_cFactors,
		sizeof(CorrectionFactors) - sizeof(EM_cFactors.crc)
	);

	// CRCが一致しない（データ破損）場合にはデフォルト値で再初期化
	if (expected_crc != actual_crc) initCFactors();
}

//風速の特性係数を読み込む
static void loadVCCoefficients()
{
	read_eep_block(&EM_vcCoefficients, ADDR_VCCOEFS, sizeof(VelocityCharacteristicCoefficients));

	// 読み込んだデータのCRCを検証
	uint8_t expected_crc = EM_vcCoefficients.crc;
	uint8_t actual_crc = CRC_calc8(
		(uint8_t*)&EM_vcCoefficients,
		sizeof(VelocityCharacteristicCoefficients) - sizeof(EM_vcCoefficients.crc)
	);

	// CRCが一致しない（データ破損）場合にはデフォルト値で再初期化
	if (expected_crc != actual_crc) initVCCoefficients();
}

//計測設定を読み込む
static void loadMSettings()
{
	read_eep_block(&EM_mSettings, ADDR_MSETTINGS, sizeof(MeasurementSettings));

	// 読み込んだデータのCRCを検証
	uint8_t expected_crc = EM_mSettings.crc;
	uint8_t actual_crc = CRC_calc8(
		(uint8_t*)&EM_mSettings,
		sizeof(MeasurementSettings) - sizeof(EM_mSettings.crc)
	);

	// CRCが一致しない（データ破損）場合にはデフォルト値で再初期化
	if (expected_crc != actual_crc) initMSettings();
}

//名称を読み込む
static void loadName()
{
	read_eep_block((void *)EM_mlName, ADDR_NAME, sizeof(EM_mlName));
}

// </editor-fold>


void EM_applySetCorrectionFactorCommand(const char data[])
{
	float buff;
	char num[5];
	num[4] = '\0';
	
	//乾球温度補正係数A
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EM_cFactors.dbtA = buff;
	//乾球温度補正係数B
	strncpy(num, data + 7, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		EM_cFactors.dbtB = buff;
	
	//相対湿度補正係数A
	strncpy(num, data + 11, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EM_cFactors.hmdA = buff;
	//相対湿度補正係数B
	strncpy(num, data + 15, 4);
	buff = 0.01 * atol(num);
	if(-9.99 <= buff && buff <= 9.99)
		EM_cFactors.hmdB = buff;
	
	//グローブ温度補正係数A
	strncpy(num, data + 19, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EM_cFactors.glbA = buff;
	//グローブ温度補正係数B
	strncpy(num, data + 23, 4);
	buff = 0.01 * atol(num);
	if(-3.0 <= buff && buff <= 3.0)
		EM_cFactors.glbB = buff;
	
	//照度補正係数A
	strncpy(num, data + 27, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EM_cFactors.luxA = buff;
	//照度補正係数B
	strncpy(num, data + 31, 4);
	buff = atol(num);
	if(-999 <= buff && buff <= 999)
		EM_cFactors.luxB = buff;
	
	//風速補正係数A
	strncpy(num, data + 35, 4);
	buff = 0.001 * atol(num);
	if(0.8 <= buff && buff <= 1.2)
		EM_cFactors.velA = buff;
	//風速補正係数B
	strncpy(num, data + 39, 4);
	buff = 0.001 * atol(num);
	if(-0.5 <= buff && buff <= 0.5)
		EM_cFactors.velB = buff;
	//風速無風電圧
	/*strncpy(num, data + 43, 4);
	buff = 0.001 * atol(num);
	if(1.40 <= buff && buff <= 1.50)
		EM_cFactors.vel0 = buff;*/
	
	//EEPROMに書き込む
	writeCFactors();
}

void EM_makeSetCorrectionFactorResponse(char * txbuff, const char * command)
{
	char dbtA[6],dbtB[6],hmdA[6],hmdB[6],glbA[6],glbB[6],luxA[6],luxB[5],velA[6],velB[7],vel0[6];
	
    sprintf(dbtA, "%5.3f", EM_cFactors.dbtA);
    sprintf(dbtB, "%5.2f", EM_cFactors.dbtB);

    sprintf(hmdA, "%5.3f", EM_cFactors.hmdA);
    sprintf(hmdB, "%5.2f", EM_cFactors.hmdB);

    sprintf(glbA, "%5.3f", EM_cFactors.glbA);
    sprintf(glbB, "%5.2f", EM_cFactors.glbB);

    sprintf(luxA, "%5.3f", EM_cFactors.luxA);
    sprintf(luxB, "%4.0f", EM_cFactors.luxB); // 精度0なので小数点以下なし

    sprintf(velA, "%5.3f", EM_cFactors.velA);
    sprintf(velB, "%6.3f", EM_cFactors.velB);
    sprintf(vel0, "%5.3f", EM_vcCoefficients.vel_swt);
    	
	sprintf(txbuff, "%s:%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\r",
		command, dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, vel0);
}

void EM_applySetVelocityCharacteristicsCommand(const char data[])
{
	float buff;
	char num[8];
	
	//無風電圧
	num[4] = '\0';
	strncpy(num, data + 3, 4);
	buff = 0.001 * atol(num);
	if(0.00 <= buff && buff <= 1.60)
        EM_vcCoefficients.vol0 = buff;
	
	//特性A, A1
	num[7] = '\0';
	strncpy(num, data + 7, 7);
	buff = 0.001 * atol(num);
	EM_vcCoefficients.ccA1 = buff;
	
	//特性B, B1
	strncpy(num, data + 14, 7);
	buff = 0.001 * atol(num);
	EM_vcCoefficients.ccB1 = buff;
	
	//特性C, A2
	strncpy(num, data + 21, 7);
	buff = 0.001 * atol(num);
	EM_vcCoefficients.ccA2 = buff;
    
    //Version 4.0以降
    if(28 <= strlen(data))
    {
        //特性B2
        strncpy(num, data + 28, 7);
        buff = 0.001 * atol(num);
        EM_vcCoefficients.ccA2 = buff;
        
        //切り替え風速
        strncpy(num, data + 35, 4);
        buff = 0.001 * atol(num);
        EM_vcCoefficients.vel_swt = buff;
    }

	writeVCCoefficients();
}

void EM_makeSetVelocityCharateristicsResponse(char * txbuff, const char * command)
{
	char vol0[6],vccA1[9],vccB1[9],vccA2[9],vccB2[9], vel_swt[6];
	
    sprintf(vol0, "%5.3f", EM_vcCoefficients.vol0);
    sprintf(vccA1, "%8.3f", EM_vcCoefficients.ccA1);
    sprintf(vccB1, "%8.3f", EM_vcCoefficients.ccB1);
    sprintf(vccA2, "%8.3f", EM_vcCoefficients.ccA2);
    sprintf(vccB2, "%8.3f", EM_vcCoefficients.ccB2);
	sprintf(txbuff, "%s:%s,%s,%s,%s,%s,%s\r",
        command, vol0, vccA1, vccB1, vccA2, vccB2, vel_swt);
	//sprintf(txbuff, "%s:%s,%s,%s,%s\r",
	//command, vol0, vccA1, vccB1, vccA2);
}

void EM_applyChangeMeasurementSettingsCommand(const char data[])
{
    //測定の是非
    EM_mSettings.measure_th = (data[3] == 't');
    EM_mSettings.measure_glb = (data[9] == 't');
    EM_mSettings.measure_vel = (data[15] == 't');
    EM_mSettings.measure_ill = (data[21] == 't');
    EM_mSettings.measure_AD1 = (data[37] == 't');
    EM_mSettings.measure_AD2 = (data[43] == 't');
    EM_mSettings.measure_AD3 = (data[49] == 't');
    EM_mSettings.measure_Prox = (data[55] == 't');
    //バージョンが低い場合の処理
    if(56 < strlen(data)) 
        EM_mSettings.measure_co2 = (data[56] == 't');
    else EM_mSettings.measure_co2 = false;

    //測定時間間隔
    char num[6];
    num[5] = '\0';
    strncpy(num, data + 4, 5);
    EM_mSettings.interval_th = atoi(num);
    strncpy(num, data + 10, 5);
    EM_mSettings.interval_glb = atoi(num);
    strncpy(num, data + 16, 5);
    EM_mSettings.interval_vel = atoi(num);
    strncpy(num, data + 22, 5);
    EM_mSettings.interval_ill = atoi(num);
    strncpy(num, data + 38, 5);
    EM_mSettings.interval_AD1 = atoi(num);
    strncpy(num, data + 44, 5);
    EM_mSettings.interval_AD2 = atoi(num);
    strncpy(num, data + 50, 5);
    EM_mSettings.interval_AD3 = atoi(num);
    //バージョンが低い場合の処理
    if(56 < strlen(data))
    {
        strncpy(num, data + 57,5);
        EM_mSettings.interval_co2 = atoi(num);
    }
    else EM_mSettings.interval_co2 = 60;

    //計測開始時刻
    char num2[11];
    num2[10] = '\0';
    strncpy(num2, data + 27, 10);
    EM_mSettings.start_dt = atol(num2);
    
	writeMSettings();	
}

void EM_makeMeasurementSettingsResponse(char * txbuff, const char * command)
{    
	sprintf(txbuff, "%s:%d,%u,%d,%u,%d,%u,%d,%u,%ld,%d,%u,%d,%u,%d,%u,%d,%d,%u\r",
        command, 
        EM_mSettings.measure_th, EM_mSettings.interval_th, 
        EM_mSettings.measure_glb, EM_mSettings.interval_glb, 
        EM_mSettings.measure_vel, EM_mSettings.interval_vel, 
        EM_mSettings.measure_ill, EM_mSettings.interval_ill, 
        EM_mSettings.start_dt,
        EM_mSettings.measure_AD1, EM_mSettings.interval_AD1, 
        EM_mSettings.measure_AD2, EM_mSettings.interval_AD2, 
        EM_mSettings.measure_AD3, EM_mSettings.interval_AD3,
        EM_mSettings.measure_Prox,
        EM_mSettings.measure_co2, EM_mSettings.interval_co2);
}

//計測設定を保存する
void EM_saveMeasurementSetting()
{
	writeMSettings();	
}

//名称を書き込む
void EM_saveName()
{
	write_eep_block((const void *)EM_mlName, ADDR_NAME, sizeof(EM_mlName));
}

//データ世代番号を書き込む
void EM_saveGenerationNumber()
{
    while(EEPROM_IsBusy());
    EEPROM_Write(ADDR_GEN_NUMBER, EM_generationNumber);
}

//設定を読み込む
void EM_loadEEPROM()
{
	if (EEPROM_Read(ADDR_INIT_FLAG) != 'T') initMemory();
	loadCFactors();
	loadVCCoefficients();
	loadMSettings();
    EM_generationNumber = EEPROM_Read(ADDR_GEN_NUMBER);
	loadName();
}

//XBeeが初期化済か否かを取得する
bool EM_isXBeeInitialized(){
	return EEPROM_Read(ADDR_XB_INIT) == 'T';
}

//XBee初期化を記録する
void EM_xbeeInitialized(){
    while(EEPROM_IsBusy());
   	EEPROM_Write(ADDR_XB_INIT, 'T');
}