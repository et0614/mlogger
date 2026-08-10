"""
風速計子機 (poem_velocity_sensor / OSL 規格) の風洞校正スクリプト。

Phase 1: 既知風速 5 点で生電圧を実測
Phase 2: King の法則 ( v = C * (E^2 - E0^2)^m ) で 3 領域フィッティングし
         子機 EXTENSION 領域 (coef A / coef B) に書き込み
Phase 3: 7 点で再現精度を検証
+ グラフ画像と Markdown レポートを出力

旧版との違い:
- 電圧は内部・出力ともに [V] に統一 (旧 firmware は mV だったが OSL は float V)
- デバイス識別は OSL device_id (FNV-1a 22bit) と name に簡素化
  (旧版の UUID / firmware version / MCU 温度は OSL に存在しない)
- AnemometerManager.set_coefficients_a / _b (lowercase) を使用
- 出力は e-sensor 互換の JSON 1 ファイル (PNG は base64 で同梱)。
  保存先は software/web/calibration/reports/<6hex>.json。
  Web (software/web/calibration/index+viewer.html) から fetch される。
"""
import base64
import datetime
import io
import json
import math
import os
import statistics
import time

import matplotlib.pyplot as plt
import numpy as np

from anemometer_manager import AnemometerManager, AnemometerRegisters
from quadro_fan_controller import QuadroFanController


# ==========================================
# 定数・設定値
# ==========================================
# 使用する風洞(Calibrator)を 1 または 2 で指定する。風洞ごとにファン power と
# 基準風速の対応が異なるため、ここを切り替えるだけで校正・検証の両方の点群が
# 一括で差し替わる。トレーサビリティ用に JSON へも記録する。
CALIBRATOR_ID = 1

CALIBRATOR_PROFILES = {
    1: {
        "calibration_points": [
            {"fan_power": 0,  "ref_velocity": 0.00},
            {"fan_power": 8,  "ref_velocity": 0.23},
            {"fan_power": 12, "ref_velocity": 0.52},
            {"fan_power": 40, "ref_velocity": 2.76},
            {"fan_power": 69, "ref_velocity": 5.00},
        ],
        "validation_points": [
            {"fan_power": 0,  "ref_velocity": 0.00},   # 再現性（下端）
            {"fan_power": 10, "ref_velocity": 0.37},   # 補間 Range A
            {"fan_power": 21, "ref_velocity": 1.24},   # 補間 Range B
            {"fan_power": 53, "ref_velocity": 3.75},   # 補間 Range C
            {"fan_power": 69, "ref_velocity": 5.00},   # 再現性（上端）
        ],
    },
    #2: {
    #    "calibration_points": [
    #        {"fan_power": 0,  "ref_velocity": 0.00},
    #        {"fan_power": 8,  "ref_velocity": 0.23},
    #        {"fan_power": 12, "ref_velocity": 0.50},
    #        {"fan_power": 38, "ref_velocity": 2.73},
    #        {"fan_power": 67, "ref_velocity": 5.03},
    #    ],
    #    "validation_points": [
    #        {"fan_power": 0,  "ref_velocity": 0.00},   # 再現性（下端）
    #        {"fan_power": 10, "ref_velocity": 0.32},   # 補間 Range A
    #        {"fan_power": 19, "ref_velocity": 1.09},   # 補間 Range B
    #        {"fan_power": 51, "ref_velocity": 3.80},   # 補間 Range C
    # b       {"fan_power": 67, "ref_velocity": 5.03},   # 再現性（上端）
    #    ],
    #},
}

if CALIBRATOR_ID not in CALIBRATOR_PROFILES:
    raise ValueError(
        f"CALIBRATOR_ID={CALIBRATOR_ID} は未定義です。"
        f"利用可能: {sorted(CALIBRATOR_PROFILES.keys())}"
    )

CALIBRATION_POINTS = CALIBRATOR_PROFILES[CALIBRATOR_ID]["calibration_points"]
VALIDATION_POINTS  = CALIBRATOR_PROFILES[CALIBRATOR_ID]["validation_points"]

SLAVE_ADDRESS = 0x10

SAMPLING_INTERVAL    = 0.1           # センサ読み取り間隔 [s]
FILTER_N             = 6             # EWMA フィルタ係数 (0~20)

# --- 計測窓（各点で平均を取る時間）。安定化待機の後にこの秒数だけ計測する。
# 高風速は std が小さいので短く、低風速・無風は環境ノイズが大きいので長めに平均する
# （校正のみ風速依存。検証は既に短いので 5s 一律）。
CAL_MEAS_HIGH_WIND       = 5     # 校正: 高風速(>= MEAS_HIGH_WIND_THRESHOLD)の計測窓 [s]
CAL_MEAS_LOW_WIND        = 10    # 校正: 低風速・無風の計測窓 [s]
MEAS_HIGH_WIND_THRESHOLD = 2.0   # これ以上を「高風速(低ノイズ)」とみなす [m/s]
VAL_MEASUREMENT_DURATION = 5     # 検証: 一律

# --- 安定化待機は風速帯ごとに設定（降順計測前提）。
# 高風速は数秒で整定するので短く、低風速ほど熱整定が緩慢（特に 0 m/s は無風で
# 緩慢な尾を引く）ため長く取る。降順計測でファンを0から起動しないため、起動キックの
# 待機は不要。
# ※ここの秒数は E-Sensor の風速計で実測した整定時間（降順 stab_profile）に個体差の
#   ゆとりを載せた「起点値」。本子機(OSL)の熱時定数は異なりうるので、初回は降順で
#   各点の整定を 1 秒毎に記録して検証・調整すること。
#   E-Sensor 実測: 高風速≈8s / 0.4帯≈15-20s(前段からの大ステップで最遅) / 0.2帯≈12s /
#   0 m/s ≈25s（20sでまだ+13mV, 25sで+4.5mV）。
# (ref_v 下限[m/s], 安定化[s]) を大きい順に並べ、最初に該当した帯を採用する。
STAB_BANDS = [
    (2.0, 10),   # 高風速
    (0.8, 15),   # ~1 m/s 帯
    (0.1, 20),   # 低風速 (0.1〜0.8)
    (0.0, 25),   # 0 m/s (無風の緩慢な整定)
]

# 0 m/s 電圧がこの値[V]を下回ったら異常（回路未起動/断線等）とみなし警告する。
# ※本子機の無風時電圧に合わせて調整すること（E-Sensor は無風 ~0.2V で 0.1V 判定）。
ABNORMAL_NO_WIND_VOLTAGE = 0.05

# 検証フェーズの最大誤差がこの値[%]以下なら合格（JSON の "pass"）。0 m/s は対象外。
VERIFY_ERROR_THRESHOLD_PCT = 10.0


def stabilization_time(ref_v):
    """風速帯(STAB_BANDS)から安定化待機時間[s]を返す（降順計測前提・個体差のゆとり込み）。"""
    for vmin, t in STAB_BANDS:
        if ref_v >= vmin:
            return t
    return STAB_BANDS[-1][1]


def measurement_duration(ref_v):
    """校正の計測窓[s]。高風速は低ノイズなので短く、低風速・無風は長めに平均する。"""
    return CAL_MEAS_HIGH_WIND if ref_v >= MEAS_HIGH_WIND_THRESHOLD else CAL_MEAS_LOW_WIND


# ==========================================
# 通知（ビープ）
# ==========================================

def notify_done():
    """校正完了をビープ音で通知する（Windows: winsound、他: BEL 文字）。"""
    try:
        import winsound
        winsound.Beep(880, 200)
        winsound.Beep(1320, 300)
    except Exception:
        print('\a', end='', flush=True)


def notify_abnormal():
    """異常検知時の警告ビープ。下降音 × 3 回で注意を引く。"""
    try:
        import winsound
        for _ in range(3):
            winsound.Beep(1500, 180)
            winsound.Beep(700, 220)
    except Exception:
        for _ in range(3):
            print('\a', end='', flush=True)
            time.sleep(0.15)


# ==========================================
# Phase 0: 初期化 + デバイス情報取得
# ==========================================

def init_sensor():
    sensor = AnemometerManager(slave_addr=SLAVE_ADDRESS)
    sensor.open()
    if not sensor.is_open:
        print("Failed to open Anemometer device.")
        return None
    try:
        sensor.set_enable(True)
        sensor.set_filter_n(FILTER_N)
        info = {
            "device_id": sensor.get_device_id(),
            "name":      sensor.get_name(),
            "data_count": sensor.get_data_count(),
        }
    finally:
        sensor.close()
    return info


# ==========================================
# Phase 1: リファレンス風速に対する生電圧の実測
# ==========================================

def run_phase_1():
    sensor = AnemometerManager(slave_addr=SLAVE_ADDRESS)
    fan    = QuadroFanController()

    sensor.open()
    if not sensor.is_open:
        print("Failed to open Anemometer device.")
        return None

    results = []

    try:
        print("=== Phase 1: Data Collection Started ===")

        # 降順（高風速→低風速、最後に 0 m/s）で計測する。理由:
        #  - ファンを0から起動しないので、起動キックの気流スパイクを回避できる。
        #  - 起動直後のサージが τ の小さい高風速点で速く収まる（0 m/s では緩慢）。
        #  - 遅い整定が必要なのは最後の 0 m/s だけになる。
        # フィット(Phase 2)は昇順(index0=0 m/s)前提なので、計測後に並べ替える。
        for point in sorted(CALIBRATION_POINTS,
                            key=lambda p: p["ref_velocity"], reverse=True):
            target_power = point["fan_power"]
            ref_vel      = point["ref_velocity"]

            print(f"\n[Step] Target: {ref_vel} m/s (Fan: {target_power}%)")
            fan.set_power(target_power)

            wait_time = stabilization_time(ref_vel)
            print(f"Waiting {wait_time}s for stabilization...")
            time.sleep(wait_time)

            meas_dur = measurement_duration(ref_vel)
            print(f"Measuring for {meas_dur}s...")
            start = time.time()
            buf_v = []
            while time.time() - start < meas_dur:
                # status1 の該当ビットが立っていない(=有効)サンプルだけ採用する。
                # 予熱中や通信失敗の stale 値を平均に混ぜない。
                poll = sensor.read_poll_block()
                if poll is not None and not (
                        poll["status1"] & (1 << AnemometerRegisters.VAL_IDX_VOLTAGE)):
                    buf_v.append(poll["values"][AnemometerRegisters.VAL_IDX_VOLTAGE])
                time.sleep(SAMPLING_INTERVAL)

            if buf_v:
                avg_v = statistics.mean(buf_v)
                std_v = statistics.stdev(buf_v) if len(buf_v) > 1 else 0.0
                print(f"Result: Avg = {avg_v:.4f} V, StdDev = {std_v:.4f} V")
                results.append({
                    "fan_power":     target_power,
                    "ref_velocity":  ref_vel,
                    "measured_avg":  avg_v,    # V
                    "std_dev":       std_v,    # V
                })
            else:
                print("Warning: No sensor data could be collected.")

        # 降順で計測したので、フィット・出力のため風速昇順へ並べ替える
        # （e0 = results[0] が 0 m/s、レンジも昇順であることを担保する）。
        results.sort(key=lambda r: r["ref_velocity"])

        # 0 m/s 電圧が異常に低くないか（回路未起動/断線の検知）
        zero = next((r for r in results if r["ref_velocity"] == 0.0), None)
        if zero is not None and zero["measured_avg"] < ABNORMAL_NO_WIND_VOLTAGE:
            fan.set_power(0)
            notify_abnormal()
            print("\n" + "!" * 60)
            print(f"!! WARNING: 0 m/s 電圧が異常に低い: {zero['measured_avg']*1000:.1f} mV "
                  f"(基準 > {ABNORMAL_NO_WIND_VOLTAGE*1000:.0f} mV)")
            print("!! 風速計回路の不具合（未起動/断線等）が疑われます。中止を推奨。")
            print("!" * 60)
            ans = input("続行するには 'yes' と入力 / それ以外で中止: ").strip().lower()
            if ans != "yes":
                print("校正を中止しました。デバイスを確認のうえ再実行してください。")
                return None

        print("\n" + "=" * 55)
        print("Phase 1: Measurement Summary")
        print("=" * 55)
        print(f"{'Fan %':>6} | {'Ref(m/s)':>10} | {'Measured(V)':>13} | {'StdDev(V)':>10}")
        print("-" * 55)
        for r in results:
            print(f"{r['fan_power']:>6}% | {r['ref_velocity']:>10.2f} | "
                  f"{r['measured_avg']:>13.4f} | {r['std_dev']:>10.4f}")
        return results

    except KeyboardInterrupt:
        print("\nInterrupted by user.")
        return None
    finally:
        print("\nShutting down phase 1 devices...")
        fan.set_power(0)
        sensor.close()


# ==========================================
# Phase 2: 係数計算 + 子機に書き込み
# ==========================================

def calculate_kings_law_params(v1, e1, v2, e2, e0):
    """
    2 点 (v1,e1), (v2,e2) と無風時電圧 e0 から King の法則の (C, m) を返す。
    v: 風速 [m/s], e/e0: 電圧 [V]
    """
    x1 = math.log(max(1e-6, e1 ** 2 - e0 ** 2))
    y1 = math.log(v1)
    x2 = math.log(max(1e-6, e2 ** 2 - e0 ** 2))
    y2 = math.log(v2)
    m    = (y2 - y1) / (x2 - x1)
    ln_c = y1 - m * x1
    return math.exp(ln_c), m


def run_phase_2(measurement_results):
    """King の法則による 3 領域フィッティングと EXTENSION への書き込み。
    校正点 5 点 (v0..v4) から 3 区間 (v1-v2, v2-v3, v3-v4) の (m, lnC) を導出。
    切替点は v_split1=v_speeds[2], v_split2=v_speeds[3]。
    """
    print("\n=== Phase 2: King's Law Calibration Fitting (3-range) ===")

    e_volts  = [r["measured_avg"] for r in measurement_results]   # 既に V
    v_speeds = [r["ref_velocity"] for r in measurement_results]

    e0 = e_volts[0]
    print(f"Zero-wind Voltage (E0): {e0:.4f} V")

    c1, m1 = calculate_kings_law_params(v_speeds[1], e_volts[1],
                                        v_speeds[2], e_volts[2], e0)
    c2, m2 = calculate_kings_law_params(v_speeds[2], e_volts[2],
                                        v_speeds[3], e_volts[3], e0)
    c3, m3 = calculate_kings_law_params(v_speeds[3], e_volts[3],
                                        v_speeds[4], e_volts[4], e0)

    print(f"\n[Range 1 (Low)]  C: {c1:.6e}, m: {m1:.6e}")
    print(f"[Range 2 (Mid)]  C: {c2:.6e}, m: {m2:.6e}")
    print(f"[Range 3 (High)] C: {c3:.6e}, m: {m3:.6e}")

    # firmware (poem_velocity_sensor.X) updateVelocity の係数配置に合わせる:
    #   coA = [E0, m1, lnC1, m2, lnC2]
    #   coB = [m3, lnC3, v_split1, v_split2, _]
    coef_a = [
        float(e0),
        float(m1), float(math.log(c1)),
        float(m2), float(math.log(c2)),
    ]
    coef_b = [
        float(m3), float(math.log(c3)),
        float(v_speeds[2]), float(v_speeds[3]),
        0.0,
    ]

    sensor = AnemometerManager(slave_addr=SLAVE_ADDRESS)
    sensor.open()
    if not sensor.is_open:
        print("Error: Could not open device for coefficient write.")
        return coef_a, coef_b

    try:
        print("\nWriting King's Law parameters to device...")
        ok = sensor.set_coefficients_a(coef_a) and sensor.set_coefficients_b(coef_b)
        if ok:
            print("Update successful: Range A and Range B coefficients stored.")
            v_a = sensor.get_coefficients_a()
            v_b = sensor.get_coefficients_b()
            print(f"Verified A: {v_a}")
            print(f"Verified B: {v_b}")
        else:
            print("Error: Failed to write coefficients.")
    finally:
        sensor.close()

    return coef_a, coef_b


# ==========================================
# Phase 3: 補正後風速の検証
# ==========================================

def run_phase_3():
    sensor = AnemometerManager(slave_addr=SLAVE_ADDRESS)
    fan    = QuadroFanController()

    sensor.open()
    if not sensor.is_open:
        print("Failed to open device.")
        return None

    print("\n=== Phase 3: Calibration Verification Started ===")
    results = []

    try:
        # 検証も校正と同じ降順で計測（ファン起動キック回避・整定のため）。
        for point in sorted(VALIDATION_POINTS,
                            key=lambda p: p["ref_velocity"], reverse=True):
            target = point["fan_power"]
            ref_v  = point["ref_velocity"]

            print(f"\n[Validation] Fan: {target}% (Ref: {ref_v} m/s)")
            fan.set_power(target)

            wait_time = stabilization_time(ref_v)
            print(f"Waiting {wait_time}s for stabilization...")
            time.sleep(wait_time)

            vels, volts = [], []
            start = time.time()
            while time.time() - start < VAL_MEASUREMENT_DURATION:
                # velocity / voltage それぞれ status1 の有効ビットを確認して採用。
                poll = sensor.read_poll_block()
                if poll is not None:
                    s1 = poll["status1"]
                    vv = poll["values"]
                    if not (s1 & (1 << AnemometerRegisters.VAL_IDX_VELOCITY)):
                        vels.append(vv[AnemometerRegisters.VAL_IDX_VELOCITY])
                    if not (s1 & (1 << AnemometerRegisters.VAL_IDX_VOLTAGE)):
                        volts.append(vv[AnemometerRegisters.VAL_IDX_VOLTAGE])
                time.sleep(0.5)

            if vels and volts:
                avg_vel  = statistics.mean(vels)
                avg_volt = statistics.mean(volts)
                err_pct  = (abs(avg_vel - ref_v) / ref_v * 100) if ref_v > 0 else 0.0
                print(f"Measured: {avg_vel:.3f} m/s "
                      f"(Error: {err_pct:.1f}%)  {avg_volt:.4f} V")
                results.append({
                    "ref":       ref_v,
                    "measuredV": avg_volt,      # V
                    "measured":  avg_vel,       # m/s
                    "error":     err_pct,
                })

        # 降順計測したので、出力(JSON/プロット)のため風速昇順へ並べ替える。
        results.sort(key=lambda r: r["ref"])

        print("\n" + "=" * 65)
        print("Final Accuracy Report")
        print("=" * 65)
        print(f"{'Ref(m/s)':>10} | {'Measured(m/s)':>15} | "
              f"{'Error(%)':>10} | {'Volt(V)':>10}")
        for r in results:
            print(f"{r['ref']:>10.2f} | {r['measured']:>15.3f} | "
                  f"{r['error']:>9.1f}% | {r['measuredV']:>10.4f}")

    finally:
        fan.set_power(0)
        sensor.close()

    return results


# ==========================================
# 結果出力 (e-sensor 互換 JSON; PNG は base64 同梱)
# ==========================================

# 成績の保存先 (スクリプトと同階層の reports/)。ファイル名は <6桁hex device_id>.json。
# 公開サイト (Drive 側 web/velocity_calibration/reports) への配置は手動で行う。
REPORTS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "reports")


def build_plot(coef_a, coef_b, phase1_data, phase3_data):
    """King の法則 (3 区分) 曲線 + 実測点を描いた matplotlib Figure を返す (クローズしない)。
    呼び出し側で base64 化・PNG 保存・表示に使い、最後に plt.close(fig) すること。"""
    e0 = coef_a[0]
    m1, ln_c1 = coef_a[1], coef_a[2]
    m2, ln_c2 = coef_a[3], coef_a[4]
    m3, ln_c3 = coef_b[0], coef_b[1]
    v_split1  = coef_b[2]
    v_split2  = coef_b[3]

    v_curve   = np.linspace(0.01, 5.5, 200)
    vol_curve = []
    for v in v_curve:
        if v < v_split1:
            m, ln_c = m1, ln_c1
        elif v < v_split2:
            m, ln_c = m2, ln_c2
        else:
            m, ln_c = m3, ln_c3
        e_sq = e0 ** 2 + np.exp((np.log(v) - ln_c) / m)
        vol_curve.append(np.sqrt(e_sq) * 1000)  # mV (e-sensor 流のグラフ軸単位)

    ref_v  = [r['ref_velocity'] for r in phase1_data]
    meas_v = [r['measured_avg'] * 1000 for r in phase1_data]   # mV

    verify_vel = [r['ref']                  for r in phase3_data]
    verify_vol = [r['measuredV'] * 1000     for r in phase3_data]  # mV

    fig = plt.figure(figsize=(8, 5))
    plt.plot(v_curve, vol_curve, 'r-', label="King's Law Fit", alpha=0.7)
    plt.scatter(ref_v, meas_v, color='blue',
                label='Reference Points', zorder=5)
    plt.scatter(verify_vel, verify_vol, color='green', marker='x', s=80,
                linewidths=2, label='Verification Points', zorder=5)
    plt.xlabel('Air Velocity [m/s]')
    plt.ylabel('Sensor Voltage [mV]')
    plt.grid(True, linestyle='--', alpha=0.6)
    plt.legend()

    return fig


def build_anemometer_doc(coef_a, coef_b, phase1_data, phase3_data, png_b64,
                         device_info=None, calibrator_id=None):
    """e-sensor schema 互換の anemometer ノードを構築。
    calibrator_id: 使用した風洞 ID。None なら本モジュールの CALIBRATOR_ID
    (単体 CLI 実行時)。GUI (複数風洞) からは風洞ごとの値を渡す。"""
    e0 = float(coef_a[0])
    # 検証の最大誤差と合否（0 m/s は誤差計算対象外）
    errs = [float(r["error"]) for r in phase3_data if r["ref"] > 0]
    max_err = max(errs) if errs else 0.0
    return {
        "calibrated_at": datetime.datetime.now().astimezone().isoformat(timespec="seconds"),
        "calibrator_id": CALIBRATOR_ID if calibrator_id is None else calibrator_id,
        # 装置ラベル (トレーサビリティ用)。本子機は温湿度を持たないため、元 e-sensor 版の
        # ambient_temp/humidity の代わりに device name を残す。
        "name":          (device_info or {}).get("name"),
        "model":         "kings_law_3range",
        "E0":            round(e0, 6),
        "E0_mV":         round(e0 * 1000, 1),
        "ranges": [
            {"v_min": 0.0,
             "v_max": round(float(coef_b[2]), 4),
             "m":     round(float(coef_a[1]), 4),
             "lnC":   round(float(coef_a[2]), 4)},
            {"v_min": round(float(coef_b[2]), 4),
             "v_max": round(float(coef_b[3]), 4),
             "m":     round(float(coef_a[3]), 4),
             "lnC":   round(float(coef_a[4]), 4)},
            {"v_min": round(float(coef_b[3]), 4),
             "v_max": None,
             "m":     round(float(coef_b[0]), 4),
             "lnC":   round(float(coef_b[1]), 4)},
        ],
        # フィットに用いた実測点（電圧とばらつき）。std は大きいとセンサ/気流の
        # 不安定を示す（低風速では環境ノイズで大きめになりやすい）。
        "calibration_points": [
            {
                "ref_velocity": round(float(r["ref_velocity"]), 2),
                "voltage_mV":   round(float(r["measured_avg"]) * 1000, 1),
                "std_dev_mV":   round(float(r["std_dev"]) * 1000, 1),
            }
            for r in phase1_data
        ],
        "verification": [
            {
                "ref_velocity":      round(float(r['ref']),       2),
                "measured_velocity": round(float(r['measured']),  3),
                "error_pct":         round(float(r['error']),     1),
                "voltage_mV":        round(float(r['measuredV'] * 1000), 1),
            }
            for r in phase3_data
        ],
        "max_error_pct": round(float(max_err), 1),
        "pass":          bool(max_err <= VERIFY_ERROR_THRESHOLD_PCT),
        "plot": {
            "format":   "image/png",
            "data_url": f"data:image/png;base64,{png_b64}",
        },
    }


def save_calibration_report(device_info, phase1_data, coef_a, coef_b, phase3_data,
                            show_plot=True, calibrator_id=None):
    """JSON 1 ファイルに集約して reports/ に書き出し (既存ファイルがあれば merge)。
    確認用に PNG も同ディレクトリへ保存し、show_plot=True ならグラフを表示する。"""
    fig = build_plot(coef_a, coef_b, phase1_data, phase3_data)
    buf = io.BytesIO()
    fig.savefig(buf, format='png', dpi=120)
    buf.seek(0)
    png_b64 = base64.b64encode(buf.read()).decode('ascii')

    anemo = build_anemometer_doc(coef_a, coef_b, phase1_data, phase3_data, png_b64,
                                 device_info, calibrator_id=calibrator_id)

    device_id_hex = f"{device_info['device_id']:06X}"
    os.makedirs(REPORTS_DIR, exist_ok=True)
    out_path = os.path.join(REPORTS_DIR, f"{device_id_hex}.json")

    # 既存 JSON があれば merge (将来 CO2 等の別校正を併存させる余地)
    if os.path.exists(out_path):
        with open(out_path, "r", encoding="utf-8") as f:
            doc = json.load(f)
        doc.setdefault("calibrations", {})
        doc["calibrations"]["anemometer"] = anemo
    else:
        doc = {
            "schema_version": 1,
            "device_id":      device_id_hex,
            "calibrations":   {"anemometer": anemo},
        }

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)

    print(f"\nReport written: {out_path}")

    # ローカル確認用に PNG も保存 (JSON には base64 で同梱済み)。
    png_path = os.path.join(REPORTS_DIR, f"{device_id_hex}.png")
    fig.savefig(png_path, dpi=120)
    print(f"Plot saved    : {png_path}")

    notify_done()

    # 確認用にグラフを表示 (ウィンドウを閉じるまでブロック)。一括実行時は show_plot=False。
    if show_plot:
        print("\nClose the plot window to exit.")
        plt.show()
    plt.close(fig)


# ==========================================
# エントリーポイント
# ==========================================

if __name__ == "__main__":
    device_info = init_sensor()
    if not device_info:
        exit(1)
    print(f"Device ID: 0x{device_info['device_id']:06X}  "
          f"Name: {device_info['name']!r}  "
          f"DataCount: {device_info['data_count']}")

    data1 = run_phase_1()
    if not data1:
        exit(1)

    coef_a, coef_b = run_phase_2(data1)

    data3 = run_phase_3()
    if not data3:
        exit(1)

    save_calibration_report(device_info, data1, coef_a, coef_b, data3)
