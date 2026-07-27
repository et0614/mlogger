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
static void (*SCL_InterruptHandler)(void);
static void (*SDA_InterruptHandler)(void);
static void (*RST_InterruptHandler)(void);
static void (*FLASH_CS_InterruptHandler)(void);
static void (*RST8_InterruptHandler)(void);
static void (*RST7_InterruptHandler)(void);
static void (*RST6_InterruptHandler)(void);
static void (*RST5_InterruptHandler)(void);
static void (*RST4_InterruptHandler)(void);
static void (*RST3_InterruptHandler)(void);
static void (*RST2_InterruptHandler)(void);
static void (*RST1_InterruptHandler)(void);
static void (*LED_InterruptHandler)(void);

void PIN_MANAGER_Initialize()
{

  /* OUT Registers Initialization */
    PORTA.OUT = 0x80;
    PORTC.OUT = 0x0;
    PORTD.OUT = 0x0;
    PORTF.OUT = 0x0;

  /* DIR Registers Initialization */
    PORTA.DIR = 0xD0;
    PORTC.DIR = 0x8;
    PORTD.DIR = 0xFE;
    PORTF.DIR = 0x20;

  /* PINxCTRL registers Initialization */
    PORTA.PIN0CTRL = 0x8;
    PORTA.PIN1CTRL = 0x0;
    PORTA.PIN2CTRL = 0x0;
    PORTA.PIN3CTRL = 0x0;
    PORTA.PIN4CTRL = 0x0;
    PORTA.PIN5CTRL = 0x0;
    PORTA.PIN6CTRL = 0x0;
    PORTA.PIN7CTRL = 0x8;
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
    PORTD.PIN3CTRL = 0x4;
    PORTD.PIN4CTRL = 0x0;
    PORTD.PIN5CTRL = 0x8;
    PORTD.PIN6CTRL = 0x0;
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
    SCL_SetInterruptHandler(SCL_DefaultInterruptHandler);
    SDA_SetInterruptHandler(SDA_DefaultInterruptHandler);
    RST_SetInterruptHandler(RST_DefaultInterruptHandler);
    FLASH_CS_SetInterruptHandler(FLASH_CS_DefaultInterruptHandler);
    RST8_SetInterruptHandler(RST8_DefaultInterruptHandler);
    RST7_SetInterruptHandler(RST7_DefaultInterruptHandler);
    RST6_SetInterruptHandler(RST6_DefaultInterruptHandler);
    RST5_SetInterruptHandler(RST5_DefaultInterruptHandler);
    RST4_SetInterruptHandler(RST4_DefaultInterruptHandler);
    RST3_SetInterruptHandler(RST3_DefaultInterruptHandler);
    RST2_SetInterruptHandler(RST2_DefaultInterruptHandler);
    RST1_SetInterruptHandler(RST1_DefaultInterruptHandler);
    LED_SetInterruptHandler(LED_DefaultInterruptHandler);
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
  Allows selecting an interrupt handler for RST8 at application runtime
*/
void RST8_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST8_InterruptHandler = interruptHandler;
}

void RST8_DefaultInterruptHandler(void)
{
    // add your RST8 interrupt custom code
    // or set custom function using RST8_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST7 at application runtime
*/
void RST7_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST7_InterruptHandler = interruptHandler;
}

void RST7_DefaultInterruptHandler(void)
{
    // add your RST7 interrupt custom code
    // or set custom function using RST7_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST6 at application runtime
*/
void RST6_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST6_InterruptHandler = interruptHandler;
}

void RST6_DefaultInterruptHandler(void)
{
    // add your RST6 interrupt custom code
    // or set custom function using RST6_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST5 at application runtime
*/
void RST5_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST5_InterruptHandler = interruptHandler;
}

void RST5_DefaultInterruptHandler(void)
{
    // add your RST5 interrupt custom code
    // or set custom function using RST5_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST4 at application runtime
*/
void RST4_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST4_InterruptHandler = interruptHandler;
}

void RST4_DefaultInterruptHandler(void)
{
    // add your RST4 interrupt custom code
    // or set custom function using RST4_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST3 at application runtime
*/
void RST3_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST3_InterruptHandler = interruptHandler;
}

void RST3_DefaultInterruptHandler(void)
{
    // add your RST3 interrupt custom code
    // or set custom function using RST3_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST2 at application runtime
*/
void RST2_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST2_InterruptHandler = interruptHandler;
}

void RST2_DefaultInterruptHandler(void)
{
    // add your RST2 interrupt custom code
    // or set custom function using RST2_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for RST1 at application runtime
*/
void RST1_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    RST1_InterruptHandler = interruptHandler;
}

void RST1_DefaultInterruptHandler(void)
{
    // add your RST1 interrupt custom code
    // or set custom function using RST1_SetInterruptHandler()
}
/**
  Allows selecting an interrupt handler for LED at application runtime
*/
void LED_SetInterruptHandler(void (* interruptHandler)(void)) 
{
    LED_InterruptHandler = interruptHandler;
}

void LED_DefaultInterruptHandler(void)
{
    // add your LED interrupt custom code
    // or set custom function using LED_SetInterruptHandler()
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
    if(VPORTA.INTFLAGS & PORT_INT3_bm)
    {
       SCL_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT2_bm)
    {
       SDA_InterruptHandler(); 
    }
    if(VPORTA.INTFLAGS & PORT_INT0_bm)
    {
       RST_InterruptHandler(); 
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
       RST8_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTC.INTFLAGS = 0xff;
}

ISR(PORTD_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTD.INTFLAGS & PORT_INT1_bm)
    {
       RST7_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT2_bm)
    {
       RST6_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT3_bm)
    {
       RST5_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT4_bm)
    {
       RST4_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT5_bm)
    {
       RST3_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT6_bm)
    {
       RST2_InterruptHandler(); 
    }
    if(VPORTD.INTFLAGS & PORT_INT7_bm)
    {
       RST1_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTD.INTFLAGS = 0xff;
}

ISR(PORTF_PORT_vect)
{ 
    // Call the interrupt handler for the callback registered at runtime
    if(VPORTF.INTFLAGS & PORT_INT5_bm)
    {
       LED_InterruptHandler(); 
    }
    /* Clear interrupt flags */
    VPORTF.INTFLAGS = 0xff;
}

/**
 End of File
*/