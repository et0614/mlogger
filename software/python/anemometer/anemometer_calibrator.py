"""
GUI (calibration_gui.py) 用の class ベース風速校正器。

calibrate_anemometer.py の単体実行パイプライン (Phase 1-3) を、複数風洞から
並行利用できる形にしたもの:
  - CP2112 アダプタを HID path で指定して開く (風洞 ↔ 物理ポート固定)
  - QuadroFan の fan_index を風洞ごとに指定 (liquidctl 呼び出しは直列化)
  - on_progress / on_abnormal / should_cancel コールバックで GUI と連携
  - フィット式・判定閾値・レポート生成は calibrate_anemometer の関数/定数を再利用
    (校正方法の変更は calibrate_anemometer.py 側だけで済む)

単体 CLI 実行は従来どおり calibrate_anemometer.py を使う。
"""
import math
import statistics
import threading
import time

import calibrate_anemometer as ca
from anemometer_manager import AnemometerManager, AnemometerRegisters
from quadro_fan_controller import QuadroFanController

# liquidctl は同一 Quadro を全風洞で共有するため、並行呼び出しを直列化する
_fan_lock = threading.Lock()

# matplotlib pyplot はグローバル状態を持つため、figure 生成〜保存を直列化する
_plot_lock = threading.Lock()


class _Cancelled(Exception):
    pass


class AnemometerCalibrator:
    """1 風洞 1 プローブぶんの校正実行器。ワーカースレッドから run_calibration を呼ぶ。

    結果:
      run_calibration() が True で成功 (レポート保存済み)。
      False のときは .wrong_device (取り違え) と .error (理由文字列) を確認する。
    """

    def __init__(self, cp2112_path, fan_index,
                 calibration_points, validation_points, calibrator_id,
                 expected_device_id=None,
                 on_progress=None, on_abnormal=None, should_cancel=None,
                 slave_addr=AnemometerRegisters.DEFAULT_I2C_ADDR):
        """
        cp2112_path        : 対象 CP2112 アダプタの HID パス
        fan_index          : QuadroFan のチャネル (1-4)
        calibration_points : [{"fan_power", "ref_velocity"}] x5 (King 3 領域フィット前提)
        validation_points  : 検証点
        calibrator_id      : 風洞 ID (JSON にトレーサビリティ記録)
        expected_device_id : 6桁hex。接続個体と不一致なら取り違えとして中止
        on_progress(msg, frac) : 進捗通知 (frac は 0..1)
        on_abnormal(voltage_v) -> bool : 0 m/s 異常電圧時に続行可否を尋ねる
        should_cancel() -> bool        : True を返すと安全な区切りで中断
        """
        if len(calibration_points) != 5:
            raise ValueError("calibration_points must have exactly 5 points (3-range fit)")
        self.cp2112_path = cp2112_path
        self.fan_index = fan_index
        self.calibration_points = calibration_points
        self.validation_points = validation_points
        self.calibrator_id = calibrator_id
        self.expected_device_id = expected_device_id
        self.on_progress = on_progress or (lambda msg, frac=None: None)
        self.on_abnormal = on_abnormal or (lambda voltage: False)
        self.should_cancel = should_cancel or (lambda: False)
        self.slave_addr = slave_addr
        self.fan = QuadroFanController()
        self.wrong_device = False
        self.error = None
        # 進捗: 校正点 + フィット/書込 + 検証点 で等分
        self._total_steps = len(calibration_points) + 1 + len(validation_points)
        self._step = 0

    # ---------------- 内部ユーティリティ ----------------
    def _progress(self, msg, bump=False):
        if bump:
            self._step += 1
        self.on_progress(msg, self._step / self._total_steps)

    def _check_cancel(self):
        if self.should_cancel():
            raise _Cancelled()

    def _sleep(self, sec):
        """中断に反応できる sleep (0.2s 刻み)。"""
        end = time.time() + sec
        while True:
            remain = end - time.time()
            if remain <= 0:
                return
            self._check_cancel()
            time.sleep(min(0.2, remain))

    def _set_fan(self, power):
        with _fan_lock:
            self.fan.set_power(power, self.fan_index)

    def _open_sensor(self, attempts=3):
        """CP2112 を開く。GUI の監視ポーリングと open が競合し得るためリトライする。"""
        for i in range(attempts):
            sensor = AnemometerManager(slave_addr=self.slave_addr, path=self.cp2112_path)
            if sensor.open():
                return sensor
            time.sleep(0.5)
        return None

    # ---------------- 校正本体 ----------------
    def run_calibration(self, show_plot=False):
        sensor = self._open_sensor()
        if sensor is None:
            self.error = 'CP2112 を開けません (監視と競合/抜線?)'
            return False
        try:
            # 個体確認 (取り違え防止)
            dev_id = sensor.get_device_id()
            if dev_id is None:
                self.error = 'プローブが応答しません'
                return False
            id_hex = f"{dev_id:06X}"
            if (self.expected_device_id is not None
                    and id_hex.upper() != self.expected_device_id.upper()):
                self.wrong_device = True
                self.error = f'取り違え検出: 接続 {id_hex} != 期待 {self.expected_device_id}'
                return False
            info = {
                "device_id":  dev_id,
                "name":       sensor.get_name(),
                "data_count": sensor.get_data_count(),
            }

            self._progress('プローブ初期化中...')
            if not (sensor.set_enable(True) and sensor.set_filter_n(ca.FILTER_N)):
                self.error = 'プローブ初期化書込に失敗しました'
                return False

            phase1 = self._run_phase1(sensor)
            if phase1 is None:
                return False

            self._progress('フィッティングと係数書込中...')
            coef_a, coef_b = self._run_phase2(sensor, phase1)
            if coef_a is None:
                return False
            self._progress('係数書込完了', bump=True)

            phase3 = self._run_phase3(sensor)
            if phase3 is None:
                return False

            self._progress('レポート生成中...')
            with _plot_lock:
                ca.save_calibration_report(info, phase1, coef_a, coef_b, phase3,
                                           show_plot=show_plot,
                                           calibrator_id=self.calibrator_id)
            return True
        except _Cancelled:
            self.error = '中断'
            return False
        finally:
            try:
                self._set_fan(0)
            except Exception:
                pass
            try:
                sensor.set_enable(False)   # 熱線を止めて片付ける
            except Exception:
                pass
            sensor.close()

    # ---------------- Phase 1: 生電圧の実測 ----------------
    def _run_phase1(self, sensor):
        results = []
        # 降順計測 (高風速→0 m/s)。理由は calibrate_anemometer.run_phase_1 参照。
        for point in sorted(self.calibration_points,
                            key=lambda p: p["ref_velocity"], reverse=True):
            self._check_cancel()
            ref_v = point["ref_velocity"]
            power = point["fan_power"]

            self._progress(f"校正 {ref_v} m/s: 安定化待機 ({ca.stabilization_time(ref_v)}s)...")
            self._set_fan(power)
            self._sleep(ca.stabilization_time(ref_v))

            dur = ca.measurement_duration(ref_v)
            self._progress(f"校正 {ref_v} m/s: 計測中 ({dur}s)...")
            buf = []
            start = time.time()
            while time.time() - start < dur:
                self._check_cancel()
                poll = sensor.read_poll_block()
                if poll is not None and not (
                        poll["status1"] & (1 << AnemometerRegisters.VAL_IDX_VOLTAGE)):
                    buf.append(poll["values"][AnemometerRegisters.VAL_IDX_VOLTAGE])
                time.sleep(ca.SAMPLING_INTERVAL)

            if not buf:
                self.error = f'{ref_v} m/s: 有効サンプル無し (プローブ切断/予熱不良?)'
                return None
            results.append({
                "fan_power":    power,
                "ref_velocity": ref_v,
                "measured_avg": statistics.mean(buf),
                "std_dev":      statistics.stdev(buf) if len(buf) > 1 else 0.0,
            })
            self._progress(f"校正 {ref_v} m/s: 完了 "
                           f"({results[-1]['measured_avg']*1000:.1f} mV)", bump=True)

        # フィット・出力のため風速昇順へ (e0 = results[0])
        results.sort(key=lambda r: r["ref_velocity"])

        # 0 m/s 電圧が異常に低くないか (回路未起動/断線の検知)
        zero = next((r for r in results if r["ref_velocity"] == 0.0), None)
        if zero is not None and zero["measured_avg"] < ca.ABNORMAL_NO_WIND_VOLTAGE:
            self._set_fan(0)
            if not self.on_abnormal(zero["measured_avg"]):
                self.error = (f"0 m/s 電圧異常 ({zero['measured_avg']*1000:.1f} mV) "
                              f"により中止")
                return None
        return results

    # ---------------- Phase 2: フィット + 係数書込 ----------------
    def _run_phase2(self, sensor, phase1):
        e = [r["measured_avg"] for r in phase1]
        v = [r["ref_velocity"] for r in phase1]
        e0 = e[0]
        c1, m1 = ca.calculate_kings_law_params(v[1], e[1], v[2], e[2], e0)
        c2, m2 = ca.calculate_kings_law_params(v[2], e[2], v[3], e[3], e0)
        c3, m3 = ca.calculate_kings_law_params(v[3], e[3], v[4], e[4], e0)

        # firmware updateVelocity の係数配置 (calibrate_anemometer.run_phase_2 と同一)
        coef_a = [float(e0),
                  float(m1), float(math.log(c1)),
                  float(m2), float(math.log(c2))]
        coef_b = [float(m3), float(math.log(c3)),
                  float(v[2]), float(v[3]), 0.0]

        if not (sensor.set_coefficients_a(coef_a) and sensor.set_coefficients_b(coef_b)):
            self.error = '係数書込に失敗しました'
            return None, None

        # 読み返し検証 (float32 丸めがあるため相対 1e-4 で比較)
        ra = sensor.get_coefficients_a()
        rb = sensor.get_coefficients_b()

        def close(written, read):
            return read is not None and all(
                abs(a - b) <= 1e-4 * max(1.0, abs(a)) for a, b in zip(written, read))

        if not (close(coef_a, ra) and close(coef_b, rb)):
            self.error = f'係数読み返し不一致: A={ra} B={rb}'
            return None, None
        return coef_a, coef_b

    # ---------------- Phase 3: 補正後風速の検証 ----------------
    def _run_phase3(self, sensor):
        results = []
        for point in sorted(self.validation_points,
                            key=lambda p: p["ref_velocity"], reverse=True):
            self._check_cancel()
            ref_v = point["ref_velocity"]
            power = point["fan_power"]

            self._progress(f"検証 {ref_v} m/s: 安定化待機 ({ca.stabilization_time(ref_v)}s)...")
            self._set_fan(power)
            self._sleep(ca.stabilization_time(ref_v))

            self._progress(f"検証 {ref_v} m/s: 計測中 ({ca.VAL_MEASUREMENT_DURATION}s)...")
            vels, volts = [], []
            start = time.time()
            while time.time() - start < ca.VAL_MEASUREMENT_DURATION:
                self._check_cancel()
                poll = sensor.read_poll_block()
                if poll is not None:
                    s1 = poll["status1"]
                    vv = poll["values"]
                    if not (s1 & (1 << AnemometerRegisters.VAL_IDX_VELOCITY)):
                        vels.append(vv[AnemometerRegisters.VAL_IDX_VELOCITY])
                    if not (s1 & (1 << AnemometerRegisters.VAL_IDX_VOLTAGE)):
                        volts.append(vv[AnemometerRegisters.VAL_IDX_VOLTAGE])
                time.sleep(0.5)

            if not (vels and volts):
                self.error = f'検証 {ref_v} m/s: 有効サンプル無し'
                return None
            avg_vel  = statistics.mean(vels)
            avg_volt = statistics.mean(volts)
            err_pct  = (abs(avg_vel - ref_v) / ref_v * 100) if ref_v > 0 else 0.0
            results.append({
                "ref":       ref_v,
                "measuredV": avg_volt,
                "measured":  avg_vel,
                "error":     err_pct,
            })
            self._progress(f"検証 {ref_v} m/s: {avg_vel:.3f} m/s "
                           f"(誤差 {err_pct:.1f}%)", bump=True)

        results.sort(key=lambda r: r["ref"])
        return results
