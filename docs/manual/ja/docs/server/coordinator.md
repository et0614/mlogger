# Zigbee 親機の準備

MLServer は、PC に挿した XBee を Zigbee の親機 (Coordinator) として使い、M-Logger からの計測値を受け取ります。

## 必要な機材

- XBee (XBee 3 または S2C)
- XBee を USB で PC につなぐアダプタ

次の USB アダプタは動作を確認しています。

- [https://akizukidenshi.com/catalog/g/gK-06188](https://akizukidenshi.com/catalog/g/gK-06188)
- [https://flashtree.com/products/11697](https://flashtree.com/products/11697)

これらのアダプタを使うには、FTDI 社の D2XX ドライバのインストールが必要です ([www.ftdichip.com](https://www.ftdichip.com))。

## XBee の設定

購入したばかりの XBee は、そのままでは M-Logger と通信できません。Digi 社の [XCTU](https://www.digi.com/products/embedded-systems/digi-xbee/digi-xbee-tools/xctu) で次のように設定を書き換えます。

| 項目 | 名称 | 設定値 |
|---|---|---|
| ID | PAN ID | `19800614` |
| SP | Cyclic Sleep Period | `64` (1000 ms) |
| SN | Number of Cyclic Sleep Periods | `E10` (3600) |
| CE | Coordinator Enable | Enabled [1] |
| SM | Sleep Mode | No sleep [0] |
| AP | API Enable | API enabled [1] |
| BD | Baud Rate | 9600 [3] |

- **ID (PAN ID)**: Zigbee のネットワークの番号です。PAN ID が異なる機器どうしは通信しません。1 つの現場で複数のネットワークを分けたい場合は、ネットワークごとに変えます。M-Logger 側の PAN ID も同じ値にする必要があります。
- **SP・SN**: 親機が M-Logger 宛てのメッセージを保持する時間と、ネットワークを維持する時間です。
- **CE**: Enabled にすると、その XBee が親機になります。
- **SM**: 親機は常に起動している必要があるので、スリープしない設定にします。

## 接続できる台数

1 台の親機に接続できる M-Logger は 20 台までです。それ以上の台数を使う場合は、中継器 (Router) となる XBee を追加します。Router 1 台につき、さらに 20 台を接続できます。

Router にする XBee は、上の表と同じ設定で **CE のみ Disabled [0]** にします。コンセントに直接挿して使う XBee もあります ([例](https://akizukidenshi.com/catalog/g/gM-10502))。

台数を増やすほど無線が混み合い、計測値を取りこぼしやすくなります。小部屋に 80 台を設置した試験では、3 秒間隔の計測が限界でした。電子レンジなど、同じ周波数帯を使う機器の近くでも取りこぼしが増えます。
