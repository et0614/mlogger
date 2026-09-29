# インストールと起動

## .NET のインストール

MLServer は .NET 10 で動きます。Microsoft の Web サイトから **.NET Runtime 10** をインストールしてください。

- [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

## ダウンロード

MLServer は、Python ツールと一緒に配布している「M-Logger ツール」に含まれています。[GitHub の Releases](https://github.com/et0614/mlogger/releases) から、最新版の `mlogger_tools.zip` をダウンロードして展開します。

展開すると `MLServer` フォルダに次のファイルがあります。

| ファイル | 内容 |
|---|---|
| `MLServer.exe` / `MLServer.dll` | MLServer 本体 |
| `MLServer.sh` | macOS / Linux 用の起動スクリプト |
| `setting.ini` | 設定ファイル ([設定](settings.md)) |
| `mlnames.txt` | M-Logger の名前の一覧 ([設定](settings.md)) |
| `data/` | 計測データとブラウザ表示用のファイル ([計測の開始とデータ](data.md)) |

## 起動

1. [Zigbee 親機](coordinator.md) の XBee を PC の USB ポートに挿します。
2. MLServer を起動します。
    - Windows: `MLServer.exe` を実行
    - macOS / Linux: `dotnet MLServer.dll` (または `MLServer.sh`)
3. XBee が認識されると、次のように表示されます。

```
COM5: Connection succeeded. S/N = 0013A200xxxxxxxx
```

`COM5` の部分は PC によって異なります。`0013A200xxxxxxxx` は XBee 固有のアドレスです。

XBee は自動で探すので、ポートの指定は不要です。複数の XBee を挿すと、それぞれを親機として使います。

## 常時運転 (Raspberry Pi)

現場で長期間運転する場合は、Raspberry Pi を使うと省スペースで運用できます。停電に備えて、コンセントと Raspberry Pi の間にパススルー充電に対応したモバイルバッテリーを挟むと、停電中も運転を続けられます (Raspberry Pi の消費電力は 3 W 程度)。

### .NET のインストール

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime dotnet
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
dotnet --list-runtimes
```

### 起動時に自動で実行する

MLServer をサービスとして登録すると、Raspberry Pi の起動時に自動で動き始め、万一止まっても自動で再起動します。以下は、ユーザー名が `pi`、MLServer をホームフォルダの `MLServer` に置いた場合の例です。

1. 次の内容で `/etc/systemd/system/mlserver.service` を作ります (`sudo nano /etc/systemd/system/mlserver.service` など)。

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

2. サービスを有効にして起動します。

    ```bash
    sudo systemctl daemon-reload
    sudo systemctl enable --now mlserver
    ```

3. 動作は次のコマンドで確認できます。MLServer の画面に表示される内容が流れます。

    ```bash
    journalctl -u mlserver -f
    ```

XBee を USB で使うには、ユーザーがシリアルポートを使える必要があります (Raspberry Pi OS の標準ユーザーは使えます)。

### 定期的な再起動

念のため Raspberry Pi 自体を毎日再起動させることもできます。`sudo crontab -e` で次の行を追加すると、毎日 0 時 0 分に再起動します。

```
0 0 * * * /sbin/reboot
```
