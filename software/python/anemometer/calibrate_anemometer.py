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
"""
import datetime
import math
import statistics
import time

import matplotlib.pyplot as plt
import numpy as np

from anemometer_manager import AnemometerManager
from quadro_fan_controller import QuadroFanController


# ==========================================
# 定数・設定値
# ==========================================
CALIBRATION_POINTS = [
    {"fan_power": 0,  "ref_velocity": 0.00},
    {"fan_power": 7,  "ref_velocity": 0.12},
    {"fan_power": 12, "ref_velocity": 0.41},
    {"fan_power": 40, "ref_velocity": 2.50},
    {"fan_power": 74, "ref_velocity": 5.00},
]

VALIDATION_POINTS = [
    {"fan_power": 0,  "ref_velocity": 0.00},
    {"fan_power": 7,  "ref_velocity": 0.12},
    {"fan_power": 10, "ref_velocity": 0.30},
    {"fan_power": 12, "ref_velocity": 0.41},
    {"fan_power": 21, "ref_velocity": 1.06},
    {"fan_power": 40, "ref_velocity": 2.50},
    {"fan_power": 74, "ref_velocity": 5.00},
]

SLAVE_ADDRESS = 0x10
MEASUREMENT_DURATION = 20            # 1 条件あたりの計測時間 [s]
STABILIZATION_TIME   = 10            # 風洞とセンサの安定までの待機 [s]
ANALYSIS_WINDOW      = MEASUREMENT_DURATION - STABILIZATION_TIME
SPECIAL_STABILIZATION_TIME = 15      # ファン起動直後の突風安定化追加待機 [s]
SAMPLING_INTERVAL    = 0.1           # センサ読み取り間隔 [s]
FILTER_N             = 6             # EWMA フィルタ係数 (0~20)


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
    prev_power = -1

    try:
        print("=== Phase 1: Data Collection Started ===")

        for point in CALIBRATION_POINTS:
            target_power = point["fan_power"]
            ref_vel      = point["ref_velocity"]

            print(f"\n[Step] Target: {ref_vel} m/s (Fan: {target_power}%)")
            fan.set_power(target_power)

            if prev_power == 0 and target_power != 0:
                print(f"Waiting {SPECIAL_STABILIZATION_TIME}s for pulse stabilization...")
                time.sleep(SPECIAL_STABILIZATION_TIME)

            print(f"Measuring for {MEASUREMENT_DURATION}s "
                  f"(averaging last {ANALYSIS_WINDOW}s)...")
            start = time.time()
            buf_v = []

            while True:
                elapsed = time.time() - start
                if elapsed > MEASUREMENT_DURATION:
                    break
                volt = sensor.get_voltage()
                if volt is not None and elapsed > (MEASUREMENT_DURATION - ANALYSIS_WINDOW):
                    buf_v.append(volt)
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

            prev_power = target_power

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
    prev_power = -1
    results = []

    try:
        for point in VALIDATION_POINTS:
            target = point["fan_power"]
            ref_v  = point["ref_velocity"]

            print(f"\n[Validation] Fan: {target}% (Ref: {ref_v} m/s)")
            fan.set_power(target)

            if prev_power == 0 and target != 0:
                print(f"Waiting {SPECIAL_STABILIZATION_TIME}s for pulse stabilization...")
                time.sleep(SPECIAL_STABILIZATION_TIME)

            print(f"Waiting {STABILIZATION_TIME}s for stabilization...")
            time.sleep(STABILIZATION_TIME)

            vels, volts = [], []
            for _ in range(20):
                vel = sensor.get_velocity()
                vol = sensor.get_voltage()
                if vel is not None:
                    vels.append(vel)
                if vol is not None:
                    volts.append(vol)
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

            prev_power = target

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
# 結果可視化 + レポート出力
# ==========================================

def generate_calibration_plot(device_id, phase1_data, coef_a, coef_b, phase3_data):
    """King の法則 (3 区分) 近似曲線と実測点を 1 枚にまとめた PNG を保存。"""
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
        vol_curve.append(np.sqrt(e_sq))  # V

    ref_v  = [r['ref_velocity'] for r in phase1_data]
    meas_v = [r['measured_avg'] for r in phase1_data]

    verify_vel = [r['ref'] for r in phase3_data]
    verify_vol = [r['measuredV'] for r in phase3_data]

    plt.figure(figsize=(8, 5))
    plt.plot(v_curve, vol_curve, 'r-', label="King's Law Fit", alpha=0.7)
    plt.scatter(ref_v, meas_v, color='blue', label='Reference Points', zorder=5)
    plt.scatter(verify_vel, verify_vol, color='green', marker='x', s=80,
                linewidths=2, label='Verification Points', zorder=5)
    plt.xlabel('Air Velocity [m/s]')
    plt.ylabel('Sensor Voltage [V]')
    plt.title(f'Calibration Curve (Device ID: 0x{device_id:06X})')
    plt.grid(True, linestyle='--', alpha=0.6)
    plt.legend()

    fname = f"plot_{device_id:06X}.png"
    plt.savefig(fname, dpi=150)
    plt.close()
    return fname


def save_calibration_report(device_info, phase1_data, coef_a, coef_b,
                            phase3_data, img_filename):
    device_id = device_info['device_id']
    name      = device_info['name']

    ts       = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = f"calib_{device_id:06X}_{ts}.md"

    with open(filename, "w", encoding="utf-8") as f:
        f.write("# Anemometer Calibration Report\n\n")
        f.write("## 1. Device Information\n")
        f.write(f"- **Device ID (FNV-1a 22bit)**: `0x{device_id:06X}` ({device_id})\n")
        f.write(f"- **Device Name**: `{name}`\n")
        f.write(f"- **Calibration Date**: "
                f"{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")

        f.write("## 2. Phase 1: Reference Measurements\n")
        f.write("| Fan Power | Ref Velocity [m/s] | Measured Voltage [V] | StdDev [V] |\n")
        f.write("|---|---|---|---|\n")
        for r in phase1_data:
            f.write(f"| {r['fan_power']}% | {r['ref_velocity']:.2f} | "
                    f"{r['measured_avg']:.4f} | {r['std_dev']:.4f} |\n")
        f.write("\n")

        f.write("## 3. Phase 2: Estimated Coefficients (King's Law, 3-range)\n")
        f.write("Formula: $v = e^{m \\cdot \\ln(E^2 - E_0^2) + \\ln(C)}$\n\n")
        f.write(f"- **Common $E_0$ (Zero-wind)**: `{coef_a[0]:.6f} V`\n")
        f.write(f"### Range 1 (Low, v < {coef_b[2]:.2f} m/s)\n")
        f.write(f"- **Slope ($m_1$)**: `{coef_a[1]:.6e}`\n")
        f.write(f"- **Intercept ($\\ln(C_1)$)**: `{coef_a[2]:.6e}`\n")
        f.write(f"### Range 2 (Mid, {coef_b[2]:.2f} <= v < {coef_b[3]:.2f} m/s)\n")
        f.write(f"- **Slope ($m_2$)**: `{coef_a[3]:.6e}`\n")
        f.write(f"- **Intercept ($\\ln(C_2)$)**: `{coef_a[4]:.6e}`\n")
        f.write(f"### Range 3 (High, v >= {coef_b[3]:.2f} m/s)\n")
        f.write(f"- **Slope ($m_3$)**: `{coef_b[0]:.6e}`\n")
        f.write(f"- **Intercept ($\\ln(C_3)$)**: `{coef_b[1]:.6e}`\n\n")

        f.write("## 4. Phase 3: Verification Results\n")
        f.write("| Ref Velocity [m/s] | Measured Velocity [m/s] | Error [%] | "
                "Measured Voltage [V] |\n")
        f.write("|---|---|---|---|\n")
        for r in phase3_data:
            f.write(f"| {r['ref']:.2f} | {r['measured']:.3f} | "
                    f"{r['error']:.1f}% | {r['measuredV']:.4f} |\n")
        max_err = max(r['error'] for r in phase3_data)
        f.write(f"\n- **Maximum Verification Error**: `{max_err:.1f}%`\n\n")

        f.write("## 5. Calibration Visualization\n")
        f.write(f"![Calibration Curve](./{img_filename})\n\n")

    print(f"\nReport generated: {filename}")


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

    fname = generate_calibration_plot(device_info["device_id"],
                                      data1, coef_a, coef_b, data3)
    save_calibration_report(device_info, data1, coef_a, coef_b, data3, fname)
