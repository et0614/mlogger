/* 
 * File:   stcc4.h
 * Author: e.togashi
 *
 * Created on 2025/12/14, 14:15
 */

#ifndef STCC4_H
#define	STCC4_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
    
/**
* @brief センサーがバス上に存在するかを確認する
* @return センサーが応答すればtrue
*/
bool STCC4_isConnected();

/**
* @fn
* 初期化する
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_initialize();

/**
* @fn
* 3時間以上の不使用時などの初期化処理（22秒かかる）
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_performConditioning();

/**
* @fn
* 強制校正処理
* @param correction 補正した濃度[ppm]
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_performForcedRecalibration(uint16_t co2Level, int16_t* correction);

/**
* @fn
* 工場出荷状態に初期化
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_performFactoryReset();

/**
* @fn
* スリープさせる
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_enterSleep();

/**
* @fn
* スリープ解除する
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_exitSleep();

/**
* @fn
* 連続測定開始
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_startContinuousMeasurement();

/**
* @fn
* 連続測定開始
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_stopContinuousMeasurement();

/**
* @fn
* 1回測定する
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_measureSingleShot();

/**
* @fn
* 計測結果を読む
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_readMeasurement(uint16_t * co2, float * temperature, float * humidity);

/**
* @fn
* 調整用温湿度を設定する
* @return 成功でtrue、失敗でfalse
*/
bool STCC4_setRHTCompensation(float temperature, float humidity);


#ifdef	__cplusplus
}
#endif

#endif	/* STCC4_H */

