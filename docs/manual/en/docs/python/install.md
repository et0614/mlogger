# Setup

## Download

The Python tools are in the `python_tools` folder of the "M-Logger Tools" package, distributed together with MLServer. Download the latest `mlogger_tools.zip` from [GitHub Releases](https://github.com/et0614/mlogger/releases) and extract it.

## Installing Python

Install Python 3.

- **Windows**: search for "Python" in the Microsoft Store and install it. If you get it from [python.org](https://www.python.org/), check **"Add python.exe to PATH"** during installation.
- **macOS / Linux**: install it from [python.org](https://www.python.org/) or with the package manager of your OS.

## Installing the library

Open a command prompt (a terminal on macOS / Linux) in the `python_tools` folder and run:

```
pip install pyserial
```

## Connecting the M-Logger

Turn on the M-Logger and connect it to the PC with a USB Type-C cable.

Every script finds the port of the M-Logger automatically. To specify the port, write it after the script name.

```
python load_data.py COM5
```

On macOS / Linux the port name looks like `/dev/tty.usbmodem...` or `/dev/ttyACM0`.
