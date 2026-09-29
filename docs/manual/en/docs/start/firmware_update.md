# Updating the firmware

<span class="fw">v4+</span> Rewrites the firmware of the M-Logger device from a PC over USB.

The files for the update are in the `firmware_update` folder of the "M-Logger Tools" package. Download the latest `mlogger_tools.zip` from [GitHub Releases](https://github.com/et0614/mlogger/releases) and extract it.

## Preparation

1. **Remove the batteries.** The power switch selects between USB power and battery power, so the device does not turn off while batteries are inserted, whichever way the switch is set.
2. Keep the power switch on the **battery side** and connect the M-Logger to the PC with a USB cable. The device stays off because there are no batteries.

Connect only one M-Logger to the PC.

## Writing

=== "Windows"

    1. Double-click `update.bat` in the `firmware_update` folder.
    2. Read the instructions on the screen, then **move the power switch to the USB side while holding the Reset button**.
    3. Check that the red LED is blinking, then release the Reset button. The device is now waiting for the update.
    4. Press Enter in the `update.bat` window. Writing starts and takes about 10 seconds.
    5. When "Update completed" is shown, move the power switch to the battery side and back to the USB side. The device starts with the new firmware.

=== "macOS / Linux"

    1. Install avrdude (8.0 or later).
        - macOS: `brew install avrdude`
        - Linux: your distribution's package (if it is old, get it from the [avrdude releases](https://github.com/avrdudes/avrdude/releases))
    2. **Move the power switch to the USB side while holding the Reset button**.
    3. Check that the red LED is blinking, then release the Reset button. The device is now waiting for the update.
    4. Run the following in the `firmware_update` folder (always include `-D`).

        ```
        avrdude -P usb:04d8:0b12 -c jtag3updi -p avr64du32 -D -U flash:w:mlogger_main.X.production.hex:i
        ```

    5. When writing is finished, move the power switch to the battery side and back to the USB side. The device starts with the new firmware.

Do not unplug the USB cable or move the switch while writing. If writing fails, move the power switch back to the battery side and start the writing steps again from the beginning.

## Checking the update

The firmware version can be checked in "Other settings" on the [Measurement settings](../mobile/settings.md) screen of the smartphone app, or with [`check_device.py`](../python/check_device.md) of the Python tools.
