# Advanced settings and permanent mode

The items in this chapter are not used in normal measurement.
**Misconfiguring them may interfere with M-Logger operation.**

The three items "Correction coefficients" and "CO2 sensor calibration / initialization" only appear after you shake the smartphone on the [Measurement settings](settings.md) screen. The explicit shake gesture is the gate that protects them from accidental taps.

## Renaming the M-Logger

Use "Set M-Logger name" to change the display name shown in the scanner list.
Useful for distinguishing units when running several at once.

## Correction coefficients

Each unit is calibrated at the factory, so you normally do not need to touch this.
If you want to compensate for sensor drift after long-term use, the per-sensor gain and offset can be adjusted linearly.

On both v3 and v4 firmware, correction coefficients are set independently for the **five sensors** (drybulb temperature, relative humidity, globe temperature, illuminance, velocity — CO2 is excluded because it uses factory calibration + automatic self-calibration). Even though v4 collapses the measurement settings into 3 categories, correction coefficients remain per-sensor.

## CO2 sensor calibration, reset, and full initialization

The CO2 sensor (Sensirion STCC4) has an **Automatic Self-Calibration (ASC)** feature: as long as it is exposed roughly once a day to fresh outdoor-equivalent air (about 400 ppm), it auto-calibrates against that level.
In normal indoor measurement — with daily ventilation, window opening, or carrying the device outdoors — ASC works without any special action.

For long-term measurement in continuously high CO2 environments (sealed rooms, greenhouses, experimental chambers, etc.) or when sensor output drift becomes noticeable, use one of the manual operations below. Each maps to a different command in the Sensirion datasheet (ICD01).

| Button | Operation | Duration | Datasheet |
|--------|-----------|---------|-----------|
| **Calibrate CO2 Sensor** | 30 s measurement + forced recalibration to the entered known concentration | ~35 s | §3.4.15 `perform_forced_recalibration` |
| **Factory Reset CO2 Sensor** | Erases ASC / FRC history and re-enables the bypass phase (returns the sensor to a fresh-out-of-the-box state) | ~90 ms | §3.4.11 `perform_factory_reset` |
| **Fully Initialize CO2 Sensor** | Factory reset → 12-hour stabilization run → forced recalibration (reproduces datasheet §1.1.4 "Initial Operation") | ~12 h | combination of the above |

### Calibrate CO2 Sensor

Place the sensor under a clearly known CO2 concentration, enter that value, and calibrate.

- The reference CO2 concentration should be a value verified with another calibrated CO2 meter or with a known reference gas
- The calibration operation takes about **35 seconds**. Keep the sensor and surrounding conditions (CO2 concentration, temperature, humidity, power) stable during that time

### Factory Reset CO2 Sensor

Clears the ASC / FRC history accumulated inside the sensor and returns it to the same state as a brand-new unit. Completes immediately (~90 ms).

Use this if sensor output is clearly wrong after long-term use (e.g. ASC has converged on an incorrect baseline). After the reset, the sensor re-enters the "Initial Operation" phase described in datasheet §1.1.4 and rebuilds accuracy through 12 hours of continuous operation combined with exposure to fresh air.

### Fully Initialize CO2 Sensor

Performs the **factory reset above + 12-hour continuous stabilization run + forced recalibration to the specified CO2 concentration**, all sequenced automatically by the app.
You don't have to wait 12 hours manually, but the M-Logger is occupied by this measurement during that time, so it cannot be used for other measurements. Run this only in an environment where the specified CO2 concentration (typically 400 ppm fresh air) can be maintained for the full duration.

## Communication with PC

Used for multi-unit operation with PC + Zigbee.
In this topology the PC + XBee coordinator is the **parent**, and each M-Logger is a **child**.
For the concrete connection procedure and address assignment, see the PC operation manual (in preparation).

## Switching to permanent mode

![Permanent mode confirmation dialog](assets/screenshots/permanent_mode_dialog.png){ width="280" }

Selecting "Switch to permanent mode" configures the M-Logger so that it automatically starts measurement and sends data to the PC every time it is powered on.
This mode is intended for fixed installations on walls, ceilings, and the like.

!!! warning "Cannot be cancelled from the smartphone"
    Once in permanent mode, the M-Logger stays in permanent mode through any power cycle. You must use the physical Reset switch as described in the next section.

### Exiting permanent mode

![Reset switch location (legacy, provisional)](assets/screenshots/reset_switch_legacy.jpg){ width="280" }

!!! warning "Photo is from the legacy hardware"
    The photo above shows the Reset switch position of the legacy hardware (v3.x). On the v4 hardware the position differs. The photo will be replaced once a v4 picture is available.

Press and hold the Reset switch on the unit for 3 seconds or more; permanent mode is cancelled and the LED blinks three times.
