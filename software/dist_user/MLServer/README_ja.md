# MLServer

M-Logger の計測データを Zigbee で受信し、CSV ファイルに記録します。

## 準備

1. .NET Runtime 10 をインストールします —
   [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
   (Windows / macOS / Linux)
2. XBee コーディネータを USB ポートに接続します。

## 実行

- Windows: `MLServer.exe` を実行
- macOS / Linux: `dotnet MLServer.dll`

XBee コーディネータは自動検出されます。各 M-Logger のデータは
`data/<機器アドレス>.csv` に追記され、最新値は `data/latest.json` に
書き出されます。

## ファイル

- `setting.ini` — 設定 (BACnet、温熱指標計算のデフォルト値)
- `mlnames.txt` — 機器アドレスと表示名の対応表

*English version: [README.md](README.md)*
