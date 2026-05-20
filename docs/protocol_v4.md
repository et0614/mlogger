# M-Logger 通信プロトコル v4 (覚書)

> **本文書はv4設計検討中の覚書です。実装と合わせて正式な仕様書を別途用意する予定。**
> 作成: 2026-05-17

## 1. 概要と前提

M-Logger v4 のメイン基板ファームと親機(MAUI/Python/その他クライアント)間の通信仕様。

- **transport**: XBee ZigBee (主)、XBee BLE (副)、USB-CDC (PC直結)
- **エンコーディング**: JSON Lines (`\n` 区切り、UTF-8)
- **設計思想**: JSON-RPC 2.0 を参考にした非対称request/response/eventモデル
- **後方互換**: ファームv4は新プロトコルのみ実装。v3端末との互換は **親機(MAUI)側で吸収**
- **サイズ方針**: 1メッセージ ≤200B を目安。**XBee 3 の APS レベル断片化**(XBee モジュール内で透過的にフレーム分割)に任せる。アプリ側ではチャンク制御を持たない

### v3 からの主な変更

- 固定オフセットASCII → JSON Lines
- 親機→子機(3文字コマンド) と 子機→親機(CSV) の非対称構造 → 統一エンベロープ
- get/set ペアコマンド (CMS/LMS など) → 単一リソースの get/set
- 風速校正コマンド一式(SVC/LVC/SCV/ECV/CBV) → 風速プローブ側のMCUに移管、メインからは削除
- CBT(温度自動校正)、PLS(パルス)、HCS(CO2有無問合せ)、KPA、中継系(RTB/SRB/CRY) を廃止
- WHO/VER/LLN/HCS を `hello` 1コマンドに統合

## 2. 共通エンベロープ

全メッセージはJSON 1オブジェクト + `\n` 終端。`v` フィールドでプロトコルバージョンを明示。

| 種別 | 方向 | 形式 |
|---|---|---|
| コマンド (request) | 親機 → 子機 | `{"v":1,"id":<int>,"command":"<name>","params":{...}}` |
| 成功応答 | 子機 → 親機 | `{"v":1,"id":<int>,"result":{...}}` |
| 失敗応答 | 子機 → 親機 | `{"v":1,"id":<int>,"error":{"code":"<str>","message":"<str>"}}` |
| イベント (自発) | 子機 → 親機 | `{"v":1,"event":"<name>","ts":<unix_sec>,"data":{...}}` |

### ルール

- **`v`**: プロトコルバージョン (本書は `v:1`)
- **`id`**: コマンドと応答の紐付けに必須。32bit unsigned integer、親機側でユニーク採番
- **`params` / `data`**: 引数や本体がなければ省略可
- **`result` と `error` は排他**: 存在する方で成否判定 (`ok:bool` のような冗長フィールドは使わない)
- **イベントには `id` なし**: 自発送信のため
- **set系コマンドは PATCH-style**: 指定キーのみ更新。省略キーは現状維持
- **欠測値**: `null`
- **boolean**: `true` / `false` (バイト節約より可読性優先)
- **タイムスタンプ `ts`**: UNIX秒 (整数)

## 3. transport別の特性と XBee 設定要件

| transport | 1メッセージ目安 | 備考 |
|---|---|---|
| **XBee ZigBee** | 200B程度まで実用 | XBee 3 firmware の APS 断片化が透過処理 (~1500Bまで対応) |
| **XBee BLE** | 200B程度まで実用 | スマホ↔1子機専用 |
| **USB-CDC** | 実質無制限 | バルクダンプ専用バイナリルートあり |

### XBee 設定要件 (重要)

親機/子機の XBee 3 module で以下を確認:

| AT command | 設定値 | 目的 |
|---|---|---|
| `NP` | 84以上 (read-only) | 単一フレーム上限の確認。デフォルト ~84 |
| `EE` | 0 (Encryption Disable) | 暗号化有効だと NP が小さくなる (デフォルトオフ) |
| **APS 断片化** | 有効 | 200B超のメッセージを XBee 側で複数フレーム送信 |

→ 親機(MAUI) 側の README に明記する。XCTU で初回設定。

## 4. コマンド一覧

センサ名 (固定): `t_dry` / `humidity` / `t_glb` / `velocity` / `illuminance` / `co2`
(線形補正は CO2 を除く 5 センサ対象)

### 4.1 `hello` — 接続確認と機器情報取得

旧 `WHO` + `VER` + `LLN` + `HCS` を統合。接続後最初に呼ぶ。

```jsonc
// 要求
{"v":1,"id":1,"command":"hello"}

// 応答 (~150B)
{"v":1,"id":1,"result":{
  "device": "M-Logger",
  "firmware_version": "4.0.0",
  "protocol_version": 1,
  "hardware_id": "4B2F0D12",
  "name": "SIH-01",
  "logging": false
}}
```

| キー | 意味 |
|---|---|
| `device` | 機器種別文字列 (固定値 `"M-Logger"`、版数は `firmware_version` で別途返す) |
| `firmware_version` | ファーム版数 (semver) |
| `protocol_version` | プロトコルバージョン |
| `hardware_id` | AVR `SIGROW.SERNUM0` を `fnv1a_32` でハッシュ化した8文字hex (vel_probe `main.c:226-229` と同方式) |
| `name` | ユーザ設定可能な機器名 (最大20文字 UTF-8) |
| `logging` | 現在ロギング中か |

### 4.2 `start_logging` / `stop_logging`

旧 `STL` / `ENL`。

```jsonc
// ~110B
{"v":1,"id":2,"command":"start_logging","params":{
  "transports": {"zigbee":true, "ble":false, "flash":true, "usb":false},
  "mode": "once"
}}
{"v":1,"id":2,"result":{}}
```

| キー | 意味 |
|---|---|
| `transports.zigbee/ble/flash/usb` | 各出力先の有効/無効 |
| `mode` | `"once"` (一回限り) または `"auto_restart"` (常設モード、電源再投入後も自動再開) |

リセットボタン3秒長押しで `auto_restart` 解除 (ハードウェア機能、変更なし)。

```jsonc
{"v":1,"id":3,"command":"stop_logging"}
{"v":1,"id":3,"result":{}}
```

### 4.3 `get_settings` / `set_settings`

旧 `LMS` / `CMS`。**get/set とも構造体形式**で対称。set は PATCH-style (指定キーのみ更新)。

```jsonc
// 取得
{"v":1,"id":4,"command":"get_settings"}

// 応答 (~200B)
{"v":1,"id":4,"result":{
  "t_dry":       {"enabled":true,  "interval":60},
  "humidity":    {"enabled":true,  "interval":60},
  "t_glb":       {"enabled":true,  "interval":60},
  "velocity":    {"enabled":true,  "interval":300},
  "illuminance": {"enabled":false, "interval":0},
  "co2":         {"enabled":true,  "interval":60},
  "start_ts":    1747500000
}}

// 設定 (PATCH-style、変更したいセンサのみ指定)
{"v":1,"id":5,"command":"set_settings","params":{
  "velocity":    {"interval":60},
  "illuminance": {"enabled":true, "interval":60}
}}
// 応答: 更新後の全状態 (get と同じ形式)
{"v":1,"id":5,"result":{ ... 上と同じ構造 ... }}
```

| キー | 意味 |
|---|---|
| `<sensor>.enabled` | センサ有効 |
| `<sensor>.interval` | 計測間隔 [秒]、範囲 1〜99999 |
| `start_ts` | 計測開始時刻 (UNIX秒、0なら即時開始) |

### 4.4 `get_correction` / `set_correction`

旧 `LCF` / `SCF`。線形補正 `y = a*x + b`。CO2 除く 5 センサ対象。

```jsonc
// 取得
{"v":1,"id":6,"command":"get_correction"}

// 応答 (~180B)
{"v":1,"id":6,"result":{
  "t_dry":       {"a":1.000, "b": 0.00},
  "humidity":    {"a":1.000, "b": 0.00},
  "t_glb":       {"a":1.000, "b": 0.00},
  "illuminance": {"a":1.000, "b": 0.0},
  "velocity":    {"a":1.000, "b": 0.000}
}}

// 設定 (PATCH-style)
{"v":1,"id":7,"command":"set_correction","params":{
  "humidity": {"a":1.020, "b":-0.50}
}}
// 応答: 更新後の全状態
```

許容範囲(v3継承):

| センサ | a の範囲 | b の範囲 |
|---|---|---|
| `t_dry` | 0.800 〜 1.200 | -3.00 〜 3.00 |
| `humidity` | 0.800 〜 1.200 | -9.99 〜 9.99 |
| `t_glb` | 0.800 〜 1.200 | -3.00 〜 3.00 |
| `illuminance` | 0.800 〜 1.200 | -999 〜 999 |
| `velocity` | 0.800 〜 1.200 | -0.500 〜 0.500 |

`velocity` は風速プローブMCU側で生風速を求めた後、メインで線形補正を適用する。

### 4.5 `set_name`

旧 `CLN`。`get_name` は `hello.result.name` で代替。

```jsonc
{"v":1,"id":8,"command":"set_name","params":{"name":"SIH-01"}}
{"v":1,"id":8,"result":{"name":"SIH-01"}}
```

最大長: 20文字 (UTF-8)

### 4.6 `set_time`

旧 `UCT`。

```jsonc
{"v":1,"id":9,"command":"set_time","params":{"ts":1747500000}}
{"v":1,"id":9,"result":{"ts":1747500000}}
```

### 4.7 `calibrate_co2`

旧 `IC2` + `CCL` 統合。

```jsonc
{"v":1,"id":10,"command":"calibrate_co2","params":{
  "mode":"forced",
  "target_ppm":420
}}
// 即時ACK
{"v":1,"id":10,"result":{}}
// 以降、校正完了まで co2_calibration_progress イベントが流れる
```

| キー | 意味 |
|---|---|
| `mode` | `"forced"` (30秒モード) または `"factory"` (12時間モード) |
| `target_ppm` | 基準CO2濃度 [ppm] |

### 4.8 `clear_data`

旧 `CLR`。記録データの論理消去 (世代番号インクリメント)。XBee/USB 両方から受付。

```jsonc
{"v":1,"id":11,"command":"clear_data"}
{"v":1,"id":11,"result":{}}
```

### 4.9 `dump` (USB-CDC専用)

旧 `DMP`。記録データ全量転送。**バイナリ形式は v3 を踏襲。**

```jsonc
// 要求
{"v":1,"id":12,"command":"dump"}

// 制御応答: 件数とフォーマット情報
{"v":1,"id":12,"result":{
  "count": <int>,
  "record_size": 18,
  "format": "<BIBIhhHHHH>"
}}

// 以降、バイナリストリーム:
// レコード × count、1レコード18バイトのリトルエンディアン struct
//   uint8  gen, uint32 ts, uint8 flags, uint32 reserved,
//   int16  lux, int16  t_dry, uint16 t_glb, uint16 hum,
//   uint16 wind, uint16 volt, uint16 co2
// (v3 Python `software/python/load_data.py` の <BIBIhhHHHH> と同一)

// 終了通知 (JSON モードに戻る合図)
{"v":1,"event":"dump_end","ts":1747500000,"data":{"sent":<int>}}
```

XBee/BLE から送ると:
```jsonc
{"v":1,"id":12,"error":{"code":"unsupported_transport","message":"dump is USB-CDC only"}}
```

## 5. イベント一覧

### 5.1 `ready` — ハートビート

旧 `WFC` + XBee接続維持用空 `\r` パケット を統合。

```jsonc
{"v":1,"event":"ready","ts":1747500000,"data":{
  "uptime_s":3600,
  "logging":false
}}
```

- 送信間隔: **60秒** (XBee接続維持に十分)
- ロギング中は `smp` が流れるため **`ready` は送らない**(従来通り)

### 5.2 `smp` — 計測サンプル

旧 `DTT`。**高頻度送信のため短縮キーを採用** (XBee 1フレーム=84B以内に確実に収める)。

```jsonc
{"v":1,"event":"smp","ts":1747500000,"data":{
  "t":  23.45,
  "h":  55.2,
  "g":  24.10,
  "vel":0.420,
  "l":  420,
  "c":  425
}}
```

短縮キーと完全名の対応(プロトコルv1固定):

| 短縮 | 完全名 | 単位 | 丸め桁 |
|---|---|---|---|
| `t` | `t_dry` | °C | 小数2桁 |
| `h` | `humidity` | % | 小数1桁 |
| `g` | `t_glb` | °C | 小数2桁 |
| `vel` | `velocity` | m/s | 小数3桁 |
| `l` | `illuminance` | lx | 整数 |
| `c` | `co2` | ppm | 整数 |

- 計測しないセンサのキーは **送信時に省略**
- 計測したが値が無効/欠測の場合は `null`

### 5.3 `co2_calibration_progress` — CO2校正進捗

旧 `CCL:残秒,状態,補正値,現在値` を置き換え。

```jsonc
{"v":1,"event":"co2_calibration_progress","ts":1747500000,"data":{
  "remaining_s": 15,
  "state": "measuring",
  "correction_ppm": 0,
  "current_ppm": 423
}}
```

| キー | 意味 |
|---|---|
| `remaining_s` | 残り秒数 |
| `state` | `"measuring"` / `"pass"` / `"fail"` |
| `correction_ppm` | 補正値 [ppm] (校正完了時のみ意味あり) |
| `current_ppm` | 現在の計測値 [ppm] |

- 校正中 1秒毎に送信
- 終了時に `state:"pass"` または `state:"fail"` を1回送って終了

### 5.4 `time_sync_request` — 時刻同期要求 (子機 → 親機)

子機が長期計測中に RTC drift 補正を目的として親機 (MLServer 等の Zigbee 受信側を想定)
に対して「いま時刻設定コマンドを送ってほしい」と能動的に要求するイベント。子機は本
イベント送出直後から `data.window_s` 秒間、無線を awake に維持し `set_time` 受信を待つ。

```jsonc
{"v":1,"event":"time_sync_request","ts":1747500000,"data":{
  "window_s": 30
}}
```

| キー | 意味 |
|---|---|
| `ts` | 子機の現 RTC (UTC unix 秒)。親機側で drift 量を観測可能 |
| `data.window_s` | 子機が wake を維持する秒数 (この時間内に `set_time` を送れば確実に届く) |

**動作シーケンス**:

1. 子機: 計測開始から (or 前回同期から) 24 時間経過で `time_sync_request` 送出 +
   無線 awake 維持開始
2. 親機: 本イベント受信 → 即座に `set_time` コマンド送信
3. 子機: `set_time` 受信 → RTC 更新 → `set_time` の応答 (`{"v":1,"id":...,"result":{"ts":...}}`)
   を送信 → 同期カウンタリセット → sleep 復帰
4. 子機: window タイムアウト (`window_s` 秒経過) で親機応答無し → sleep 復帰、次回
   24 時間後に再試行

**子機内部スケジュール**:

- 初回の `time_sync_request` は計測開始時刻に依存せず、最初に到来する深夜 0:00 を
  ターゲットとする (2.4GHz 帯の混雑が少ない時間帯で同期成功率を上げる)
- 以降は前回同期成功時刻から 24 時間ごと

**注意**:

- 本機能は v4 protocol_version >= 1 のみ。v3 firmware では未実装
- 親機が応答しない場合でも子機はそのまま計測継続。RTC drift は次回 24 時間で再試行

## 6. エラー応答

```jsonc
{"v":1,"id":<int>,"error":{
  "code":"<エラーコード>",
  "message":"<人間向け説明>"
}}
```

`code` がプログラム判定用、`message` がデバッグ/ログ向け。詳細データ (許容範囲など) は載せない方針 — エラーコード対応表は別途リファレンスで提供する。

### エラーコード初期セット

| code | 意味 |
|---|---|
| `unknown_command` | 未定義のコマンド名 |
| `invalid_params` | パラメータの構造/型不正 |
| `out_of_range` | 値が許容範囲外 (補正係数の範囲超過など) |
| `unsupported_transport` | 現在のtransportでは未サポート (例: dump on XBee) |
| `busy` | 別操作実行中 (CO2校正中に別校正コマンド等) |
| `internal_error` | その他、ハードエラー等 |

## 7. 後方互換性 (親機側で吸収)

ファーム v4 は新プロトコルのみ実装。市場の v3 端末は旧プロトコルで動き続けるため、親機(MAUI)側で両対応する。

### バージョン判定フロー (親機側)

1. 接続後、親機は `{"v":1,"id":1,"command":"hello"}` を送信
2. 応答が
   - **JSON で `result.protocol_version >= 1`** → 新プロトコル (`JsonRpcProtocol`) を採用
   - **タイムアウト or 非JSON** → v3 端末と判断、旧 `VER` コマンドにフォールバック (`LegacyProtocol`)

実装はインタフェース抽象化で吸収:

```csharp
interface IDeviceProtocol { ... }
class LegacyProtocol : IDeviceProtocol { ... }   // 旧3文字コマンド+CSV
class JsonRpcProtocol : IDeviceProtocol { ... }  // 新JSON
```

## 8. 未決定事項

- **XBee APS断片化の実機動作確認**: XBee 3 firmware の現状設定で 200B 程度のメッセージが透過的に断片化送信されるかを実測。NP値とAS/NW系AT command の状態を XCTU で確認する。実測が異なる結果なら設計見直し (実装フェーズで verify)

## 9. 参考

- v3 仕様書: `~/OneDrive/デスクトップ/claude連携/document_ja_2026.01.01.docx` 第6章「通信仕様」
- 旧コマンド実装: `firmware/mlogger_main.X/command_handler.c`, `eeprom_manager.c`, `logger_control.c`
- v3 dump 形式の参照実装: `software/python/load_data.py`
- vel_probe の HW ID 生成参考: `firmware/mlogger_vel_probe.X/main.c:226-229`
- XBee 3 ZigBee リファレンス: Digi XBee 3 RF Module, Hardware Reference Manual (Rev. V, 2022.3)
- 関連設計判断はリポジトリの git log 参照

## 10. 短縮キー対応表 (`smp` イベント専用)

`smp` イベント以外は全てフルキー名を使用。

| 短縮 | 完全名 | 単位 |
|---|---|---|
| `t` | `t_dry` | °C |
| `h` | `humidity` | % |
| `g` | `t_glb` | °C |
| `vel` | `velocity` | m/s |
| `l` | `illuminance` | lx |
| `c` | `co2` | ppm |
