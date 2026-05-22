/* 
 * File:   logger_control.h
 * Author: e.togashi
 *
 * Created on 2025/12/31, 21:07
 */

#ifndef LOGGER_CONTROL_H
#define	LOGGER_CONTROL_H

#ifdef	__cplusplus
extern "C" {
#endif

#include "w25q256.h"
#include "command_handler.h"

#include <time.h>

// <editor-fold defaultstate="collapsed" desc="初期化処理">

/**
 * @brief センサを初期化する
 */
void LC_InitSensors(void);

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="日時管理">

/**
 * @brief 現在時刻をUNIX時間で設定する
 * @param unixTime 現在時刻(UNIX時間)
 */
void LC_SetCurrentTime(time_t unixTime);

/**
 * @brief 現在時刻をUNIX時間で取得する
 * @return 現在時刻(UNIX時間)
 */
time_t LC_GetCurrentTime(void);

/**
 * @brief 現在時刻を1秒進める
 */
void LC_TickSecond(void);

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="状態監視">

/**
 * @brief CO2センサの初期化中か否か
 * @return true:初期化中, false:初期化済
 */
bool LC_IsInitializingCO2(void);

/**
 * @brief Zigbee通信を使っているか否か
 * @return true:使用中, false:不使用
 */
bool LC_UseZigbeeConnection(void);

/**
 * @brief BLE通信を使っているか否か
 * @return true:使用中, false:不使用
 */
bool LC_UseBLEConnection(void);

/**
 * @brief ロギング中か否か
 * @return true:ロギング中, false:非ロギング中
 */
bool LC_IsLogging(void);

/**
 * @brief USB-CDC を計測値の出力先としているか否か
 * @return true: USB 出力有効、main 側はスリープせず USB タスクを駆動すべき
 */
bool LC_OutputToUSB(void);

/**
 * @brief タスクがあるか否か
 * @return true:タスクあり, false:タスクなし
 *         time_sync wake window 中も true を返す (sleep 抑制)
 */
bool LC_HasTask(void);

/**
 * @brief 時刻同期 wake window 中か否か
 *        true の間は XBee を sleep させず set_time 受信を待つ
 * @return true: window 中、false: 通常状態
 */
bool LC_IsTimeSyncWindowActive(void);

/**
 * @brief 時刻同期タスクを進める (per-second、main loop から呼ぶ)
 *        LC_TickSecond で立てられた emit pending フラグを処理し、
 *        time_sync_request イベントを送出する。
 */
void LC_ProcessTimeSyncTask(void);

/**
 * @brief CO2センサの接続状況を確認する
 */
void LC_CheckCO2Connection(void);

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="タスク管理">

/**
 * @brief ロギングタスクを開始する
 * @param toZigbee 結果をZigbeeに書き出すか否か
 * @param toBLE 結果をBluetoothLEに書き出すか否か
 * @param toFlash 結果をFlashに書き出すか否か
 * @param toUSB 結果をUSB CDCに書き出すか否か
 */
void LC_StartLoggingTask(bool toZigbee, bool toBLE, bool toFlash, bool toUSB);

/**
 * @brief ロギングタスクを終える
 */
void LC_EndLoggingTask(void);

/**
 * @brief センシングタスクを進める
 */
void LC_ProcessSensingTask(void);

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="風速関連処理">

void LC_Update_Anemometer();

// (LC_StartVelocityCalibration / LC_EndVelocityCalibration は v4 で削除:
//  風速校正は風速プローブ側 MCU に移管)

// </editor-fold>

// <editor-fold defaultstate="collapsed" desc="CO2関連処理">

bool LC_HasCO2Sensor(void);

void LC_FactoryResetCO2(uint16_t co2Level, uint16_t time);

void LC_CalibrateCO2(uint16_t co2Level, uint16_t time);

// </editor-fold>

void LC_ClearData(void);

#ifdef	__cplusplus
}
#endif

#endif	/* LOGGER_CONTROL_H */

