# M-Logger Smartphone Operation Manual

This manual explains how to operate the indoor thermal-environment measurement system **M-Logger** from an iPhone or Android smartphone.

This manual covers app **version 1.3.1**.

## Downloading the app

| OS | Download |
|---|---|
| iPhone / iPad | [App Store](https://apps.apple.com/us/app/mlogger-server/id1599907037) |
| Android | [Google Play](https://play.google.com/store/apps/details?id=net.hvacsimulator.mls) |

The app is free. Its name is **MLogger Server**.

## How this manual is organised

1. [Installation and first launch](installation.md) — download and permission setup
2. [Screen layout](overview.md) — the four tabs and common UI
3. [Finding an M-Logger](scanner.md) — scanning and connecting
4. [Measurement settings](settings.md) — destination, sensors, start time
5. [During measurement](logging.md) — real-time values, clothing and metabolic rate
6. [Reviewing measured data](data.md) — viewing, sharing, deleting saved data
7. [Thermal comfort and moist air calculators](calculators.md) — PMV / PPD / SET\* and moist-air properties
8. [Advanced settings and permanent mode](advanced.md) — correction coefficients, CO2 calibration, PC communication, permanent mode

## Which firmware version is on your M-Logger

M-Loggers ship with one of two firmware lineages, and the app shows slightly
different screens depending on which firmware your unit runs.

| Firmware | Differences |
|---------|-------------|
| **v4 (new)** | Sensor settings collapsed into 3 categories / battery info shown / recorded data can be downloaded and cleared from the phone |
| **v3 (legacy)** | Sensor settings shown as 5 rows / no battery or data-management section / retrieving recorded data requires PC + USB |

When you scan and connect, **the app auto-detects the firmware and switches the
UI accordingly**. To check explicitly, look at the **M-Logger version** displayed
in the "Other settings" section of the [Measurement settings](settings.md) screen
— `4.x.x` is v4, `3.x.x` is v3.

Where this manual shows different screens, both versions are presented in tabs.
Pick the tab matching your device.

## Out of scope

This manual is limited to **smartphone operation**. For other topics, see the following:

- **M-Logger hardware operation, LED meanings, MMC / flash memory specification**: [Hardware operation manual (PDF, Japanese)](https://mlogger.jp/ja/document_3.4.1.pdf)
- **Multi-unit operation using PC + Zigbee**: PC operation manual (in preparation)
- **Communication specification for controlling M-Logger from your own software**: Communication specification (in preparation)

## Support

- Official site: [https://mlogger.jp](https://mlogger.jp)
- Contact: e.togashi@gmail.com
