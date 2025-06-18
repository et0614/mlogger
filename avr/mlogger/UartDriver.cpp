/**
 * @file UartDriver.cpp
 * @brief AVR(ATMega328)でUART通信を行う
 * @author E.Togashi
 * @date 2020/8/4
 */

#include "UartDriver.h"

#include <stdio.h>
#include <string.h>
#include <avr/io.h>
#include <avr/interrupt.h>

#define BAUD_CALC(BAUD_RATE)	((float)(64 * F_CPU / (16 * (float)BAUD_RATE)) + 0.5)

//受信リングバッファ
#define UART_RX_BUFFER_SIZE 128 //サイズ（256以下の2のべき乗が効率的）
static char g_rx_buffer[UART_RX_BUFFER_SIZE]; //リングバッファ
static volatile uint8_t g_rx_head = 0; // 次に書き込む場所（受信リングバッファ）
static volatile uint8_t g_rx_tail = 0; // 次に読み出す場所（受信リングバッファ）

//送信リングバッファ
#define UART_TX_BUFFER_SIZE 128
static char g_tx_buffer[UART_TX_BUFFER_SIZE];
static volatile uint8_t g_tx_head = 0;
static volatile uint8_t g_tx_tail = 0;

//初期化
void UartDriver::initialize(void)
{
	//ポートの入出力設定
	PORTA.DIRSET = PIN0_bm; //TX:書き出し
	PORTA.DIRCLR = PIN1_bm; //RX:読み込み
	//RXをPullUp
	PORTA.OUTSET = PIN1_bm; //INT0：設定
	
	//ボーレートの設定
	USART0.BAUD = (uint16_t)BAUD_CALC(9600);
	
	USART0_CTRLA |= USART_RXCIF_bm; //受信完了イベント有効化
	USART0.CTRLB |= (USART_RXEN_bm | USART_TXEN_bm); //送受信有効化
	USART0.CTRLC = 0b00000011;//00 00 0 11: Asynchronous, noparity, stopbit=1, 8bit
	
	// リングバッファのインデックスを初期化
	g_rx_head = g_rx_tail = 0;
	g_tx_head = g_tx_tail = 0;
}

//1文字送信
void UartDriver::sendChar(const char data)
{
	// 次のheadの位置を計算
	uint8_t next_head = (g_tx_head + 1) % UART_TX_BUFFER_SIZE;

	// 送信バッファが満杯になるまで待機
	// (通常、メインループが十分に速ければここで待つことは稀)
	while (next_head == g_tx_tail);

	// バッファにデータを格納し、headを進める
	g_tx_buffer[g_tx_head] = data;
	g_tx_head = next_head;
	
	// 「送信データレジスタ空き(DRE)」割り込みを有効化する（送信可能な状態になったら自動的にISRが呼ばれる）
	USART0.CTRLA |= USART_DREIE_bm;
}

//文字配列を送信
void UartDriver::sendChars(const char data[])
{
	for(int i = 0; data[i] != '\0'; i++)
		sendChar(data[i]);
}

//送信処理が実行中か確認
bool UartDriver::isTransmitting(void)
{
	return (g_tx_head != g_tx_tail); // 送信バッファが空でなければ送信中
}

//受信リングバッファにデータがあるか確認する
bool UartDriver::uartRingBufferHasData(void)
{
    // headとtailが違う場所にあれば、未読データが存在する
    return (g_rx_head != g_rx_tail);
}

//受信リングバッファから1バイト読み出す
char UartDriver::uartRingBufferGet(void)
{
    // バッファが空の場合は読み出さない（呼び出し側でhas_data()を確認する前提）
    if (g_rx_head == g_rx_tail) {
        return 0; 
    }

    // tailの位置からデータを読み出す
    char data = g_rx_buffer[g_rx_tail];
    
    // tailを進める（バッファの終端に達したら0に戻る）
    g_rx_tail = (g_rx_tail + 1) % UART_RX_BUFFER_SIZE;
    
    return data;
}

/**
 * @brief UART受信割り込みサービスルーチン
 */
ISR(USART0_RXC_vect)
{
    // 受信したデータを読み出す
    char data = USART0.RXDATAL;

    // 次のheadの位置を計算
    uint8_t next_head = (g_rx_head + 1) % UART_RX_BUFFER_SIZE;

    // バッファが満杯でなければデータを格納
    if (next_head != g_rx_tail)
    {
        g_rx_buffer[g_rx_head] = data;
        g_rx_head = next_head;
    }
    // バッファが満杯の場合、受信したデータは破棄される（オーバーフロー）
}

/**
 * @brief UART送信データレジスタ空き割り込みサービスルーチン
 */
ISR(USART0_DRE_vect)
{
	// 送信バッファにデータがあれば
	if (g_tx_head != g_tx_tail)
	{
		// バッファから1文字取り出して送信
		USART0.TXDATAL = g_tx_buffer[g_tx_tail];
		g_tx_tail = (g_tx_tail + 1) % UART_TX_BUFFER_SIZE;
	}
	// 送信するデータがなくなったら、割り込みを無効化
	else USART0.CTRLA &= ~USART_DREIE_bm;
}

