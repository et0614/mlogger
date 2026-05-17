/* 
 * File:   opt3001.h
 * Author: e.togashi
 *
 * Created on 2026/02/19, 14:56
 */

#ifndef OPT3001_H
#define	OPT3001_H

#ifdef	__cplusplus
extern "C" {
#endif


bool OPT3001_Initialize(void);

bool OPT3001_ReadALS(float *als);


#ifdef	__cplusplus
}
#endif

#endif	/* OPT3001_H */

