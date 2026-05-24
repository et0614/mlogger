# Measurement settings

Selecting an M-Logger opens the **Measurement settings** screen.
On this screen you decide *what*, *where*, and *when* to record, then write the settings to the M-Logger.

![Measurement settings (top half)](assets/screenshots/device_setting_top.png){ width="280" }

## Logging destination

Choose where measurement values are saved: Phone, PC, or Flash.

- **Phone**: saved incrementally on the smartphone during measurement. The easiest option.
- **PC**: received over PC + Zigbee and saved on the PC. Intended for multi-unit operation ([Advanced settings](advanced.md#communication-with-pc)).
- **Flash**: saved to the M-Logger's onboard flash memory. Allows long-term standalone operation with no smartphone or PC connection. After the measurement ends, retrieve the data by connecting the **PC** with a **USB Type-C** cable (Bluetooth is too slow for the transfer).

## Active sensors and intervals

For each sensor, configure ON / OFF and the measurement interval (in seconds).
Disabling unused sensors reduces power consumption and extends the internal battery life.
Each sensor's interval can be set independently.

## Starting date and time

If you specify a future date and time, the M-Logger waits until that moment and then starts measurement automatically.
Use this for scheduled measurements (e.g. "start at 9 a.m. tomorrow").

## Exchanging settings with the M-Logger

- **"Read from M-Logger"**: pulls the settings currently written on the M-Logger into the smartphone. Useful when you want to inspect or reuse the settings of an M-Logger already in service.
- **"Write to M-Logger"**: writes the edited settings to the M-Logger. **The M-Logger does not pick up the changes until you write them**.

## Other settings

![Measurement settings (bottom half, normal state)](assets/screenshots/device_setting_bottom.png){ width="280" }

In the normal state, the "Other settings" section at the bottom of the screen shows:

- Name, XBee Name, XBee address, firmware version (read-only display)
- **Set M-Logger name**: change the display name of the M-Logger

This is all you need for regular measurement; the advanced settings below are intentionally hidden.

## Shake to reveal advanced settings

Shake the smartphone **once** (one short back-and-forth) to reveal extra buttons in the "Other settings" section.
**Shake once more** to hide them again.

![After shaking — advanced settings revealed](assets/screenshots/device_setting_bottom_shake.png){ width="280" }

- **Set Correction Coefficients**
- **Calibrate CO2 Sensor**
- **Initialize CO2 Sensor**

These can change the M-Logger's behaviour if set incorrectly, so they are hidden from the normal menu.
The shake gesture acts as an explicit gate so that you only reach them deliberately.

See [Advanced settings and permanent mode](advanced.md) for the meaning and usage of each item.

## Starting the measurement

After writing the settings to the M-Logger, start the measurement with the button matching your logging destination.

- Logging destination **Phone**: tap "Record to Smartphone" → the [During measurement](logging.md) screen opens; press Back to end.
- Logging destination **Flash**: tap "Record in Flash mode" → as soon as you press the start button, the app returns to ML Scanner and the M-Logger continues to measure on its own. **The M-Logger will not accept any Bluetooth connection until you cycle its power**.
- Logging destination **PC**: tap "Send to PC" → like Flash, the app returns to ML Scanner immediately. Subsequent data reception is handled on the PC + Zigbee side.
