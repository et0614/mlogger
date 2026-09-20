# M-Logger Python Tools

Command-line tools for using an M-Logger from a Windows PC over USB (Type-C).

| Script | Purpose |
|---|---|
| `check_device.py` | Verify the device works: connection, battery, probes, live sensor readings |
| `load_data.py` | Download recorded data from the device and save it as a CSV file |
| `clear_data.py` | Clear the recorded data on the device |

*Japanese version: see [README_ja.md](README_ja.md).*

## Setup

1. Install Python 3 — from the Microsoft Store (search for "Python") or from
   [python.org](https://www.python.org/) (if you use the python.org installer,
   check **"Add python.exe to PATH"** during installation).
2. Open a Command Prompt in the folder containing these scripts and run:

   ```
   pip install pyserial
   ```

   (or `pip install -r requirements.txt`)

3. Power on the M-Logger and connect it to the PC with a USB Type-C cable.

## Usage

All scripts find the device automatically; you can also pass a COM port
explicitly (e.g. `python load_data.py COM5`).

### Check the device

```
python check_device.py
```

Scans the serial ports, connects to the M-Logger, and prints the device name,
hardware ID, firmware version, battery voltage, attached probes, and the
number of stored records. It then runs a short test measurement and shows the
current sensor readings (sensor warm-up can take up to about 90 seconds).
If a measurement is already running on the device, the test part is skipped.

### Download recorded data

```
python load_data.py
```

Downloads all records and writes `mlogger_<hardware id>_<date>.csv` into the
current folder. Downloading is not possible while the device is recording.

Options:

```
python load_data.py -o out.csv     # choose the output filename
python load_data.py --all          # also recover cleared data
```

### Clear recorded data

```
python clear_data.py
```

Clears the recorded data after a confirmation prompt.

## CSV format

The file starts with `#` comment lines (device and download metadata),
followed by a column-name row, a unit row, and one row per record:

| Column | Unit | Description |
|---|---|---|
| `iso_time` | | Measurement time (local) |
| `ts` | s | UNIX timestamp |
| `gen` | | Data generation number (incremented by each clear) |
| `t_dry` | C | Dry-bulb temperature |
| `humidity` | % | Relative humidity |
| `t_glb` | C | Globe temperature |
| `wind_speed` | m/s | Wind speed |
| `voltage` | mV | Velocity sensor voltage |
| `illuminance` | lx | Illuminance |
| `co2` | ppm | CO2 concentration |

An empty cell means the sensor was disabled or not connected for that record.

## Troubleshooting

- **No serial ports found** — check the USB cable connection and that the
  device is powered on. `check_device.py` prints a per-port diagnosis.
- **Port is busy** — another application (a serial monitor, another script)
  has the port open. Close it and retry.
- **`ModuleNotFoundError: No module named 'serial'`** — install the package
  named `pyserial` (not `serial`): `pip uninstall serial` if you installed
  the wrong one, then `pip install pyserial`.
- **`python` is not recognized** — Python is not installed or not on PATH.
  On Windows 11, typing `python` opens the Microsoft Store page to install it.
