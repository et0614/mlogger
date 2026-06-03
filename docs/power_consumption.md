# M-Logger v4 消費電流試験レポート

**試験実施日**: 2026-05-30 〜 2026-06-01
**対象**: M-Logger v4 本体 + 温湿度・CO2 プローブ (mlogger_th_sensor) + 風速プローブ (mlogger_velocity_sensor)
**計測器**: 共立電気 KEW2510 (DC 電流計、4mA / 40mA / 400mA / 4A レンジ、0.1μA 分解能、1sec sampling)

---

## 1. 試験目的

- 電池駆動 (アルカリ AA × 2 / Eneloop AA × 2) での連続計測可能時間 (battery life) を算出するため、各種計測モードでの消費電流を把握する
- 本体・プローブの各部位の消費を分解し、設計の改善余地を見極める
- センサ構成と polling 周期の組合せが battery life に与える影響を定量化する

## 2. 試験方法と環境

### 2.1 計測対象モード

| 計測対象 | 動作モード | firmware test mode |
|---------|----------|-------------------|
| プローブ単体 (TH/CO2) | Sleep (POWER-DOWN 固定) | `TEST_MODE_SLEEP` |
| プローブ単体 (TH/CO2) | Active (連続 measureOnce) | `TEST_MODE_ACTIVE` |
| プローブ単体 (風速) | Sleep (熱線 OFF + POWER-DOWN) | `TEST_MODE_SLEEP` |
| プローブ単体 (風速) | Active (熱線 ON + ADC 連続) | `TEST_MODE_ACTIVE` |
| 本体 system (各ケース) | 本番動作、内蔵 flash 書込のみ | `TEST_MODE_NONE` |

### 2.2 本体 system 計測の 5 ケース

| ケース | 有効化センサ | polling 周期 |
|--------|-------------|-------------|
| 1. 計測なし | (全 disable) | - |
| 2. TH/GLB/CO2 のみ | t_dry, humidity, t_glb, co2 | 1 sec |
| 3. 風速のみ | velocity | 1 sec |
| 4. 照度のみ | illuminance | 1 sec |
| 5. すべて | 上記全部 | 1 sec |

各ケースで 60sec 間連続計測。出力先は内蔵 flash のみ (BLE/Zigbee/USB 無効化)。

### 2.3 電源条件

- **プローブ単体計測**: 安定化電源 3.3V を probe VDD に直接供給 (I2C 配線は SDA/SCL → 3.3V 直結)
- **本体 system 計測**: Eneloop AA × 2 (フレッシュ、実測 2.76V) を VBAT に接続

### 2.4 重要な前置き: 安定化電源 vs 電池

開発初期、安定化電源 (CV mode) で本体 system を計測しようとしたところ、低 VBAT (~2.4V) で **cascade boost (VBAT → 3.3V → 5V) の collapse** が頻発した。詳細解析の結果、これは **安定化電源と m-logger 内蔵 DC-DC1 の制御ループ干渉** が主因と判明し、**実電池では発生しない試験アーティファクト**であることが確認された (Eneloop 1 本 = 約 1.3V でも cascade boost 起動可)。

→ **本試験は battery 駆動で計測**。bench PSU は系統的に高めの電流値を示すため、battery life 試算には不適。

## 3. 計測結果

### 3.1 プローブ単体 (probe に 3.3V 直接供給、60sec 安定領域平均)

| プローブ | モード | mean [mA] | min [mA] | max [mA] | 備考 |
|---------|--------|-----------|----------|----------|------|
| 温湿度・CO2 | Sleep | **0.03** | 0.01 | 0.14 | POWER-DOWN、STCC4 + SHT4x × 2 idle |
| 温湿度・CO2 | Active | **5.58** | 5.50 | 6.18 | 連続 measureOnce (~520ms/iter) |
| 風速 | Sleep | **0.13** | 0.01 | 0.31 | 熱線 OFF (SLP=low)、bridge op-amp idle ~100μA 含 |
| 風速 | Active | **39.4** | 39.0 | 58.4 | 熱線 ON 定常 (無風)、bridge feedback 動作 |

#### 内訳解釈
- **TH Sleep 30μA** ≈ AVR POWER-DOWN ~1μA + STCC4 sleep ~0.4μA + SHT4x × 2 sleep ~0.16μA + PCB leakage 残り
- **TH Active 5.58mA** ≈ AVR active ~3mA + STCC4 single-shot 平均 ~2mA + SHT4x_glb 読込 ~0.5mA
- **風速 Sleep 0.13mA** ≈ bridge op-amp quiescent (常時 ON) が支配的
- **風速 Active 39.4mA** ≈ 熱線駆動 ~35mA + AVR + bridge

### 3.2 本体 system 全体 (VBAT 2.76V、60sec 平均)

| ケース | mean [mA] | min | max | 備考 |
|--------|-----------|-----|-----|------|
| 1. 計測なし | **1.64** | 0.30 | 7.35 | baseline (内 ~1mA は LED 点滅) |
| 2. TH/GLB/CO2 のみ (1sec) | **4.92** | 2.31 | 13.2 | base + th_probe ~3.3mA |
| 3. 風速のみ (1sec) | **61.3** | 56.4 | 164.5 | base + 風速 ~59.7mA |
| 4. 照度のみ (1sec) | **1.69** | 0.35 | 7.27 | base + OPT3001 ~0.05mA |
| 5. すべて (1sec) | **65.8** | 58.0 | 176.7 | 全部足したのと一致 |

#### Baseline 1.64mA の内訳推定

| 要素 | mA | 備考 |
|------|-----|------|
| AVR64DU32 idle (24MHz) | ~0.5 | sleep + 時々 active |
| XBee Zigbee sleep | ~0.01 | datasheet: 5μA |
| W25Q256 standby | ~0.01 | datasheet: ~10μA |
| DC-DC1 quiescent | ~0.05 | typical boost IC |
| LED 5sec 周期点滅 | **~1.0** | mean-median 差分から推定 (`blinkGreenLED(1)` を 5sec ごと、375ms 点灯) |
| その他 | ~0.07 | 全体 1.64mA との差分 |

**5sec ごとの LED 点滅 (計測中視認用) で baseline に 1mA 程度上乗せされている**。長期運用ではこの点滅を間引く設計余地あり。

### 3.3 加算性検証

各ケースの増分 (vs baseline):

| ケース | mean 増分 [mA] |
|--------|---------------|
| TH/GLB/CO2 増分 | 4.92 - 1.64 = **3.28** |
| 風速 増分 | 61.3 - 1.64 = **59.7** |
| 照度 増分 | 1.69 - 1.64 = **0.05** |
| 合計 (3.28 + 59.7 + 0.05) | **63.0** |
| すべて 増分 (実測) | 65.8 - 1.64 = **64.2** |

**誤差 1.2mA = 約 2%** で加算性が成立。各センサの消費は他センサと独立に評価可能。

## 4. Battery 寿命試算

### 前提

- Eneloop Pro AA × 2 直列 (1 本 2500 mAh、直列接続なので **容量 = 2500 mAh**、電圧 = 1 本ぶんの倍 = 2.76V)
- 計測した平均電流 (= 電池から流れる電流) で除算し、満充電状態からの理論的な連続計測可能時間を出す
- 自己放電、低電圧域の DC-DC 効率低下、温度依存は未考慮 (実運用ではこれより短くなる)

### interval = 1sec (worst case = 連続計測)

| ケース | 平均電流 | 寿命 |
|--------|---------|------|
| 計測なし | 1.64 mA | 1524 h = **64 日** |
| TH/CO2/Glb のみ | 4.92 mA | 508 h = **21 日** |
| 風速のみ | 61.3 mA | 41 h = **1.7 日** |
| 照度のみ | 1.69 mA | 1479 h = **62 日** |
| すべて | 65.8 mA | 38 h = **1.6 日** |

### interval = 60sec (現実的)

- TH/CO2/Glb のみ: ~1.67 mA → **62 日** (probe duty 1.7% で baseline とほぼ同等)
- 風速のみ: ~11 mA → **9 日** (熱線 V_WAKEUP_TIME=10sec で duty 17%)
- 照度のみ: ~1.64 mA → **64 日**
- すべて: ~12 mA → **9 日**

### interval = 600sec (10 分)

- TH/CO2/Glb のみ: ~1.65 mA → **63 日**
- 風速のみ: ~2.6 mA → **40 日**
- すべて: ~2.7 mA → **39 日**

## 5. app 側 battery life 試算 (power-based)

実機計測は VBAT = 2.76V 固定で取得しているが、battery 種別 (Alkaline ~3.0V / NiMH ~2.6V) や残量によって VBAT は変動する。そのうえ M-Logger の 3.3V cascade boost は入力電圧によらず**出力 power がほぼ一定**になる (入力電流は VBAT に反比例) ため、**電流ではなく power (V × I) を係数として持ち**、現在の VBAT で割って予想電流を再計算する方法を採る。

### 係数 (試験データから)

各係数は §3.2/§3.3 の VBAT=2.76V 試験結果を power 換算したもの (mW)。

| 定数 | 値 [mW] | 説明 |
|------|---------|------|
| `P_BASELINE_MW` | **4.53** | 常時消費 (logging 中、XBee/USB/Flash 全 OFF、`P_LED_BLINK_MW` を含む) |
| `P_LED_BLINK_MW` | **2.76** | 5sec 周期 LED 点滅の寄与 (`P_BASELINE_MW` の内訳)。間引き設定を入れる場合はこの値ぶんを差し引く |
| `P_GENERAL_ACTIVE_MW` | **17** | 温湿度+CO2+グローブ温度 (th_probe) measureOnce 中の本体側 power |
| `P_VELOCITY_ACTIVE_MW` | **124** | 風速プローブ熱線 ON 中の本体側 power |
| `P_ILLUMINANCE_ACTIVE_MW` | **0.4** | OPT3001 単発 read 時の power (微小) |

active 期間の長さ (duty 計算用):

| 定数 | 値 [sec] | 説明 |
|------|----------|------|
| `T_GENERAL_ACTIVE_S` | 0.52 | th_probe `measureOnce` 1 回あたりの所要時間 |
| `T_VELOCITY_WAKEUP_S` | 10 | 風速プローブ熱線立ち上がり時間 (`V_WAKEUP_TIME`) |
| `T_ILLUMINANCE_ACTIVE_S` | 0.01 | OPT3001 read 所要 (推定、微小) |

電池側:

| 定数 | 値 | 説明 |
|------|-----|------|
| `BATTERY_MAH` | 2000 | 想定容量 (NiMH 標準、Alkaline ~2500mAh は安全側で同値扱い) |
| `SAFETY_FACTOR` | 0.8 | 試算値に掛ける安全係数 (DC-DC 効率低下や温度劣化を吸収) |

### Battery 種別判定 (新品前提)

電源投入直後 (= 計測開始前) の VBAT で判別:

| VBAT 範囲 | 判定 | 表示 |
|-----------|------|------|
| > 2.85V | Alkaline (新品 ~3.0-3.2V) | `"Alkaline"` |
| ≤ 2.85V | NiMH (満充電 ~2.7-2.9V) | `"NiMH"` |

判定値は **表示専用**で寿命試算には使わない (寿命は power / VBAT / 固定容量だけで決まる)。ユーザーが表示と異なる種別の電池を入れた場合は「新品ではない or 異常電池」のサインになるので参考情報として有用。

### 試算式 (擬似コード)

```python
def estimate_power_mW(settings, *, led_blink_enabled=True):
    p = P_BASELINE_MW
    if not led_blink_enabled:
        p -= P_LED_BLINK_MW
    if settings.general.enabled:
        duty = min(T_GENERAL_ACTIVE_S / settings.general.interval, 1.0)
        p += P_GENERAL_ACTIVE_MW * duty
    if settings.velocity.enabled:
        if settings.velocity.interval < T_VELOCITY_WAKEUP_S:
            duty = 1.0   # 熱線常時 ON
        else:
            duty = T_VELOCITY_WAKEUP_S / settings.velocity.interval
        p += P_VELOCITY_ACTIVE_MW * duty
    if settings.illuminance.enabled:
        duty = min(T_ILLUMINANCE_ACTIVE_S / settings.illuminance.interval, 1.0)
        p += P_ILLUMINANCE_ACTIVE_MW * duty
    return p

def estimate_hours(power_mW, voltage_mV):
    current_mA = power_mW / (voltage_mV / 1000.0)
    return (BATTERY_MAH * SAFETY_FACTOR) / current_mA   # = 1600 / I
```

### 試算例 (新品電池、Alkaline 3.0V / NiMH 2.6V)

| 設定 | Power [mW] | I @3.0V [mA] | 時間 @3.0V | I @2.6V [mA] | 時間 @2.6V |
|------|-----------|--------------|-----------|--------------|-----------|
| general (60sec) のみ | 4.68 | 1.56 | **43 日** | 1.80 | **37 日** |
| general (60sec) + velocity (300sec) | 13.0 | 4.32 | **15 日** | 5.00 | **13 日** |
| general (60sec) + velocity (60sec) + illuminance (60sec) | 46.0 | 15.3 | **4.3 日** | 17.7 | **3.8 日** |
| 全有効 (10分間隔) | 8.67 | 2.89 | **23 日** | 3.33 | **20 日** |

### 注記

- これらの係数は VBAT=2.76V Eneloop での実測由来。VBAT が極端に低下 (1.9V 等) すると cascade boost 効率がさらに落ちて power がやや増えるが、`SAFETY_FACTOR=0.8` で吸収される想定
- LED 間引き設定を実装する場合は `led_blink_enabled=False` で `P_LED_BLINK_MW` を差し引ける
- 試算結果はあくまで「参考値」。実運用では温度・自己放電・電池個体差で ±30% 程度の差が出る
