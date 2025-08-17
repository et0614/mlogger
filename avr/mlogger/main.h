/**
 * @file main.h
 * @brief AVR(ATMega328)���g�p�����v���f�[�^���W�E���M�v���O����
 * @author E.Togashi
 * @date 2020/7/14
 */

//�v�����ԊԊu��ێ�����\����
struct MeasurementPassCounters {
	int th;  //�����x
	int glb; //�O���[�u���x
	int vel; //������
	int ill; //�Ɠx
	int ad1; //�ėpAD
	int co2; //CO2�Z�x
};

static void initializePort(void);

static void initializeTimer(void);

/**
* @fn
* XBee�̃y�C���[�h��������R�}���h�g���o�b�t�@�ɒǉ����A�R�}���h������������
* @param payload XBee�t���[�����璊�o���ꂽ�y�C���[�h������
*/
static void appendCommand(const char* payload);

/**
* @fn
* ��M�����R�}���h����������
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

//�ȉ���inline�֐�************************************

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


