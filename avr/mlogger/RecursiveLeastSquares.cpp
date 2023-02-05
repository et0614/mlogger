/**
 * @file RecursiveLeastSquares.cpp
 * @brief ’€ŸÅ¬“ñæ–@‚Åy=ax+b‚Ì‰ñ‹AŒW”‚ğ„’è‚·‚é
 * @author E.Togashi
 * @date 2022/2/5
 */

#include "RecursiveLeastSquares.h"

//‰Šú‰»Ï‚©”Û‚©
volatile bool RecursiveLeastSquares::Initialized = false;

//‰ñ‹AŒW”A
volatile double RecursiveLeastSquares::coefA = 1.0;
		
//‰ñ‹AŒW”B
volatile double RecursiveLeastSquares::coefB = 0.0;

//P(t)
volatile static double matP00 = 0.0;
volatile static double matP01 = 0.0;
volatile static double matP10 = 0.0;
volatile static double matP11 = 0.0;

volatile static bool firstUpdate = false;
volatile static double x0;
volatile static double y0;

//‰ñ‹AŒW”‚ğ‰Šú‰»‚·‚é
void RecursiveLeastSquares::InitializeCoefficients(double x, double y)
{
	x0 = x;
	y0 = y;
	firstUpdate = true;
	
	RecursiveLeastSquares::Initialized = true;
}

//‰ñ‹AŒW”‚ğXV‚·‚é
void RecursiveLeastSquares::UpdateCoefficients(double x, double y)
{
	//‰‰ñ‚Í’¼Ú‚ÉŒW”‚ğ„’è
	if(firstUpdate)
	{
		RecursiveLeastSquares::coefA = (y-y0)/(x-x0);
		RecursiveLeastSquares::coefB = -x0 * RecursiveLeastSquares::coefA + y0;
		
		double a00 = 2.0;
		double a01 = x0 + x;
		double a10 = a01;
		double a11 = x0 * x0 + x * x;
		
		double bf = 1 / (a00 * a11 - a01 * a10);
		matP00 = bf * a11;
		matP01 = -bf * a01;
		matP10 = -bf * a10;
		matP11 = bf * 2;
		
		firstUpdate = false;
	}
	//2‰ñ–ÚˆÈ~‚Í’€Ÿ„’è
	else
	{		
		double paap00 = matP00*(matP00+matP01*x)+matP10*(matP00*x+matP01*x*x);
		double paap01 = matP01*(matP00+matP01*x)+matP11*(matP00*x+matP01*x*x);
		double paap10 = matP00*(matP10+matP11*x)+matP10*(matP10*x+matP11*x*x);
		double paap11 = matP01*(matP10+matP11*x)+matP11*(matP10*x+matP11*x*x);
		
		double atpa = 1 + matP00+ matP10*x + x*(matP01+matP11*x);
		
		matP00 -= paap00 / atpa;
		matP01 -= paap01 / atpa;
		matP10 -= paap10 / atpa;
		matP11 -= paap11 / atpa;
		
		double bf = y - (RecursiveLeastSquares::coefB + RecursiveLeastSquares::coefA * x);
		
		RecursiveLeastSquares::coefB += bf * (matP00 + matP01 * x);
		RecursiveLeastSquares::coefA += bf * (matP10 + matP11 * x);		
	}
}