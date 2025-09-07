/**
 * @file XbeeController.h
 * @brief AVR(ATMega328)��XBee�ƒʐM����
 * @author E.Togashi
 * @date 2021/11/28
 */ 

#ifndef XBEE_CONTROLLER_H_
#define XBEE_CONTROLLER_H_

#include <stdint.h>

// XBee API�Ɋւ���萔���܂Ƃ߂閼�O���
namespace XbeeApi {
	// API�t���[���̊�{�v�f
	constexpr uint8_t START_DELIMITER = 0x7E;
	constexpr uint8_t CHECKSUM_SUCCESS = 0xFF;

	// �t���[���^�C�vID
	namespace FrameType {
		constexpr uint8_t AT_COMMAND            = 0x08;
		constexpr uint8_t ZIGBEE_TX_REQUEST		= 0x10;
		constexpr uint8_t USER_DATA_RELAY		= 0x2D;
		constexpr uint8_t AT_COMMAND_RESPONSE   = 0x88;
		constexpr uint8_t ZIGBEE_RECEIVE_PACKET = 0x90;
		constexpr uint8_t USER_DATA_RELAY_IN	= 0xAD; // ��M���̃t���[���^�C�v
	}

	// �t���[���\���Ɋւ���萔
	namespace TxRequest {
		constexpr uint8_t FRAME_ID_NO_ACK = 0x00;
		constexpr uint16_t ADDR16_COORDINATOR = 0xFFFE;
		constexpr uint8_t BROADCAST_RADIUS_MAX = 0x00;
		constexpr uint8_t OPTIONS_DEFAULT = 0x00;
		// �w�b�_�[�� (FrameType����Options�܂�)
		constexpr uint8_t HEADER_LENGTH = 14;
	}

	namespace UserDataRelay {
		constexpr uint8_t FRAME_ID_DEFAULT = 0x00;
		constexpr uint8_t INTERFACE_BLUETOOTH = 0x01;
		// �w�b�_�[�� (FrameType����Interface�܂�)
		constexpr uint8_t HEADER_LENGTH = 3;
	}

	// ��M�t���[���̃y�C���[�h�I�t�Z�b�g
	namespace RxPayloadOffset {
		constexpr uint8_t ZIGBEE_RECEIVE_PACKET = 14;
		constexpr uint8_t USER_DATA_RELAY_IN  = 4;
	}
} // namespace XbeeApi

// AT�R�}���h�̉������i�[����\����
struct AtCommandResponse {
	uint8_t frame_id;
	char command[2];
	uint8_t status;
	uint8_t value[16]; // �����f�[�^�i�ϒ������A�\���ȃT�C�Y���m�ہj
	uint8_t value_len;
};

class XbeeController
{
	public:
		/**
		 * @fn
		 * XBee�ʐM�̂��߂̏���������
		 * @param ����
		 * @return ����
		 */
		static void initialize(void);
		
		/**
		 * @fn
		 * Frame�ɕ����������Xbee�iZigbee�ʐM�j�ő��M����
		 * @param (data) ������
		 * @return ����
		 */
		static void txChars(const char data[]);
			
		/**
		 * @fn
		 * Frame�ɕ����������Xbee�iBluetooth�ʐM�j�ő��M����
		 * @param (data) ������
		 * @return ����
		 */
		static void blChars(const char data[]);
	
		/**
		 * @fn
		 * Frame�ɕ����������Xbee�iZigbee+Bluetooth�ʐM�j�ő��M����
		 * @param (data) ������
		 * @return ����
		 */
		static void bltxChars(const char data[]);

		/**
		 * @fn
		 * UART�ڑ����XBee��AT�R�}���h�𑗂�
		 * @param (data) AT�R�}���h
		 * @return ����
		 */
		static void sendAtCmd(const char data[]);
	
		/**
		 * @fn
		 * XBee�̐ݒ������������
		 * @return �������ς܂��͏����������̏ꍇ��1
		 */
		static bool xbeeSettingInitialized();
	
		// ...
		/**
		 * @brief ��M����1�o�C�g���g���AXBee�t���[���̉�͂��s��
		 * @param data ��M����1�o�C�g
		 * @param output_buffer [out] ���������R�}���h���i�[����o�b�t�@
		 * @param buffer_size output_buffer�̃T�C�Y
		 * @return �R�}���h�����������ꍇ��true��Ԃ�
		 */
		static bool processXbeeByte(char data, char* output_buffer, int buffer_size);
	
	private:
	
		/**
		 * @fn
		 * ������̑傫�����擾����
		 * @param (data) ������
		 * @return ������̑傫��
		 */
		static int getCharLength(const char data[]);

		/**
		 * @fn
		 * �`�F�b�N�T�������Z����
		 * @param (csum) �]�O�̃`�F�b�N�T��
		 * @param (nbyte) �ǉ�����o�C�g
		 * @return �X�V��̃`�F�b�N�T��
		 */
		static int addCsum(int csum, char nbyte);
	
		static void receiveMessage(char message[]);
		
		//AT�R�}���hAPI�֘A�̃v���C�x�[�g�֐�
		static void sendAtCommandApiFrame(const char at_command[2], uint8_t frame_id, const uint8_t* param = nullptr, uint8_t param_len = 0);
};

#endif /* XBEE_CONTROLLER_H_ */