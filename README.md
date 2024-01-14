# MLogger

Materials for the production of MLogger, an indoor thermal environment measurement device, are available.

## Description

MLogger was developed for the purpose of easily measuring the indoor thermal environment. <br>
To evaluate the indoor thermal environment, it is necessary to measure at least dry bulb temperature, relative humidity, radiation temperature, and wind speed. <br>
A comprehensive instrument that measures these physical quantities typically costs over $1000. They are also large in size and difficult to install many in the field because they are powered from an electrical outlet. <br>
The MLogger costs less than $50 to produce, fits in the palm of your hand, and runs on AA batteries. <br>
For more information about it, check the [paper](https://www.jstage.jst.go.jp/article/aijt/28/68/28_267/_article/-char/ja) and [website](https://www.mlogger.jp). <br> <br>

Here, four materials for producing MLogger are published in a directory.

### board
This directory contains materials for making the board. <br>
There is a project file of the board by Autodesk's Eagle, a list of components to be mounted on the board (BOM), a data sheet of the components, which could be outsourced using PCBA services.

### avr
This directory contains a program to be written to the microchip to make the board work. <br>
Since this instrument uses Atmel's AVR128DB32, it is being developed in Atmel Studio.

### server
The measured data is sent to a PC via Zigbee communication using XBee. <br>
A solution file which contains a software project file for receiving this data on a PC is available. <br>
The software is developed with Microsoft's Visual Studio, and since .NET 8 is the target, it has also been confirmed to work on Linux.<br>
XBee can also communicate via Bluetooth from 3.0. <br>
Therefore, data can also be received by ordinary smartphones. <br>
The solution file also contains MAUI project file to receive measurements on iPhone and Android.

### license
Copyright Eisuke Togashi 2023. <br>
This source describes Open Hardware and is licensed under the CERN-OHL-P v2 <br>
You may redistribute and modify this documentation and make products using it under the terms of the CERN-OHL-P v2 (https:/cern.ch/cern-ohl).  <br>
This documentation is distributed WITHOUT ANY EXPRESS OR IMPLIED WARRANTY, INCLUDING OF MERCHANTABILITY, SATISFACTORY QUALITY AND FITNESS FOR A PARTICULAR PURPOSE.  <br>
Please see the CERN-OHL-P v2 for applicable conditions

## Author

[eisuke togashi](https:www.hvacsimulator.net)
