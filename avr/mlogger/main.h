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
static void solve_command(void);

static float readGlbVoltage(void);

static float readVelVoltage(void);

static float readVoltage(unsigned int adNumber);

static void writeSDcard(const tm dtNow, const char write_chars[]);

//�ȉ���inline�֐�************************************

inline static void sleep_anemo(void);

inline static void wakeup_anemo(void);

inline static void sleep_xbee(void);

inline static void wakeup_xbee(void);

inline static void blinkLED(int iterNum);

inline static float max(float x, float y);

inline static float min(float x, float y);

