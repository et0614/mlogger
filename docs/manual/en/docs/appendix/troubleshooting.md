# Troubleshooting

## MLServer

**`Failed to connect port COMx` is shown**
: MLServer looks for the XBee on every port of the PC, so this is shown for ports without an XBee. If `Connection succeeded` is shown for the XBee port, there is no problem.

**`Connection succeeded` is not shown**
: Check that the USB adapter of the XBee is recognized by the PC (the driver is installed). Also check that the XBee settings (API mode, baud rate 9600) are as described in [Preparing the Zigbee coordinator](../server/coordinator.md).

**No data arrives from the M-Logger**
: Check that the measurement was started with **PC** as the destination in the smartphone app, that the M-Logger and the coordinator use the same PAN ID, and the distance and number of units (up to 20 per coordinator).

**Shown as `MLogger_` and an address instead of the name**
: Until the name is received from the M-Logger, it is shown by its address. A measuring M-Logger sends its name when the measurement starts and at the daily clock adjustment. To name it right away, register it in [mlnames.txt](../server/settings.md#mlnamestxt).

**The list in the browser is not updated**
: Some browsers do not update `data/index.htm` when it is opened directly as a file. Publish it with a web server and open it from there ([Starting measurement and the data](../server/data.md)).

**Not visible from the BACnet management system**
: If `bacip` in `setting.ini` is still the default `127.0.0.1`, only the same PC can connect. Set the IP address of the PC (or `0.0.0.0`). Also check that the BACnet port (`bacport`) is open in the firewall of the PC.

## Python tools

**The M-Logger is not found**
: Check the USB cable and the power of the M-Logger. Running [`check_device.py`](../python/check_device.md) shows the state of each port.

**The port is shown as busy**
: Another application (a serial monitor, another running script, etc.) is using the port. Close it and run again.

**`ModuleNotFoundError: No module named 'serial'`**
: The library to install is `pyserial`, not `serial`. If you installed `serial` by mistake, run `pip uninstall serial` and then `pip install pyserial`.

**`python` is not recognized**
: Python is not installed or not on the PATH. On Windows 11, typing `python` in the command prompt opens the Microsoft Store installation page.
