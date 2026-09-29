# Checking the device

Checks that the M-Logger is connected correctly and working.

```
python check_device.py
```

## What is shown

1. **Port scan**: checks the serial ports of the PC one by one and shows the port where the M-Logger was found. If it is not found, the state of each port (busy, no response, etc.) is shown.
2. **Device information**: name, hardware ID, firmware version, battery voltage, attached probes and number of recorded records.
3. **Current sensor readings**: runs a short test measurement and shows dry-bulb temperature, relative humidity, globe temperature, CO2 concentration, illuminance and air velocity.

The hardware ID is also used to look up the factory inspection report on the website.

The test measurement may take up to about 90 seconds for the sensors to warm up. The measurement settings are changed temporarily and restored afterwards. If the M-Logger is measuring, the test measurement is skipped.

## Options

| Option | Description |
|---|---|
| `--no-live` | Show only the device information, without the test measurement |
