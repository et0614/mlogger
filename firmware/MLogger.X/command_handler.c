#include "command_handler.h"
#include "xbee_controller.h"
#include "eeprom_manager.h"
#include "logger_control.h"
#include "usb_extension.h"

#include <string.h>
#include <stdio.h>
#include <stdlib.h>

//バージョンを示す文字列
#define VERSION_NUMBER  "VER:3.5.0\r"

//コマンドの文字数
#define CMD_LENGTH  3

//コマンド全体の最大文字数
#define MAX_CMD_CHAR  256

// ソースごとの受信状態を管理する構造体
typedef struct {
    char buff[MAX_CMD_CHAR];
    uint16_t pos;
} CommandBuffer_t;

// レスポンス作成用の文字列配列
static char charBuff[MAX_CMD_CHAR];

//コマンド組み立て用バッファ
static CommandBuffer_t usb_buffer = { {0}, 0 };
static CommandBuffer_t xbee_buffer = { {0}, 0 };

// <editor-fold defaultstate="collapsed" desc="内部関数">

static void reply(const char *msg, CommandSource_t src) {
    switch(src)
    {
        case SRC_USB:
            USB_CDC_SendString(msg);
            break;
        case SRC_XBEE:
            Xbee_TxChars(msg);
            break;
        case SRC_BLE:
            Xbee_BlChars(msg);
            break;
    }
}

// --- 内部関数: 1文字をバッファに追加し、完成したら実行する ---
static void append_char_internal(char c, CommandSource_t src) {
    CommandBuffer_t *b = (src == SRC_USB) ? &usb_buffer : &xbee_buffer;

    if (c == '\r' || c == '\n') { // \r または \n でコマンド確定
        if (b->pos > 0) {
            b->buff[b->pos] = '\0';
            CH_ProcessCommand(b->buff, src);
            b->pos = 0;
        }
    } else if (b->pos < MAX_CMD_CHAR - 1) {
        b->buff[b->pos++] = c;
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="コマンド組み立て">

void CH_AppendChar(char c, CommandSource_t src) {
    append_char_internal(c, src);
}

void CH_AppendString(const char *str, CommandSource_t src) {
    while (*str) {
        append_char_internal(*str++, src);
    }
}

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="コマンド処理">

void CH_ProcessCommand(const char *command, CommandSource_t src) {
    //接続先
	if (strncmp(command, "WHO", 3) == 0) 
        reply("M_LOGGER", src);
    
    //バージョン
	else if (strncmp(command, "VER", 3) == 0) 
        reply(VERSION_NUMBER, src);
    
	//ロギング開始
	else if (strncmp(command, "STL", 3) == 0)
	{
		//現在時刻を設定（最終計測日時=現在時刻とする）
		char num[11];
		num[10] = '\0';
		strncpy(num, command + CMD_LENGTH, 10);
        time_t ctt = atol(num);
        LC_SetCurrentTime(ctt);
        
        //書き出し先
        bool zig = (command[13]=='t' || command[13]=='e'); //XBeeで親機に書き出すか否か
		bool ble = (command[14]=='t'); //Bluetoothで書き出すか否か
		bool flsh = (command[15]=='t'); //FMに書き出すか否か
        bool usb = false;        
		
		//ロギング設定をEEPROMに保存
		EM_mSettings.start_auto = command[13]=='e'; //Endlessロギング
		EM_saveMeasurementSetting();
		
        //ロギングに入るとスリープする場合があるので直前にレスポンスを返す
        reply("STL\r", src);
        
		//ロギング開始
        LC_StartLoggingTask(zig, ble, flsh, usb);
	}
    
	//Change Measurement Settings
	else if(strncmp(command, "CMS", 3) == 0)
	{
		//設定を変更する場合にはロギングを停止させる
        LC_EndLoggingTask();
        
        EM_applyChangeMeasurementSettingsCommand(command);
		EM_makeMeasurementSettingsResponse(charBuff, "CMS");
        reply(charBuff, src);
	}
    
	//Load Measurement Settings
	else if(strncmp(command, "LMS", 3) == 0)
	{
		EM_makeMeasurementSettingsResponse(charBuff, "LMS");
		reply(charBuff, src);
	}
    
	//End Logging
	else if(strncmp(command, "ENL", 3) == 0)
	{
        LC_EndLoggingTask();
        
        reply("ENL\r", src);
	}
    
	//Set Correction Factor
	else if(strncmp(command, "SCF", 3) == 0)
	{
		EM_applySetCorrectionFactorCommand(command);
		EM_makeSetCorrectionFactorResponse(charBuff, "SCF");
        reply(charBuff, src);
	}
    
	//Load Correction Factor
	else if(strncmp(command, "LCF", 3) == 0)
	{
		EM_makeSetCorrectionFactorResponse(charBuff, "LCF");
        reply(charBuff, src);
	}	
    
	//Set Velocity Characteristics
	else if(strncmp(command, "SVC", 3) == 0)
	{
		EM_applySetVelocityCharacteristicsCommand(command);
		EM_makeSetVelocityCharateristicsResponse(charBuff, "SVC");
        reply(charBuff, src);
	}
    
	//Load Velocity Characteristics
	else if(strncmp(command, "LVC", 3) == 0)
	{
		EM_makeSetVelocityCharateristicsResponse(charBuff, "LVC");
        reply(charBuff, src);
	}	
    
	//Change Logger Name
	else if(strncmp(command, "CLN", 3) == 0)
	{
		strncpy(EM_mlName, command + CMD_LENGTH, 21);
		EM_saveName();
		
		//ACK
		char ack[22 + 4];
		sprintf(ack, "CLN:%s\r", EM_mlName);
        reply(ack, src);
	}
    
	//Load Logger Name
	else if(strncmp(command, "LLN", 3) == 0)
	{
		char name[22 + 4];
		sprintf(name, "LLN:%s\r", EM_mlName);
        reply(name, src);
	}
    
	//風速の手動校正開始
	else if(strncmp(command, "SCV", 3) == 0) 
	{
		LC_StartVelocityCalibration();
		//以降、毎秒"SCV:電圧"が送信される
	}
    
	//風速の手動校正終了
	else if(strncmp(command, "ECV", 3) == 0)
	{
		LC_EndVelocityCalibration();
        reply("ECV\r", src);
	}
    
	//CO2センサの有無
	else if(strncmp(command, "HCS", 3) == 0)
	{
        reply(LC_HasCO2Sensor() ? "HCS:1\r" : "HCS:0\r", src);
	}
    
	//CO2センサの初期化
	else if(strncmp(command, "IC2", 3) == 0){
        char num[6];
		num[5] = '\0';
		strncpy(num, command + CMD_LENGTH, 5);
        
        //初期化には12時間の連続測定が必要
        LC_FactoryResetCO2(atoi(num), 12U * 3600U);
        
        reply("IC2\r", src);
	}
    
	//CO2センサの校正
	else if(strncmp(command, "CCL", 3) == 0)
	{
        char num[6];
        num[5] = '\0';
        strncpy(num, command + CMD_LENGTH, 5);
        LC_CalibrateCO2(atoi(num), 30);
	}
    
	//現在時刻の更新
	else if (strncmp(command, "UCT", 3) == 0)
	{
		//現在時刻を設定
		char num[11];
		num[10] = '\0';
		strncpy(num, command + CMD_LENGTH, 10);
        LC_SetCurrentTime(atol(num));
        reply("UCT\r", src);
	}
    
    // Dump Data
	else if(strncmp(command, "DMP", 3) == 0)
	{
        if (src == SRC_USB) USB_DumpData(); 
        else reply("ERR:USB ONLY\r", src);
	}
    
    // Clear Data
	else if(strncmp(command, "CLR", 3) == 0)
	{
        LC_ClearData();
        reply("CLR\r\n", src); //echo back
	}
}

// </editor-fold>
