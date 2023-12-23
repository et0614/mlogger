/**
 * @file main.h
 * @brief AVR(ATMega328)を使用した計測データ収集・送信プログラム
 * @author E.Togashi
 * @date 2020/7/14
 */

static void initialize_port(void);

static void initialize_timer(void);

/**
* @fn
* XBeeの受信文字列をコマンドバッファに追加する
*/
static void append_command(void);

/**
* @fn
* 受信したコマンドを処理する
*/
static void solve_command(void);

static float readGlbVoltage(void);

static float readVelVoltage(void);

static float readVoltage(unsigned int adNumber);

static void writeSDcard(const tm dtNow, const char write_chars[]);

static void execLogging(void);

static void calibrateVelocityVoltage(void);

static void autoCalibrateVelocitySensor(void);

static void autoCalibrateTemperatureSensor(void);

static void showLowBattery(void);

static bool isLowBattery(void);

//以下はinline関数************************************

inline static void sleep_anemo(void);

inline static void wakeup_anemo(void);

inline static void sleep_xbee(void);

inline static void wakeup_xbee(void);

inline static void turnOnLED(void);

inline static void turnOffLED(void);

inline static void toggleLED(void);

inline static void blinkLED(int iterNum);

inline static float max(float x, float y);

inline static float min(float x, float y);

