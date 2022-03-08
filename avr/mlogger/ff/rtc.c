/*--------------------------------------------------------------------------*/
/*  RTC controls                                                            */

#include <avr/io.h>
#include <string.h>
#include "rtc.h"
#include "integer.h"

//12.09.10 RTCÇÕå≈íËéûçè

//#define SCL_LOW()     DDRE |= 0x04                    /* SCL = LOW */
//#define SCL_HIGH()    DDRE &= 0xFB                    /* SCL = High-Z */
//#define       SCL_VAL         ((PINE & 0x04) ? 1 : 0) /* SCL input level */
//#define SDA_LOW()     DDRE |= 0x08                    /* SDA = LOW */
//#define SDA_HIGH()    DDRE &= 0xF7                    /* SDA = High-Z */
//#define       SDA_VAL         ((PINE & 0x08) ? 1 : 0) /* SDA input level */

/*-------------------------------------------------*/

/* I2C block read/write controls                   */
int iic_read (
        BYTE dev,               /* Device address */
        UINT adr,               /* Read start address */
        UINT cnt,               /* Read byte count */
        void* buff              /* Read data buffer */
)
{
// 12.09.10 RTCÇÕå≈íËéûçè
        return 1;
        //BYTE *rbuff = buff;
        //int n;

        //if (!cnt) return 0;
        //n = 10;
        //do {                                                /* Select device */
		           //iic_start();
		//} while (!iic_send(dev) && --n);

        //if (n) {
                //if (iic_send((BYTE)adr)) {            /* Set start address */
                        //iic_start();                          /* Reselect device in read mode */
                        //if (iic_send(dev | 1)) {
                                //do {                                  /* Receive data */
                                        //cnt--;
                                        //*rbuff++ = iic_rcvr(cnt ? 1 : 0);
                                //} while (cnt);
                        //}
                //}
        //}
        //iic_stop();                                           /* Deselect device */
        //return cnt ? 0 : 1;
}

int iic_write (
        BYTE dev,                       /* Device address */
        UINT adr,                       /* Write start address */
        UINT cnt,                       /* Write byte count */
        const void* buff        /* Data to be written */
)

{

        // 12.09.10 RTCÇÕå≈íËéûçè

        return 1;

        //const BYTE *wbuff = buff;

        //int n;

//

//

        //if (!cnt) return 0;

//

        //n = 10;

        //do {                                                  /* Select device */

                //iic_start();

        //} while (!iic_send(dev) && --n);

        //if (n) {

                //if (iic_send((BYTE)adr)) {            /* Set start address */

                        //do {                                          /* Send data */

                                //if (!iic_send(*wbuff++)) break;

                        //} while (--cnt);

                //}

        //}

//

        //iic_stop();                                           /* Deselect device */

//

        //return cnt ? 0 : 1;

}



int rtc_gettime (RTC *rtc)
{

        // 12.09.10 RTCÇÕå≈íËéûçè

        

        //BYTE buf[8];



        //if (!iic_read(0xD0, 0, 7, buf)) return 0;

//

        //rtc->sec = (buf[0] & 0x0F) + ((buf[0] >> 4) & 7) * 10;

        //rtc->min = (buf[1] & 0x0F) + (buf[1] >> 4) * 10;

        //rtc->hour = (buf[2] & 0x0F) + ((buf[2] >> 4) & 3) * 10;

        //rtc->wday = (buf[2] & 0x07);

        //rtc->mday = (buf[4] & 0x0F) + ((buf[4] >> 4) & 3) * 10;

        //rtc->month = (buf[5] & 0x0F) + ((buf[5] >> 4) & 1) * 10;

        //rtc->year = 2000 + (buf[6] & 0x0F) + (buf[6] >> 4) * 10;



        rtc->sec = 0;

        rtc->min = 0;

        rtc->hour = 0;

        rtc->wday = 1;

        rtc->mday = 1;

        rtc->month = 1;

        rtc->year = 2012;

        return 1;

}


/*---------------------------------------------------------*/
/* User Provided Timer Function for FatFs module */
/*---------------------------------------------------------*/
/* This is a real time clock service to be called from */
/* FatFs module. Any valid time must be returned even if */
/* the system does not support a real time clock. */
/* This is not required in read-only configuration. */
DWORD get_fattime (void)
{
	//RTC rtc;

	/* Get local time */
	//rtc_gettime(&rtc);

	/* Pack date and time into a DWORD variable */
	return ((DWORD)(myRTC.year - 1980) << 25)
	| ((DWORD)myRTC.month << 21)
	| ((DWORD)myRTC.mday << 16)
	| ((DWORD)myRTC.hour << 11)
	| ((DWORD)myRTC.min << 5)
	| ((DWORD)myRTC.sec >> 1);
}


int rtc_settime (const RTC *rtc)

{

        // 12.09.10 RTCÇÕå≈íËéûçè    

        //BYTE buf[8];

//

//

        //buf[0] = rtc->sec / 10 * 16 + rtc->sec % 10;

        //buf[1] = rtc->min / 10 * 16 + rtc->min % 10;

        //buf[2] = rtc->hour / 10 * 16 + rtc->hour % 10;

        //buf[3] = rtc->wday & 7;

        //buf[4] = rtc->mday / 10 * 16 + rtc->mday % 10;

        //buf[5] = rtc->month / 10 * 16 + rtc->month % 10;

        //buf[6] = (rtc->year - 2000) / 10 * 16 + (rtc->year - 2000) % 10;

        //return iic_write(0xD0, 0, 7, buf);

        return 1;

}









int rtc_init (void)

{

        // 12.09.10 RTCÇÕå≈íËéûçè

        //BYTE buf[8];  /* RTC R/W buffer */

        //UINT adr;

//

//

        ///* Read RTC registers */

        //if (!iic_read(0xD0, 0, 8, buf)) return 0;     /* IIC error */

//

        //if (buf[7] & 0x20) {  /* When data has been volatiled, set default time */

                ///* Clear nv-ram. Reg[8..63] */

                //memset(buf, 0, 8);

                //for (adr = 8; adr < 64; adr += 8)

                        //iic_write(0x0D, adr, 8, buf);

                ///* Reset time to Jan 1, '08. Reg[0..7] */

                //buf[4] = 1; buf[5] = 1; buf[6] = 8;

                //iic_write(0x0D, 0, 8, buf);

        //}

        return 1;

        }