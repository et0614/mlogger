# Preparing the Zigbee coordinator

MLServer uses an XBee plugged into the PC as the Zigbee coordinator to receive readings from the M-Loggers.

## Equipment

- XBee (XBee 3 or S2C)
- An adapter to connect the XBee to the PC over USB

The following USB adapters have been confirmed to work.

- [https://akizukidenshi.com/catalog/g/gK-06188](https://akizukidenshi.com/catalog/g/gK-06188)
- [https://flashtree.com/products/11697](https://flashtree.com/products/11697)

These adapters need the FTDI D2XX driver ([www.ftdichip.com](https://www.ftdichip.com)).

## XBee settings

A new XBee cannot communicate with M-Loggers as it is. Change its settings as follows with Digi's [XCTU](https://www.digi.com/products/embedded-systems/digi-xbee/digi-xbee-tools/xctu).

| Parameter | Name | Value |
|---|---|---|
| ID | PAN ID | `19800614` |
| SP | Cyclic Sleep Period | `64` (1000 ms) |
| SN | Number of Cyclic Sleep Periods | `E10` (3600) |
| CE | Coordinator Enable | Enabled [1] |
| SM | Sleep Mode | No sleep [0] |
| AP | API Enable | API enabled [1] |
| BD | Baud Rate | 9600 [3] |

- **ID (PAN ID)**: the number of the Zigbee network. Devices with different PAN IDs do not communicate. To separate several networks at one site, use a different value for each network. The M-Loggers must use the same PAN ID.
- **SP, SN**: how long the coordinator holds messages for the M-Loggers, and how long the network is maintained.
- **CE**: when Enabled, the XBee becomes the coordinator.
- **SM**: the coordinator must always be awake, so it does not sleep.

## Number of units

One coordinator can connect up to 20 M-Loggers. For more units, add XBees as routers. Each router can connect another 20 units.

An XBee used as a router has the same settings as the table above **except CE, which is Disabled [0]**. There are also XBees that plug directly into an outlet ([example](https://akizukidenshi.com/catalog/g/gM-10502)).

The more units there are, the busier the radio becomes and the more readings may be lost. In a test with 80 units in a small room, a 3-second interval was the limit. Losses also increase near devices using the same frequency band, such as microwave ovens.
