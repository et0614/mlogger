/**
 * Generated Driver File
 * 
 * @file pins.c
 * 
 * @ingroup  pinsdriver
 * 
 * @brief This is generated driver implementation for pins. 
 *        This file provides implementations for pin APIs for all pins selected in the GUI.
 *
 * @version Driver Version 1.1.0
*/

/*
? [2026] Microchip Technology Inc. and its subsidiaries.

    Subject to your compliance with these terms, you may use Microchip 
    software and any derivatives exclusively with Microchip products. 
    You are responsible for complying with 3rd party license terms  
    applicable to your use of 3rd party software (including open source  
    software) that may accompany Microchip software. SOFTWARE IS ?AS IS.? 
    NO WARRANTIES, WHETHER EXPRESS, IMPLIED OR STATUTORY, APPLY TO THIS 
    SOFTWARE, INCLUDING ANY IMPLIED WARRANTIES OF NON-INFRINGEMENT,  
    MERCHANTABILITY, OR FITNESS FOR A PARTICULAR PURPOSE. IN NO EVENT 
    WILL MICROCHIP BE LIABLE FOR ANY INDIRECT, SPECIAL, PUNITIVE, 
    INCIDENTAL OR CONSEQUENTIAL LOSS, DAMAGE, COST OR EXPENSE OF ANY 
    KIND WHATSOEVER RELATED TO THE SOFTWARE, HOWEVER CAUSED, EVEN IF 
    MICROCHIP HAS BEEN ADVISED OF THE POSSIBILITY OR THE DAMAGES ARE 
    FORESEEABLE. TO THE FULLEST EXTENT ALLOWED BY LAW, MICROCHIP?S 
    TOTAL LIABILITY ON ALL CLAIMS RELATED TO THE SOFTWARE WILL NOT 
    EXCEED AMOUNT OF FEES, IF ANY, YOU PAID DIRECTLY TO MICROCHIP FOR 
    THIS SOFTWARE.
*/

#include "../pins.h"

static void (*MISO_InterruptHandler)(void);
static void (*MOSI_InterruptHandler)(void);
static void (*SCK_InterruptHandler)(void);
static void (*CTS0_InterruptHandler)(void);
static void (*RTS0_InterruptHandler)(void);
static void (*RXD_InterruptHandler)(void);
static void (*TXD_InterruptHandler)(void);
static void (*ADC_IN_InterruptHandler)(void);
static void (*IO_PD3_InterruptHandler)(void);
static void (*SCL_InterruptHandler)(void);
static void (*SDA_InterruptHandler)(void);
static void (*AVREF_InterruptHandler)(void);
static void (*RST_InterruptHandler)(void);
static void (*FLASH_CS_InterruptHandler)(void);
static void (*EN5V_InterruptHandler)(void);
static void (*R_LED_InterruptHandler)(void);
static void (*G_LED_InterruptHandler)(void);
static void (*IO_PF3_InterruptHandler)(void);
static void (*SLP_XBEE_InterruptHandler)(void);

void PIN_MANAGER_Initialize()
{

  /* OUT Registers Initialization */
    PORTA.OUT = 0x80;
    PORTC.OUT = 0x0;
    PORTD.OUT = 0x0;
    PORTF.OUT = 0x0;

  /* DIR Registers Initialization */
    PORTA.DIR = 0xD1;
    PORTC.DIR = 0x8;
    PORTD.DIR = 0x43;
    PORTF.DIR = 0x28;

  /* PINxCTRL registers Initialization */
    PORTA.PIN0CTRL = 0x0;
    PORTA.PIN1CTRL = 0x0;
    PORTA.PIN2CTRL = 0x0;
    PORTA.PIN3CTRL = 0x0;
    PORTA.PIN4CTRL = 0x0;
    PORTA.PIN5CTRL = 0x0;
    PORTA.PIN6CTRL = 0x0;
    PORTA.PIN7CTRL = 0x9;
    PORTC.PIN0CTRL = 0x0;
    PORTC.PIN1CTRL = 0x0;
    PORTC.PIN2CTRL = 0x0;
    PORTC.PIN3CTRL = 0x0;
    PORTC.PIN4CTRL = 0x0;
    PORTC.PIN5CTRL = 0x0;
    PORTC.PIN6CTRL = 0x0;
    PORTC.PIN7CTRL = 0x0;
    PORTD.PIN0CTRL = 0x0;
    PORTD.PIN1CTRL = 0x0;
    PORTD.PIN2CTRL = 0x0;
    PORTD.PIN3CTRL = 0x0;
    PORTD.PIN4CTRL = 0x0;
    PORTD.PIN5CTRL = 0x9;
    PORTD.PIN6CTRL = 0x0;
    PORTD.PIN7CTRL = 0x0;
    PORTF.PIN0CTRL = 0x0;
    PORTF.PIN1CTRL = 0x0;
    PORTF.PIN2CTRL = 0x1;
    PORTF.PIN3CTRL = 0x0;
    PORTF.PIN4CTRL = 0x0;
    PORTF.PIN5CTRL = 0x0;
    PORTF.PIN6CTRL = 0x0;
    PORTF.PIN7CTRL = 0x0;

  /* PORTMUX Initialization */
    PORTMUX.CCLROUTEA = 0x0;
    PORTMUX.EVSYSROUTEA = 0x0;
    PORTMUX.SPIROUTEA = 0x0;
    PORTMUX.TCAROUTEA = 0x0;
    PORTMUX.TCBROUTEA = 0x0;
    PORTMUX.TWIROUTEA = 0x0;
    PORTMUX.USARTROUTEA = 0x0;

  // register default ISC callback functions at runtime; use these methods to register a custom function
    MISO_SetInterruptHandler(MISO_DefaultInterruptHandler);
    MOSI_SetInterruptHandler(MOSI_DefaultInterruptHandler);
    SCK_SetInterruptHandler(SCK_DefaultInterruptHandler);
    CTS0_SetInterruptHandler(CTS0_DefaultInterruptHandler);
    RTS0_SetInterruptHandler(RTS0_DefaultInterruptHandler);
    RXD_SetInterruptHandler(RXD_DefaultInterruptHandler);
    TXD_SetInterruptHandler(TXD_DefaultInterruptHandler);
    ADC_IN_SetInterruptHandler(ADC_IN_DefaultInterruptHandler);
    IO_PD3_SetInterruptHandler(IO_PD3_DefaultInterruptHandler);
    SCL_SetInterruptHandler(SCL_DefaultInterruptHandler);
    SDA_SetInterruptHandler(SDA_DefaultInterruptHandler);
    AVREF_SetInterruptHandler(AVREF_DefaultInterruptHandler);
    RST_SetInterruptHandler(RST_DefaultInterruptHandler);
    FLASH_CS_SetInterruptHandler(FLASH_CS_DefaultInterruptHandler);
    EN5V_SetInterruptHandler(EN5V_DefaultInterruptHandler);
    R_LED_SetInterruptHandler(R_LED_DefaultInterruptHandler);
    G_LED_SetInterruptHandler(G_LED_DefaultInterruptHandler);
    IO_PF3_SetInterruptHandler(IO_PF3_DefaultInterruptHandler);
    SLP_XBEE_SetInterruptHandler(SLP_XBEE_DefaultInterruptHandler);
}

/**
  Allows selecting an interrupt handler for MISO at application runtime
*/
void MISO_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    MISO_InterruptHandler = interruptHandler;
}

void MISO_DefaultInterruptHandler(void)
{
    // add your MISO interrupt custom code
    // or set custom function using MISO_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for MOSI at application runtime
*/
void MOSI_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    MOSI_InterruptHandler = interruptHandler;
}

void MOSI_DefaultInterruptHandler(void)
{
    // add your MOSI interrupt custom code
    // or set custom function using MOSI_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for SCK at application runtime
*/
void SCK_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    SCK_InterruptHandler = interruptHandler;
}

void SCK_DefaultInterruptHandler(void)
{
    // add your SCK interrupt custom code
    // or set custom function using SCK_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for CTS0 at application runtime
*/
void CTS0_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    CTS0_InterruptHandler = interruptHandler;
}

void CTS0_DefaultInterruptHandler(void)
{
    // add your CTS0 interrupt custom code
    // or set custom function using CTS0_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RTS0 at application runtime
*/
void RTS0_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RTS0_InterruptHandler = interruptHandler;
}

void RTS0_DefaultInterruptHandler(void)
{
    // add your RTS0 interrupt custom code
    // or set custom function using RTS0_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RXD at application runtime
*/
void RXD_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RXD_InterruptHandler = interruptHandler;
}

void RXD_DefaultInterruptHandler(void)
{
    // add your RXD interrupt custom code
    // or set custom function using RXD_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for TXD at application runtime
*/
void TXD_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    TXD_InterruptHandler = interruptHandler;
}

void TXD_DefaultInterruptHandler(void)
{
    // add your TXD interrupt custom code
    // or set custom function using TXD_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for ADC_IN at application runtime
*/
void ADC_IN_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    ADC_IN_InterruptHandler = interruptHandler;
}

void ADC_IN_DefaultInterruptHandler(void)
{
    // add your ADC_IN interrupt custom code
    // or set custom function using ADC_IN_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for IO_PD3 at application runtime
*/
void IO_PD3_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_PD3_InterruptHandler = interruptHandler;
}

void IO_PD3_DefaultInterruptHandler(void)
{
    // add your IO_PD3 interrupt custom code
    // or set custom function using IO_PD3_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for SCL at application runtime
*/
void SCL_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    SCL_InterruptHandler = interruptHandler;
}

void SCL_DefaultInterruptHandler(void)
{
    // add your SCL interrupt custom code
    // or set custom function using SCL_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for SDA at application runtime
*/
void SDA_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    SDA_InterruptHandler = interruptHandler;
}

void SDA_DefaultInterruptHandler(void)
{
    // add your SDA interrupt custom code
    // or set custom function using SDA_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for AVREF at application runtime
*/
void AVREF_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    AVREF_InterruptHandler = interruptHandler;
}

void AVREF_DefaultInterruptHandler(void)
{
    // add your AVREF interrupt custom code
    // or set custom function using AVREF_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST at application runtime
*/
void RST_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST_InterruptHandler = interruptHandler;
}

void RST_DefaultInterruptHandler(void)
{
    // add your RST interrupt custom code
    // or set custom function using RST_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for FLASH_CS at application runtime
*/
void FLASH_CS_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    FLASH_CS_InterruptHandler = interruptHandler;
}

void FLASH_CS_DefaultInterruptHandler(void)
{
    // add your FLASH_CS interrupt custom code
    // or set custom function using FLASH_CS_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for EN5V at application runtime
*/
void EN5V_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    EN5V_InterruptHandler = interruptHandler;
}

void EN5V_DefaultInterruptHandler(void)
{
    // add your EN5V interrupt custom code
    // or set custom function using EN5V_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for R_LED at application runtime
*/
void R_LED_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    R_LED_InterruptHandler = interruptHandler;
}

void R_LED_DefaultInterruptHandler(void)
{
    // add your R_LED interrupt custom code
    // or set custom function using R_LED_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for G_LED at application runtime
*/
void G_LED_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    G_LED_InterruptHandler = interruptHandler;
}

void G_LED_DefaultInterruptHandler(void)
{
    // add your G_LED interrupt custom code
    // or set custom function using G_LED_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for IO_PF3 at application runtime
*/
void IO_PF3_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_PF3_InterruptHandler = interruptHandler;
}

void IO_PF3_DefaultInterruptHandler(void)
{
    // add your IO_PF3 interrupt custom code
    // or set custom function using IO_PF3_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for SLP_XBEE at application runtime
*/
void SLP_XBEE_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    SLP_XBEE_InterruptHandler = interruptHandler;
}

void SLP_XBEE_DefaultInterruptHandler(void)
{
    // add your SLP_XBEE interrupt custom code
    // or set custom function using SLP_XBEE_SetInterruptHandler()
}
ISR(PORTA_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTA.INTFLAGS & PORT_INT5_bm)
    {
       MISO_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT4_bm)
    {
       MOSI_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT6_bm)
    {
       SCK_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT1_bm)
    {
       RXD_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT0_bm)
    {
       TXD_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT3_bm)
    {
       SCL_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT2_bm)
    {
       SDA_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT7_bm)
    {
       FLASH_CS_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTA.INTFLAGS = 0xff;
}

ISR(PORTC_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTC.INTFLAGS & PORT_INT3_bm)
    {
       EN5V_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTC.INTFLAGS = 0xff;
}

ISR(PORTD_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTD.INTFLAGS & PORT_INT5_bm)
    {
       CTS0_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT6_bm)
    {
       RTS0_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT2_bm)
    {
       ADC_IN_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT3_bm)
    {
       IO_PD3_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT7_bm)
    {
       AVREF_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT0_bm)
    {
       R_LED_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT1_bm)
    {
       G_LED_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTD.INTFLAGS = 0xff;
}

ISR(PORTF_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTF.INTFLAGS & PORT_INT2_bm)
    {
       RST_InterruptHandler(); 
    }
    if(VPORTF.INTFLAGS & PORT_INT3_bm)
    {
       IO_PF3_InterruptHandler(); 
    }
    if(VPORTF.INTFLAGS & PORT_INT5_bm)
    {
       SLP_XBEE_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTF.INTFLAGS = 0xff;
}

/**
 End of File
*/