/**
 * @file main.c
 * @brief AVR(AVR64DU32)を使用した計測データ収集・送信プログラム
 * @author E.Togashi
 * @date 2025/12/13
 */

/**XBee端末の設定****************************************
 * ・親機子機共通
 *   1) Firmwareは、Product family:「XB3-24」,Function set:「Digi XBee3 Zigbee 3.0」,Firmware version:「1010」
 *   2) PAN IDを同じ値にする
 *   3) SP:Cyclic Sleep Period = 0x64（=1000 msec）
 *      SN:Number of Cyclic Sleep Periods = 3600
 *      これで3600×3=3hourはネットワークから外れない
 *   4) AP:API Mode Without Escapes[1]
 * ・親機のみ
 *   1) CE:Coordinator Enable = Enabled
 *   2) SM:Sleep Mode = No sleep
 *   3) AR:many-to-one routing = 0
 *   4) NJ:Node Join Time = FF（時間無制限にネットワーク参加可能）
 * ・子機のみ
 *   1) CE:Coordinator Enable = Join Network [0]
 *   2) SM:Sleep Mode = Pin Hibernate [1]（ATMegaからの指令でスリープ解除するため）
 *   以下はBluetooth対応の場合のみ
 *   3) BT:Bluetooth Enable = Enabled [1]
 *   4) BI:Bluetooth Identifier = "MLogger_xxxx"（xxxxは適当な桁数の数字でIDとして使う）
 *   5) Bluetooth Authenticationに「ml_pass」
*********************************************************/

// <editor-fold defaultstate="collapsed" desc="ヘッダインクルード">

//自動生成ヘッダ
#include "mcc_generated_files/system/clock.h" //F_CPUの設定
#include "mcc_generated_files/system/system.h"
#include "mcc_generated_files/adc/adc0.h" // ADC（電圧計測）
#include "mcc_generated_files/timer/delay.h"

//自作ヘッダ
#include "main.h"
#include "hal_io.h"
#include "logger_control.h"
#include "adc0_extension.h" //AD変換拡張
#include "usb_extension.h" //USBブロック転送のための拡張機能
#include "eeprom_manager.h" //EEPROM
#include "xbee_controller.h" //XBee通信
#include "anemometer.h" //風速計

#include "sht4x.h" //debug

//標準ヘッダ
#include <avr/sleep.h>

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="広域変数定義">

//1秒毎の処理実行フラグ
volatile bool process_logging_flag = false;

//風速の電圧計測フラグ
volatile bool velocity_adc_flag = false;

//WFCを送信用タイマ[sec]
static uint8_t wfc_timer = 0;

//電池不足継続タイマ[sec]
static uint8_t lowBattery_timer = 0;

//リセット処理用タイマ[sec]
static uint8_t reset_timer = 0;

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="main">

int main(void)
{
    SYSTEM_Initialize();    
    
    //風速ADCの初期化
    ADC_VEL_Initialize();
    
    // SPIモジュールを有効化
    SPI0_Open(SPI0_DEFAULT);
    
    //EEPROMロード
    EM_loadEEPROM();
        
    //低電圧が1秒継続したら電池残量エラー
	int count = 0;
	while(isLowBattery()){
		count++;
		if(10 <= count)	showError(1);
        DELAY_milliseconds(100);
	}
    
    // イベントハンドラ登録
    RTC_SetPITIsrCallback(oneSecHandler); // 1sec割り込み
    RST_SetInterruptHandler(resetButtonHandler); // リセットスイッチ押し込み割り込み
    
    //割り込み許可
	DELAY_milliseconds(100); //割り込み前の待機
    sei();
        
    //センサ初期化処理
    LC_InitSensors();
    
    //XBeeスリープ解除
    Xbee_Wakeup();
    
    //初期設定が終わったら少し待機
	DELAY_milliseconds(100);
    
    //XBee設定確認
	if(!Xbee_Initialize()) showError(2);

    // USB仮想シリアル通信の初期化
    USB_CDCVirtualSerialPortInitialize();
    
    //通信再開フラグを確認
	if(EM_mSettings.start_auto)
        LC_StartLoggingTask(true, false, false, false); //Zigbeeに書き出し
    
    //10秒以上電圧不足時間が継続したら終了
    while (lowBattery_timer <= 10)
    {
		// XBeeコマンド受信と処理
        Xbee_LoadUART();
            
		//1秒毎の処理を実施
		if(process_logging_flag)
        {
			process_logging_flag = false;
			executeSecondlyTask();
            
            //XBeeの使用状況を確認してスリープ・起動・再接続などを実行
            Xbee_InterfaceConfig_t xb_cfg;
            xb_cfg.zigbee_enabled = LC_UseZigbeeConnection();
            xb_cfg.ble_enabled = LC_UseBLEConnection();
            Xbee_MaintainTask(xb_cfg);
		}

        //ロギング中は状態に応じてスリープ
        if(LC_IsLogging())
        {
            // USB を出力 transport に指定した場合はスリープせず USB タスクを駆動 (smp/dump_end 等を流す)
            if(LC_OutputToUSB())
            {
                USBDevice_Handle();
                USB_CDCVirtualSerialPortHandler();
                if (USB_DescriptorActiveConfigurationValueGet() == 1) USB_Stream_Task();
            }
            else
            {
                set_sleep_mode((!Xbee_IsSleeping() || EM_mSettings.start_auto) ? SLEEP_MODE_IDLE : SLEEP_MODE_PWR_DOWN);
                sleep_mode();
            }
        }
        else
        {
            //USBの接続状況を確認
            USBDevice_Handle(); // USB通信基本タスク
            USB_CDCVirtualSerialPortHandler(); // CDCタスク
            if (USB_DescriptorActiveConfigurationValueGet() == 1) USB_Stream_Task(); // コマンド処理
        }
    }
    
    //電池が不足した場合のみ、ここまでたどり着く
	cli(); //割り込み終了
	showError(1); //LED表示
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="割り込みコールバック関数">

// 1秒ごとのコールバック関数
void oneSecHandler(void)
{
    LC_TickSecond();
	
	//1秒毎の処理フラグを立てる
	process_logging_flag = true;
}

// リセットスイッチ操作時のコールバック関数
void resetButtonHandler(void)
{
    //Push:LED点灯, None:LED消灯
    if(RST_GetValue()) turnOffRedLED();
	else turnOnRedLED();
}

// </editor-fold>

void executeSecondlyTask(void)
{
    //電圧確認（限界まで使うとFlashメモリのデータが破損することがある）
	if(isLowBattery()) lowBattery_timer++;
	else lowBattery_timer = 0;
	
	//CO2センサ接続確認
    LC_CheckCO2Connection();
	
	//リセットボタン押し込み確認********************************
	if(!RST_GetValue())
	{
		reset_timer++;
		if(reset_timer == 3)
		{
			//Endlessロギングを解除
			EM_mSettings.start_auto = false;
			EM_saveMeasurementSetting();
            blinkRedLED(3);	//赤LED点滅
            
            // マイコン自体をソフトウェアリセット
            _PROTECTED_WRITE(RSTCTRL.SWRR, RSTCTRL_SWRST_bm);
		}
	}
	else reset_timer = 0; //Resetボタン押し込み時間を0に戻す
	
    //センシングタスクを実施
    LC_ProcessSensingTask();
    //センシングタスクがない場合
    if(!LC_HasTask())
    {
        Anemometer_Sleep(); //風速計を停止
		
		//定期的にコマンド待受状態を送信
		if(wfc_timer <= 0)
		{
            //Waiting for command.
			Xbee_BlTxChars("WFC\r");
			wfc_timer = 6;
		}
		wfc_timer--;
		
		// Reset押し込み中でなければ明滅
		if(RST_GetValue()) blinkGreenLED(1);
    }
}

//電池残量が小さくなったか否か
bool isLowBattery(void)
{    
    //debug
    return false;
    
    // 基準電圧を「VDD (3.3V)」に設定
    ADC0_SetReferenceVoltage(ADC_VREF_VDD);

    ADC0_SampleRepeatCountSet(ADC_8_SAMPLES_ACCUMULATED); //積算回数
    ADC0_ChannelSelect(ADC0_CHANNEL_AIN3); // チャンネルを選択
    ADC0_ConversionStart(); // 変換開始
    while(!ADC0_IsConversionDone()); // 完了待ち (BUSYフラグが落ちるのを待つ)
    adc_accumulate_t acc_val = ADC0_AccumulatedResultGet(); // 結果を取得
    
    // 電圧計算（基準電圧=3300mV）
    uint32_t battery_mv = (uint32_t)acc_val * 3300 / 8192; //1024*8 (10bit,8回平均)
    
    //1.8V（0.9V/本)以下で電池不足と判定
    return battery_mv < 1800;
}

//エラー表示
void showError(short int errNum)
{	
	switch(errNum){
		case 1: //電池不足
			while(true)
			{
				blinkRedLED(1);
				DELAY_milliseconds(3000);
			}
		case 2: //XBee設定エラー
			while(true)
			{
				blinkRedLED(2);
				DELAY_milliseconds(3000);
			}
	}
}


// <editor-fold defaultstate="collapsed" desc="ダミーデータ作成処理">

/*#define DUMMY_RECORD_COUNT 10000 //52429
#define START_TIMESTAMP    1764514800 // 2025-12-01 00:00:00 (JST)
#define INTERVAL_SECONDS   60        // 10分

void genDummyData(void)
{
    SensorData_t dummy;
    uint32_t currentTimestamp = START_TIMESTAMP;

    // データの書き込み ---
    for (uint32_t i = 0; i < DUMMY_RECORD_COUNT; i++)
    {
        dummy.generation  = EM_generationNumber;
        dummy.timestamp   = currentTimestamp;
        dummy.illuminance = 1000 + (i % 100);
        dummy.valid_flags = FLAG_ILLUMINANCE | FLAG_TEMP_DRY | FLAG_TEMP_GLOBE | FLAG_HUMIDITY | FLAG_WIND_SPEED | FLAG_VOLTAGE | FLAG_CO2_PPM;
        dummy.temp_dry    = 2500 + (i % 100);
        dummy.temp_globe  = 2600 + (i % 100);
        dummy.humidity    = 5000 + (i % 1000);
        dummy.wind_speed  = 100;
        dummy.voltage     = 3300;
        dummy.co2_ppm     = 800;

        if (!W25_WriteRecord(i, &dummy)) while(1); // エラー停止
        currentTimestamp += INTERVAL_SECONDS;
        
        if (i % 10000 == 0) blinkRedLED(1);
    }
}*/

// </editor-fold>