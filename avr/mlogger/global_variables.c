/*
 * global_variables.c
 *
 * Created: 2024/05/03 13:18:18
 *  Author: etoga
 */ 

#include "global_variables.h"

//MLogger名称
char* ML_NAME = "MLogger_1130";

//補正係数
float DBT_COEF_A = 1.003; //乾球温度
float DBT_COEF_B = 0.51;
float HMD_COEF_A = 0.978; //相対湿度
float HMD_COEF_B = 0.69;
float GLB_COEF_A = 1.013; //グローブ温度
float GLB_COEF_B = -0.11;