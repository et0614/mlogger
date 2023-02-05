/**
 * @file RecursiveLeastSquares.h
 * @brief �����ŏ����@��y=ax+b�̉�A�W���𐄒肷��
 * @author E.Togashi
 * @date 2022/2/5
 */ 


#ifndef RECURSIVELEASTSQUARES_H_
#define RECURSIVELEASTSQUARES_H_

class RecursiveLeastSquares
{
	public:
		//�������ς��ۂ�
		volatile static bool Initialized;
	
		//��A�W��A
		volatile static double coefA;
	
		//��A�W��B
		volatile static double coefB;

		//��A�W��������������
		static void InitializeCoefficients(double x, double y);
		
		//��A�W�����X�V����
		static void UpdateCoefficients(double x, double y);
};

#endif /* RECURSIVELEASTSQUARES_H_ */