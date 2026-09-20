# M-Logger Python ツール

M-Logger を Windows PC から USB (Type-C) 経由で操作するコマンドラインツールです。

| スクリプト | 用途 |
|---|---|
| `check_device.py` | 動作確認: 接続・電池・プローブ・センサ現在値の表示 |
| `load_data.py` | 記録データをダウンロードして CSV ファイルに保存 |
| `clear_data.py` | 機器内の記録データを消去 |

*English version: [README.md](README.md)*

## 準備

1. Python 3 をインストールします — Microsoft Store で「Python」を検索するか、
   [python.org](https://www.python.org/) から入手します (python.org 版の場合は
   インストール時に **「Add python.exe to PATH」** に必ずチェック)。
2. スクリプトを置いたフォルダでコマンドプロンプトを開き、以下を実行します:

   ```
   pip install pyserial
   ```

   (`pip install -r requirements.txt` でも可)

3. M-Logger の電源を入れ、USB Type-C ケーブルで PC に接続します。

## 使い方

どのスクリプトも接続ポートを自動検出します。明示指定も可能です
(例: `python load_data.py COM5`)。

### 動作確認

```
python check_device.py
```

シリアルポートを走査して M-Logger に接続し、機器名・ハードウェア ID・
ファームウェアバージョン・電池電圧・接続プローブ・記録データ件数を表示します。
続けて短いテスト計測を行い、センサの現在値を表示します (センサのウォームアップに
最大 90 秒ほどかかることがあります)。機器が計測中の場合、テスト計測は
スキップされます。

### 記録データのダウンロード

```
python load_data.py
```

全記録をダウンロードし、カレントフォルダに
`mlogger_<ハードウェアID>_<日時>.csv` を書き出します。
機器が計測中はダウンロードできません。

オプション:

```
python load_data.py -o out.csv     # 出力ファイル名を指定
python load_data.py --all          # 消去済みのデータも復旧して取り出す
```

### 記録データの消去

```
python clear_data.py
```

確認プロンプトの後、記録データを消去します。

## CSV の形式

ファイル先頭に `#` で始まるコメント行 (機器情報・ダウンロード時のメタ情報)、
続いて列名の行・単位の行、その後 1 記録 1 行のデータが並びます:

| 列 | 単位 | 内容 |
|---|---|---|
| `iso_time` | | 計測時刻 (ローカル時刻) |
| `ts` | s | UNIX タイムスタンプ |
| `gen` | | データ世代番号 (消去のたびに増加) |
| `t_dry` | C | 乾球温度 |
| `humidity` | % | 相対湿度 |
| `t_glb` | C | グローブ温度 |
| `wind_speed` | m/s | 風速 |
| `voltage` | mV | 風速計の電圧 |
| `illuminance` | lx | 照度 |
| `co2` | ppm | CO2 濃度 |

空欄は、その記録の時点でセンサが無効または未接続だったことを意味します。

## よくあるトラブル

- **シリアルポートが見つからない** — USB ケーブルの接続と機器の電源を確認して
  ください。`check_device.py` がポートごとの診断を表示します。
- **ポートが使用中 (busy)** — 別のアプリ (シリアルモニタや他のスクリプト) が
  ポートを開いています。閉じてから再実行してください。
- **`ModuleNotFoundError: No module named 'serial'`** — インストールする
  パッケージ名は `serial` ではなく `pyserial` です。誤って入れた場合は
  `pip uninstall serial` の後、`pip install pyserial` を実行してください。
- **`python` が認識されない** — Python が未インストールか PATH にありません。
  Windows 11 ではコマンドプロンプトで `python` と入力すると Microsoft Store の
  インストールページが開きます。
