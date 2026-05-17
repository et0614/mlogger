/* 
 * File:   parameters.h
 * Author: e.togashi
 *
 * Created on December 13, 2025, 11:24 AM
 */

#ifndef PARAMETERS_H
#define	PARAMETERS_H

#ifdef	__cplusplus
extern "C" {
#endif

// ==========================================
// 【製造用設定】書き込み前にここの数値を変更する
#define MLPARAM_0000
// ==========================================
    
//ここからペースト*********************************
#ifdef	MLPARAM_0000
#define ML_NAME	"MLogger_0000"
#define DBT_COEF_A	1.0
#define DBT_COEF_B	0.0
#define HMD_COEF_A	1.0
#define HMD_COEF_B	0.0
#define GLB_COEF_A	1.0
#define GLB_COEF_B	0.0

#elif	MLPARAM_0550
#define ML_NAME	"MLogger_0550"
#define DBT_COEF_A	1.000
#define DBT_COEF_B	0.00
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.00
#define GLB_COEF_A	1.000
#define GLB_COEF_B	0.00


//ここまでペースト*********************************
#else
#error "ID not defined"
#endif

//風速係数未定義時のための汎用係数
#ifndef VOL_VEL0
#define VOL_VEL0 0.462
#endif
#ifndef VEL_COEF_A1
#define VEL_COEF_A1 0.970
#endif
#ifndef VEL_COEF_B1
#define VEL_COEF_B1 0.183
#endif
#ifndef VEL_COEF_A2
#define VEL_COEF_A2 1.584
#endif
#ifndef VEL_COEF_B2
#define VEL_COEF_B2 0.865
#endif
#ifndef VEL_SWITCH
#define VEL_SWITCH 0.410
#endif

#ifdef	__cplusplus
}
#endif

#endif	/* PARAMETERS_H */

