# MLServer

Receives measurement data from M-Loggers over Zigbee and logs it to CSV files.

## Setup

1. Install the .NET Runtime 10 —
   [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
   (Windows / macOS / Linux)
2. Connect the XBee coordinator to a USB port.

## Run

- Windows: run `MLServer.exe`
- macOS / Linux: `dotnet MLServer.dll`

The XBee coordinator is detected automatically. Data from each M-Logger is
appended to `data/<device address>.csv`, and the latest readings are written
to `data/latest.json`.

## Files

- `setting.ini` — configuration (BACnet, defaults for comfort indices)
- `mlnames.txt` — device address to display name mapping

*Japanese version: [README_ja.md](README_ja.md)*
