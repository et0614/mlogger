# 準備

## ダウンロード

Python ツールは、MLServer と一緒に配布している「M-Logger ツール」の `python_tools` フォルダに入っています。[GitHub の Releases](https://github.com/et0614/mlogger/releases) から、最新版の `mlogger_tools.zip` をダウンロードして展開します。

## Python のインストール

Python 3 をインストールします。

- **Windows**: Microsoft Store で「Python」を検索してインストールします。[python.org](https://www.python.org/) から入手する場合は、インストール時に **「Add python.exe to PATH」** にチェックを入れてください。
- **macOS / Linux**: [python.org](https://www.python.org/) または各 OS のパッケージ管理ツールでインストールします。

## ライブラリのインストール

`python_tools` フォルダでコマンドプロンプト (macOS / Linux ではターミナル) を開き、次を実行します。

```
pip install pyserial
```

## M-Logger の接続

M-Logger の電源を入れ、USB Type-C ケーブルで PC につなぎます。

どのスクリプトも、M-Logger がつながっているポートを自動で探します。ポートを指定する場合は、スクリプト名の後に書きます。

```
python load_data.py COM5
```

macOS / Linux では `/dev/tty.usbmodem...` や `/dev/ttyACM0` のような名前になります。
