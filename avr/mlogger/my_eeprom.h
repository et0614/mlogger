/**
 * @file my_eeprom.h
 * @brief AVR(ATMega328)のEEPROMを処理する
 * @author E.Togashi
 * @date 2021/12/19
 */

#ifndef MY_EEPROM_H_
#define MY_EEPROM_H_

class my_eeprom
{
	public:
		//自動通信開始設定
		volatile static bool startAuto;
	
		//補正係数
		volatile static float Cf_dbtA, Cf_dbtB, Cf_hmdA, Cf_hmdB, Cf_glbA, Cf_glbB, Cf_luxA, Cf_luxB, Cf_velA, Cf_velB, Cf_vel0;
		
		//計測真偽  th:温湿度, glb:グローブ温度, vel:微風速, ill:照度
		volatile static bool measure_th, measure_glb, measure_vel, measure_ill, measure_AD1, measure_AD2, measure_AD3, measure_Prox;
		
		//計測間隔  th:温湿度, glb:グローブ温度, vel:微風速, ill:照度
		volatile static unsigned int interval_th, interval_glb, interval_vel, interval_ill, interval_AD1, interval_AD2, interval_AD3;
		
		//名称
		static char mlName[20];

		//補正係数を書き込む
		static void SetCorrectionFactor(const char * data);

		//補正係数を表す文字列を作成する
		static void MakeCorrectionFactorString(char * txbuff, const char * command);
		
		//EEPROMを読み込む
		static void LoadEEPROM();
		
		//計測設定を書き込む
		static void SetMeasurementSetting();
		
		//名称を書き込む
		static void SaveName();
};

#endif /* MY_EEPROM_H_ */