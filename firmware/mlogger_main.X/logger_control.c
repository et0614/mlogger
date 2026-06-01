// <editor-fold defaultstate="collapsed" desc="include">
#include "hal_io.h"
#include "logger_control.h"
#include "eeprom_manager.h"
#include "xbee_controller.h"
#include "smAverage.h" //平均化ユーティリティ
#include "th_probe.h" //温湿度+CO2+グローブ温度プローブ (mlogger_th_sensor 子機)
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
static bool hasCO2Sensor = false;
static uint8_t co2_connection_check_timer = 0;
volatile static uint8_t co2_condition_time = 0;	 //perform_conditioning 進行残[sec]
static uint32_t co2InitializingTime = 0;         //安定化(任意秒)→FRC モード残り秒数
static uint16_t reforcedCO2Level = 400;          //強制校正CO2濃度[ppm]
static SmAverage smaCO2;                         //60秒平均

// FRC 進行管理 (子機 stcc4_state を polling)
typedef enum { FRC_PHASE_IDLE = 0, FRC_PHASE_RUNNING } FrcPhase_t;
static FrcPhase_t frcPhase = FRC_PHASE_IDLE;
static uint8_t    frcProgressSec = 0;

//温湿度+CO2+グローブ温度プローブ (mlogger_th_sensor 子機)
static ThProbe_t th_probe;
static bool th_trigger_pending = false;          //true なら次 sec で ThProbe_Read

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

//きりの良い時刻になるように最初の計測時間間隔を調整する
static int getNormTime(struct tm time, unsigned int interval)
{
	if(interval == 1) return interval; //1secの場合には直ちに計測
	if(interval <= 5) return interval - (5 - time.tm_sec % 5);
	else if(interval <= 10) return interval - (10 - time.tm_sec % 10);
	else if(interval <= 30) return interval - (30 - time.tm_sec % 30);
	else return interval - (60 - time.tm_sec % 60);
}

// CO2 FRC: 子機内で 30sec 連続測定 + perform_forced_recalibration ~5sec が完結する。
// 本体は ThProbe_GetState() を毎秒 polling して完了/失敗を検知し progress イベントを送る。
static void pollCO2Calibration(void)
{
    blinkGreenAndRedLED(1);
    uint8_t state = ThProbe_GetState();
    uint16_t co2_now = th_probe.co2_valid ? th_probe.co2_ppm : 0;

    if (state == TH_PROBE_STATE_FRC_DONE) {
        pe_emit_co2_calibration_progress(0, "pass", ThProbe_GetFrcCorrection(), co2_now);
        frcPhase = FRC_PHASE_IDLE;
        frcProgressSec = 0;
        return;
    }
    if (state == TH_PROBE_STATE_FRC_FAIL) {
        pe_emit_co2_calibration_progress(0, "fail", 0, co2_now);
        frcPhase = FRC_PHASE_IDLE;
        frcProgressSec = 0;
        return;
    }

    // 進行中: 経過秒を進めて progress イベント (30 sec 想定 + FRC ~5 sec の余裕)
    frcProgressSec++;
    uint8_t remaining = (frcProgressSec < 35) ? (35 - frcProgressSec) : 1;
    pe_emit_co2_calibration_progress(remaining, "measuring", 0, co2_now);
}

// 任意秒安定化フェーズ。0 到達で FRC へ移行 (以降は pollCO2Calibration が引き継ぐ)
static void pollCO2Initialization(void)
{
    blinkGreenAndRedLED(1);
    co2InitializingTime--;
    if (co2InitializingTime == 0) {
        ThProbe_StartFrc(reforcedCO2Level);
        frcPhase = FRC_PHASE_RUNNING;
        frcProgressSec = 0;
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

	//温湿度・CO2・グローブ温度測定 (th_probe 一括取得) ****************************
	// pre-trigger 方式: 前 sec で Trigger 済みのデータを今 sec で読み出す。
	// 補正は子機側 (製造校正) と本体側 EM_cFactors (ユーザー任意オフセット) の二段適用。
	pass_counters.th++;
	pass_counters.co2++;
	pass_counters.glb++;
	bool mesTH  = EM_mSettings.measure_th  && (int)EM_mSettings.interval_th  <= pass_counters.th;
	bool mesCO2 = hasCO2Sensor && EM_mSettings.measure_co2 && co2_condition_time == 0
	              && (int)EM_mSettings.interval_co2 <= pass_counters.co2;
	bool mesGlb = EM_mSettings.measure_glb && (int)EM_mSettings.interval_glb <= pass_counters.glb;

	if (mesTH || mesCO2 || mesGlb) {
		ThProbe_Read(&th_probe);
		th_trigger_pending = false;

		if (mesTH) {
			send_needed = true;
			pass_counters.th = 0;
			if (th_probe.t_valid) {
				data.temp_dry = 100 * max(-40, min(99, EM_cFactors.dbtA * th_probe.temp_c + EM_cFactors.dbtB));
				data.valid_flags |= FLAG_TEMP_DRY;
			}
			if (th_probe.rh_valid) {
				data.humidity = 100 * max(0, min(100, EM_cFactors.hmdA * th_probe.rh_pct + EM_cFactors.hmdB));
				data.valid_flags |= FLAG_HUMIDITY;
			}
		}
		if (mesCO2) {
			send_needed = true;
			pass_counters.co2 = 0;
			if (th_probe.co2_valid) {
				SMA_Add(&smaCO2, th_probe.co2_ppm);
				data.co2_ppm = SMA_GetAverage(&smaCO2);
				data.valid_flags |= FLAG_CO2_PPM;
			}
		}
		if (mesGlb) {
			send_needed = true;
			pass_counters.glb = 0;
			if (th_probe.glb_valid) {
				data.temp_globe = 100 * max(-40, min(99, EM_cFactors.glbA * th_probe.glb_c + EM_cFactors.glbB));
				data.valid_flags |= FLAG_TEMP_GLOBE;
			}
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
	//次 sec が計測時刻なら今 sec のうちに pre-trigger 発行 (子機の single-shot は ~520ms)
	bool nextTH  = EM_mSettings.measure_th  && (pass_counters.th  + 1) >= (int)EM_mSettings.interval_th;
	bool nextCO2 = hasCO2Sensor && EM_mSettings.measure_co2 && co2_condition_time == 0
	               && (pass_counters.co2 + 1) >= (int)EM_mSettings.interval_co2;
	bool nextGlb = EM_mSettings.measure_glb && (pass_counters.glb + 1) >= (int)EM_mSettings.interval_glb;
	if (nextTH || nextCO2 || nextGlb) {
		ThProbe_Trigger();
		th_trigger_pending = true;
	}

	//UART送信が終わったら10msec待ってXBeeをスリープさせる(XBee側の送信が終わるまで待ちたいので)
	//本来、ここはCTSを使って受信可能になったタイミングでスリープか？フローコントロールを検討。
	_delay_ms(10); //このスリープはXBeeの通信終了待ち目的。試行錯誤で用意した値なので、根拠が曖昧。そもそもここではないようにも思う
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="公開関数：初期化処理">

void LC_InitSensors(void){
    SMA_Init(&smaCO2); //CO2センサ平均化インスタンスの初期化

    // 温湿度+CO2+グローブ温度プローブ (子機 firmware は起動時に STCC4 を扱える状態へ自動遷移する)
    ThProbe_Init(&th_probe);
    hasCO2Sensor = ThProbe_IsConnected();

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
		co2_connection_check_timer = 0;
		// 子機側で再接続時の conditioning は自動完結するので、本体は flag 更新だけ。
		hasCO2Sensor = ThProbe_IsConnected();
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

    // 風速プローブの第一回計測まで V_WAKEUP_TIME 以上の余裕を確保する。
    // getNormTime は round 秒に揃える設計だが、開始秒が悪い (例 sec%60=59) と
    // 第一回 Wake (Anemometer_Wakeup → XCL102 起動) と Update (POLL read) が
    // 同 tick で連続発火する。低 VBAT では XCL102 inrush 中の POLL read が
    // cascade collapse で I2C 失敗するので、必ず V_WAKEUP_TIME 以上の warmup
    // 期間を取れるよう pass_counters.vel を巻き戻す。
    if (EM_mSettings.interval_vel >= V_WAKEUP_TIME) {
        int max_vel = (int)EM_mSettings.interval_vel - V_WAKEUP_TIME;
        if (pass_counters.vel > max_vel) pass_counters.vel = max_vel;
    }
    // interval_vel < V_WAKEUP_TIME の場合は熱線常時 ON 運用で別途設計、ここでは
    // 巻き戻ししない。

    // pre-trigger 状態を初期化 (前セッションの残骸を持ち越さない)
    th_trigger_pending = false;

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

    th_trigger_pending = false;

    // 時刻同期スケジュールも停止
    time_sync_next_unix = 0;
    time_sync_window_remaining = 0;
    time_sync_emit_pending = false;
}

void LC_ProcessSensingTask(void){
    if (0 < co2_condition_time) co2_condition_time--;

    hasTask = true;
    if (frcPhase == FRC_PHASE_RUNNING) pollCO2Calibration();
    else if (0 < co2InitializingTime) pollCO2Initialization();
    else if (logging) execLogging();
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

// 任意秒安定化 → FRC モード。子機に factory_reset → time 秒カウントダウン (pollCO2Initialization)
// → 0 到達で FRC 依頼 → pollCO2Calibration が完了/失敗判定。
void LC_FactoryResetCO2(uint16_t co2Level, uint16_t time)
{
    if (!hasCO2Sensor) return;
    ThProbe_StartFactoryReset();
    reforcedCO2Level = co2Level;
    co2InitializingTime = time;
}

// 30 sec 連続測定 → FRC モード (子機内部で完結、本体は state を polling)
void LC_CalibrateCO2(uint16_t co2Level, uint16_t time)
{
    (void)time; // 30sec は子機側で固定
    if (!hasCO2Sensor) return;
    reforcedCO2Level = co2Level;
    ThProbe_StartFrc(co2Level);
    frcPhase = FRC_PHASE_RUNNING;
    frcProgressSec = 0;
}

// </editor-fold>

void LC_ClearData(void)
{
    EM_generationNumber++;
    if(254 < EM_generationNumber) EM_generationNumber = 0;
    EM_saveGenerationNumber();
    rec_latest = 0;
}