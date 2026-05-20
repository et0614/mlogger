# M-Logger

Materials for the production of M-Logger, an indoor thermal environment measurement device.

## Description

The M-Logger was developed with the objective of facilitating the measurement of the indoor thermal environment.
In order to evaluate the environment in question with the requisite degree of accuracy, it is necessary to measure
at least the dry bulb temperature, relative humidity, radiation temperature, and wind speed.
The cost of comprehensive instruments that measure these physical quantities is typically in excess of $1000.
Furthermore, they are bulky and challenging to install in multiple locations due to the necessity of a power outlet.
In contrast, the M-Logger is manufactured at a cost of less than $100, is compact and portable, and is powered
by two AA batteries.

Further information can be found in the accompanying
[paper](https://www.jstage.jst.go.jp/article/aijt/28/68/28_267/_article/-char/ja)
and [website](https://www.mlogger.jp).

## Repository layout

```
mlogger/
  hardware/       -- PCB, BOM, datasheets, enclosure
  firmware/       -- MCU firmware (MPLAB X projects)
  software/       -- .NET host applications + Python utilities
  docs/           -- Protocol specifications
```

### hardware/

| File / directory                 | Purpose                                                                |
| -------------------------------- | ---------------------------------------------------------------------- |
| `pcb/mlogger_main.f3z` ...       | Autodesk Fusion 360 PCB project files (main / TH probe / Vel probe).   |
| `pcb/*.png` , `*.pdf`            | Rendered board images and schematic PDFs for quick reference.          |
| `pcb/*_gerber.zip`               | Gerber package for PCBA outsourcing.                                   |
| `BOM.xlsx`                       | Bill of materials.                                                     |
| `datasheets/`                    | Datasheets for each mounted component.                                 |
| `enclosure/loggercase.3dm`       | Rhinoceros 3D model for the housing.                                   |

### firmware/

Firmware for the on-board MCU. MPLAB X IDE projects (the `.X` directory is MPLAB X's project format,
the successor to Atmel Studio). MCC (MPLAB Code Configurator) is used for peripheral initialization.

| Project                | Target MCU   | Role                                                       |
| ---------------------- | ------------ | ---------------------------------------------------------- |
| `mlogger_main.X/`      | AVR64DU32    | Main logger board firmware (sensors + radio + USB CDC).    |
| `mlogger_vel_probe.X/` | AVR64DU32    | Anemometer probe board firmware.                           |

The main board firmware speaks the **v4 protocol** (JSON-RPC style, line-delimited JSON over the
XBee/USB byte stream) defined in [`docs/protocol_v4.md`](docs/protocol_v4.md). Earlier firmware
revisions used a 3-character ASCII command set (referred to as v3); host applications below remain
backward-compatible with v3 hardware through a dedicated adapter so existing units keep working.

USB-CDC is also exposed as a side channel for diagnostics (RX frame dumps, dispatch traces, TX
status events) — extremely helpful when debugging Zigbee/BLE chunking and timing.

### software/

A Visual Studio solution covering three .NET 10 projects, plus a small set of Python utilities.

```
software/dotnet/MLSoftwares.sln
  MLLib/         -- shared protocol + transport abstraction (no UI)
  MLS_Mobile/    -- MAUI app for iPhone / Android (BLE to the device)
  MLServer/      -- Windows console application: Zigbee + BACnet + HTTP dashboard
software/python/
  test_protocol_v4.py  -- v4 smoke test over USB-CDC
  ble_trace.py         -- listen to USB-CDC diagnostic stream during BLE/Zigbee tests
  ...                  -- a handful of one-off helpers
```

Communication paths:

- **MLS_Mobile** — phone-side app, connects to one M-Logger at a time via Bluetooth LE
  (XBee 3 BLE GATT). Suitable for personal use or short measurement sessions.
- **MLServer** — runs on a Windows PC with a coordinator XBee on a serial port,
  receives streams from many M-Loggers over Zigbee in parallel, writes per-device CSV,
  publishes a JSON snapshot for an HTML dashboard, and exposes measurements as a BACnet
  device for integration with building automation systems.

Both applications speak the same `IMLProtocol` abstraction defined in `MLLib`. Two implementations
are provided:

- `JsonRpcV4Protocol` — talks the v4 JSON protocol to current firmware.
- `LegacyV3Protocol` — talks the older v3 ASCII protocol to existing devices that have not been
  re-flashed yet.

`ProtocolFactory.DetectAsync` probes the device at connect time (v3 `VER` then v4 `hello`) and
returns the matching protocol implementation, so callers do not branch on firmware version.

The .NET projects target `net10.0`. Building requires .NET 10 SDK; MLS_Mobile additionally needs the
MAUI workloads (`dotnet workload install maui-ios maui-android`).

### docs/

| File                | Contents                                                                   |
| ------------------- | -------------------------------------------------------------------------- |
| `protocol_v4.md`    | Specification of the v4 JSON-RPC communication protocol (commands/events). |

## License

Copyright Eisuke Togashi 2023.

This source describes Open Hardware and is licensed under the CERN-OHL-P v2. You may redistribute
and modify this documentation and make products using it under the terms of the
[CERN-OHL-P v2](https://cern.ch/cern-ohl). This documentation is distributed WITHOUT ANY EXPRESS OR
IMPLIED WARRANTY, INCLUDING OF MERCHANTABILITY, SATISFACTORY QUALITY AND FITNESS FOR A PARTICULAR
PURPOSE. Please see the CERN-OHL-P v2 for applicable conditions.

## Author

[Eisuke Togashi](https://www.mlogger.jp)
