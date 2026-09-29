# Downloading recorded data

Saves the data recorded in the built-in memory of the M-Logger to a CSV file.

```
python load_data.py
```

`mlogger_<hardwareID>_<datetime>.csv` is created in the current folder. Data cannot be downloaded while the M-Logger is measuring, so stop the measurement first.

## Options

| Option | Description |
|---|---|
| `-o <file>` | Name of the output file |
| `--all` | Also recover and include cleared data |

## CSV format

The file starts with lines beginning with `#` (device information, download time, etc.), followed by a row of column names, a row of units, and then one row per record.

| Column | Unit | Contents |
|---|---|---|
| `iso_time` | | Measurement time (local time of the PC) |
| `ts` | s | Measurement time (UNIX time) |
| `gen` | | Data generation number (incremented at each clear) |
| `t_dry` | °C | Dry-bulb temperature |
| `humidity` | % | Relative humidity |
| `t_glb` | °C | Globe temperature |
| `wind_speed` | m/s | Air velocity |
| `voltage` | mV | Voltage of the air velocity sensor |
| `illuminance` | lx | Illuminance |
| `co2` | ppm | CO2 concentration |

An empty cell means the sensor was not in use or not connected at that time.
