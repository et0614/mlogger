/**
 * @file my_xbee.h
 * @brief AVR(ATMega328)��XBee�ƒʐM����
 * @author E.Togashi
 * @date 2021/11/28
 */ 

#ifndef MY_XBEE_H_
#define MY_XBEE_H_

class my_xbee
{
	public:

	//�R�}���h�̍ő啶����
	static const int MAX_CMD_CHAR = 100;
	
	/**
	 * @fn
	 * Frame�ɕ����������Xbee�iZigbee�ʐM�j�ő��M����
	 * @param (data) ������
	 * @return ����
	 */
	static void tx_chars(const char data[]);
			
	/**
	 * @fn
	 * Frame�ɕ����������Xbee�iBluetooth�ʐM�j�ő��M����
	 * @param (data) ������
	 * @return ����
	 */
	static void bl_chars(const char data[]);
	
	/**
	 * @fn
	 * Frame�ɕ����������Xbee�iZigbee+Bluetooth�ʐM�j�ő��M����
	 * @param (data) ������
	 * @return ����
	 */
	static void bltx_chars(const char data[]);
	
	private:
	
	/**
	 * @fn
	 * ������̑傫�����擾����
	 * @param (data) ������
	 * @return ������̑傫��
	 */
	static int get_char_length(const char data[]);

	/**
	 * @fn
	 * �`�F�b�N�T�������Z����
	 * @param (csum) �]�O�̃`�F�b�N�T��
	 * @param (nbyte) �ǉ�����o�C�g
	 * @return �X�V��̃`�F�b�N�T��
	 */
	static int add_csum(int csum, char nbyte);
};

#endif /* MY_XBEE_H_ */