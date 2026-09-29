# BACnet

MLServer works as a BACnet/IP device and can pass the readings to a building management system.

## Settings

Set the following items in [setting.ini](settings.md#settingini).

| Item | Contents |
|---|---|
| `bacnet` | `true` enables BACnet |
| `bacip` | IP address of the PC running MLServer. `0.0.0.0` listens on all networks |
| `bacport` | BACnet port number (usually 47808 or above) |
| `bacdevid` | Device ID (default 614) |

The BACnet values are read-only. They cannot be written from the management system.

## Objects

When readings are first received from an M-Logger, objects for that M-Logger are added. `i` is the order in which the M-Loggers were found (starting from 0).

`i` may change when MLServer is restarted. In the management system, map the objects by the M-Logger address contained in the object name. The list of found M-Logger addresses is in CharacterString Value 1 in CSV format.

### Readings (Analog Input)

| Instance | Contents | Unit |
|---|---|---|
| 1000 + i | Dry-bulb temperature | °C |
| 2000 + i | Globe temperature | °C |
| 3000 + i | Air velocity | m/s |
| 4000 + i | Illuminance | lx |
| 5000 + i | Relative humidity | % |
| 6000 + i | Mean radiant temperature | °C |
| 7000 + i | PMV | − |
| 8000 + i | SET\* | °C |
| 9000 + i | WBGT (indoor) | °C |
| 10000 + i | WBGT (outdoor) | °C |
| 11000 + i | CO2 concentration <span class="fw">v4+</span> | ppm |
| 12000 + i | PPD | % |

### Last measurement time (DateTime Value)

| Instance | Contents |
|---|---|
| 1000 + i | Dry-bulb temperature and relative humidity |
| 2000 + i | Globe temperature |
| 3000 + i | Air velocity |
| 4000 + i | Illuminance |
| 5000 + i | CO2 concentration <span class="fw">v4+</span> |
