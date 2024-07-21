# M-Logger

Materials for the production of M-Logger, an indoor thermal environment measurement device, are currently available.

## Description

The M-Logger was developed with the objective of facilitating the measurement of the indoor thermal environment. <br>
In order to evaluate the environment in question with the requisite degree of accuracy, it is necessary to measure at least the dry bulb temperature, relative humidity, radiation temperature, and wind speed.<br>
The cost of comprehensive instruments that measure these physical quantities is typically in excess of $1000. <br>
Furthermore, they are bulky and challenging to install in multiple locations due to the necessity of a power outlet.<br>
In contrast, the M-Logger is manufactured at a cost of less than $100, is compact and portable, and is powered by two AA batteries. <br>
Further information can be found in the accompanying [paper](https://www.jstage.jst.go.jp/article/aijt/28/68/28_267/_article/-char/ja) and [website](https://www.mlogger.jp).
<br> <br>

In this directory, four materials for the production of the M-Logger are presented.

### board
The directory contains the materials required for the fabrication of the board. <br>
The directory includes a project file for the board created with Autodesk's Eagle, a bill of materials (BOM) listing the components to be mounted on the board, and datasheets for the components, which can be outsourced using PCBA services.

### avr
The directory contains the program to be written to the microchip, which is necessary for the board to function. <br>
As the instrument employs Atmel's AVR128DB32, the development is conducted within the Atmel Studio environment.

### server
The measured data is transmitted to a personal computer via Zigbee communication using XBee. <br>
A solution file containing a software project file for the reception of the aforementioned data on a personal computer is available. <br>
The software has been developed with Microsoft's Visual Studio; and as it targets .NET 8, it has been confirmed to work on Linux as well. <br>
Furthermore, XBee can communicate via Bluetooth from version 3.0 onwards, allowing data to be received by ordinary smartphones. <br>
The solution file also includes a MAUI project file to receive measurements on iPhone and Android.

### 3d_data
The directory contains three-dimensional data for the purpose of manufacturing cases. The files are in the Rhinoceros format.

### license
Copyright Eisuke Togashi 2023. <br>
This source describes Open Hardware and is licensed under the CERN-OHL-P v2 <br>
You may redistribute and modify this documentation and make products using it under the terms of the CERN-OHL-P v2 (https:/cern.ch/cern-ohl).  <br>
This documentation is distributed WITHOUT ANY EXPRESS OR IMPLIED WARRANTY, INCLUDING OF MERCHANTABILITY, SATISFACTORY QUALITY AND FITNESS FOR A PARTICULAR PURPOSE.  <br>
Please see the CERN-OHL-P v2 for applicable conditions

## Author

[eisuke togashi](https://www.mlogger.jp)
