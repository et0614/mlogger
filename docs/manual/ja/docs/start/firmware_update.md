# ファームウェアの更新

<span class="fw">v4 以降</span> M-Logger 本体のファームウェアを、USB で PC から書き換えます。

更新に使うファイルは、配布している「M-Logger ツール」の `firmware_update` フォルダに入っています。[GitHub の Releases](https://github.com/et0614/mlogger/releases) から、最新版の `mlogger_tools.zip` をダウンロードして展開します。

## 準備

1. **電池を取り外します。** 電源スイッチは USB 給電と電池給電の切り替えなので、電池を入れたままではスイッチを切り替えても電源が切れません。
2. 電源スイッチを**電池給電の側**にしたまま、USB ケーブルで M-Logger を PC につなぎます。電池がないので、電源は入りません。

PC には M-Logger を 1 台だけつないでください。

## 書き込み

=== "Windows"

    1. `firmware_update` フォルダの `update.bat` をダブルクリックします。
    2. 画面の説明を読み、**Reset ボタンを押したまま、電源スイッチを USB 給電の側に切り替えます**。
    3. 赤 LED が点滅していることを確かめてから、Reset ボタンを離します。これで書き込み待ちの状態です。
    4. `update.bat` の画面で Enter キーを押すと、書き込みが始まります (10 秒ほど)。
    5. 「書き込み完了」と表示されたら、電源スイッチを一度電池給電の側に戻してから USB 給電の側にします。新しいファームウェアで起動します。

=== "macOS / Linux"

    1. avrdude (8.0 以降) をインストールします。
        - macOS: `brew install avrdude`
        - Linux: 各ディストリビューションのパッケージ (版が古い場合は [avrdude のリリース](https://github.com/avrdudes/avrdude/releases) から入手)
    2. **Reset ボタンを押したまま、電源スイッチを USB 給電の側に切り替えます**。
    3. 赤 LED が点滅していることを確かめてから、Reset ボタンを離します。これで書き込み待ちの状態です。
    4. `firmware_update` フォルダで次を実行します (`-D` は必ず付けてください)。

        ```
        avrdude -P usb:04d8:0b12 -c jtag3updi -p avr64du32 -D -U flash:w:mlogger_main.X.production.hex:i
        ```

    5. 書き込みが終わったら、電源スイッチを一度電池給電の側に戻してから USB 給電の側にします。新しいファームウェアで起動します。

書き込み中は USB ケーブルを抜いたり、スイッチを切り替えたりしないでください。失敗した場合は、電源スイッチを電池給電の側に戻し、書き込みの手順を最初からやり直します。

## 更新の確認

ファームウェアの版は、スマートフォンアプリの [計測の設定](../mobile/settings.md) 画面の「その他の設定」、または Python ツールの [`check_device.py`](../python/check_device.md) で確認できます。
