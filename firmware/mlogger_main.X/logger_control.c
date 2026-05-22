// <editor-fold defaultstate="collapsed" desc="include">
#include "hal_io.h"
#include "logger_control.h"
#include "eeprom_manager.h"
#include "xbee_controller.h"
#include "smAverage.h" //平均化ユーティリティ
#include "stcc4.h" //CO2センサ
#include "sht4x.h" //温湿度センサ
#include "opt3001.h" //照度センサ
#include "anemometer.h"
#include "adc0_extension.h" //AD変換拡張
#include "command_handler.h" //コマンド処理
#include "protocol_events.h" //v4 自発イベント送出

#include <util/atomic.h>
#include <time.h>

//</editor-fold>

// <editor-fold defaultstate="collapsed" desc="構造体定義">

//計測時間間隔を保持する構造体
typedef struct {
	int th;  //温湿度
	int glb; //グローブ温度
	int vel; //微風速
	int ill; //照度
	int ad1; //汎用AD
	int co2; //CO2濃度
} MeasurementPassCounters;

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="定数宣言">

//熱線式風速計の立ち上げに必要な時間[sec]
#define V_WAKEUP_TIME  20

//照度計カバーアクリル板の透過率
#define TRANSMITTANCE  0.60

//CO2センサの初期化待機時間（22秒必要）
#define CO2_CONDITIONING_SECONDS  25

//CO2センサの接続確認時間間隔[sec]
#define CO2_CHECK_INTERVAL  10

// 時刻同期 (spec 5.4 time_sync_request)
#define TIME_SYNC_INTERVAL_S  86400   // 24h ごとに time_sync_request を送出
#define TIME_SYNC_WINDOW_S    30      // event 送出後に親機 set_time 受信を待つ秒数

//</editor-fold>

// <editor-fold defaultstate="collapsed" desc="変数宣言">

//現在時刻（UNIX時間,UTC時差0で2000/1/1 00:00:00）
static volatile time_t currentTime = UNIX_OFFSET;

//計測中か否か
static bool logging = false;

// 時刻同期スケジュール: 0 なら未設定 (= 計測中でない/同期予定無し)。
// 計測開始時に次の UTC midnight にセットされ、以降は SetTime 成功で +24h、
// または window タイムアウト時にも +24h ずらして再試行する。
static time_t time_sync_next_unix = 0;

// 時刻同期 wake window の残秒数。> 0 の間 XBee を sleep させず set_time 受信を待つ。
static volatile uint16_t time_sync_window_remaining = 0;

// 1秒タスク内で「time_sync_request を emit すべき」フラグ。LC_TickSecond 内では
// USART を叩きたくない (割り込み文脈なので) ので、ここでフラグだけ立てて
// 後段 LC_ProcessTimeSyncTask で emit する。
static volatile bool time_sync_emit_pending = false;

//コマンド
static bool outputToBLE=false; //Bluetooth接続に書き出すか否か
static bool outputToZigbee=true; //ZigBee接続に書き出すか否か
static bool outputToFM=false; //Flash Memoryに書き出すか否か
static bool outputToUSB = false; //USB CDCに書き出すか否か

//計測実行時の点滅管理
static uint8_t blinkCount = 0;

//計測時間間隔
static MeasurementPassCounters pass_counters = {0};

//CO2センサ関連
static bool hasCO2Sensor = false;        //CO2センサを持つか否か
static uint8_t co2_connection_check_timer = 0; //CO2センサの接続確認用タイマ
volatile static uint8_t co2_condition_time = 0;	 //CO2センサの通電時起動時間[sec]
static uint32_t co2InitializingTime = 0; //CO2センサの12時間初期化の残り時間[sec]
static uint8_t co2CalibratingTime = 0;   //CO2校正残時間
static uint16_t reforcedCO2Level = 400;  //強制校正CO2濃度[ppm]
static SmAverage smaCO2; // 60秒平均を計算するインスタンス

//風速センサ
Anemometer_t anemometer;

static bool hasTask = false;

/**
 * @brief データ送信範囲の管理変数
 * "DUMP"コマンド受信時に、この範囲のレコードを送信する。
 * recordIndex単位で管理。
 */
uint32_t rec_latest; // 最新データの書き込み位置インデックス

//</editor-fold>

// <editor-fold defaultstate="collapsed" desc="inline関数の定義">

inline static float max(float x, float y)
{
	return (x > y) ? x : y;
}

inline static float min(float x, float y)
{
	return (x < y) ? x : y;
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="内部関数の定義">

// (旧 create_sensor_string は v4 移行で削除。protocol_events.c の pe_emit_smp が JSON で送出)

//きりの良い時刻になるように最初の計測時間間隔を調整する
static int getNormTime(struct tm time, unsigned int interval)
{	
	if(interval == 1) return interval; //1secの場合には直ちに計測
	if(interval <= 5) return interval - (5 - time.tm_sec % 5);
	else if(interval <= 10) return interval - (10 - time.tm_sec % 10);
	else if(interval <= 30) return interval - (30 - time.tm_sec % 30);
	else return interval - (60 - time.tm_sec % 60);
}

// (旧 calibrateVelocityVoltage は v4 移行で削除: 風速校正は風速プローブ側 MCU に移管)

static void calibrateCO2Level()
{
	//LED点灯
	blinkGreenAndRedLED(1);
	
	co2CalibratingTime--;
	
	//現在のCO2濃度を取得
	uint16_t co2_u = 0;
	float tmp_f = 0;
	float hmd_f = 0;
    STCC4_readMeasurement(&co2_u, &tmp_f, &hmd_f);
	
	//校正終了
	if(co2CalibratingTime == 0){
		STCC4_stopContinuousMeasurement();
		_delay_ms(1500); //連続計測の停止まで1200msecの待機が必要

		int16_t correction_val = 0;
		const char *state = "fail";
		if(STCC4_performForcedRecalibration(reforcedCO2Level, &correction_val))
		{
			if (correction_val == (int16_t)0xFFFF) {
				state = "fail";
				correction_val = 0;
			} else {
				state = "pass";
			}
		}

		STCC4_startContinuousMeasurement(); //連続計測再開
		pe_emit_co2_calibration_progress(0, state, correction_val, co2_u);
	}
	else
	{
		//残り時間を出力
		pe_emit_co2_calibration_progress(co2CalibratingTime, "measuring", 0, co2_u);
	}
}

static void performCO2InitializationProcess(){
	//LED点灯
	blinkGreenAndRedLED(1);
	
	co2InitializingTime--;
	
	//現在のCO2濃度を取得
	uint16_t co2_u = 0;
	float tmp_f = 0;
	float hmd_f = 0;
    STCC4_readMeasurement(&co2_u, &tmp_f, &hmd_f);
	
	//12時間が経過したら設定濃度で初期化
	if(co2InitializingTime == 0){
		STCC4_stopContinuousMeasurement();
		_delay_ms(1500); //連続計測終了には1200msecの待機が必要
		int16_t correction_val;
		STCC4_performForcedRecalibration(reforcedCO2Level, &correction_val);
		STCC4_startContinuousMeasurement(); //連続測定再開
	}
}

void execLogging(void)
{	
	//自動計測開始がOffで計測開始時刻の前ならば終了
	time_t current_snapshot = LC_GetCurrentTime();
	if(!EM_mSettings.start_auto && current_snapshot < EM_mSettings.start_dt) return;
	
	//ロギング中は5秒ごとに点滅
	blinkCount++;
	if(5 <= blinkCount)
	{
        blinkGreenLED(1);
		blinkCount = 0;
	}
	
	//計測のWAKEUP_TIME[sec]前から熱線式風速計回路のスリープを解除して加熱開始
	if(EM_mSettings.measure_vel && EM_mSettings.interval_vel - pass_counters.vel < V_WAKEUP_TIME) Anemometer_Wakeup();
	
	bool send_needed = false;
    SensorData_t data = {0};
    data.generation = EM_generationNumber;
    data.timestamp = (uint32_t)current_snapshot;
	
	//微風速測定************
	pass_counters.vel++;
    if(EM_mSettings.measure_vel)
    {
        // 計測時刻に到達
        if((int)EM_mSettings.interval_vel <= pass_counters.vel)
        {
            send_needed = true;
            pass_counters.vel = 0;
            
            LC_Update_Anemometer();
            // 子機切断 / status1 異常時は valid_flag を立てない
            // (= load_data.py 等で空欄になり、ゴミ値 65000 等が混入しない)
            if (anemometer.wind_valid) {
                data.wind_speed = anemometer.wind_speed_mps * 10000;
                data.valid_flags |= FLAG_WIND_SPEED;
            }
            if (anemometer.voltage_valid) {
                data.voltage = anemometer.adc_value;
                data.valid_flags |= FLAG_VOLTAGE;
            }

            //次の起動時刻が起動に必要な時間よりも後の場合には微風速計回路をスリープ
            if(V_WAKEUP_TIME <= EM_mSettings.interval_vel) Anemometer_Sleep();
        }
    }

	//CO2測定************
	pass_counters.co2++;
	bool mesCO2 = hasCO2Sensor && EM_mSettings.measure_co2 && co2_condition_time == 0;
	if(mesCO2){
        
		//常に移動平均は取り続ける
		uint16_t co2_u = 0;
		float tmp_f = 0;
		float hmd_f = 0;
		if(STCC4_readMeasurement(&co2_u, &tmp_f, &hmd_f))
            SMA_Add(&smaCO2, co2_u);
		
		//出力するか否か
		if((int)EM_mSettings.interval_co2 <= pass_counters.co2){
            send_needed = true;
			pass_counters.co2 = 0;
            
            uint16_t co2Ave = SMA_GetAverage(&smaCO2);
            data.co2_ppm = co2Ave;
            data.valid_flags |= FLAG_CO2_PPM;            
		}		
	}
	
	//温湿度測定************
	pass_counters.th++;
	//CO2計測する場合には温湿度を通知する必要がある
	bool mesTH = EM_mSettings.measure_th && (int)EM_mSettings.interval_th <= pass_counters.th;
    if(mesTH)
    {
        send_needed = true;
        pass_counters.th = 0;
    }
	if(mesCO2 || mesTH)
	{
		float tmp_f = 0;
		float hmd_f = 0;
		if(SHT4x_ReadValue(&tmp_f, &hmd_f, SHT4_BD))
		{
			if(mesCO2) STCC4_setRHTCompensation(tmp_f, hmd_f);
			if(mesTH)
			{
                data.temp_dry = 100 * max(-40,min(99,EM_cFactors.dbtA *(tmp_f) + EM_cFactors.dbtB));
                data.humidity = 100 * max(0,min(100,EM_cFactors.hmdA *(hmd_f) + EM_cFactors.hmdB));
                data.valid_flags |= (FLAG_TEMP_DRY | FLAG_HUMIDITY);
			}
		}
	}
	
	//グローブ温度測定************
	pass_counters.glb++;
	if(EM_mSettings.measure_glb && (int)EM_mSettings.interval_glb <= pass_counters.glb)
	{
        send_needed = true;
		pass_counters.glb = 0;
        
		float glbt_f = 0;
		float glbh_f = 0;
		if(SHT4x_ReadValue(&glbt_f, &glbh_f, SHT4_AD))
		{
            data.temp_globe = 100 * max(-40,min(99,EM_cFactors.glbA * glbt_f + EM_cFactors.glbB));
            data.valid_flags |= FLAG_TEMP_GLOBE;
		}
	}
	
	//照度センサ測定**************
	pass_counters.ill++;
	if(EM_mSettings.measure_ill && (int)EM_mSettings.interval_ill <= pass_counters.ill)
	{
        send_needed = true;
		pass_counters.ill = 0;        
        
		float ill_d;
        if(OPT3001_ReadALS(&ill_d))
        {
            ill_d /= TRANSMITTANCE;
            data.illuminance = 10 * max(0,min(99999.99,EM_cFactors.luxA * ill_d + EM_cFactors.luxB));
            data.valid_flags |= FLAG_ILLUMINANCE;
        }
	}
	
	//新規データがある場合は送信
	if(send_needed)
	{
        //無線/USB出力 (v4 smp イベント)
		if(outputToZigbee || outputToBLE || outputToUSB)
        {
            pe_emit_smp(&data, outputToZigbee, outputToBLE, outputToUSB);
        }

        //FM出力 (内蔵フラッシュ書き込み)
		if(outputToFM)
		{
            // ring buffer 方式 (古いデータを wrap して上書き) は廃止。
            // 満杯 (rec_latest == MAX_RECORD_COUNT) に達したら以降のサンプルは捨てる。
            // 過去データの自動消失を防ぐため、満杯到達はユーザーに赤 LED 点滅で知らせる。
            // PC や BLE への送信は別経路 (上の outputToZigbee / BLE / USB) で行われるので
            // PC 接続中 (= outputToFM=false) ではこのブロック自体に入らない = 満杯概念は無関係。
            if (rec_latest < MAX_RECORD_COUNT)
            {
                if (W25_WriteRecord(rec_latest, &data))
                {
                    rec_latest++;
                }
            }
            else
            {
                blinkRedLED(3);   // 満杯通知 (約 375ms blocking、計測周期 >= 1 sec なら許容)
            }
		}
	}
	//UART送信が終わったら10msec待ってXBeeをスリープさせる(XBee側の送信が終わるまで待ちたいので)
	//本来、ここはCTSを使って受信可能になったタイミングでスリープか？フローコントロールを検討。
	_delay_ms(10); //このスリープはXBeeの通信終了待ち目的。試行錯誤で用意した値なので、根拠が曖昧。そもそもここではないようにも思う
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：初期化処理">

void LC_InitSensors(void){
    SMA_Init(&smaCO2); //CO2センサ平均化インスタンスの初期化
    
	hasCO2Sensor = STCC4_isConnected();
    if(hasCO2Sensor) {
		STCC4_initialize(); //CO2センサ
		STCC4_exitSleep(); //スリープ解除
		STCC4_performConditioning(); //起動用処理。22秒かかる
		co2_condition_time = CO2_CONDITIONING_SECONDS;
	}
    SHT4x_Initialize(SHT4_BD); //温湿度センサ
	SHT4x_Initialize(SHT4_AD); //グローブ温度センサ
    OPT3001_Initialize(); //照度計
    
    //データ数を取得
    rec_latest = W25_Count_Record(EM_generationNumber);
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：日時管理">

void LC_SetCurrentTime(time_t unixTime) {
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        currentTime = unixTime - UNIX_OFFSET;
    }
    // 外部から時刻設定された = 同期完了。wake window をクリアし、次回は +24h 後に再要求。
    if (time_sync_window_remaining > 0) {
        time_sync_window_remaining = 0;
        time_sync_next_unix = unixTime + TIME_SYNC_INTERVAL_S;
    }
}

time_t LC_GetCurrentTime(void) {
    time_t temp;
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        temp = currentTime + UNIX_OFFSET;
    }
    return temp;
}

void LC_TickSecond(void) {
    ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
        currentTime++;
    }

    // 時刻同期 wake window のカウントダウン
    if (time_sync_window_remaining > 0) time_sync_window_remaining--;

    // ロギング中で同期時刻に達したら emit フラグを立てる (実 emit は割り込み外で)
    if (logging && time_sync_next_unix > 0) {
        time_t now_unix = currentTime + UNIX_OFFSET;
        if (now_unix >= time_sync_next_unix) {
            time_sync_emit_pending = true;
            // 次回 sync は 24h 後に予約 (set_time が来たら LC_SetCurrentTime 側で
            // 上書きされるので衝突しない)
            time_sync_next_unix = now_unix + TIME_SYNC_INTERVAL_S;
        }
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：状態監視">

bool LC_IsInitializingCO2(void){
    return 0 < co2_condition_time;
}

bool LC_UseZigbeeConnection(void)
{
    return outputToZigbee;
}

bool LC_UseBLEConnection(void)
{
    return outputToBLE;
}

bool LC_IsLogging(void){
    return logging;
}

bool LC_OutputToUSB(void){
    return outputToUSB;
}

bool LC_HasTask(void){
    return hasTask || time_sync_window_remaining > 0;
}

bool LC_IsTimeSyncWindowActive(void){
    return time_sync_window_remaining > 0;
}

void LC_ProcessTimeSyncTask(void){
    // LC_TickSecond で立ったフラグを見て event 送出 + wake window 開始
    if (!time_sync_emit_pending) return;
    time_sync_emit_pending = false;
    time_sync_window_remaining = TIME_SYNC_WINDOW_S;
    pe_emit_time_sync_request(TIME_SYNC_WINDOW_S);
}

void LC_CheckCO2Connection(void){
    co2_connection_check_timer++;
	if (CO2_CHECK_INTERVAL <= co2_connection_check_timer) {
		co2_connection_check_timer = 0; // タイマーをリセット

		// センサーが接続されているか定期的に確認
		bool is_currently_connected = STCC4_isConnected();

		//接続状態が変わった場合
		if(is_currently_connected != hasCO2Sensor){
			hasCO2Sensor = is_currently_connected;
			//再接続の場合には初期化処理
			if(is_currently_connected) {
				STCC4_initialize();
				STCC4_exitSleep();
				STCC4_performConditioning();
				co2_condition_time = CO2_CONDITIONING_SECONDS;
			}
			// (v3 では HCS:0/1 を自発送信していたが v4 では削除: CO2 センサは仕様上固定実装)
		}
	}
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：タスク管理">

void LC_StartLoggingTask(bool toZigbee, bool toBLE, bool toFlash, bool toUSB){
    //書き出し先を設定
    outputToZigbee = toZigbee;
	outputToBLE = toBLE;
    outputToFM = toFlash;
    outputToUSB = toUSB;
    
    //計測日時が正時になるように微調整    
    struct tm lastSavedTime; //最後に保存した日時（UNIX時間）
    time_t ct = LC_GetCurrentTime() - UNIX_OFFSET;
    gmtime_r(&ct, &lastSavedTime);
    pass_counters.th = getNormTime(lastSavedTime, EM_mSettings.interval_th);
    pass_counters.glb = getNormTime(lastSavedTime, EM_mSettings.interval_glb);
    pass_counters.vel = getNormTime(lastSavedTime, EM_mSettings.interval_vel);
    pass_counters.ill = getNormTime(lastSavedTime, EM_mSettings.interval_ill);
    pass_counters.ad1 = getNormTime(lastSavedTime, EM_mSettings.interval_AD1);
    pass_counters.co2 = getNormTime(lastSavedTime, EM_mSettings.interval_co2);
    
    //CO2計測不要の場合はスリープ
    if(!EM_mSettings.measure_co2 && !LC_IsInitializingCO2())
        STCC4_stopContinuousMeasurement();

    //ロギング開始
    logging = true;

    // 時刻同期スケジュール: 初回は最初に到来する UTC 0:00 を狙う (2.4GHz 混雑が
    // 少ない深夜時間帯で同期成功率を上げる)。
    time_t now_unix = LC_GetCurrentTime();
    time_sync_next_unix = ((now_unix / 86400) + 1) * 86400;
    time_sync_window_remaining = 0;
    time_sync_emit_pending = false;
}

void LC_EndLoggingTask(void){
    logging=false;	//ロギング停止
    Anemometer_Sleep(); //風速センサを停止

    //CO2連続測定再開
    STCC4_startContinuousMeasurement();

    // 時刻同期スケジュールも停止
    time_sync_next_unix = 0;
    time_sync_window_remaining = 0;
    time_sync_emit_pending = false;
}

void LC_ProcessSensingTask(void){    
    //CO2センサの初期化待機中
	if(0 < co2_condition_time) 
	{
		co2_condition_time--;
		//0秒にたどり着いた場合
		if(co2_condition_time == 0)
		{
			//既に計測を始めていてCO2が不要の場合には停止
			if(LC_IsLogging() && !EM_mSettings.measure_co2) STCC4_stopContinuousMeasurement();
			//その他の場合には連続計測開始
			else STCC4_startContinuousMeasurement();
		}
	}
    
    hasTask = true;
    //CO2センサ校正処理
	if(0 < co2CalibratingTime) calibrateCO2Level();
	//CO2センサ初期化処理
	else if(co2InitializingTime) performCO2InitializationProcess();
	//ロギング処理
	else if(logging) execLogging();
    //タスクなし
    else hasTask = false;
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：風速関連処理">

void LC_Update_Anemometer()
{
    Anemometer_Update(&anemometer);
}

// (LC_StartVelocityCalibration / LC_EndVelocityCalibration は v4 で削除:
//  風速校正は風速プローブ側 MCU に移管)

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：CO2関連処理">

bool LC_HasCO2Sensor(void)
{
    return hasCO2Sensor;
}

void LC_FactoryResetCO2(uint16_t co2Level, uint16_t time)
{
    if(hasCO2Sensor && STCC4_performFactoryReset()){
        STCC4_startContinuousMeasurement(); //連続測定再開
        reforcedCO2Level = co2Level;
        co2InitializingTime = time; //初期化には12時間の連続測定が必要
    }
}

void LC_CalibrateCO2(uint16_t co2Level, uint16_t time)
{
    if(hasCO2Sensor){
        reforcedCO2Level = co2Level;		
        co2CalibratingTime = 30;
    }
}

// </editor-fold>

void LC_ClearData(void)
{
    EM_generationNumber++;
    if(254 < EM_generationNumber) EM_generationNumber = 0;
    EM_saveGenerationNumber();
    rec_latest = 0;
}