/**
 * @file XbeeController.h
 * @brief AVR(ATMega328)��XBee�ƒʐM����
 * @author E.Togashi
 * @date 2021/11/28
 */

#include "XbeeController.h"
#include "UartDriver.h"
#include "EepromManager.h"
#include "parameters.h"

#include <string.h>
#include <avr/io.h>
#include <util/delay.h>

const int UART_RX_BUFFER_SIZE  = 256;

const size_t MBUFF_LENGTH = 20;

//XBee�t���[����͗p�ϐ�
static bool readingFrame = false; //�t���[���Ǎ������ۂ�
static uint8_t framePosition = 0; //�t���[���ǂݍ��݈ʒu
static uint8_t frameSize = 0; //�t���[��append_command�T�C�Y
static char frameBuff[UART_RX_BUFFER_SIZE]; //�t���[��
static uint8_t xbeeOffset=14; //��Mframe type�ɉ������I�t�Z�b�g
static uint8_t frameChecksum = 0; //��M�t���[���`�F�b�N�T���v�Z�p�ϐ�

void XbeeController::initialize(void)
{
	UartDriver::initialize();	
}

//�R�[�f�B�l�[�^�ɑ΂��ĕ����z��𑗐M
void XbeeController::txChars(const char data[])
{
	int chkSum = 0;
	int cl = getCharLength(data);
	
	UartDriver::sendChar(XbeeApi::START_DELIMITER); //API�t���[���J�n�R�[�h
	UartDriver::sendChar((char)(((cl + XbeeApi::TxRequest::HEADER_LENGTH) >> 8) & 0xff));	//�f�[�^���̏�ʃo�C�g
	UartDriver::sendChar((char)((cl + XbeeApi::TxRequest::HEADER_LENGTH) & 0xff));			//�f�[�^���̉��ʃo�C�g
	
	//��������`�F�b�N�T�����Z*************
	UartDriver::sendChar(XbeeApi::FrameType::ZIGBEE_TX_REQUEST); //�R�}���hID�i�f�[�^���M��0x10�j
	chkSum = addCsum(chkSum, XbeeApi::FrameType::ZIGBEE_TX_REQUEST);
	
	UartDriver::sendChar(XbeeApi::TxRequest::FRAME_ID_NO_ACK); //�t���[��ID�i�C�Ӂj//0�ȊO����ACK���߂��Ă���B
	//chkSum�ւ̉��Z��0�Ȃ̂ŏȗ�
	
	for(int i=0;i<8;i++) //64bit���M��A�h���X�̓R�[�f�B�l�[�^�ւ̑��M�Ȃ̂ł��ׂ�0�Ń`�F�b�N�T���͕s��
		UartDriver::sendChar(0x00);
	//chkSum�ւ̉��Z��0�Ȃ̂ŏȗ�
	
	// 16bit����A�h���X (�s���ȏꍇ��0xFFFE)
	const uint8_t addr16_msb = (uint8_t)(XbeeApi::TxRequest::ADDR16_COORDINATOR >> 8);
	const uint8_t addr16_lsb = (uint8_t)(XbeeApi::TxRequest::ADDR16_COORDINATOR & 0xFF);	
	UartDriver::sendChar(addr16_msb); //16bit���M��A�h���X_M
	chkSum = addCsum(chkSum, addr16_msb);	
	UartDriver::sendChar(addr16_lsb); //16bit���M��A�h���X_L
	chkSum = addCsum(chkSum, addr16_lsb);
	
	UartDriver::sendChar(XbeeApi::TxRequest::BROADCAST_RADIUS_MAX); //�u���[�h�L���X�g���a�i���j�L���X�g�Ȃ̂�0�Ń`�F�b�N�T���͕s�ρj
	//chkSum�ւ̉��Z��0�Ȃ̂ŏȗ�

	UartDriver::sendChar(XbeeApi::TxRequest::OPTIONS_DEFAULT); //���M�I�v�V������0�Ń`�F�b�N�T���͕s��
	//chkSum�ւ̉��Z��0�Ȃ̂ŏȗ�
	
	//���M�f�[�^
	for(int i=0;i<cl;i++)
	{
		UartDriver::sendChar(data[i]);		
		chkSum = addCsum(chkSum, data[i]);
	}
	
	UartDriver::sendChar((char)(XbeeApi::CHECKSUM_SUCCESS - chkSum)); //Checksum���M
}

int XbeeController::getCharLength(const char data[])
{
	int length = 0;
	for(length = 0; data[length]; length++);
	return length;
}

int XbeeController::addCsum(int csum, char nbyte)
{
	csum += (int)nbyte;
	csum = csum & 0x00ff;
	return csum;
}

//Bluetooth�ڑ���ɑ΂��ĕ����z��𑗐M
void XbeeController::blChars(const char data[])
{
	int chkSum = 0;
	int cl = getCharLength(data);
	
	UartDriver::sendChar(XbeeApi::START_DELIMITER); //API�t���[���J�n�R�[�h
	UartDriver::sendChar((char)(((cl + XbeeApi::UserDataRelay::HEADER_LENGTH) >> 8) & 0xff));	//�f�[�^���̏�ʃo�C�g
	UartDriver::sendChar((char)((cl + XbeeApi::UserDataRelay::HEADER_LENGTH) & 0xff));			//�f�[�^���̉��ʃo�C�g
	
	//��������`�F�b�N�T�����Z*************
	UartDriver::sendChar(XbeeApi::FrameType::USER_DATA_RELAY); //�R�}���hID�iDataRelay��0x2D�j
	chkSum = addCsum(chkSum, XbeeApi::FrameType::USER_DATA_RELAY);
	
	UartDriver::sendChar(XbeeApi::UserDataRelay::FRAME_ID_DEFAULT); //�t���[��ID
	
	UartDriver::sendChar(XbeeApi::UserDataRelay::INTERFACE_BLUETOOTH); //Source interface�iBluetooth��0x01�j
	chkSum = addCsum(chkSum, XbeeApi::UserDataRelay::INTERFACE_BLUETOOTH);
		
	//���M�f�[�^
	for(int i=0;i<cl;i++)
	{
		UartDriver::sendChar(data[i]);
		chkSum = addCsum(chkSum, data[i]);
	}
	
	UartDriver::sendChar((char)(XbeeApi::CHECKSUM_SUCCESS - chkSum)); //Checksum���M
}

void XbeeController::bltxChars(const char data[])
{
	txChars(data);
	blChars(data);	
}

void XbeeController::sendAtCmd(const char data[]){
	int cl = getCharLength(data);
	//���M�f�[�^
	for(int i=0;i<cl;i++)
		UartDriver::sendChar(data[i]);
}

bool XbeeController::xbeeSettingInitialized(){
	//XBee���������ς݂Ȃ�΃X�L�b�v
	if(EepromManager::isXBeeInitialized()) 
		return 1;
	
	bool hasChanged = false;
	char message[MBUFF_LENGTH];
	memset(message, 0, sizeof(message));

	//AT���[�h��
	_delay_ms(1100); // �K�[�h�^�C��
	UartDriver::sendChars("+++");
	_delay_ms(1100); // �K�[�h�^�C��
	XbeeController::receiveMessage(message);
	bool apiEnabled = strcmp(message, "OK") != 0;
	
	//API���[�h�ł̏����ݒ�********************
	if(apiEnabled){
		const uint8_t frameIdNoAck = 0x00; // ������v�����Ȃ��t���[��ID

		// 1. SP (Cyclic Sleep Period) �� 0x64 (1000 ms) �ɐݒ�
		const uint8_t sp_param[] = {0x64};
		sendAtCommandApiFrame("SP", frameIdNoAck, sp_param, sizeof(sp_param));
		_delay_ms(100);

		// 2. SN (Number of Cyclic Sleep Periods) �� 3600 �ɐݒ�
		const uint8_t sn_param[] = {0x0E, 0x10}; // 3600 = 0x0E10
		sendAtCommandApiFrame("SN", frameIdNoAck, sn_param, sizeof(sn_param));
		_delay_ms(100);

		// 3. CE (Coordinator Enable) �� 0 (End Device) �ɐݒ�
		const uint8_t ce_param[] = {0};
		sendAtCommandApiFrame("CE", frameIdNoAck, ce_param, sizeof(ce_param));
		_delay_ms(100);

		// 4. SM (Sleep Mode) �� 1 (Pin Hibernate) �ɐݒ�
		const uint8_t sm_param[] = {1};
		sendAtCommandApiFrame("SM", frameIdNoAck, sm_param, sizeof(sm_param));
		_delay_ms(100);

		// 5. BT (Bluetooth Enable) �� 1 (Enabled) �ɐݒ�
		const uint8_t bt_param[] = {1};
		sendAtCommandApiFrame("BT", frameIdNoAck, bt_param, sizeof(bt_param));
		_delay_ms(100);

		// 6. D5�iZigbee�ʐMLED�j�� 4 (OFF/Low) �ɐݒ�
		const uint8_t d5_param[] = {4};
		sendAtCommandApiFrame("D5", frameIdNoAck, d5_param, sizeof(d5_param));
		_delay_ms(100);

		// 7. BI�iBluetooth Identifier�j�����K�[���ɐݒ�
		sendAtCommandApiFrame("BI", frameIdNoAck, (const uint8_t*)ML_NAME, strlen(ML_NAME));
		_delay_ms(100);

		// 8. �S�Ă̐ݒ��s�������������ɏ������� (�d�v)
		sendAtCommandApiFrame("WR", frameIdNoAck, nullptr, 0);
		_delay_ms(100);
	}
	
	//AT���[�h�ł̏����ݒ�********************
	else{
		//SP��1000msec=0x64
		UartDriver::sendChars("atsp\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "64") != 0) {
			UartDriver::sendChars("atsp64\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//SN��3600sec
		UartDriver::sendChars("atsn\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "3600") != 0) {
			UartDriver::sendChars("atsn3600\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//CE��end device(0)
		UartDriver::sendChars("atce\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "0") != 0) {
			UartDriver::sendChars("atce0\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//SM��Pin Hibernate(1)
		UartDriver::sendChars("atsm\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "1") != 0) {
			UartDriver::sendChars("atsm1\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//d5��Zigbee�ʐM�󋵂�LED�\����Off�iOut low:4)�ɐݒ�B
		UartDriver::sendChars("atd5\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "0") != 0) {
			UartDriver::sendChars("atd54\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//Bluetooth�L��/����+password(ml_pass)
		UartDriver::sendChars("atbt\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "0") == 0) {
			UartDriver::sendChars("at$S28513497\r"); //salt
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$V6567694B0CA9ADCED8D5B2B0015718D1E2637B86E3E178E029936A078926C2B0\r"); //$V
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$W259F6833E1E1932E4485F48865FB6B76EC6E847A7272C77A8C27DD7DF94E44DC\r"); //$W
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$XA99A6E3937FBB8D05BBB4E4A8C4CB221C14D15CD004139C77B6FE0C8AF2932D8\r"); //$X
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("at$Y6D4411D507FC52AFD5877D6E8529AEE7FB931F10944BC0D058FB246D0DE071DB\r"); //$Y
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			UartDriver::sendChars("atbt1\r"); //Bluetooth �L����
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//bi��Bluetooth identifier
		UartDriver::sendChars("atbi\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, ML_NAME) != 0) {
			UartDriver::sendChars("atbi");
			UartDriver::sendChars(ML_NAME);
			UartDriver::sendChars("\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//AP
		UartDriver::sendChars("atap\r");
		XbeeController::receiveMessage(message);
		if(strcmp(message, "1") != 0) {
			UartDriver::sendChars("atap1\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
			hasChanged = true;
		}
		
		//�ύX���������ꍇ�ɂ̓������ɏ�������
		if(hasChanged){
			UartDriver::sendChars("atwr\r");
			XbeeController::receiveMessage(message);
			if(strcmp(message, "OK") != 0) return 0;
		}
	}
		
	//XBee�������t���O���L�^
	EepromManager::xbeeInitialized();
	return 1;
}

bool XbeeController::processXbeeByte(char dat,  char* output_buffer, int buffer_size)
{
	uint8_t current_byte = (uint8_t)dat;

	//�J�n�R�[�h�u~�v�������珉�����B�{����Escape���������邪�A�u~�v�̓R�}���h�Ŏg��Ȃ��̂ŗǂ����낤
	if (current_byte == XbeeApi::START_DELIMITER)
	{
		framePosition = 1;
		readingFrame = true;
		frameChecksum = 0; //�`�F�b�N�T��������
		return false; // ���̃o�C�g�̏����͏I��
	}
	
	//�t���[���Ǎ����ł͖���
	if(!readingFrame) return false;
	
	if(3 <= framePosition) frameChecksum += current_byte;

	//��������t���[�����
	switch(framePosition){
		case 1:  // �f�[�^����ʃo�C�g�B�������Ȃ�
			break;
			
		case 2: // �f�[�^�����ʃo�C�g
			frameSize = current_byte + 3;
			break;
			
		case 3: // �R�}���hID
			switch (current_byte) {
				case XbeeApi::FrameType::ZIGBEE_RECEIVE_PACKET:
					xbeeOffset = XbeeApi::RxPayloadOffset::ZIGBEE_RECEIVE_PACKET;
					break;
				case XbeeApi::FrameType::USER_DATA_RELAY_IN:
					xbeeOffset = XbeeApi::RxPayloadOffset::USER_DATA_RELAY_IN;
					break;
				default:
					readingFrame = false; // ���m�̃t���[���^�C�v�͉�͒��f
					break;
			}
			
		default:
			// �f�[�^�y�C���[�h��������
			if(xbeeOffset < framePosition) {
				// �`�F�b�N�T���ɓ��B
				if (frameSize <= framePosition) {
					// ��Ԃ����Z�b�g���Ď��̃t���[���ɔ�����
					readingFrame = false;
					framePosition = 0;
					
					//�`�F�b�N�T������
					if(frameChecksum == XbeeApi::CHECKSUM_SUCCESS){
						// �����o�b�t�@��NULL�����ŏI�[������
						int payload_len = framePosition - (xbeeOffset + 1);
						// �o�b�t�@�I�[�o�[�����h�~
						if (0 <= payload_len && payload_len < UART_RX_BUFFER_SIZE) frameBuff[payload_len] = '\0';
						else frameBuff[UART_RX_BUFFER_SIZE - 1] = '\0';
						
						// ���������y�C���[�h���A�����œn���ꂽ�O���̃o�b�t�@�Ɉ��S�ɃR�s�[
						strncpy(output_buffer, frameBuff, buffer_size - 1);
						output_buffer[buffer_size - 1] = '\0'; // �K��NULL�I�[������
						
						return true; //�t���[������
					}
					//�`�F�b�N�T���ُ�F�t���[���j������false
					else return false;
				}
				// �o�b�t�@�Ɋi�[
				else
				{
					// �����o�b�t�@�Ƀf�[�^���i�[
					int buffer_index = framePosition - (xbeeOffset + 1);
					// �o�b�t�@�I�[�o�[�����h�~
					if (buffer_index < UART_RX_BUFFER_SIZE) frameBuff[buffer_index] = current_byte;
				} 
			}
			break;
	}
	framePosition++;
	// �o�b�t�@�T�C�Y�𒴂�����A�s���ȃt���[���Ƃ��ĉ�͂𒆒f
	if (framePosition >= UART_RX_BUFFER_SIZE + xbeeOffset + 2) {
		readingFrame = false;
		framePosition = 0;
	}

	return false; // �t���[��������
}

void XbeeController::receiveMessage(char message[]){
	uint8_t index = 0;
	uint16_t timeout_counter = 0;
	const uint16_t TIMEOUT_LIMIT = 500; // �^�C���A�E�g����(ms)

	memset(message, 0, MBUFF_LENGTH); // �o�b�t�@���N���A

	while (timeout_counter < TIMEOUT_LIMIT)
	{
		//�����O�o�b�t�@����f�[�^���擾����
		if (UartDriver::uartRingBufferHasData())
		{
			char c = UartDriver::uartRingBufferGet();
			if (c != '\n' && c != '\r') {
				// �o�b�t�@�T�C�Y�𒴂��Ȃ��悤��
				if (index < MBUFF_LENGTH - 1) message[index++] = c;
			}

			if (c == '\r') {
				message[index] = '\0';
				return; // ����Ɏ�M����
			}
		}
		
		// �f�[�^���Ȃ���Ώ����҂�
		_delay_ms(1);
		timeout_counter++;
	}
}

/**
 * @brief AT�R�}���h��API�t���[���Ƃ��đ��M����
 * @param at_command 2������AT�R�}���h (��: "SP")
 * @param frame_id ���������ʂ��邽�߂̃t���[��ID (0�͉����s�v)
 * @param param �ݒ肷��p�����[�^�i�₢���킹�̏ꍇ��nullptr�j
 * @param param_len �p�����[�^�̒���
 */
void XbeeController::sendAtCommandApiFrame(const char at_command[2], uint8_t frame_id, const uint8_t* param, uint8_t param_len)
{
    uint8_t checksum = 0;
    uint8_t payload_len = 4 + param_len; // FrameType + FrameID + AT Command + Param

    UartDriver::sendChar(XbeeApi::START_DELIMITER);
    UartDriver::sendChar(0x00); // Length MSB
    UartDriver::sendChar(payload_len); // Length LSB

    // --- Checksum�Ώ� ---
    UartDriver::sendChar(XbeeApi::FrameType::AT_COMMAND);
    checksum += XbeeApi::FrameType::AT_COMMAND;

    UartDriver::sendChar(frame_id);
    checksum += frame_id;

    UartDriver::sendChar(at_command[0]);
    checksum += at_command[0];
    UartDriver::sendChar(at_command[1]);
    checksum += at_command[1];

    for (uint8_t i = 0; i < param_len; i++) {
        UartDriver::sendChar(param[i]);
        checksum += param[i];
    }
    // --------------------

    UartDriver::sendChar(0xFF - checksum);
}