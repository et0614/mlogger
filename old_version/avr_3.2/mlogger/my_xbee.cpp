/**
 * @file my_xbee.h
 * @brief AVR(ATMega328)��XBee�ƒʐM����
 * @author E.Togashi
 * @date 2021/11/28
 */

#include "my_xbee.h"
#include "my_uart.h"

#include <string.h>
#include <avr/io.h>

const size_t MBUFF_LENGTH = 20;

bool my_xbee::IsAPMode = true;

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
	my_uart::send_char(0x2D); //�R�}���hID�iDataRelay��0x2D�j
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

void my_xbee::send_atcmd(const char data[]){
	int cl = get_char_length(data);
	//���M�f�[�^
	for(int i=0;i<cl;i++)
		my_uart::send_char(data[i]);
}

bool my_xbee::xbee_setting_initialized(){
	bool hasChanged = false;
	char message[MBUFF_LENGTH];
	memset(message, 0, sizeof(message));

	//AT���[�h��
	my_uart::send_chars("+++");
	my_xbee::receive_message(message);
	//AT���[�h�֍s���Ȃ��ꍇ�ɂ͂��ł�API Enabled�ɂȂ��Ă���Ƃ������ƂŐ���Ɣ��f�B���܂�ǂ��Ȃ������B
	if(strcmp(message, "OK") != 0) return 1;
	
	//PAN ID��19800614
	my_uart::send_chars("atid\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "19800614") != 0) {
		my_uart::send_chars("atid19800614\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//SP��1000msec=0x64
	my_uart::send_chars("atsp\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "64") != 0) {
		my_uart::send_chars("atsp64\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//SN��3600sec
	my_uart::send_chars("atsn\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "3600") != 0) {
		my_uart::send_chars("atsn3600\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//CE��end device(0)
	my_uart::send_chars("atce\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "0") != 0) {
		my_uart::send_chars("atce0\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//SM��Pin Hibernate(1)
	my_uart::send_chars("atsm\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "1") != 0) {
		my_uart::send_chars("atsm1\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//d5��Zigbee�ʐM�󋵂�LED�\���t���OOff(0)
	my_uart::send_chars("atd5\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "0") != 0) {
		my_uart::send_chars("atd50\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//Bluetooth�L��/����+password
	my_uart::send_chars("atbt\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "0") == 0) {		
		my_uart::send_chars("at$S28513497\r"); //salt
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$V6567694B0CA9ADCED8D5B2B0015718D1E2637B86E3E178E029936A078926C2B0\r"); //$V
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$W259F6833E1E1932E4485F48865FB6B76EC6E847A7272C77A8C27DD7DF94E44DC\r"); //$W
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$XA99A6E3937FBB8D05BBB4E4A8C4CB221C14D15CD004139C77B6FE0C8AF2932D8\r"); //$X
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("at$Y6D4411D507FC52AFD5877D6E8529AEE7FB931F10944BC0D058FB246D0DE071DB\r"); //$Y
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		my_uart::send_chars("atbt1\r"); //Bluetooth �L����
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
	
	//bi��Bluetooth identifier
	my_uart::send_chars("atbi\r");
	my_xbee::receive_message(message);
	if(strncmp(message, "MLogger_", 8) != 0) {
		my_uart::send_chars("atbiMLogger_xxxx\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
				
	//AP
	my_uart::send_chars("atap\r");
	my_xbee::receive_message(message);
	if(strcmp(message, "1") != 0) {
		my_uart::send_chars("atap1\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
		hasChanged = true;
	}
		
	//�ύX���������ꍇ�ɂ̓������ɏ�������
	if(hasChanged){
		my_uart::send_chars("atwr\r");
		my_xbee::receive_message(message);
		if(strcmp(message, "OK") != 0) return 0;
	}
		
	return 1;
}

void my_xbee::receive_message(char message[]){
	uint8_t index = 0;
	while (1) {
		//1�����ǂ�	
		while (!(USART0.STATUS & USART_RXCIF_bm)){;}		
		char c = USART0.RXDATAL;  
		 
		if(c !='\n' && c != '\r'){ 
			message[index++] = c;
			if(MBUFF_LENGTH < index) index = 0;
		}
		if(c == '\r'){
			message[index] = '\0';
			return;
		}
	}
}
