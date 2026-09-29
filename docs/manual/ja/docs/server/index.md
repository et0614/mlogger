# PC (MLServer)

MLServer は、複数台の M-Logger から Zigbee で計測値を受け取り、PC に保存するソフトウェアです。受け取った計測値はブラウザで一覧・ヒートマップ表示でき、BACnet で建物の監視システムに渡すこともできます。

**M-Logger (複数台)** → Zigbee → **XBee 親機** → USB → **PC (MLServer)** → CSV ファイル / ブラウザでの表示 / BACnet

## この章の構成

1. [インストールと起動](install.md) — 必要なもの、起動方法、常時運転
2. [Zigbee 親機の準備](coordinator.md) — PC に挿す XBee の設定
3. [計測の開始とデータ](data.md) — M-Logger からの送信開始、保存されるファイル、ブラウザでの表示
4. [設定](settings.md) — setting.ini、mlnames.txt
5. [BACnet 連携](bacnet.md) — 建物の監視システムとの接続

## 必要なもの

- PC (Windows / macOS / Linux。Raspberry Pi も使えます)
- Zigbee 親機となる XBee と USB アダプタ
- M-Logger の送信を始めるためのスマートフォンアプリ
