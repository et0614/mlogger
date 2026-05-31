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

static void (*IO_M_SCL_InterruptHandler)(void);
static void (*IO_M_SDA_InterruptHandler)(void);
static void (*IO_S_SCL_InterruptHandler)(void);
static void (*IO_S_SDA_InterruptHandler)(void);
static void (*IO_RESET_InterruptHandler)(void);

void PIN_MANAGER_Initialize()
{

  /* OUT Registers Initialization */
    PORTA.OUT = 0x0;
    PORTC.OUT = 0x0;
    PORTD.OUT = 0x0;
    PORTF.OUT = 0x0;

  /* DIR Registers Initialization */
    PORTA.DIR = 0x0;
    PORTC.DIR = 0x0;
    PORTD.DIR = 0x0;
    PORTF.DIR = 0x0;

  /* PINxCTRL registers Initialization */
    PORTA.PIN0CTRL = 0x0;
    PORTA.PIN1CTRL = 0x0;
    PORTA.PIN2CTRL = 0x0;
    PORTA.PIN3CTRL = 0x0;
    PORTA.PIN4CTRL = 0x0;
    PORTA.PIN5CTRL = 0x0;
    PORTA.PIN6CTRL = 0x0;
    PORTA.PIN7CTRL = 0x0;
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
    PORTD.PIN5CTRL = 0x0;
    PORTD.PIN6CTRL = 0x8;
    PORTD.PIN7CTRL = 0x0;
    PORTF.PIN0CTRL = 0x0;
    PORTF.PIN1CTRL = 0x0;
    PORTF.PIN2CTRL = 0x0;
    PORTF.PIN3CTRL = 0x0;
    PORTF.PIN4CTRL = 0x0;
    PORTF.PIN5CTRL = 0x0;
    PORTF.PIN6CTRL = 0x0;
    PORTF.PIN7CTRL = 0x0;

  /* PORTMUX Initialization */
    PORTMUX.ACROUTEA = 0x0;
    PORTMUX.CCLROUTEA = 0x0;
    PORTMUX.EVSYSROUTEA = 0x0;
    PORTMUX.SPIROUTEA = 0x0;
    PORTMUX.TCAROUTEA = 0x0;
    PORTMUX.TCBROUTEA = 0x0;
    PORTMUX.TCDROUTEA = 0x0;
    PORTMUX.TWIROUTEA = 0x0;
    PORTMUX.USARTROUTEA = 0x0;
    PORTMUX.ZCDROUTEA = 0x0;

  // register default ISC callback functions at runtime; use these methods to register a custom function
    IO_M_SCL_SetInterruptHandler(IO_M_SCL_DefaultInterruptHandler);
    IO_M_SDA_SetInterruptHandler(IO_M_SDA_DefaultInterruptHandler);
    IO_S_SCL_SetInterruptHandler(IO_S_SCL_DefaultInterruptHandler);
    IO_S_SDA_SetInterruptHandler(IO_S_SDA_DefaultInterruptHandler);
    IO_RESET_SetInterruptHandler(IO_RESET_DefaultInterruptHandler);
}

/**
  Allows selecting an interrupt handler for IO_M_SCL at application runtime
*/
void IO_M_SCL_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_M_SCL_InterruptHandler = interruptHandler;
}

void IO_M_SCL_DefaultInterruptHandler(void)
{
    // add your IO_M_SCL interrupt custom code
    // or set custom function using IO_M_SCL_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for IO_M_SDA at application runtime
*/
void IO_M_SDA_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_M_SDA_InterruptHandler = interruptHandler;
}

void IO_M_SDA_DefaultInterruptHandler(void)
{
    // add your IO_M_SDA interrupt custom code
    // or set custom function using IO_M_SDA_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for IO_S_SCL at application runtime
*/
void IO_S_SCL_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_S_SCL_InterruptHandler = interruptHandler;
}

void IO_S_SCL_DefaultInterruptHandler(void)
{
    // add your IO_S_SCL interrupt custom code
    // or set custom function using IO_S_SCL_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for IO_S_SDA at application runtime
*/
void IO_S_SDA_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_S_SDA_InterruptHandler = interruptHandler;
}

void IO_S_SDA_DefaultInterruptHandler(void)
{
    // add your IO_S_SDA interrupt custom code
    // or set custom function using IO_S_SDA_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for IO_RESET at application runtime
*/
void IO_RESET_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    IO_RESET_InterruptHandler = interruptHandler;
}

void IO_RESET_DefaultInterruptHandler(void)
{
    // add your IO_RESET interrupt custom code
    // or set custom function using IO_RESET_SetInterruptHandler()
}
ISR(PORTA_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTA.INTFLAGS & PORT_INT3_bm)
    {
       IO_S_SCL_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT2_bm)
    {
       IO_S_SDA_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTA.INTFLAGS = 0xff;
}

ISR(PORTC_PORT_vect)
{ 
    /* Clear interrupt flags */
    VPORTC.INTFLAGS = 0xff;
}

ISR(PORTD_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTD.INTFLAGS & PORT_INT6_bm)
    {
       IO_RESET_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTD.INTFLAGS = 0xff;
}

ISR(PORTF_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTF.INTFLAGS & PORT_INT3_bm)
    {
       IO_M_SCL_InterruptHandler(); 
    }
    if(VPORTF.INTFLAGS & PORT_INT2_bm)
    {
       IO_M_SDA_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTF.INTFLAGS = 0xff;
}

/**
 End of File
*/