# Settings

## setting.ini

Settings are written in `setting.ini` in the same folder as MLServer. Write one item per line as `name=value;`; text after `//` is a comment. Changes take effect when MLServer is restarted.

| Item | Contents | Default |
|---|---|---|
| `bacnet` | Whether to use BACnet (`true` / `false`) | `true` |
| `bacip` | IP address of the PC used for BACnet | `127.0.0.1` |
| `bacport` | Port number used for BACnet | `47809` |
| `bacdevid` | BACnet Device ID | `614` |
| `met` | Metabolic rate used for thermal comfort [met] | `1.7` |
| `clo` | Clothing insulation used for thermal comfort [clo] | `1.0` |
| `dbt` | Value used when dry-bulb temperature is not measured [°C] | `25.0` |
| `rhd` | Value used when relative humidity is not measured [%] | `50.0` |
| `vel` | Value used when air velocity is not measured [m/s] | `1.0` |
| `mrt` | Value used when radiant temperature is not measured [°C] | `25.0` |

For the BACnet items, see [BACnet](bacnet.md).

## mlnames.txt

MLServer automatically receives and shows the name set on each M-Logger (set with the smartphone app). Use `mlnames.txt` when you want to give a different name on site. Names registered here take priority over the names on the devices.

Write one M-Logger per line: its address (lower 8 digits of the XBee address) and its name, separated by `:`. Text after `//` is a comment.

```
42114F7E:SIH-01
42114F57:SIH-02   // 2nd floor meeting room
```

Until the name is received from the device, or if neither name is available, the M-Logger is shown as `MLogger_` followed by its 16-digit address.
