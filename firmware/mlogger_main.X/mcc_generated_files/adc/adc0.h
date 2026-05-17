/**
 * ADC0 Generated Driver API Header File
 *
 * @file adc0.h
 *
 * @defgroup adc0 ADC0
 *
 * @brief This header file provides API prototypes for the ADC0 driver.
 *
 * @version ADC0 Driver Version 2.0.0
 *
 * @version ADC0 Module Version 2.0.0
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

#ifndef ADC0_H
#define ADC0_H

#include <stdint.h>
#include <stdbool.h>
#include "../system/utils/compiler.h"
#include "./adc_types.h"

/**
 * @ingroup adc0
 * @brief Defines the Custom Name pin mapping for channels in @ref adc_channel_t
 */
#define ADC_IN ADC0_CHANNEL_AIN2

/**
 * @ingroup adc0
 * @brief Defines the Custom Name pin mapping for channels in @ref adc_channel_t
 */
#define IO_PD3 ADC0_CHANNEL_AIN3

/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_Initialize
 */
#define ADC_VEL_Initialize ADC0_Initialize
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_Deinitialize
 */
#define ADC_VEL_Deinitialize ADC0_Deinitialize
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_Enable
 */
#define ADC_VEL_Enable ADC0_Enable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_Disable
 */
#define ADC_VEL_Disable ADC0_Disable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionStart
 */
#define ADC_VEL_ConversionStart ADC0_ConversionStart
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsConversionDone
 */
#define ADC_VEL_IsConversionDone ADC0_IsConversionDone
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionResultGet
 */
#define ADC_VEL_ConversionResultGet ADC0_ConversionResultGet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResolutionGet
 */
#define ADC_VEL_ResolutionGet ADC0_ResolutionGet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionDoneCallbackRegister
 */
#define ADC_VEL_ConversionDoneCallbackRegister ADC0_ConversionDoneCallbackRegister
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ChannelSelect
 */
#define ADC_VEL_ChannelSelect ADC0_ChannelSelect
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ChannelSelectAndConvert
 */
#define ADC_VEL_ChannelSelectAndConvert ADC0_ChannelSelectAndConvert
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionDoneInterruptEnable
 */
#define ADC_VEL_ConversionDoneInterruptEnable ADC0_ConversionDoneInterruptEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionDoneInterruptDisable
 */
#define ADC_VEL_ConversionDoneInterruptDisable ADC0_ConversionDoneInterruptDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionStop
 */
#define ADC_VEL_ConversionStop ADC0_ConversionStop
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_SampleRepeatCountSet
 */
#define ADC_VEL_SampleRepeatCountSet ADC0_SampleRepeatCountSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ComputationModeSet
 */
#define ADC_VEL_ComputationModeSet ADC0_ComputationModeSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_UpperThresholdSet
 */
#define ADC_VEL_UpperThresholdSet ADC0_UpperThresholdSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_LowerThresholdSet
 */
#define ADC_VEL_LowerThresholdSet ADC0_LowerThresholdSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ThresholdModeSet
 */
#define ADC_VEL_ThresholdModeSet ADC0_ThresholdModeSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_AccumulatedResultGet
 */
#define ADC_VEL_AccumulatedResultGet ADC0_AccumulatedResultGet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ThresholdCallbackRegister
 */
#define ADC_VEL_ThresholdCallbackRegister ADC0_ThresholdCallbackRegister
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ThresholdInterruptEnable
 */
#define ADC_VEL_ThresholdInterruptEnable ADC0_ThresholdInterruptEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ThresholdInterruptDisable
 */
#define ADC_VEL_ThresholdInterruptDisable ADC0_ThresholdInterruptDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_AutoTriggerEnable
 */
#define ADC_VEL_AutoTriggerEnable ADC0_AutoTriggerEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_AutoTriggerDisable
 */
#define ADC_VEL_AutoTriggerDisable ADC0_AutoTriggerDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ContinuousConversionEnable
 */
#define ADC_VEL_ContinuousConversionEnable ADC0_ContinuousConversionEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ContinuousConversionDisable
 */
#define ADC_VEL_ContinuousConversionDisable ADC0_ContinuousConversionDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_AutoTriggerSourceSet
 */
#define ADC_VEL_AutoTriggerSourceSet ADC0_AutoTriggerSourceSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_SampleOverwriteCallbackRegister
 */
#define ADC_VEL_SampleOverwriteCallbackRegister ADC0_SampleOverwriteCallbackRegister
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultOverwriteCallbackRegister
 */
#define ADC_VEL_ResultOverwriteCallbackRegister ADC0_ResultOverwriteCallbackRegister
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultReadyCallbackRegister
 */
#define ADC_VEL_ResultReadyCallbackRegister ADC0_ResultReadyCallbackRegister
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_TriggerOverrunCallbackRegister
 */
#define ADC_VEL_TriggerOverrunCallbackRegister ADC0_TriggerOverrunCallbackRegister
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsConversionDoneInterruptFlagSet
 */
#define ADC_VEL_IsConversionDoneInterruptFlagSet ADC0_IsConversionDoneInterruptFlagSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsThresholdInterruptFlagSet
 */
#define ADC_VEL_IsThresholdInterruptFlagSet ADC0_IsThresholdInterruptFlagSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsResultReadyInterruptFlagSet
 */
#define ADC_VEL_IsResultReadyInterruptFlagSet ADC0_IsResultReadyInterruptFlagSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsResultOverwriteInterruptFlagSet
 */
#define ADC_VEL_IsResultOverwriteInterruptFlagSet ADC0_IsResultOverwriteInterruptFlagSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsSampleOverwriteInterruptFlagSet
 */
#define ADC_VEL_IsSampleOverwriteInterruptFlagSet ADC0_IsSampleOverwriteInterruptFlagSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_IsTriggerOverrunInterruptFlagSet
 */
#define ADC_VEL_IsTriggerOverrunInterruptFlagSet ADC0_IsTriggerOverrunInterruptFlagSet
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ConversionDoneInterruptFlagClear
 */
#define ADC_VEL_ConversionDoneInterruptFlagClear ADC0_ConversionDoneInterruptFlagClear
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ThresholdInterruptFlagClear
 */
#define ADC_VEL_ThresholdInterruptFlagClear ADC0_ThresholdInterruptFlagClear
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultReadyInterruptFlagClear
 */
#define ADC_VEL_ResultReadyInterruptFlagClear ADC0_ResultReadyInterruptFlagClear
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultOverwriteInterruptFlagClear
 */
#define ADC_VEL_ResultOverwriteInterruptFlagClear ADC0_ResultOverwriteInterruptFlagClear
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_SampleOverwriteInterruptFlagClear
 */
#define ADC_VEL_SampleOverwriteInterruptFlagClear ADC0_SampleOverwriteInterruptFlagClear
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_TriggerOverrunInterruptFlagClear
 */
#define ADC_VEL_TriggerOverrunInterruptFlagClear ADC0_TriggerOverrunInterruptFlagClear
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ISR
 */
#define ADC_VEL_ISR ADC0_ISR
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ThresholdISR
 */
#define ADC_VEL_ThresholdISR ADC0_ThresholdISR
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_SampleOverwriteInterruptEnable
 */
#define ADC_VEL_SampleOverwriteInterruptEnable ADC0_SampleOverwriteInterruptEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_SampleOverwriteInterruptDisable
 */
#define ADC_VEL_SampleOverwriteInterruptDisable ADC0_SampleOverwriteInterruptDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultOverwriteInterruptEnable
 */
#define ADC_VEL_ResultOverwriteInterruptEnable ADC0_ResultOverwriteInterruptEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultOverwriteInterruptDisable
 */
#define ADC_VEL_ResultOverwriteInterruptDisable ADC0_ResultOverwriteInterruptDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultReadyInterruptEnable
 */
#define ADC_VEL_ResultReadyInterruptEnable ADC0_ResultReadyInterruptEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_ResultReadyInterruptDisable
 */
#define ADC_VEL_ResultReadyInterruptDisable ADC0_ResultReadyInterruptDisable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_TriggerOverrunInterruptEnable
 */
#define ADC_VEL_TriggerOverrunInterruptEnable ADC0_TriggerOverrunInterruptEnable
/**
 * @ingroup adc0
 * @brief This macro defines the Custom Name API mapping for @ref ADC0_TriggerOverrunInterruptDisable
 */
#define ADC_VEL_TriggerOverrunInterruptDisable ADC0_TriggerOverrunInterruptDisable

/** 
 * @ingroup adc0
 * @brief Initializes the registers based on the configurable options in the MPLAB?? Code Configurator (MCC) Melody UI for the Analog-to-Digital Converter (ADC) operation.
 * @param None.
 * @return None.
*/ 
void ADC0_Initialize(void);

/**
  * @ingroup adc0
  * @brief Deinitializes the registers to Power-on Reset values.
  * @param None.
  * @return None.
*/
void ADC0_Deinitialize(void);

/**
  * @ingroup adc0
  * @brief Sets the ON bit to `1`.
  * @param None.
  * @return None.
*/
void ADC0_Enable(void);

/**
  * @ingroup adc0
  * @brief Sets the ON bit to `0`.
  * @param None.
  * @return None.
*/
void ADC0_Disable(void);

/**
 * @ingroup adc0
 * @brief Sets the channel to use for the ADC conversion.
 * @param channel Desired analog channel. Refer to the @ref adc_channel_t enum for the list of available analog channels.
 * @return None.
*/
void ADC0_ChannelSelect(adc_channel_t channel);

/**
  * @ingroup adc0
  * @brief Starts the conversion and retrieves the result of one conversion on the selected channel.
  * @param channel Desired analog channel. Refer to the @ref adc_channel_t enum for the lsit of available analog channels.
  * @return The result of the ADC conversion
*/
adc_result_t ADC0_ChannelSelectAndConvert(adc_channel_t channel);


/**
  * @ingroup adc0
  * @brief Starts the ADC conversion on a selected channel.
  * @pre Select the channel using @ref ADC0_ChannelSelect and
  * call @ref ADC0_Initialize(void) to initialize the ADC module before using this API
  * @param None.
  * @return None.
*/
void ADC0_ConversionStart(void);

/**
  * @ingroup adc0
  * @brief Stops the ongoing ADC Conversion.
  * @param None.
  * @return None.
*/
void ADC0_ConversionStop(void);

/**
  * @ingroup adc0
  * @brief Checks if the ongoing ADC Conversion is complete.
  * @param None.
  * @retval True - The conversion is complete
  * @retval False - The conversion is ongoing
*/
bool ADC0_IsConversionDone(void);

/**
  * @ingroup adc0
  * @brief Retrieves the result of the latest conversion.
  * @param None.
  * @return The result of the conversion
*/
adc_result_t ADC0_ConversionResultGet(void);


/**
  * @ingroup adc0
  * @brief Sets the Computation mode.
  * @param computationMode Desired computation mode. Refer to the @ref adc_computation_mode_t enum for the list of available computation modes.
  * @return None.
*/
void ADC0_ComputationModeSet(adc_computation_mode_t computationMode);

/**
  * @ingroup adc0
  * @brief Sets the ADC Conversion Threshold mode.
  * @param thresholdMode Desired computation mode. Refer to the @ref adc_threshold_mode_t enum for the list of available threshold modes.
  * @return None.
*/
void ADC0_ThresholdModeSet(adc_threshold_mode_t thresholdMode);

/**
  * @ingroup adc0
  * @brief Sets the value of the Window Comparator High Threshold (WINHT) register.
  * @param upperThreshold Upper threshold value of the @ref adc_threshold_t type
  * @return None.
*/
void ADC0_UpperThresholdSet(adc_threshold_t upperThreshold);

/**
  * @ingroup adc0
  * @brief Sets the value of the Window Comparator Low Threshold (WINLT) register.
  * @param lowerThreshold Lower threshold value of the @ref adc_threshold_t type
  * @return None.
*/
void ADC0_LowerThresholdSet(adc_threshold_t lowerThreshold);

/**
  * @ingroup adc0
  * @brief Loads the sample length with the specified value.
  * @param repeatCount Repeat count value. Refer to the @ref adc_repeat_count_t enum for the list of available repeat count values.
  * @return None.
*/
void ADC0_SampleRepeatCountSet(adc_repeat_count_t repeatCount);

/**
  * @ingroup adc0
  * @brief Retrieves the value of the accumulated conversions.
  * @param None.
  * @return adc_accumulate_t - The value of accumulated conversions
*/
adc_accumulate_t ADC0_AccumulatedResultGet(void);

/**
 * @ingroup adc0
 * @brief Returns the resolution of the ADC module.
 * @param None.
 * @return uint8_t - Resolution value
 */
uint8_t ADC0_ResolutionGet(void);

/**
  * @ingroup adc0
  * @brief Enables the auto triggering of the ADC module.
  * @param None.
  * @return None.
*/
void ADC0_AutoTriggerEnable(void);

/**
  * @ingroup adc0
  * @brief Disables the auto triggering of the ADC module.
  * @param None.
  * @return None.
*/
void ADC0_AutoTriggerDisable(void);

/**
  * @ingroup adc0
  * @brief Sets the auto triggering to the trigger source sent as an input parameter.
  * @param triggerSource The trigger source to set the auto trigger.
  * @return None.
*/
void ADC0_AutoTriggerSourceSet(adc_trigger_source_t triggerSource);

/**
  * @ingroup adc0
  * @brief Sets the Free-Running (FREERUN) bit to `1`.
  * @param None.
  * @return None.
*/
void ADC0_ContinuousConversionEnable(void);

/**
  * @ingroup adc0
  * @brief Sets the Free-Running (FREERUN) bit to `0`.
  * @param None.
  * @return None.
*/
void ADC0_ContinuousConversionDisable(void);



/**
 * @ingroup adc0
 * @brief Sets the Result Ready Interrupt Enable (RESRDY) bit to `1`.
 * @param None.
 * @return None.
*/
void ADC0_ResultReadyInterruptEnable(void);

/**
 * @ingroup adc0
 * @brief Sets the Result Ready Interrupt Enable (RESRDY) bit to `0`.
 * @param None.
 * @return None.
*/
void ADC0_ResultReadyInterruptDisable(void);

/**
 * @ingroup adc0
 * @brief Sets the Sample Ready Interrupt Enable (SAMPRDY) bit to `1`.
 * @param None.
 * @return None.
*/
void ADC0_ConversionDoneInterruptEnable(void);

/**
 * @ingroup adc0
 * @brief Sets the Sample Ready Interrupt Enable (SAMPRDY) bit to `0`.
 * @param None.
 * @return None.
*/
void ADC0_ConversionDoneInterruptDisable(void);

/**
 * @ingroup adc0
 * @brief Sets the Window Comparator Interrupt Enable (WCMP) bit to `1`.
 * @param None.
 * @return None.
*/
void ADC0_ThresholdInterruptEnable(void);

/**
 * @ingroup adc0
 * @brief Sets the Window Comparator Interrupt Enable (WCMP) bit to `0`.
 * @param None.
 * @return None.
*/
void ADC0_ThresholdInterruptDisable(void);

// RESOVR
/**
 * @ingroup adc0
 * @brief Sets the Result Overwrite Interrupt Enable (RESOVR) bit to `1`.
 * @param None.
 * @return None.
*/
void ADC0_ResultOverwriteInterruptEnable(void);

/**
 * @ingroup adc0
 * @brief Sets the Result Overwrite Interrupt Enable (RESOVR) bit to `0`.
 * @param None.
 * @return None.
*/
void ADC0_ResultOverwriteInterruptDisable(void);

/**
 * @ingroup adc0
 * @brief Sets the Sample Overwrite Interrupt Enable (SAMPOVR) bit to `1`.
 * @param None.
 * @return None.
*/
void ADC0_SampleOverwriteInterruptEnable(void);

/**
 * @ingroup adc0
 * @brief Sets the Sample Overwrite Interrupt Enable (SAMPOVR) bit to `0`.
 * @param None.
 * @return None.
*/
void ADC0_SampleOverwriteInterruptDisable(void);

// TRIGOVR
/**
 * @ingroup adc0
 * @brief Sets the Trigger Overrun Interrupt Enable (TRIGOVR) bit to `1`.
 * @param None.
 * @return None.
*/
void ADC0_TriggerOverrunInterruptEnable(void);

/**
 * @ingroup adc0
 * @brief Sets the Trigger Overrun Interrupt Enable (TRIGOVR) bit to `0`.
 * @param None.
 * @return None.
*/
void ADC0_TriggerOverrunInterruptDisable(void);


/**
  * @ingroup adc0
  * @brief Sets the callback for the Result Ready Interrupt (RESRDY).
  * @param *callback The pointer to the function to be executed
  * @return None.
*/
void ADC0_ResultReadyCallbackRegister(void (*callback)(void));

/**
  * @ingroup adc0
  * @brief Sets the callback for the Sample Ready Interrupt (SAMPRDY).
  * @param *callback The pointer to the function to be executed
  * @return None.
*/
void ADC0_ConversionDoneCallbackRegister(void (*callback)(void));

/**
  * @ingroup adc0
  * @brief Sets the callback for the Window Comparator Interrupt (WCMP).
  * @param *callback The pointer to the function to be executed
  * @return None.
*/
void ADC0_ThresholdCallbackRegister(void (*callback)(void));

/**
  * @ingroup adc0
  * @brief Sets the callback for the Result Overwrite Interrupt (RESOVR).
  * @param *callback The pointer to the function to be executed
  * @return None.
*/
void ADC0_ResultOverwriteCallbackRegister(void (*callback)(void));

/**
  * @ingroup adc0
  * @brief Sets the callback for the Sample Overwrite Interrupt (SAMPOVR).
  * @param *callback The pointer to the function to be executed
  * @return None.
*/
void ADC0_SampleOverwriteCallbackRegister(void (*callback)(void));

/**
  * @ingroup adc0
  * @brief Sets the callback for the Trigger Overrun Interrupt (TRIGOVR).
  * @param *callback The pointer to the function to be executed
  * @return None.
*/
void ADC0_TriggerOverrunCallbackRegister(void (*callback)(void));

/**
 * @ingroup adc0
 * @brief Checks the Result Ready Interrupt (RESRDY) flag status.
 * @param None.
 * @retval True - RESRDY flag status is set
 * @retval False - RESRDY flag status is not set
*/
bool ADC0_IsResultReadyInterruptFlagSet(void);

/**
 * @ingroup adc0
 * @brief Checks the Sample Ready Interrupt (SAMPRDY) flag status.
 * @param None.
 * @retval True -  SAMPRDY flag status is set
 * @retval False - SAMPRDY flag status is not set
*/
bool ADC0_IsConversionDoneInterruptFlagSet(void);

/**
 * @ingroup adc0
 * @brief Checks the Window Comparator Interrupt (WCMP) flag status.
 * @param None.
 * @retval True - WCMP flag status is set
 * @retval False - WCMP flag status is not set
*/
bool ADC0_IsThresholdInterruptFlagSet(void);

/**
 * @ingroup adc0
 * @brief Checks the Result Overwrite Interrupt (RESOVR) flag status.
 * @param None.
 * @retval True - RESOVR flag status is set
 * @retval False - RESOVR flag status is not set
*/
bool ADC0_IsResultOverwriteInterruptFlagSet(void);

/**
 * @ingroup adc0
 * @brief Checks the Sample Overwrite Interrupt (SAMPOVR) flag status.
 * @param None.
 * @retval True - SAMPOVR flag status is set
 * @retval False - SAMPOVR flag status is not set
*/
bool ADC0_IsSampleOverwriteInterruptFlagSet(void);

/**
 * @ingroup adc0
 * @brief Checks the Trigger Overrun Interrupt (TRIGOVR) flag status.
 * @param None.
 * @retval True - TRIGOVR flag status is set
 * @retval False - TRIGOVR flag status is not set
*/
bool ADC0_IsTriggerOverrunInterruptFlagSet(void);

/**
 * @ingroup adc0
 * @brief Clears the Result Ready Interrupt (RESRDY) flag
 * @param None.
 * @return None.
*/
void ADC0_ResultReadyInterruptFlagClear(void);

/**
 * @ingroup adc0
 * @brief Clears the Sample Ready Interrupt (RESRDY) flag.
 * @param None.
 * @return None.
*/
void ADC0_ConversionDoneInterruptFlagClear(void);

/**
 * @ingroup adc0
 * @brief Clears the Window Comparator Interrupt (WCMP) flag.
 * @param None.
 * @return None.
*/
void ADC0_ThresholdInterruptFlagClear(void);

/**
 * @ingroup adc0
 * @brief Clears the Result Overwrite Interrupt (RESOVR) flag.
 * @param None.
 * @return None.
*/
void ADC0_ResultOverwriteInterruptFlagClear(void);

/**
 * @ingroup adc0
 * @brief Clears the Sample Overwrite Interrupt (SAMPOVR) flag.
 * @param None.
 * @return None.
*/
void ADC0_SampleOverwriteInterruptFlagClear(void);

/**
 * @ingroup adc0
 * @brief Clears the ADC Trigger Overrun Interrupt (TRIGOVR) flag.
 * @param None.
 * @return None.
*/
void ADC0_TriggerOverrunInterruptFlagClear(void);


#endif // ADC0_H