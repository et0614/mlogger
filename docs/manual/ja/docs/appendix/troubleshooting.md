# トラブルシューティング

## MLServer

**`Failed to connect port COMx` と表示される**
: MLServer は PC のすべてのポートで XBee を探すため、XBee がつながっていないポートではこの表示が出ます。XBee のポートで `Connection succeeded` と表示されていれば問題ありません。

**`Connection succeeded` が表示されない**
: XBee の USB アダプタが PC に認識されているか (ドライバが入っているか) を確認してください。XBee の設定 (API mode、通信速度 9600) が [Zigbee 親機の準備](../server/coordinator.md) のとおりかも確認してください。

**M-Logger のデータが届かない**
: M-Logger をスマートフォンアプリで記録先「PC」にして計測を始めたか、M-Logger と親機の PAN ID が同じか、親機からの距離や台数 (1 台の親機につき 20 台まで) を確認してください。

**名前ではなく `MLogger_` とアドレスで表示される**
: M-Logger から名前を受け取るまでの間は、アドレスで表示されます。計測中の M-Logger は、計測の開始時か、1 日 1 回の時刻合わせのときに名前を送ります。すぐに名前を付けたい場合は、[mlnames.txt](../server/settings.md#mlnamestxt) に登録してください。

**ブラウザの一覧が更新されない**
: `data/index.htm` をファイルとして直接開くと、ブラウザによっては更新されません。Web サーバーで公開して開いてください ([計測の開始とデータ](../server/data.md))。

**BACnet の監視システムから見えない**
: `setting.ini` の `bacip` が初期値の `127.0.0.1` のままだと、同じ PC からしか接続できません。PC の IP アドレス (または `0.0.0.0`) を設定してください。PC のファイアウォールで BACnet のポート (`bacport`) が開いているかも確認してください。

## Python ツール

**M-Logger が見つからない**
: USB ケーブルと M-Logger の電源を確認してください。[`check_device.py`](../python/check_device.md) を実行すると、ポートごとの状態が表示されます。

**ポートが使用中 (busy) と表示される**
: 別のアプリ (シリアルモニタや、実行中の別のスクリプトなど) がポートを使っています。閉じてから実行し直してください。

**`ModuleNotFoundError: No module named 'serial'` と表示される**
: インストールするライブラリの名前は `serial` ではなく `pyserial` です。誤って `serial` を入れた場合は、`pip uninstall serial` の後に `pip install pyserial` を実行してください。

**`python` が認識されない**
: Python がインストールされていないか、PATH が通っていません。Windows 11 では、コマンドプロンプトで `python` と入力すると Microsoft Store のインストール画面が開きます。
