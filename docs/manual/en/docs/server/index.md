# PC (MLServer)

MLServer receives readings from multiple M-Loggers over Zigbee and saves them on a PC. The readings can be shown in a browser as a list or a heat map, and passed to a building management system over BACnet.

**M-Loggers (multiple)** → Zigbee → **XBee coordinator** → USB → **PC (MLServer)** → CSV files / browser display / BACnet

## Contents of this chapter

1. [Installation and startup](install.md) — requirements, startup, continuous operation
2. [Preparing the Zigbee coordinator](coordinator.md) — settings of the XBee on the PC
3. [Starting measurement and the data](data.md) — starting transmission, saved files, browser display
4. [Settings](settings.md) — setting.ini, mlnames.txt
5. [BACnet](bacnet.md) — connecting to a building management system

## Requirements

- A PC (Windows / macOS / Linux; a Raspberry Pi also works)
- An XBee and a USB adapter to serve as the Zigbee coordinator
- The smartphone app, to make the M-Loggers start sending
