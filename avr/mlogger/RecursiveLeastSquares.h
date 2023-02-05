/**
 * @file RecursiveLeastSquares.h
 * @brief 逐次最小二乗法でy=ax+bの回帰係数を推定する
 * @author E.Togashi
 * @date 2022/2/5
 */ 


#ifndef RECURSIVELEASTSQUARES_H_
#define RECURSIVELEASTSQUARES_H_

class RecursiveLeastSquares
{
	public:
		//初期化済か否か
		volatile static bool Initialized;
	
		//回帰係数A
		volatile static double coefA;
	
		//回帰係数B
		volatile static double coefB;

		//回帰係数を初期化する
		static void InitializeCoefficients(double x, double y);
		
		//回帰係数を更新する
		static void UpdateCoefficients(double x, double y);
};

#endif /* RECURSIVELEASTSQUARES_H_ */