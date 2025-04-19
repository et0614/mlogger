/*
 * parameters.h
 *
 * Created: 2024/07/23 12:13:59
 *  Author: etoga
 */ 

#ifndef PARAMETERS_H_
#define PARAMETERS_H_

//ここからペースト*********************************
#ifdef	MLPARAM_0000
#define ML_NAME	"MLogger_0000"
#define DBT_COEF_A	1.0
#define DBT_COEF_B	0.0
#define HMD_COEF_A	1.0
#define HMD_COEF_B	0.0
#define GLB_COEF_A	1.0
#define GLB_COEF_B	0.0

//以下第2期製造**********************************
#elif	MLPARAM_1101
#define ML_NAME	"MLogger_1101"
#define DBT_COEF_A	1.031
#define DBT_COEF_B	-0.22
#define HMD_COEF_A	0.976
#define HMD_COEF_B	1.2
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1102
#define ML_NAME	"MLogger_1102"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	0.06
#define HMD_COEF_A	0.974
#define HMD_COEF_B	0.46
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.17

#elif	MLPARAM_1103
#define ML_NAME	"MLogger_1103"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.28
#define HMD_COEF_A	0.967
#define HMD_COEF_B	1.2
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.22

#elif	MLPARAM_1104
#define ML_NAME	"MLogger_1104"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	0.02
#define HMD_COEF_A	0.985
#define HMD_COEF_B	0.89
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.16

#elif	MLPARAM_1105
#define ML_NAME	"MLogger_1105"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.35
#define HMD_COEF_A	0.971
#define HMD_COEF_B	1.49
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.19

#elif	MLPARAM_1106
#define ML_NAME	"MLogger_1106"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.06
#define HMD_COEF_A	0.966
#define HMD_COEF_B	1.08
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1107
#define ML_NAME	"MLogger_1107"
#define DBT_COEF_A	1.003
#define DBT_COEF_B	0.51
#define HMD_COEF_A	0.958
#define HMD_COEF_B	1.82
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.17

#elif	MLPARAM_1108
#define ML_NAME	"MLogger_1108"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.37
#define HMD_COEF_A	0.972
#define HMD_COEF_B	1.01
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1109
#define ML_NAME	"MLogger_1109"
#define DBT_COEF_A	1.008
#define DBT_COEF_B	0.4
#define HMD_COEF_A	0.953
#define HMD_COEF_B	2.37
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1110
#define ML_NAME	"MLogger_1110"
#define DBT_COEF_A	1.005
#define DBT_COEF_B	0.46
#define HMD_COEF_A	0.977
#define HMD_COEF_B	1.15
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.13

#elif	MLPARAM_1111
#define ML_NAME	"MLogger_1111"
#define DBT_COEF_A	1.006
#define DBT_COEF_B	0.44
#define HMD_COEF_A	0.959
#define HMD_COEF_B	2.5
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.15

#elif	MLPARAM_1112
#define ML_NAME	"MLogger_1112"
#define DBT_COEF_A	1.024
#define DBT_COEF_B	-0.07
#define HMD_COEF_A	0.994
#define HMD_COEF_B	0.52
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.17

#elif	MLPARAM_1113
#define ML_NAME	"MLogger_1113"
#define DBT_COEF_A	0.999
#define DBT_COEF_B	0.62
#define HMD_COEF_A	0.98
#define HMD_COEF_B	1.25
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.08

#elif	MLPARAM_1114
#define ML_NAME	"MLogger_1114"
#define DBT_COEF_A	1.007
#define DBT_COEF_B	0.45
#define HMD_COEF_A	0.951
#define HMD_COEF_B	2.34
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1115
#define ML_NAME	"MLogger_1115"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	0.18
#define HMD_COEF_A	0.978
#define HMD_COEF_B	1.58
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.12

#elif	MLPARAM_1116
#define ML_NAME	"MLogger_1116"
#define DBT_COEF_A	1.008
#define DBT_COEF_B	0.32
#define HMD_COEF_A	0.968
#define HMD_COEF_B	0.99
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1117
#define ML_NAME	"MLogger_1117"
#define DBT_COEF_A	1.008
#define DBT_COEF_B	0.4
#define HMD_COEF_A	0.979
#define HMD_COEF_B	0.76
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1118
#define ML_NAME	"MLogger_1118"
#define DBT_COEF_A	1.032
#define DBT_COEF_B	-0.22
#define HMD_COEF_A	0.979
#define HMD_COEF_B	1.03
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.06

#elif	MLPARAM_1119
#define ML_NAME	"MLogger_1119"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.03
#define HMD_COEF_A	0.948
#define HMD_COEF_B	3.14
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.12

#elif	MLPARAM_1120
#define ML_NAME	"MLogger_1120"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	0.16
#define HMD_COEF_A	0.973
#define HMD_COEF_B	1.65
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1121
#define ML_NAME	"MLogger_1121"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.34
#define HMD_COEF_A	0.976
#define HMD_COEF_B	1.53
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1122
#define ML_NAME	"MLogger_1122"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.31
#define HMD_COEF_A	0.981
#define HMD_COEF_B	1.45
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.09

#elif	MLPARAM_1123
#define ML_NAME	"MLogger_1123"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.41
#define HMD_COEF_A	0.973
#define HMD_COEF_B	0.75
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.05

#elif	MLPARAM_1124
#define ML_NAME	"MLogger_1124"
#define DBT_COEF_A	1.004
#define DBT_COEF_B	0.53
#define HMD_COEF_A	0.971
#define HMD_COEF_B	1.13
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1125
#define ML_NAME	"MLogger_1125"
#define DBT_COEF_A	1.001
#define DBT_COEF_B	0.57
#define HMD_COEF_A	0.973
#define HMD_COEF_B	1.14
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1126
#define ML_NAME	"MLogger_1126"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	0.28
#define HMD_COEF_A	0.966
#define HMD_COEF_B	1.5
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1127
#define ML_NAME	"MLogger_1127"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.37
#define HMD_COEF_A	0.971
#define HMD_COEF_B	1
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1128
#define ML_NAME	"MLogger_1128"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	0.15
#define HMD_COEF_A	0.965
#define HMD_COEF_B	0.72
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.13

#elif	MLPARAM_1129
#define ML_NAME	"MLogger_1129"
#define DBT_COEF_A	1.009
#define DBT_COEF_B	0.45
#define HMD_COEF_A	0.977
#define HMD_COEF_B	1.45
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1130
#define ML_NAME	"MLogger_1130"
#define DBT_COEF_A	1.003
#define DBT_COEF_B	0.51
#define HMD_COEF_A	0.978
#define HMD_COEF_B	0.69
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1131
#define ML_NAME	"MLogger_1131"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	0.13
#define HMD_COEF_A	0.985
#define HMD_COEF_B	1.04
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1132
#define ML_NAME	"MLogger_1132"
#define DBT_COEF_A	1.006
#define DBT_COEF_B	0.53
#define HMD_COEF_A	0.978
#define HMD_COEF_B	0.79
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1133
#define ML_NAME	"MLogger_1133"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.08
#define HMD_COEF_A	0.986
#define HMD_COEF_B	0.93
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1134
#define ML_NAME	"MLogger_1134"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.26
#define HMD_COEF_A	0.947
#define HMD_COEF_B	2.68
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.09

#elif	MLPARAM_1135
#define ML_NAME	"MLogger_1135"
#define DBT_COEF_A	1.008
#define DBT_COEF_B	0.48
#define HMD_COEF_A	0.979
#define HMD_COEF_B	0.69
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1136
#define ML_NAME	"MLogger_1136"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.19
#define HMD_COEF_A	0.956
#define HMD_COEF_B	1.9
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.08

#elif	MLPARAM_1137
#define ML_NAME	"MLogger_1137"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	0.26
#define HMD_COEF_A	0.97
#define HMD_COEF_B	0.89
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1138
#define ML_NAME	"MLogger_1138"
#define DBT_COEF_A	1.009
#define DBT_COEF_B	0.4
#define HMD_COEF_A	0.979
#define HMD_COEF_B	0.25
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1139
#define ML_NAME	"MLogger_1139"
#define DBT_COEF_A	1.006
#define DBT_COEF_B	0.54
#define HMD_COEF_A	0.975
#define HMD_COEF_B	0.81
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1140
#define ML_NAME	"MLogger_1140"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.15
#define HMD_COEF_A	0.979
#define HMD_COEF_B	0.17
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.15

#elif	MLPARAM_1141
#define ML_NAME	"MLogger_1141"
#define DBT_COEF_A	1.033
#define DBT_COEF_B	-0.16
#define HMD_COEF_A	0.987
#define HMD_COEF_B	-0.07
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1142
#define ML_NAME	"MLogger_1142"
#define DBT_COEF_A	1.032
#define DBT_COEF_B	-0.12
#define HMD_COEF_A	0.967
#define HMD_COEF_B	0.88
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.23

#elif	MLPARAM_1143
#define ML_NAME	"MLogger_1143"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	0.15
#define HMD_COEF_A	0.99
#define HMD_COEF_B	-0.45
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.18

#elif	MLPARAM_1144
#define ML_NAME	"MLogger_1144"
#define DBT_COEF_A	1.024
#define DBT_COEF_B	-0.01
#define HMD_COEF_A	0.967
#define HMD_COEF_B	1.1
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1145
#define ML_NAME	"MLogger_1145"
#define DBT_COEF_A	1.034
#define DBT_COEF_B	-0.17
#define HMD_COEF_A	0.996
#define HMD_COEF_B	-0.12
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.17

#elif	MLPARAM_1146
#define ML_NAME	"MLogger_1146"
#define DBT_COEF_A	1.027
#define DBT_COEF_B	-0.16
#define HMD_COEF_A	1.002
#define HMD_COEF_B	-0.1
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1147
#define ML_NAME	"MLogger_1147"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.01
#define HMD_COEF_A	0.997
#define HMD_COEF_B	0.11
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.23

#elif	MLPARAM_1148
#define ML_NAME	"MLogger_1148"
#define DBT_COEF_A	1.036
#define DBT_COEF_B	-0.3
#define HMD_COEF_A	0.986
#define HMD_COEF_B	0.32
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.22

#elif	MLPARAM_1149
#define ML_NAME	"MLogger_1149"
#define DBT_COEF_A	1.026
#define DBT_COEF_B	-0.07
#define HMD_COEF_A	0.969
#define HMD_COEF_B	1.53
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.34

#elif	MLPARAM_1150
#define ML_NAME	"MLogger_1150"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.01
#define HMD_COEF_A	0.979
#define HMD_COEF_B	1.35
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.3

#elif	MLPARAM_1151
#define ML_NAME	"MLogger_1151"
#define DBT_COEF_A	1.007
#define DBT_COEF_B	0.35
#define HMD_COEF_A	1
#define HMD_COEF_B	0.62
#define GLB_COEF_A	1.021
#define GLB_COEF_B	-0.39

#elif	MLPARAM_1152
#define ML_NAME	"MLogger_1152"
#define DBT_COEF_A	1.034
#define DBT_COEF_B	-0.34
#define HMD_COEF_A	0.984
#define HMD_COEF_B	0.72
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.33

#elif	MLPARAM_1153
#define ML_NAME	"MLogger_1153"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.15
#define HMD_COEF_A	0.993
#define HMD_COEF_B	-0.16
#define GLB_COEF_A	1.021
#define GLB_COEF_B	-0.38

#elif	MLPARAM_1154
#define ML_NAME	"MLogger_1154"
#define DBT_COEF_A	1.029
#define DBT_COEF_B	-0.19
#define HMD_COEF_A	0.989
#define HMD_COEF_B	0.65
#define GLB_COEF_A	1.022
#define GLB_COEF_B	-0.42

#elif	MLPARAM_1155
#define ML_NAME	"MLogger_1155"
#define DBT_COEF_A	1.007
#define DBT_COEF_B	0.34
#define HMD_COEF_A	0.967
#define HMD_COEF_B	1.67
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.22

#elif	MLPARAM_1156
#define ML_NAME	"MLogger_1156"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	0.15
#define HMD_COEF_A	0.937
#define HMD_COEF_B	3
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.25

#elif	MLPARAM_1157
#define ML_NAME	"MLogger_1157"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.27
#define HMD_COEF_A	0.984
#define HMD_COEF_B	0.18
#define GLB_COEF_A	1.02
#define GLB_COEF_B	-0.39

#elif	MLPARAM_1158
#define ML_NAME	"MLogger_1158"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	0.01
#define HMD_COEF_A	0.974
#define HMD_COEF_B	1.69
#define GLB_COEF_A	1.02
#define GLB_COEF_B	-0.38

#elif	MLPARAM_1159
#define ML_NAME	"MLogger_1159"
#define DBT_COEF_A	1.032
#define DBT_COEF_B	-0.3
#define HMD_COEF_A	0.938
#define HMD_COEF_B	2.52
#define GLB_COEF_A	1.02
#define GLB_COEF_B	-0.37

#elif	MLPARAM_1160
#define ML_NAME	"MLogger_1160"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.06
#define HMD_COEF_A	0.989
#define HMD_COEF_B	0.89
#define GLB_COEF_A	1.026
#define GLB_COEF_B	-0.55

#elif	MLPARAM_1161
#define ML_NAME	"MLogger_1161"
#define DBT_COEF_A	1.02
#define DBT_COEF_B	0.1
#define HMD_COEF_A	0.977
#define HMD_COEF_B	1.2
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1162
#define ML_NAME	"MLogger_1162"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.3
#define HMD_COEF_A	0.967
#define HMD_COEF_B	1.22
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.13

#elif	MLPARAM_1163
#define ML_NAME	"MLogger_1163"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.32
#define HMD_COEF_A	0.975
#define HMD_COEF_B	1.24
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.17

#elif	MLPARAM_1164
#define ML_NAME	"MLogger_1164"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.34
#define HMD_COEF_A	0.972
#define HMD_COEF_B	1.42
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.17

#elif	MLPARAM_1165
#define ML_NAME	"MLogger_1165"
#define DBT_COEF_A	1.02
#define DBT_COEF_B	0.11
#define HMD_COEF_A	0.979
#define HMD_COEF_B	1.02
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.13

#elif	MLPARAM_1166
#define ML_NAME	"MLogger_1166"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.08
#define HMD_COEF_A	0.972
#define HMD_COEF_B	0.99
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.23

#elif	MLPARAM_1167
#define ML_NAME	"MLogger_1167"
#define DBT_COEF_A	1.005
#define DBT_COEF_B	0.48
#define HMD_COEF_A	0.977
#define HMD_COEF_B	1.3
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.25

#elif	MLPARAM_1168
#define ML_NAME	"MLogger_1168"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	0.17
#define HMD_COEF_A	0.969
#define HMD_COEF_B	1.79
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1169
#define ML_NAME	"MLogger_1169"
#define DBT_COEF_A	1.004
#define DBT_COEF_B	0.56
#define HMD_COEF_A	0.955
#define HMD_COEF_B	2.22
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1170
#define ML_NAME	"MLogger_1170"
#define DBT_COEF_A	1.004
#define DBT_COEF_B	0.5
#define HMD_COEF_A	0.963
#define HMD_COEF_B	1.16
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.15

#elif	MLPARAM_1171
#define ML_NAME	"MLogger_1171"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	0.06
#define HMD_COEF_A	0.969
#define HMD_COEF_B	1.16
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1172
#define ML_NAME	"MLogger_1172"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.09
#define HMD_COEF_A	0.99
#define HMD_COEF_B	0.11
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.15

#elif	MLPARAM_1173
#define ML_NAME	"MLogger_1173"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.11
#define HMD_COEF_A	0.975
#define HMD_COEF_B	0.72
#define GLB_COEF_A	1.009
#define GLB_COEF_B	-0.06

#elif	MLPARAM_1174
#define ML_NAME	"MLogger_1174"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.07
#define HMD_COEF_A	0.978
#define HMD_COEF_B	1.2
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1175
#define ML_NAME	"MLogger_1175"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.39
#define HMD_COEF_A	0.945
#define HMD_COEF_B	2.75
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.06

#elif	MLPARAM_1176
#define ML_NAME	"MLogger_1176"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.33
#define HMD_COEF_A	0.951
#define HMD_COEF_B	2.01
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.13

#elif	MLPARAM_1177
#define ML_NAME	"MLogger_1177"
#define DBT_COEF_A	1.007
#define DBT_COEF_B	0.43
#define HMD_COEF_A	0.979
#define HMD_COEF_B	1.52
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.13

#elif	MLPARAM_1178
#define ML_NAME	"MLogger_1178"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.32
#define HMD_COEF_A	0.972
#define HMD_COEF_B	1.47
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.05

#elif	MLPARAM_1179
#define ML_NAME	"MLogger_1179"
#define DBT_COEF_A	1.024
#define DBT_COEF_B	0.01
#define HMD_COEF_A	0.976
#define HMD_COEF_B	1.33
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.09

#elif	MLPARAM_1180
#define ML_NAME	"MLogger_1180"
#define DBT_COEF_A	1.001
#define DBT_COEF_B	0.59
#define HMD_COEF_A	0.976
#define HMD_COEF_B	0.99
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.11

#elif	MLPARAM_1181
#define ML_NAME	"MLogger_1181"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	0.32
#define HMD_COEF_A	0.966
#define HMD_COEF_B	1.97
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.05

#elif	MLPARAM_1182
#define ML_NAME	"MLogger_1182"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.35
#define HMD_COEF_A	0.974
#define HMD_COEF_B	1.11
#define GLB_COEF_A	1.01
#define GLB_COEF_B	-0.06

#elif	MLPARAM_1183
#define ML_NAME	"MLogger_1183"
#define DBT_COEF_A	1.009
#define DBT_COEF_B	0.41
#define HMD_COEF_A	0.84
#define HMD_COEF_B	6.79
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.09

#elif	MLPARAM_1184
#define ML_NAME	"MLogger_1184"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.28
#define HMD_COEF_A	0.972
#define HMD_COEF_B	1.25
#define GLB_COEF_A	1.012
#define GLB_COEF_B	-0.1

#elif	MLPARAM_1185
#define ML_NAME	"MLogger_1185"
#define DBT_COEF_A	1.003
#define DBT_COEF_B	0.49
#define HMD_COEF_A	0.974
#define HMD_COEF_B	1.22
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.18

#elif	MLPARAM_1186
#define ML_NAME	"MLogger_1186"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.41
#define HMD_COEF_A	0.972
#define HMD_COEF_B	1.22
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.14

#elif	MLPARAM_1187
#define ML_NAME	"MLogger_1187"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	0.27
#define HMD_COEF_A	0.98
#define HMD_COEF_B	0.94
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.18

#elif	MLPARAM_1188
#define ML_NAME	"MLogger_1188"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.4
#define HMD_COEF_A	0.972
#define HMD_COEF_B	1.34
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.2

#elif	MLPARAM_1189
#define ML_NAME	"MLogger_1189"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.53
#define HMD_COEF_A	0.961
#define HMD_COEF_B	1.97
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.24

#elif	MLPARAM_1190
#define ML_NAME	"MLogger_1190"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	0.06
#define HMD_COEF_A	0.991
#define HMD_COEF_B	0.55
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.26

#elif	MLPARAM_1191
#define ML_NAME	"MLogger_1191"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.33
#define HMD_COEF_A	0.99
#define HMD_COEF_B	0.94
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.23

#elif	MLPARAM_1192
#define ML_NAME	"MLogger_1192"
#define DBT_COEF_A	1.01
#define DBT_COEF_B	0.3
#define HMD_COEF_A	0.975
#define HMD_COEF_B	0.8
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.27

#elif	MLPARAM_1193
#define ML_NAME	"MLogger_1193"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	0.26
#define HMD_COEF_A	0.984
#define HMD_COEF_B	0.67
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.19

#elif	MLPARAM_1194
#define ML_NAME	"MLogger_1194"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	0.27
#define HMD_COEF_A	0.968
#define HMD_COEF_B	1.72
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.19

#elif	MLPARAM_1195
#define ML_NAME	"MLogger_1195"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.31
#define HMD_COEF_A	0.976
#define HMD_COEF_B	1.14
#define GLB_COEF_A	1.021
#define GLB_COEF_B	-0.31

#elif	MLPARAM_1196
#define ML_NAME	"MLogger_1196"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	0.07
#define HMD_COEF_A	0.987
#define HMD_COEF_B	0.69
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.26

#elif	MLPARAM_1197
#define ML_NAME	"MLogger_1197"
#define DBT_COEF_A	1.034
#define DBT_COEF_B	-0.17
#define HMD_COEF_A	0.991
#define HMD_COEF_B	-0.11
#define GLB_COEF_A	1.02
#define GLB_COEF_B	-0.3

#elif	MLPARAM_1198
#define ML_NAME	"MLogger_1198"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.37
#define HMD_COEF_A	0.983
#define HMD_COEF_B	0.65
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.24

#elif	MLPARAM_1199
#define ML_NAME	"MLogger_1199"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	0.14
#define HMD_COEF_A	0.957
#define HMD_COEF_B	2.1
#define GLB_COEF_A	1.022
#define GLB_COEF_B	-0.36

#elif	MLPARAM_1200
#define ML_NAME	"MLogger_1200"
#define DBT_COEF_A	1.03
#define DBT_COEF_B	-0.13
#define HMD_COEF_A	0.968
#define HMD_COEF_B	1.34
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.42

#elif	MLPARAM_0501
#define ML_NAME	"MLogger_0501"
#define DBT_COEF_A	1.027
#define DBT_COEF_B	-0.05
#define HMD_COEF_A	0.984
#define HMD_COEF_B	0.12
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.4

#elif	MLPARAM_0502
#define ML_NAME	"MLogger_0502"
#define DBT_COEF_A	1.032
#define DBT_COEF_B	-0.21
#define HMD_COEF_A	1.006
#define HMD_COEF_B	0.19
#define GLB_COEF_A	1.029
#define GLB_COEF_B	-0.56

#elif	MLPARAM_0503
#define ML_NAME	"MLogger_0503"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	0.06
#define HMD_COEF_A	1.012
#define HMD_COEF_B	-0.21
#define GLB_COEF_A	1.022
#define GLB_COEF_B	-0.36

#elif	MLPARAM_0504
#define ML_NAME	"MLogger_0504"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	0.11
#define HMD_COEF_A	0.988
#define HMD_COEF_B	0.62
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.39

#elif	MLPARAM_0505
#define ML_NAME	"MLogger_0505"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	0.05
#define HMD_COEF_A	0.999
#define HMD_COEF_B	0.2
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.37

#elif	MLPARAM_0506
#define ML_NAME	"MLogger_0506"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	0.09
#define HMD_COEF_A	0.996
#define HMD_COEF_B	0.42
#define GLB_COEF_A	1.03
#define GLB_COEF_B	-0.51

#elif	MLPARAM_0507
#define ML_NAME	"MLogger_0507"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	0.15
#define HMD_COEF_A	0.964
#define HMD_COEF_B	1.46
#define GLB_COEF_A	1.028
#define GLB_COEF_B	-0.51

#elif	MLPARAM_0508
#define ML_NAME	"MLogger_0508"
#define DBT_COEF_A	1.039
#define DBT_COEF_B	-0.38
#define HMD_COEF_A	0.998
#define HMD_COEF_B	-0.25
#define GLB_COEF_A	1.029
#define GLB_COEF_B	-0.55

#elif	MLPARAM_0509
#define ML_NAME	"MLogger_0509"
#define DBT_COEF_A	1.035
#define DBT_COEF_B	-0.25
#define HMD_COEF_A	0.998
#define HMD_COEF_B	-0.68
#define GLB_COEF_A	1.032
#define GLB_COEF_B	-0.65

#elif	MLPARAM_0510
#define ML_NAME	"MLogger_0510"
#define DBT_COEF_A	1.033
#define DBT_COEF_B	-0.22
#define HMD_COEF_A	1.008
#define HMD_COEF_B	-0.36
#define GLB_COEF_A	1.035
#define GLB_COEF_B	-0.71

#elif	MLPARAM_0511
#define ML_NAME	"MLogger_0511"
#define DBT_COEF_A	1.044
#define DBT_COEF_B	-0.51
#define HMD_COEF_A	1.013
#define HMD_COEF_B	-1.66
#define GLB_COEF_A	1.031
#define GLB_COEF_B	-0.59

#elif	MLPARAM_0512
#define ML_NAME	"MLogger_0512"
#define DBT_COEF_A	1.029
#define DBT_COEF_B	-0.04
#define HMD_COEF_A	1.006
#define HMD_COEF_B	-1.07
#define GLB_COEF_A	1.037
#define GLB_COEF_B	-0.73

#elif	MLPARAM_0513
#define ML_NAME	"MLogger_0513"
#define DBT_COEF_A	1.034
#define DBT_COEF_B	-0.25
#define HMD_COEF_A	0.999
#define HMD_COEF_B	-0.15
#define GLB_COEF_A	1.038
#define GLB_COEF_B	-0.74

#elif	MLPARAM_0514
#define ML_NAME	"MLogger_0514"
#define DBT_COEF_A	1.046
#define DBT_COEF_B	-0.51
#define HMD_COEF_A	1.016
#define HMD_COEF_B	-1.08
#define GLB_COEF_A	1.037
#define GLB_COEF_B	-0.75

#elif	MLPARAM_0515
#define ML_NAME	"MLogger_0515"
#define DBT_COEF_A	1.041
#define DBT_COEF_B	-0.43
#define HMD_COEF_A	0.982
#define HMD_COEF_B	1.07
#define GLB_COEF_A	1.036
#define GLB_COEF_B	-0.73

#elif	MLPARAM_0516
#define ML_NAME	"MLogger_0516"
#define DBT_COEF_A	1.05
#define DBT_COEF_B	-0.62
#define HMD_COEF_A	1.021
#define HMD_COEF_B	-1.75
#define GLB_COEF_A	1.037
#define GLB_COEF_B	-0.76

#elif	MLPARAM_0517
#define ML_NAME	"MLogger_0517"
#define DBT_COEF_A	1.051
#define DBT_COEF_B	-0.65
#define HMD_COEF_A	1.024
#define HMD_COEF_B	-1.75
#define GLB_COEF_A	1.038
#define GLB_COEF_B	-0.82

#elif	MLPARAM_0518
#define ML_NAME	"MLogger_0518"
#define DBT_COEF_A	1.041
#define DBT_COEF_B	-0.43
#define HMD_COEF_A	1.02
#define HMD_COEF_B	-1.51
#define GLB_COEF_A	1.041
#define GLB_COEF_B	-0.87

#elif	MLPARAM_0519
#define ML_NAME	"MLogger_0519"
#define DBT_COEF_A	1.043
#define DBT_COEF_B	-0.43
#define HMD_COEF_A	1.007
#define HMD_COEF_B	-0.81
#define GLB_COEF_A	1.039
#define GLB_COEF_B	-0.77

#elif	MLPARAM_0520
#define ML_NAME	"MLogger_0520"
#define DBT_COEF_A	1.038
#define DBT_COEF_B	-0.35
#define HMD_COEF_A	0.997
#define HMD_COEF_B	0.06
#define GLB_COEF_A	1.041
#define GLB_COEF_B	-0.87

#elif	MLPARAM_0521
#define ML_NAME	"MLogger_0521"
#define DBT_COEF_A	1.038
#define DBT_COEF_B	-0.33
#define HMD_COEF_A	1.015
#define HMD_COEF_B	-1
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.42

#elif	MLPARAM_0522
#define ML_NAME	"MLogger_0522"
#define DBT_COEF_A	1.043
#define DBT_COEF_B	-0.46
#define HMD_COEF_A	1.011
#define HMD_COEF_B	-0.79
#define GLB_COEF_A	1.029
#define GLB_COEF_B	-0.59

#elif	MLPARAM_0523
#define ML_NAME	"MLogger_0523"
#define DBT_COEF_A	1.051
#define DBT_COEF_B	-0.72
#define HMD_COEF_A	1.032
#define HMD_COEF_B	-1.81
#define GLB_COEF_A	1.032
#define GLB_COEF_B	-0.67

#elif	MLPARAM_0524
#define ML_NAME	"MLogger_0524"
#define DBT_COEF_A	1.054
#define DBT_COEF_B	-0.67
#define HMD_COEF_A	1.016
#define HMD_COEF_B	-1.35
#define GLB_COEF_A	1.037
#define GLB_COEF_B	-0.81

#elif	MLPARAM_0525
#define ML_NAME	"MLogger_0525"
#define DBT_COEF_A	1.049
#define DBT_COEF_B	-0.56
#define HMD_COEF_A	0.989
#define HMD_COEF_B	0.46
#define GLB_COEF_A	1.041
#define GLB_COEF_B	-0.85

#elif	MLPARAM_0526
#define ML_NAME	"MLogger_0526"
#define DBT_COEF_A	1.048
#define DBT_COEF_B	-0.56
#define HMD_COEF_A	0.992
#define HMD_COEF_B	0.28
#define GLB_COEF_A	1.038
#define GLB_COEF_B	-0.81

#elif	MLPARAM_0527
#define ML_NAME	"MLogger_0527"
#define DBT_COEF_A	1.038
#define DBT_COEF_B	-0.32
#define HMD_COEF_A	1.003
#define HMD_COEF_B	-0.88
#define GLB_COEF_A	1.022
#define GLB_COEF_B	-0.42

#elif	MLPARAM_0528
#define ML_NAME	"MLogger_0528"
#define DBT_COEF_A	1.045
#define DBT_COEF_B	-0.54
#define HMD_COEF_A	1.02
#define HMD_COEF_B	-1.37
#define GLB_COEF_A	1.024
#define GLB_COEF_B	-0.47

#elif	MLPARAM_0529
#define ML_NAME	"MLogger_0529"
#define DBT_COEF_A	1.05
#define DBT_COEF_B	-0.67
#define HMD_COEF_A	1.011
#define HMD_COEF_B	-2.08
#define GLB_COEF_A	1.026
#define GLB_COEF_B	-0.54

#elif	MLPARAM_0530
#define ML_NAME	"MLogger_0530"
#define DBT_COEF_A	1.052
#define DBT_COEF_B	-0.65
#define HMD_COEF_A	1.017
#define HMD_COEF_B	-1.43
#define GLB_COEF_A	1.029
#define GLB_COEF_B	-0.55

//以下第3期製造**********************************

#elif	MLPARAM_1201
#define ML_NAME	"MLogger_1201"
#define DBT_COEF_A	1.014
#define DBT_COEF_B	-0.01
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.73
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.495
#define VEL_COEF_A	2.352
#define VEL_COEF_B	64.287

#elif	MLPARAM_1202
#define ML_NAME	"MLogger_1202"
#define DBT_COEF_A	1.014
#define DBT_COEF_B	-0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.86
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.03
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.479
#define VEL_COEF_B	90.132

#elif	MLPARAM_1203
#define ML_NAME	"MLogger_1203"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.07
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.84
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.01
#define VEL_VEL0	1.490
#define VEL_COEF_A	2.731
#define VEL_COEF_B	101.481

#elif	MLPARAM_1204
#define ML_NAME	"MLogger_1204"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.08
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.93
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.02
#define VEL_VEL0	1.493
#define VEL_COEF_A	2.710
#define VEL_COEF_B	130.936

#elif	MLPARAM_1205
#define ML_NAME	"MLogger_1205"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.06
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.79
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.05
#define VEL_VEL0	1.510
#define VEL_COEF_A	2.632
#define VEL_COEF_B	101.413

#elif	MLPARAM_1206
#define ML_NAME	"MLogger_1206"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.83
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.06
#define VEL_VEL0	1.505
#define VEL_COEF_A	2.126
#define VEL_COEF_B	42.797

#elif	MLPARAM_1207
#define ML_NAME	"MLogger_1207"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.13
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.08
#define VEL_VEL0	1.504
#define VEL_COEF_A	2.362
#define VEL_COEF_B	50.829

#elif	MLPARAM_1208
#define ML_NAME	"MLogger_1208"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.16
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.06
#define VEL_VEL0	1.511
#define VEL_COEF_A	2.482
#define VEL_COEF_B	83.102

#elif	MLPARAM_1209
#define ML_NAME	"MLogger_1209"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.07
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.72
#define GLB_COEF_A	1.012
#define GLB_COEF_B	0.05
#define VEL_VEL0	1.509
#define VEL_COEF_A	2.817
#define VEL_COEF_B	114.745

#elif	MLPARAM_1210
#define ML_NAME	"MLogger_1210"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	-0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.88
#define GLB_COEF_A	1.012
#define GLB_COEF_B	0.07
#define VEL_VEL0	1.496
#define VEL_COEF_A	2.480
#define VEL_COEF_B	86.633

#elif	MLPARAM_1211
#define ML_NAME	"MLogger_1211"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.06
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.10
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.488
#define VEL_COEF_A	2.591
#define VEL_COEF_B	100.980

#elif	MLPARAM_1212
#define ML_NAME	"MLogger_1212"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.97
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.11
#define VEL_VEL0	1.495
#define VEL_COEF_A	2.736
#define VEL_COEF_B	125.155

#elif	MLPARAM_1213
#define ML_NAME	"MLogger_1213"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.04
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.97
#define GLB_COEF_A	1.014
#define GLB_COEF_B	0.02
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.753
#define VEL_COEF_B	121.749

#elif	MLPARAM_1214
#define ML_NAME	"MLogger_1214"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.86
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.03
#define VEL_VEL0	1.490
#define VEL_COEF_A	2.735
#define VEL_COEF_B	156.884

#elif	MLPARAM_1215
#define ML_NAME	"MLogger_1215"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	0.00
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.89
#define GLB_COEF_A	1.017
#define GLB_COEF_B	0.00
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.854
#define VEL_COEF_B	141.904

#elif	MLPARAM_1216
#define ML_NAME	"MLogger_1216"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	-0.07
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.93
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.02
#define VEL_VEL0	1.506
#define VEL_COEF_A	2.650
#define VEL_COEF_B	84.382

#elif	MLPARAM_1217
#define ML_NAME	"MLogger_1217"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.91
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.08
#define VEL_VEL0	1.495
#define VEL_COEF_A	2.814
#define VEL_COEF_B	169.154

#elif	MLPARAM_1218
#define ML_NAME	"MLogger_1218"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	-0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.87
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.486
#define VEL_COEF_A	2.686
#define VEL_COEF_B	105.927

#elif	MLPARAM_1219
#define ML_NAME	"MLogger_1219"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.22
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.03
#define VEL_VEL0	1.497
#define VEL_COEF_A	2.854
#define VEL_COEF_B	174.572

#elif	MLPARAM_1220
#define ML_NAME	"MLogger_1220"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	0.00
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.28
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.06
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.949
#define VEL_COEF_B	232.837

#elif	MLPARAM_1221
#define ML_NAME	"MLogger_1221"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	0.01
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.02
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.05
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.329
#define VEL_COEF_B	81.995

#elif	MLPARAM_1222
#define ML_NAME	"MLogger_1222"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.96
#define GLB_COEF_A	1.020
#define GLB_COEF_B	-0.11
#define VEL_VEL0	1.523
#define VEL_COEF_A	2.674
#define VEL_COEF_B	167.197

#elif	MLPARAM_1223
#define ML_NAME	"MLogger_1223"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.12
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.75
#define GLB_COEF_A	1.014
#define GLB_COEF_B	0.03
#define VEL_VEL0	1.493
#define VEL_COEF_A	2.259
#define VEL_COEF_B	51.506

#elif	MLPARAM_1224
#define ML_NAME	"MLogger_1224"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.33
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.09
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.551
#define VEL_COEF_B	81.973

#elif	MLPARAM_1225
#define ML_NAME	"MLogger_1225"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.09
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.13
#define VEL_VEL0	1.490
#define VEL_COEF_A	2.608
#define VEL_COEF_B	95.850

#elif	MLPARAM_1226
#define ML_NAME	"MLogger_1226"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.21
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.13
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.742
#define VEL_COEF_B	155.426

#elif	MLPARAM_1227
#define ML_NAME	"MLogger_1227"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	-0.16
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.13
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.04
#define VEL_VEL0	1.506
#define VEL_COEF_A	2.625
#define VEL_COEF_B	114.979

#elif	MLPARAM_1228
#define ML_NAME	"MLogger_1228"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.20
#define GLB_COEF_A	1.011
#define GLB_COEF_B	-0.02
#define VEL_VEL0	1.505
#define VEL_COEF_A	2.495
#define VEL_COEF_B	100.103

#elif	MLPARAM_1229
#define ML_NAME	"MLogger_1229"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	-0.07
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.15
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.08
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.767
#define VEL_COEF_B	145.240

#elif	MLPARAM_1230
#define ML_NAME	"MLogger_1230"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	-0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.16
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.11
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.718
#define VEL_COEF_B	142.183

#elif	MLPARAM_1231
#define ML_NAME	"MLogger_1231"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.21
#define GLB_COEF_A	1.013
#define GLB_COEF_B	-0.06
#define VEL_VEL0	1.498
#define VEL_COEF_A	2.654
#define VEL_COEF_B	122.297

#elif	MLPARAM_1232
#define ML_NAME	"MLogger_1232"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	-0.17
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.06
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.22
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.746
#define VEL_COEF_B	115.477

#elif	MLPARAM_1233
#define ML_NAME	"MLogger_1233"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.00
#define GLB_COEF_A	1.014
#define GLB_COEF_B	-0.11
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.765
#define VEL_COEF_B	120.758

#elif	MLPARAM_1234
#define ML_NAME	"MLogger_1234"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.15
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.16
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.13
#define VEL_VEL0	1.505
#define VEL_COEF_A	2.867
#define VEL_COEF_B	179.520

#elif	MLPARAM_1235
#define ML_NAME	"MLogger_1235"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.13
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.97
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.14
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.743
#define VEL_COEF_B	105.729

#elif	MLPARAM_1236
#define ML_NAME	"MLogger_1236"
#define DBT_COEF_A	1.016
#define DBT_COEF_B	-0.07
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.03
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.10
#define VEL_VEL0	1.498
#define VEL_COEF_A	2.734
#define VEL_COEF_B	108.896

#elif	MLPARAM_1237
#define ML_NAME	"MLogger_1237"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.18
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.23
#define GLB_COEF_A	1.028
#define GLB_COEF_B	-0.43
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.723
#define VEL_COEF_B	113.849

#elif	MLPARAM_1238
#define ML_NAME	"MLogger_1238"
#define DBT_COEF_A	1.024
#define DBT_COEF_B	-0.21
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.86
#define GLB_COEF_A	1.030
#define GLB_COEF_B	-0.43
#define VEL_VEL0	1.495
#define VEL_COEF_A	2.807
#define VEL_COEF_B	138.314

#elif	MLPARAM_1239
#define ML_NAME	"MLogger_1239"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.36
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.18
#define GLB_COEF_A	1.028
#define GLB_COEF_B	-0.37
#define VEL_VEL0	1.504
#define VEL_COEF_A	2.689
#define VEL_COEF_B	108.353

#elif	MLPARAM_1240
#define ML_NAME	"MLogger_1240"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.30
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.17
#define GLB_COEF_A	1.030
#define GLB_COEF_B	-0.45
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.804
#define VEL_COEF_B	123.713

#elif	MLPARAM_1241
#define ML_NAME	"MLogger_1241"
#define DBT_COEF_A	1.027
#define DBT_COEF_B	-0.32
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.88
#define GLB_COEF_A	1.031
#define GLB_COEF_B	-0.51
#define VEL_VEL0	1.499
#define VEL_COEF_A	2.882
#define VEL_COEF_B	157.955

#elif	MLPARAM_1242
#define ML_NAME	"MLogger_1242"
#define DBT_COEF_A	1.030
#define DBT_COEF_B	-0.39
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.19
#define GLB_COEF_A	1.030
#define GLB_COEF_B	-0.43
#define VEL_VEL0	1.502
#define VEL_COEF_A	2.962
#define VEL_COEF_B	195.622

#elif	MLPARAM_1243
#define ML_NAME	"MLogger_1243"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.13
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.89
#define GLB_COEF_A	1.024
#define GLB_COEF_B	-0.29
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.910
#define VEL_COEF_B	201.831

#elif	MLPARAM_1244
#define ML_NAME	"MLogger_1244"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.20
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.04
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.24
#define VEL_VEL0	1.497
#define VEL_COEF_A	3.078
#define VEL_COEF_B	278.383

#elif	MLPARAM_1245
#define ML_NAME	"MLogger_1245"
#define DBT_COEF_A	1.025
#define DBT_COEF_B	-0.29
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.22
#define GLB_COEF_A	1.026
#define GLB_COEF_B	-0.35
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.878
#define VEL_COEF_B	157.600

#elif	MLPARAM_1246
#define ML_NAME	"MLogger_1246"
#define DBT_COEF_A	1.025
#define DBT_COEF_B	-0.26
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.22
#define GLB_COEF_A	1.027
#define GLB_COEF_B	-0.40
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.864
#define VEL_COEF_B	151.754

#elif	MLPARAM_1247
#define ML_NAME	"MLogger_1247"
#define DBT_COEF_A	1.027
#define DBT_COEF_B	-0.31
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.07
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.28
#define VEL_VEL0	1.502
#define VEL_COEF_A	2.802
#define VEL_COEF_B	159.565

#elif	MLPARAM_1248
#define ML_NAME	"MLogger_1248"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.36
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.07
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.30
#define VEL_VEL0	1.509
#define VEL_COEF_A	2.848
#define VEL_COEF_B	149.230

#elif	MLPARAM_1249
#define ML_NAME	"MLogger_1249"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.17
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.81
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.27
#define VEL_VEL0	1.511
#define VEL_COEF_A	2.833
#define VEL_COEF_B	145.804

#elif	MLPARAM_1250
#define ML_NAME	"MLogger_1250"
#define DBT_COEF_A	1.025
#define DBT_COEF_B	-0.24
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.26
#define GLB_COEF_A	1.022
#define GLB_COEF_B	-0.20
#define VEL_VEL0	1.505
#define VEL_COEF_A	2.794
#define VEL_COEF_B	150.260

#elif	MLPARAM_1251
#define ML_NAME	"MLogger_1251"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.15
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.04
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.20
#define VEL_VEL0	1.513
#define VEL_COEF_A	2.381
#define VEL_COEF_B	70.893

#elif	MLPARAM_1252
#define ML_NAME	"MLogger_1252"
#define DBT_COEF_A	1.024
#define DBT_COEF_B	-0.22
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.03
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.29
#define VEL_VEL0	1.496
#define VEL_COEF_A	2.529
#define VEL_COEF_B	93.598

#elif	MLPARAM_1253
#define ML_NAME	"MLogger_1253"
#define DBT_COEF_A	1.026
#define DBT_COEF_B	-0.26
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.15
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.34
#define VEL_VEL0	1.496
#define VEL_COEF_A	2.502
#define VEL_COEF_B	79.495

#elif	MLPARAM_1254
#define ML_NAME	"MLogger_1254"
#define DBT_COEF_A	1.027
#define DBT_COEF_B	-0.27
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.28
#define GLB_COEF_A	1.027
#define GLB_COEF_B	-0.37
#define VEL_VEL0	1.492
#define VEL_COEF_A	2.401
#define VEL_COEF_B	61.768

#elif	MLPARAM_1255
#define ML_NAME	"MLogger_1255"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.23
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.10
#define GLB_COEF_A	1.026
#define GLB_COEF_B	-0.32
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.684
#define VEL_COEF_B	130.003

#elif	MLPARAM_1256
#define ML_NAME	"MLogger_1256"
#define DBT_COEF_A	1.025
#define DBT_COEF_B	-0.22
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.09
#define GLB_COEF_A	1.027
#define GLB_COEF_B	-0.36
#define VEL_VEL0	1.514
#define VEL_COEF_A	2.667
#define VEL_COEF_B	91.409

#elif	MLPARAM_1257
#define ML_NAME	"MLogger_1257"
#define DBT_COEF_A	1.026
#define DBT_COEF_B	-0.32
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.04
#define GLB_COEF_A	1.026
#define GLB_COEF_B	-0.30
#define VEL_VEL0	1.506
#define VEL_COEF_A	2.701
#define VEL_COEF_B	86.862

#elif	MLPARAM_1258
#define ML_NAME	"MLogger_1258"
#define DBT_COEF_A	1.027
#define DBT_COEF_B	-0.35
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.25
#define GLB_COEF_A	1.027
#define GLB_COEF_B	-0.36
#define VEL_VEL0	1.497
#define VEL_COEF_A	2.704
#define VEL_COEF_B	127.176

#elif	MLPARAM_1259
#define ML_NAME	"MLogger_1259"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.35
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.95
#define GLB_COEF_A	1.028
#define GLB_COEF_B	-0.33
#define VEL_VEL0	1.502
#define VEL_COEF_A	2.688
#define VEL_COEF_B	116.871

#elif	MLPARAM_1260
#define ML_NAME	"MLogger_1260"
#define DBT_COEF_A	1.026
#define DBT_COEF_B	-0.25
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.96
#define GLB_COEF_A	1.029
#define GLB_COEF_B	-0.35
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.760
#define VEL_COEF_B	138.240

#elif	MLPARAM_1261
#define ML_NAME	"MLogger_1261"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.00
#define GLB_COEF_A	1.021
#define GLB_COEF_B	-0.13
#define VEL_VEL0	1.496
#define VEL_COEF_A	2.836
#define VEL_COEF_B	160.594

#elif	MLPARAM_1262
#define ML_NAME	"MLogger_1262"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.01
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.86
#define GLB_COEF_A	1.020
#define GLB_COEF_B	-0.14
#define VEL_VEL0	1.507
#define VEL_COEF_A	2.761
#define VEL_COEF_B	117.219

#elif	MLPARAM_1263
#define ML_NAME	"MLogger_1263"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.30
#define GLB_COEF_A	1.021
#define GLB_COEF_B	-0.13
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.771
#define VEL_COEF_B	137.069

#elif	MLPARAM_1264
#define ML_NAME	"MLogger_1264"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.87
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.630
#define VEL_COEF_B	82.945

#elif	MLPARAM_1265
#define ML_NAME	"MLogger_1265"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.08
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.81
#define GLB_COEF_A	1.022
#define GLB_COEF_B	-0.15
#define VEL_VEL0	1.511
#define VEL_COEF_A	2.834
#define VEL_COEF_B	129.242

#elif	MLPARAM_1266
#define ML_NAME	"MLogger_1266"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.09
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.20
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.01
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.732
#define VEL_COEF_B	146.114

#elif	MLPARAM_1267
#define ML_NAME	"MLogger_1267"
#define DBT_COEF_A	1.024
#define DBT_COEF_B	-0.20
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.21
#define GLB_COEF_A	1.027
#define GLB_COEF_B	-0.32
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.767
#define VEL_COEF_B	175.816

#elif	MLPARAM_1268
#define ML_NAME	"MLogger_1268"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.04
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.23
#define VEL_VEL0	1.512
#define VEL_COEF_A	2.783
#define VEL_COEF_B	130.666

#elif	MLPARAM_1269
#define ML_NAME	"MLogger_1269"
#define DBT_COEF_A	1.021
#define DBT_COEF_B	-0.12
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.12
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.22
#define VEL_VEL0	1.497
#define VEL_COEF_A	2.785
#define VEL_COEF_B	210.172

#elif	MLPARAM_1270
#define ML_NAME	"MLogger_1270"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	-0.15
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.03
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.23
#define VEL_VEL0	1.498
#define VEL_COEF_A	2.719
#define VEL_COEF_B	110.297

#elif	MLPARAM_1271
#define ML_NAME	"MLogger_1271"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.08
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.93
#define GLB_COEF_A	1.020
#define GLB_COEF_B	-0.08
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.388
#define VEL_COEF_B	67.408

#elif	MLPARAM_1272
#define ML_NAME	"MLogger_1272"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.08
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.18
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.06
#define VEL_VEL0	1.498
#define VEL_COEF_A	2.780
#define VEL_COEF_B	134.396

#elif	MLPARAM_1273
#define ML_NAME	"MLogger_1273"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	-0.03
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.76
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.18
#define VEL_VEL0	1.496
#define VEL_COEF_A	2.615
#define VEL_COEF_B	128.953

#elif	MLPARAM_1274
#define ML_NAME	"MLogger_1274"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.08
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.86
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.02
#define VEL_VEL0	1.490
#define VEL_COEF_A	2.881
#define VEL_COEF_B	119.332

#elif	MLPARAM_1275
#define ML_NAME	"MLogger_1275"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	-0.03
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.28
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.12
#define VEL_VEL0	1.499
#define VEL_COEF_A	2.755
#define VEL_COEF_B	132.190

#elif	MLPARAM_1276
#define ML_NAME	"MLogger_1276"
#define DBT_COEF_A	1.014
#define DBT_COEF_B	-0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.93
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.15
#define VEL_VEL0	1.493
#define VEL_COEF_A	2.703
#define VEL_COEF_B	135.224

#elif	MLPARAM_1277
#define ML_NAME	"MLogger_1277"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.03
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.96
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.12
#define VEL_VEL0	1.498
#define VEL_COEF_A	2.623
#define VEL_COEF_B	122.447

#elif	MLPARAM_1278
#define ML_NAME	"MLogger_1278"
#define DBT_COEF_A	1.014
#define DBT_COEF_B	0.03
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.03
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.604
#define VEL_COEF_B	91.822

#elif	MLPARAM_1279
#define ML_NAME	"MLogger_1279"
#define DBT_COEF_A	1.014
#define DBT_COEF_B	0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.35
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.496
#define VEL_COEF_A	2.765
#define VEL_COEF_B	130.226

#elif	MLPARAM_1280
#define ML_NAME	"MLogger_1280"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.04
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.99
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.03
#define VEL_VEL0	1.509
#define VEL_COEF_A	2.829
#define VEL_COEF_B	153.437

#elif	MLPARAM_1281
#define ML_NAME	"MLogger_1281"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.08
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.87
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.03
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.866
#define VEL_COEF_B	193.915

#elif	MLPARAM_1282
#define ML_NAME	"MLogger_1282"
#define DBT_COEF_A	1.011
#define DBT_COEF_B	0.10
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.84
#define GLB_COEF_A	1.015
#define GLB_COEF_B	-0.07
#define VEL_VEL0	1.510
#define VEL_COEF_A	2.885
#define VEL_COEF_B	155.599

#elif	MLPARAM_1283
#define ML_NAME	"MLogger_1283"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.06
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.04
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.03
#define VEL_VEL0	1.517
#define VEL_COEF_A	2.874
#define VEL_COEF_B	177.977

#elif	MLPARAM_1284
#define ML_NAME	"MLogger_1284"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.04
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.02
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.09
#define VEL_VEL0	1.504
#define VEL_COEF_A	2.646
#define VEL_COEF_B	104.486

#elif	MLPARAM_1285
#define ML_NAME	"MLogger_1285"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	-0.25
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.43
#define GLB_COEF_A	1.024
#define GLB_COEF_B	-0.33
#define VEL_VEL0	1.498
#define VEL_COEF_A	2.783
#define VEL_COEF_B	141.338

#elif	MLPARAM_1286
#define ML_NAME	"MLogger_1286"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.14
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.28
#define GLB_COEF_A	1.024
#define GLB_COEF_B	-0.31
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.658
#define VEL_COEF_B	92.828

#elif	MLPARAM_1287
#define ML_NAME	"MLogger_1287"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	-0.24
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.02
#define GLB_COEF_A	1.024
#define GLB_COEF_B	-0.32
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.699
#define VEL_COEF_B	105.487

#elif	MLPARAM_1288
#define ML_NAME	"MLogger_1288"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.16
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.97
#define GLB_COEF_A	1.024
#define GLB_COEF_B	-0.31
#define VEL_VEL0	1.504
#define VEL_COEF_A	2.912
#define VEL_COEF_B	178.784

#elif	MLPARAM_1289
#define ML_NAME	"MLogger_1289"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.20
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.02
#define GLB_COEF_A	1.023
#define GLB_COEF_B	-0.23
#define VEL_VEL0	1.504
#define VEL_COEF_A	2.879
#define VEL_COEF_B	282.960

#elif	MLPARAM_1290
#define ML_NAME	"MLogger_1290"
#define DBT_COEF_A	1.022
#define DBT_COEF_B	-0.24
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.12
#define GLB_COEF_A	1.025
#define GLB_COEF_B	-0.28
#define VEL_VEL0	1.514
#define VEL_COEF_A	2.689
#define VEL_COEF_B	158.557

#elif	MLPARAM_1291
#define ML_NAME	"MLogger_1291"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.55
#define GLB_COEF_A	1.020
#define GLB_COEF_B	-0.20
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.464
#define VEL_COEF_B	78.730

#elif	MLPARAM_1292
#define ML_NAME	"MLogger_1292"
#define DBT_COEF_A	1.019
#define DBT_COEF_B	-0.17
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.18
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.12
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.501
#define VEL_COEF_B	94.002

#elif	MLPARAM_1293
#define ML_NAME	"MLogger_1293"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.13
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.26
#define GLB_COEF_A	1.021
#define GLB_COEF_B	-0.21
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.683
#define VEL_COEF_B	132.944

#elif	MLPARAM_1294
#define ML_NAME	"MLogger_1294"
#define DBT_COEF_A	1.020
#define DBT_COEF_B	-0.19
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.30
#define GLB_COEF_A	1.019
#define GLB_COEF_B	-0.18
#define VEL_VEL0	1.511
#define VEL_COEF_A	2.879
#define VEL_COEF_B	149.223

#elif	MLPARAM_1295
#define ML_NAME	"MLogger_1295"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.11
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.20
#define GLB_COEF_A	1.016
#define GLB_COEF_B	-0.16
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.651
#define VEL_COEF_B	90.696

#elif	MLPARAM_1296
#define ML_NAME	"MLogger_1296"
#define DBT_COEF_A	1.014
#define DBT_COEF_B	0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.17
#define GLB_COEF_A	1.018
#define GLB_COEF_B	-0.18
#define VEL_VEL0	1.516
#define VEL_COEF_A	2.824
#define VEL_COEF_B	124.614

#elif	MLPARAM_1297
#define ML_NAME	"MLogger_1297"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.04
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.82
#define GLB_COEF_A	1.014
#define GLB_COEF_B	0.04
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.794
#define VEL_COEF_B	140.665

#elif	MLPARAM_1298
#define ML_NAME	"MLogger_1298"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.11
#define GLB_COEF_A	1.015
#define GLB_COEF_B	0.07
#define VEL_VEL0	1.494
#define VEL_COEF_A	2.844
#define VEL_COEF_B	126.832

#elif	MLPARAM_1299
#define ML_NAME	"MLogger_1299"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.06
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.60
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.06
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.912
#define VEL_COEF_B	156.114

#elif	MLPARAM_1300
#define ML_NAME	"MLogger_1300"
#define DBT_COEF_A	1.026
#define DBT_COEF_B	-0.32
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.06
#define GLB_COEF_A	1.032
#define GLB_COEF_B	-0.46
#define VEL_VEL0	1.504
#define VEL_COEF_A	2.895
#define VEL_COEF_B	214.914

#elif	MLPARAM_0531
#define ML_NAME	"MLogger_0531"
#define DBT_COEF_A	1.017
#define DBT_COEF_B	-0.02
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.95
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.05
#define VEL_VEL0	1.518
#define VEL_COEF_A	2.556
#define VEL_COEF_B	81.323

#elif	MLPARAM_0532
#define ML_NAME	"MLogger_0532"
#define DBT_COEF_A	1.012
#define DBT_COEF_B	0.15
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.82
#define GLB_COEF_A	1.015
#define GLB_COEF_B	0.05
#define VEL_VEL0	1.506
#define VEL_COEF_A	2.463
#define VEL_COEF_B	88.246

#elif	MLPARAM_0533
#define ML_NAME	"MLogger_0533"
#define DBT_COEF_A	1.018
#define DBT_COEF_B	-0.03
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.97
#define GLB_COEF_A	1.011
#define GLB_COEF_B	0.13
#define VEL_VEL0	1.497
#define VEL_COEF_A	2.379
#define VEL_COEF_B	74.392

#elif	MLPARAM_0534
#define ML_NAME	"MLogger_0534"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.04
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.97
#define GLB_COEF_A	1.016
#define GLB_COEF_B	0.01
#define VEL_VEL0	1.497
#define VEL_COEF_A	2.429
#define VEL_COEF_B	108.624

#elif	MLPARAM_0535
#define ML_NAME	"MLogger_0535"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	0.05
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.11
#define GLB_COEF_A	1.015
#define GLB_COEF_B	0.04
#define VEL_VEL0	1.489
#define VEL_COEF_A	2.457
#define VEL_COEF_B	60.665

#elif	MLPARAM_0536
#define ML_NAME	"MLogger_0536"
#define DBT_COEF_A	1.013
#define DBT_COEF_B	0.13
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.75
#define GLB_COEF_A	1.012
#define GLB_COEF_B	0.08
#define VEL_VEL0	1.502
#define VEL_COEF_A	2.538
#define VEL_COEF_B	80.196

#elif	MLPARAM_0537
#define ML_NAME	"MLogger_0537"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	0.04
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.94
#define GLB_COEF_A	1.017
#define GLB_COEF_B	-0.03
#define VEL_VEL0	1.510
#define VEL_COEF_A	2.510
#define VEL_COEF_B	104.761

#elif	MLPARAM_0538
#define ML_NAME	"MLogger_0538"
#define DBT_COEF_A	1.015
#define DBT_COEF_B	0.00
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.07
#define GLB_COEF_A	1.013
#define GLB_COEF_B	0.06
#define VEL_VEL0	1.519
#define VEL_COEF_A	2.553
#define VEL_COEF_B	88.064

#elif	MLPARAM_0539
#define ML_NAME	"MLogger_0539"
#define DBT_COEF_A	1.033
#define DBT_COEF_B	-0.50
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.25
#define GLB_COEF_A	1.034
#define GLB_COEF_B	-0.49
#define VEL_VEL0	1.515
#define VEL_COEF_A	2.503
#define VEL_COEF_B	79.109

#elif	MLPARAM_0540
#define ML_NAME	"MLogger_0540"
#define DBT_COEF_A	1.032
#define DBT_COEF_B	-0.42
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.19
#define GLB_COEF_A	1.033
#define GLB_COEF_B	-0.47
#define VEL_VEL0	1.511
#define VEL_COEF_A	2.570
#define VEL_COEF_B	138.400

#elif	MLPARAM_0541
#define ML_NAME	"MLogger_0541"
#define DBT_COEF_A	1.029
#define DBT_COEF_B	-0.34
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.19
#define GLB_COEF_A	1.033
#define GLB_COEF_B	-0.48
#define VEL_VEL0	1.511
#define VEL_COEF_A	2.542
#define VEL_COEF_B	79.702

#elif	MLPARAM_0542
#define ML_NAME	"MLogger_0542"
#define DBT_COEF_A	1.029
#define DBT_COEF_B	-0.38
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.42
#define GLB_COEF_A	1.032
#define GLB_COEF_B	-0.48
#define VEL_VEL0	1.508
#define VEL_COEF_A	2.579
#define VEL_COEF_B	92.303

#elif	MLPARAM_0543
#define ML_NAME	"MLogger_0543"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.33
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.19
#define GLB_COEF_A	1.031
#define GLB_COEF_B	-0.42
#define VEL_VEL0	1.502
#define VEL_COEF_A	2.334
#define VEL_COEF_B	77.285

#elif	MLPARAM_0544
#define ML_NAME	"MLogger_0544"
#define DBT_COEF_A	1.023
#define DBT_COEF_B	-0.14
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.39
#define GLB_COEF_A	1.028
#define GLB_COEF_B	-0.36
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.362
#define VEL_COEF_B	96.061

#elif	MLPARAM_0545
#define ML_NAME	"MLogger_0545"
#define DBT_COEF_A	1.031
#define DBT_COEF_B	-0.44
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.41
#define GLB_COEF_A	1.037
#define GLB_COEF_B	-0.55
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.619
#define VEL_COEF_B	79.888

#elif	MLPARAM_0546
#define ML_NAME	"MLogger_0546"
#define DBT_COEF_A	1.032
#define DBT_COEF_B	-0.42
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.28
#define GLB_COEF_A	1.032
#define GLB_COEF_B	-0.51
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.386
#define VEL_COEF_B	54.240

#elif	MLPARAM_0547
#define ML_NAME	"MLogger_0547"
#define DBT_COEF_A	1.029
#define DBT_COEF_B	-0.41
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.38
#define GLB_COEF_A	1.032
#define GLB_COEF_B	-0.50
#define VEL_VEL0	1.509
#define VEL_COEF_A	2.459
#define VEL_COEF_B	75.949

#elif	MLPARAM_0548
#define ML_NAME	"MLogger_0548"
#define DBT_COEF_A	1.028
#define DBT_COEF_B	-0.34
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.21
#define GLB_COEF_A	1.033
#define GLB_COEF_B	-0.52
#define VEL_VEL0	1.503
#define VEL_COEF_A	2.339
#define VEL_COEF_B	66.942

#elif	MLPARAM_0549
#define ML_NAME	"MLogger_0549"
#define DBT_COEF_A	1.026
#define DBT_COEF_B	-0.33
#define HMD_COEF_A	1.000
#define HMD_COEF_B	1.40
#define GLB_COEF_A	1.033
#define GLB_COEF_B	-0.56
#define VEL_VEL0	1.500
#define VEL_COEF_A	2.229
#define VEL_COEF_B	58.864

#elif	MLPARAM_0550
#define ML_NAME	"MLogger_0550"
#define DBT_COEF_A	1.000
#define DBT_COEF_B	0.00
#define HMD_COEF_A	1.000
#define HMD_COEF_B	0.00
#define GLB_COEF_A	1.000
#define GLB_COEF_B	0.00
#define VEL_VEL0	1.501
#define VEL_COEF_A	2.166
#define VEL_COEF_B	48.757


//ここまでペースト*********************************
#else
#error "ID not defined"
#endif

//風速係数未定義時のための汎用係数
#ifndef VEL_VEL0
#define VEL_VEL0 1.50
#endif
#ifndef VEL_COEF_A
#define VEL_COEF_A 2.730
#endif
#ifndef VEL_COEF_B
#define VEL_COEF_B 128.0
#endif

#endif /* PARAMETERS_H_ */