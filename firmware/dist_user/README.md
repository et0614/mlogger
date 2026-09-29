# Updating the M-Logger firmware

Rewrites the firmware of the M-Logger device from a PC over USB (v4 and later).

| File | Contents |
|---|---|
| `update.bat` | Starts the update (Windows) |
| `avrdude.exe`, `avrdude.conf` | Programming tool (avrdude 8.1, GPL v2. License: `COPYING_avrdude.txt`) |
| `mlogger_main.X.production.hex` | New firmware |

*日本語版: [README_ja.md](README_ja.md)*

## Steps (Windows)

1. **Remove the batteries.** The power switch selects between USB power and battery power, so the device does not turn off while batteries are inserted.
2. Keep the power switch on the **battery side** and connect the M-Logger to the PC with a USB cable. The device stays off because there are no batteries.
3. Double-click `update.bat`.
4. Read the instructions on the screen, then **move the power switch to the USB side while holding the Reset button**.
5. Check that the red LED is blinking, then release the Reset button. The device is now waiting for the update.
6. Press Enter in the `update.bat` window. Writing starts and takes about 10 seconds.
7. When "Update completed" is shown, move the power switch to the battery side and back to the USB side. The device starts with the new firmware.

## Notes

- Do not unplug the USB cable or move the switch while writing.
- Connect only one M-Logger to the PC.
- If writing fails, move the power switch back to the battery side and start again from step 3.

For macOS / Linux, see "Updating the firmware" in the user manual.

## Source code of avrdude

The source code of the included avrdude 8.1 is available on request for three years from this distribution. Please contact us through [GitHub Issues](https://github.com/et0614/mlogger/issues).
