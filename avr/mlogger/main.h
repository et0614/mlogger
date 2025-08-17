/**
 * @file main.h
 * @brief AVR(ATMega328)を使用した計測データ収集・送信プログラム
 * @author E.Togashi
 * @date 2020/7/14
 */

//計測時間間隔を保持する構造体
struct MeasurementPassCounters {
	int th;  //温湿度
	int glb; //グローブ温度
	int vel; //微風速
	int ill; //照度
	int ad1; //汎用AD
	int co2; //CO2濃度
};

static void initializePort(void);

static void initializeTimer(void);

/**
* @fn
* XBeeのペイロード文字列をコマンド組立バッファに追加し、コマンドを完成させる
* @param payload XBeeフレームから抽出されたペイロード文字列
*/
static void appendCommand(const char* payload);

/**
* @fn
* 受信したコマンドを処理する
*/
static void solveCommand(const char *command);

static float readVelVoltage(void);

static float readVoltage(unsigned int adNumber);

static void writeFlashMemory(const tm dtNow, const char write_chars[]);

static void executeSecondlyTask(void);

static void execLogging(void);

static void calibrateVelocityVoltage(void);

static void calibrateCO2Level(void);

static void showError(short int errNum);

static bool isLowBattery(void);

static void alignLeft(char *str);

static int getNormTime(tm time, unsigned int interval);

//以下はinline関数************************************

inline static void sleepAnemo(void);

inline static void wakeupAnemo(void);

inline static void sleepXbee(void);

inline static void wakeupXbee(void);

inline static bool isXbeeSleeping(void);

inline static void blinkLED(int iterNum, uint8_t pin_mask);

inline static void blinkGreenAndRedLED(int iterNum);

inline static void turnOnGreenLED(void);

inline static void turnOffGreenLED(void);

inline static void toggleGreenLED(void);

inline static void blinkGreenLED(int iterNum);

inline static void turnOnRedLED(void);

inline static void turnOffRedLED(void);

inline static void toggleRedLED(void);

inline static void blinkRedLED(int iterNum);

inline static float max(float x, float y);

inline static float min(float x, float y);


