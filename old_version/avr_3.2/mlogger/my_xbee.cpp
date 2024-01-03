/**
 * @file my_xbee.h
 * @brief AVR(ATMega328)��XBee�ƒʐM����
 * @author E.Togashi
 * @date 2021/11/28
 */

#include "my_xbee.h"
#include "my_uart.h"

void my_xbee::Initialize(void)
{
	my_uart::Initialize();	
}

//�R�[�f�B�l�[�^�ɑ΂��ĕ����z��𑗐M
void my_xbee::tx_chars(const char data[])
{
	int chkSum = 0;
	int cl = get_char_length(data);
	
	my_uart::send_char(0x7E); //API�t���[���J�n�R�[�h
	my_uart::send_char((char)(((cl + 14) >> 8) & 0xff));	//�f�[�^���̏�ʃo�C�g
	my_uart::send_char((char)((cl + 14) & 0xff));			//�f�[�^���̉��ʃo�C�g
	
	//��������`�F�b�N�T�����Z*************
	my_uart::send_char(0x10); //�R�}���hID�i�f�[�^���M��0x10�j
	chkSum = add_csum(chkSum, 0x10);
	
	my_uart::send_char(0x00); //�t���[��ID�i�C�Ӂj//0�ȊO����ACK���߂��Ă���B����Ȃ��̂�0
	
	for(int i=0;i<8;i++) //64bit���M��A�h���X�̓R�[�f�B�l�[�^�ւ̑��M�Ȃ̂ł��ׂ�0�Ń`�F�b�N�T���͕s��
		my_uart::send_char(0x00);
		
	my_uart::send_char(0xFF); //16bit���M��A�h���X_M
	chkSum = add_csum(chkSum, 0xFF);
	
	my_uart::send_char(0xFE); //16bit���M��A�h���X_L
	chkSum = add_csum(chkSum, 0xFE);
	
	my_uart::send_char(0x00); //�u���[�h�L���X�g���a�i���j�L���X�g�Ȃ̂�0�Ń`�F�b�N�T���͕s�ρj

	my_uart::send_char(0x00); //���M�I�v�V������0�Ń`�F�b�N�T���͕s��
	
	//���M�f�[�^
	for(int i=0;i<cl;i++)
	{
		my_uart::send_char(data[i]);		
		chkSum = add_csum(chkSum, data[i]);
	}
	
	my_uart::send_char((char)(0xff - chkSum)); //Checksum���M
}

int my_xbee::get_char_length(const char data[])
{
	int length = 0;
	for(length = 0; data[length]; length++);
	return length;
}

int my_xbee::add_csum(int csum, char nbyte)
{
	csum += (int)nbyte;
	csum = csum & 0x00ff;
	return csum;
}

//Bluetooth�ڑ���ɑ΂��ĕ����z��𑗐M
void my_xbee::bl_chars(const char data[])
{
	int chkSum = 0;
	int cl = get_char_length(data);
	
	my_uart::send_char(0x7E); //API�t���[���J�n�R�[�h
	my_uart::send_char((char)(((cl + 3) >> 8) & 0xff));	//�f�[�^���̏�ʃo�C�g
	my_uart::send_char((char)((cl + 3) & 0xff));			//�f�[�^���̉��ʃo�C�g
	
	//��������`�F�b�N�T�����Z*************
	my_uart::send_char(0x2D); //�t���[�� type�iDataRelay��0x2D�j
	chkSum = add_csum(chkSum, 0x2D);
	
	my_uart::send_char(0x00); //�t���[��ID
	
	my_uart::send_char(0x01); //Source interface�iBluetooth��0x01�j
	chkSum = add_csum(chkSum, 0x01);
		
	//���M�f�[�^
	for(int i=0;i<cl;i++)
	{
		my_uart::send_char(data[i]);
		chkSum = add_csum(chkSum, data[i]);
	}
	
	my_uart::send_char((char)(0xff - chkSum)); //Checksum���M
}

void my_xbee::bltx_chars(const char data[])
{
	tx_chars(data);
	bl_chars(data);	
}

