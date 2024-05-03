/*
 * global_variables.c
 *
 * Created: 2024/05/03 13:18:18
 *  Author: etoga
 */ 

#include "global_variables.h"

//MLogger名称
char* ML_NAME = "MLogger_****";

//補正係数
float DBT_COEF_A = 1.000; //乾球温度
float DBT_COEF_B = 0.00;
float HMD_COEF_A = 1.000; //相対湿度
float HMD_COEF_B = 0.00;
float GLB_COEF_A = 1.000; //グローブ温度
float GLB_COEF_B = 0.00;