# BACnet 連携

MLServer は BACnet/IP の機器 (BACnet Device) として動作し、受け取った計測値を建物の監視システムに渡せます。

## 設定

[setting.ini](settings.md#settingini) で次の項目を設定します。

| 項目 | 内容 |
|---|---|
| `bacnet` | `true` にすると BACnet 連携が有効になる |
| `bacip` | MLServer を動かす PC の IP アドレス。`0.0.0.0` にするとすべてのネットワークで待ち受ける |
| `bacport` | BACnet のポート番号 (通常 47808 以降) |
| `bacdevid` | Device ID (初期値 614) |

BACnet の値は読み出し専用です。監視システムから書き込むことはできません。

## オブジェクトの一覧

M-Logger から最初に計測値を受け取ったときに、その M-Logger 用のオブジェクトが追加されます。`i` は M-Logger を見つけた順の番号 (0 から) です。

MLServer を再起動すると `i` が変わることがあります。監視システムでは、オブジェクト名に含まれる M-Logger のアドレスで対応付けてください。見つかっている M-Logger のアドレスの一覧は、CharacterString Value 1 に CSV 形式で入っています。

### 計測値 (Analog Input)

| インスタンス番号 | 内容 | 単位 |
|---|---|---|
| 1000 + i | 乾球温度 | °C |
| 2000 + i | グローブ温度 | °C |
| 3000 + i | 風速 | m/s |
| 4000 + i | 照度 | lx |
| 5000 + i | 相対湿度 | % |
| 6000 + i | 平均放射温度 | °C |
| 7000 + i | PMV | − |
| 8000 + i | SET\* | °C |
| 9000 + i | WBGT (屋内) | °C |
| 10000 + i | WBGT (屋外) | °C |
| 11000 + i | CO2 濃度 <span class="fw">v4 以降</span> | ppm |
| 12000 + i | PPD | % |

### 最終計測日時 (DateTime Value)

| インスタンス番号 | 内容 |
|---|---|
| 1000 + i | 乾球温度・相対湿度 |
| 2000 + i | グローブ温度 |
| 3000 + i | 風速 |
| 4000 + i | 照度 |
| 5000 + i | CO2 濃度 <span class="fw">v4 以降</span> |
