# System overview

M-Logger is a compact instrument for measuring the indoor thermal environment. Probes are plugged into the device, and the readings are received on a smartphone or a PC.

![Assembled device](../assets/photos/device_assembled.jpg){ width="400" }

## Device and probes

| Part | Measures |
|---|---|
| Device | Illuminance. Has wireless (Zigbee / Bluetooth), USB and built-in memory |
| Temperature/humidity/CO2 probe (with globe) | Dry-bulb temperature, relative humidity, CO2 concentration, globe temperature |
| Air velocity probe | Air velocity (low air speed) |

Either probe can be plugged into either of the two sockets on the top of the device. When air velocity is not measured, the air velocity probe can be stored on the inside of the battery cover ([Basic device operation](device.md)).

Indices such as mean radiant temperature (MRT), PMV, PPD, SET\* and WBGT can also be calculated from the readings.

## Receiving the readings

| Use | Link | Software |
|---|---|---|
| Set up and measure one unit at hand | Bluetooth (BLE) | [Smartphone app](../mobile/index.md) |
| Record to the built-in memory and retrieve later | Bluetooth or USB <span class="fw">v4+</span> | [Smartphone app](../mobile/data.md) or [Python tools](../python/load_data.md) |
| Collect readings from multiple units on a PC | Zigbee | [MLServer](../server/index.md) |
| Check the device or retrieve recorded data over USB | USB <span class="fw">v4+</span> | [Python tools](../python/index.md) |

Measurement settings (items, interval, destination) are made in the smartphone app for every use.
