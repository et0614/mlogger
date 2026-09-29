# Firmware versions and supported features

## How to check the version

- **Smartphone app**: the app detects it automatically when you connect. The **M-Logger version** shown in "Other settings" at the bottom of the [Measurement settings](../mobile/settings.md) screen is `4.x.x` for v4 and `3.x.x` for v3.
- **Python tools** <span class="fw">v4+</span>: shown in the "Firmware" field of [`check_device.py`](../python/check_device.md).
- **MLServer**: when data is first received from an M-Logger, a line such as `<name>: Protocol = v4 (JSON-RPC), FW 4.0.0` is shown (`Protocol = v3` for v3). It may not be shown for an M-Logger that is measuring, because it cannot respond.

## How this manual marks the differences

- Features available only on some versions carry a badge such as <span class="fw">v4+</span>.
- Where the operation or screens differ by version, both are shown in "v4 firmware" / "v3 firmware" tabs.

## Features and supported firmware

The version badges throughout this manual are based on this table.

| Feature | v4 | v3 |
|---|:---:|:---:|
| Setup and measurement from a smartphone | ✓ | ✓ |
| Battery voltage display | ✓ | − |
| Download / clear recorded data from a smartphone | ✓ | − |
| Sending readings to a PC over Zigbee (MLServer) | ✓ | ✓ |
| USB communication (Python tools) | ✓ | − |
| Recording on the device | ✓ (built-in memory) | ✓ (memory card) |
| Firmware update over USB | ✓ | − |

## Main differences

| Firmware | Differences |
|---------|-------------|
| **v4** | Sensor settings collapsed into 3 categories / battery info shown / recorded data can be downloaded and cleared from the phone |
| **v3** | Sensor settings shown as 5 rows / no battery or data-management section / recorded data is read by removing the memory card and using a card reader on a PC |
