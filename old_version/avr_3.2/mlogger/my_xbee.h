/**
 * @file my_xbee.h
 * @brief AVR(ATMega328)でXBeeと通信する
 * @author E.Togashi
 * @date 2021/11/28
 */ 

#ifndef MY_XBEE_H_
#define MY_XBEE_H_

class my_xbee
{
	public:

	//コマンドの最大文字数
	static const int MAX_CMD_CHAR = 150;
	
	static bool IsAPMode;

	/**
	 * @fn
	 * XBee通信のための初期化処理
	 * @param 無し
	 * @return 無し
	 */
	static void Initialize(void);
		
	/**
	 * @fn
	 * Frameに文字列を入れてXbee（Zigbee通信）で送信する
	 * @param (data) 文字列
	 * @return 無し
	 */
	static void tx_chars(const char data[]);
			
	/**
	 * @fn
	 * Frameに文字列を入れてXbee（Bluetooth通信）で送信する
	 * @param (data) 文字列
	 * @return 無し
	 */
	static void bl_chars(const char data[]);
	
	/**
	 * @fn
	 * Frameに文字列を入れてXbee（Zigbee+Bluetooth通信）で送信する
	 * @param (data) 文字列
	 * @return 無し
	 */
	static void bltx_chars(const char data[]);

	/**
	 * @fn
	 * UART接続先のXBeeにATコマンドを送る
	 * @param (data) ATコマンド
	 * @return 無し
	 */
	static void send_atcmd(const char data[]);
	
	/**
	 * @fn
	 * XBeeの設定を初期化する
	 * @return 初期化済または初期化成功の場合に1
	 */
	static bool xbee_setting_initialized();
	
	private:
	
	/**
	 * @fn
	 * 文字列の大きさを取得する
	 * @param (data) 文字列
	 * @return 文字列の大きさ
	 */
	static int get_char_length(const char data[]);

	/**
	 * @fn
	 * チェックサムを加算する
	 * @param (csum) 従前のチェックサム
	 * @param (nbyte) 追加するバイト
	 * @return 更新後のチェックサム
	 */
	static int add_csum(int csum, char nbyte);
	
	static void receive_message(char message[]);
};

#endif /* MY_XBEE_H_ */