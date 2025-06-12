/**
 * @file main.h
 * @brief AVR(ATMega328)���g�p�����v���f�[�^���W�E���M�v���O����
 * @author E.Togashi
 * @date 2020/7/14
 */

static void initialize_port(void);

static void initialize_timer(void);

/**
* @fn
* XBee�̎�M��������R�}���h�o�b�t�@�ɒǉ�����
*/
static void append_command(void);

/**
* @fn
* ��M�����R�}���h����������
*/
static void solve_command(const char *command);

static float readGlbVoltage(void);

static float readVelVoltage(void);

static float readVoltage(unsigned int adNumber);

static void writeFlashMemory(const tm dtNow, const char write_chars[]);

static void execLogging(void);

static void calibrateVelocityVoltage(void);

static void showError(short int errNum);

static bool isLowBattery(void);

static void alignLeft(char *str);

static int getNormTime(tm time, unsigned int interval);

//�ȉ���inline�֐�************************************

inline static void sleep_anemo(void);

inline static void wakeup_anemo(void);

inline static void sleep_xbee(void);

inline static void wakeup_xbee(void);

inline static void turnOnGreenAndRedLED(void);

inline static void turnOffGreenAndRedLED(void);

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

