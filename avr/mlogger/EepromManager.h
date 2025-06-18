/**
 * @file EepromManager.h
 * @brief AVR(ATMega328)のEEPROMを処理する
 * @author E.Togashi
 * @date 2021/12/19
 */

#ifndef EEPROM_MANAGER_H_
#define EEPROM_MANAGER_H_

// 補正係数
struct CorrectionFactors {
	uint16_t version; //バージョン
	float dbtA; //乾球温度a
	float dbtB; //乾球温度b
	float hmdA; //相対湿度a
	float hmdB; //相対湿度b
	float glbA; //グローブ温度a
	float glbB; //グローブ温度b
	float luxA; //照度a
	float luxB; //照度b
	float velA; //風速a
	float velB; //風速b
	float vel0; //無風時
	uint8_t crc; //CRC
};

// 風速特性係数
struct VelocityCharacteristicCoefficients{
	uint16_t version; //バージョン
	float ccA;
	float ccB;
	float ccC;
	uint8_t crc; //CRC
};

//計測設定
struct MeasurementSettings{
	uint16_t version; //バージョン
	bool start_auto; //自動測定開始
	bool measure_th; //乾球温度の計測真偽
	bool measure_glb; //グローブ温度の計測真偽
	bool measure_vel; //風速の計測真偽
	bool measure_ill; //照度の計測真偽
	bool measure_AD1; //汎用AD1の計測真偽
	bool measure_AD2; //汎用AD2の計測真偽
	bool measure_AD3; //汎用AD3の計測真偽
	bool measure_Prox; //近接センサの計測真偽
	bool measure_co2; //CO2の計測真偽
	unsigned int interval_th; //乾球温度の計測間隔[sec]
	unsigned int interval_glb; //グローブ温度の計測間隔[sec]
	unsigned int interval_vel; //風速の計測間隔[sec]
	unsigned int interval_ill; //照度の計測間隔[sec]
	unsigned int interval_AD1; //汎用AD1の計測間隔[sec]
	unsigned int interval_AD2; //汎用AD2の計測間隔[sec]
	unsigned int interval_AD3; //汎用AD3の計測間隔[sec]
	unsigned int interval_Prox; //近接センサの計測間隔[sec]
	unsigned int interval_co2; //CO2の計測間隔[sec]
	uint32_t start_dt;	//計測開始日時
	uint8_t crc; //CRC
};

class EepromManager
{
	public:
		//補正係数
		static CorrectionFactors cFactors;

		//風速特性係数
		static VelocityCharacteristicCoefficients vcCoefficients;
		
		//計測設定
		static MeasurementSettings mSettings;
		
		//名称
		static char mlName[21];

		//補正係数を書き込む
		static void setCorrectionFactor(const char * data);

		//補正係数を表す文字列を作成する
		static void makeCorrectionFactorString(char * txbuff, const char * command);

		//風速の特性係数を書き込む
		static void setVelocityCharacteristics(const char * data);
		
		//風速の特性係数を表す文字列を作成する
		static void makeVelocityCharateristicsString(char * txbuff, const char * command);
		
		//EEPROMを読み込む
		static void loadEEPROM();
		
		//計測設定を書き込む
		static void setMeasurementSetting();
		
		//名称を書き込む
		static void saveName();
		
		//XBeeが初期化済か否かを取得する
		static bool isXBeeInitialized();
		
		//XBee初期化を記録する
		static void xbeeInitialized();
};

#endif /* EEPROM_MANAGER_H_ */