# MLogger

Materials for the production of MLogger, an indoor thermal environment measurement device, are available.

## Description

MLogger was developed for the purpose of easily measuring the indoor thermal environment.
To evaluate the indoor thermal environment, it is necessary to measure at least dry bulb temperature, relative humidity, radiation temperature, and wind speed.
A comprehensive instrument that measures these physical quantities typically costs over $1000. They are also large in size and difficult to install many in the field because they are powered from an electrical outlet.
The MLogger costs less than $50 to produce, fits in the palm of your hand, and runs on AA batteries.
For more information about it, check the [paper](https://www.jstage.jst.go.jp/article/aijt/28/68/28_267/_article/-char/ja) and [website](https://www.hvacsimulator.net/mlogger).

Here, four materials for producing MLogger are published in a directory.

### board
It contains materials for making the board.
There is a project file of the board by Autodesk's Eagle, a list of components to be mounted on the board (BOM), a data sheet of the components, which could be outsourced using PCBA services.

### avr
It contains a program to be written to the microchip to make the board work.
Since this instrument uses Atmel's ATMega328P, it is being developed in Atmel Studio.

### server
The measured data is sent to a PC via Zigbee communication using XBee.
A software project file for receiving this data on a PC is available.
The software is developed with Microsoft's Visual Studio, and since .NET 5 is the target, it has also been confirmed to work on Linux.

### mobile
XBee can also communicate via Bluetooth from 3.0.
Therefore, data can also be received by ordinary smartphones.
This directory contains Xamarin project files to receive measurements on iPhone and Android.

## Author

[eisuke togashi](https:www.hvacsimulator.net)
