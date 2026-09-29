# Installation and startup

## Installing .NET

MLServer runs on .NET 10. Install **.NET Runtime 10** from Microsoft's website.

- [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

## Download

MLServer is included in the "M-Logger Tools" package together with the Python tools. Download the latest `mlogger_tools.zip` from [GitHub Releases](https://github.com/et0614/mlogger/releases) and extract it.

The `MLServer` folder contains the following files.

| File | Contents |
|---|---|
| `MLServer.exe` / `MLServer.dll` | MLServer |
| `MLServer.sh` | Startup script for macOS / Linux |
| `setting.ini` | Settings file ([Settings](settings.md)) |
| `mlnames.txt` | List of M-Logger names ([Settings](settings.md)) |
| `data/` | Measured data and files for the browser display ([Starting measurement and the data](data.md)) |

## Startup

1. Plug the XBee of the [Zigbee coordinator](coordinator.md) into a USB port of the PC.
2. Start MLServer.
    - Windows: run `MLServer.exe`
    - macOS / Linux: `dotnet MLServer.dll` (or `MLServer.sh`)
3. When the XBee is recognized, a line like the following is shown.

```
COM5: Connection succeeded. S/N = 0013A200xxxxxxxx
```

`COM5` depends on the PC. `0013A200xxxxxxxx` is the address of the XBee.

The XBee is found automatically, so there is no need to specify the port. If several XBees are plugged in, each one is used as a coordinator.

## Continuous operation (Raspberry Pi)

For long-term operation on site, a Raspberry Pi keeps the setup compact. To keep running through power outages, put a mobile battery with pass-through charging between the outlet and the Raspberry Pi (the Raspberry Pi consumes about 3 W).

### Installing .NET

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime dotnet
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
dotnet --list-runtimes
```

### Starting automatically

When MLServer is registered as a service, it starts automatically when the Raspberry Pi boots and restarts automatically if it ever stops. The example below assumes the user name `pi` and MLServer placed in `MLServer` in the home folder.

1. Create `/etc/systemd/system/mlserver.service` with the following contents (for example with `sudo nano /etc/systemd/system/mlserver.service`).

    ```ini
    [Unit]
    Description=MLServer
    After=network-online.target

    [Service]
    User=pi
    WorkingDirectory=/home/pi/MLServer
    ExecStart=/home/pi/.dotnet/dotnet /home/pi/MLServer/MLServer.dll
    Restart=always
    RestartSec=10

    [Install]
    WantedBy=multi-user.target
    ```

2. Enable and start the service.

    ```bash
    sudo systemctl daemon-reload
    sudo systemctl enable --now mlserver
    ```

3. The following command shows what MLServer prints.

    ```bash
    journalctl -u mlserver -f
    ```

To use the XBee over USB, the user must be allowed to use serial ports (the default Raspberry Pi OS user is).

### Periodic reboot

You can also reboot the Raspberry Pi itself every day just in case. Add the following line with `sudo crontab -e` to reboot at 0:00 every day.

```
0 0 * * * /sbin/reboot
```
