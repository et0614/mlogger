/*
 * File:   main.h
 * Author: e.togashi
 *
 * th_calibrator (mlogger_th_sensor 一括校正治具) の main 骨格。
 */

#ifndef MAIN_H
#define	MAIN_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdbool.h>
#include <stdint.h>
#include "w25q64.h" //内蔵フラッシュ

void showError(short int errNum);

void oneSecHandler(void);

void rstButtonHandler(void);

void executeSecondlyTask(void);

// ロギング(計測)モードの開始/停止と状態取得 (USBコマンドから制御)
void setLoggingActive(bool on);
bool getLoggingActive(void);

// ソフトウェア時計 (UNIX epoch秒) の設定/取得
void     Clock_Set(uint32_t epoch);
uint32_t Clock_Get(void);

#ifdef	__cplusplus
}
#endif

#endif	/* MAIN_H */
