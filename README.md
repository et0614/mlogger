# M-Logger

Materials for the production of M-Logger, an indoor thermal environment measurement device, are available.

## Description

The M-Logger was developed to easily measure the indoor thermal environment. <br>
To evaluate this environment accurately, it is necessary to measure at least the dry bulb temperature, relative humidity, radiation temperature, and wind speed.<br>
Comprehensive instruments that measure these physical quantities typically cost over $1000. <br>
Additionally, they are large, cumbersome, and difficult to install in multiple locations since they require a power outlet.<br>
In contrast, the M-Logger costs less than $100 to produce, fits in the palm of your hand, and runs on AA batteries. <br>
For more information, please refer to the [paper](https://www.jstage.jst.go.jp/article/aijt/28/68/28_267/_article/-char/ja) and [website](https://www.mlogger.jp).
<br> <br>

Here, four materials for producing M-Logger are published in a directory.

### board
This directory contains materials for making the board. <br>
It includes a project file for the board created with Autodesk's Eagle, a bill of materials (BOM) listing the components to be mounted on the board, and datasheets for the components, which can be outsourced using PCBA services.

### avr
This directory contains the program to be written to the microchip to make the board function. <br>
Since this instrument uses Atmel's AVR128DB32, the development is done in Atmel Studio.

### server
The measured data is sent to a PC via Zigbee communication using XBee. <br>
A solution file containing a software project file for receiving this data on a PC is available. <br>
The software is developed with Microsoft's Visual Studio, and since it targets .NET 8, it has been confirmed to work on Linux as well. <br>
XBee can also communicate via Bluetooth from version 3.0 onwards, allowing data to be received by ordinary smartphones. <br>
The solution file also includes a MAUI project file to receive measurements on iPhone and Android.

### 3d_data
This directory contains 3D data for manufacturing cases. The files are in Rhinoceros format.

### license
Copyright Eisuke Togashi 2023. <br>
This source describes Open Hardware and is licensed under the CERN-OHL-P v2 <br>
You may redistribute and modify this documentation and make products using it under the terms of the CERN-OHL-P v2 (https:/cern.ch/cern-ohl).  <br>
This documentation is distributed WITHOUT ANY EXPRESS OR IMPLIED WARRANTY, INCLUDING OF MERCHANTABILITY, SATISFACTORY QUALITY AND FITNESS FOR A PARTICULAR PURPOSE.  <br>
Please see the CERN-OHL-P v2 for applicable conditions

## Author

[eisuke togashi](https://www.mlogger.jp)
