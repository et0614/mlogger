"""
風速校正の異常 (低風速のばらつき・0 m/s の不一致・時間ドリフト) の原因を切り分ける診断記録。

接続中の全プローブ (CP2112 経由) の電圧を 10 Hz で、QuadroFan の各ファン回転数を
約 2 秒毎に同時記録しながら、指定したファン出力スケジュールを実行する。
記録中に Enter を押すと、その時刻にマーカー (例: USB の抜き差し) を打てる。

検出するもの:
  - プローブのリセット (電源の瞬断): meas_count の巻き戻り / enable=0 への復帰
  - CP2112 の切断・読出し失敗 (USB の瞬断)
  - ファン停止 (指令 > 0% なのに 0 rpm) と、ファン設定コマンドの失敗
  - 1〜2 秒だけ電圧が跳ねる区間 (校正と同じ 1 秒区間の外れ判定)
  - 熱線 ON 後の電圧ドリフト (予熱)

プリセット (--preset):
  drift     : 全風洞を 40% (約 2.5 m/s) で 10 分。予熱ドリフトの大きさと落ち着くまでの時間
  lowspeed  : 全風洞を 12% → 8% → 0%。低出力でのファン停止と、低風速の跳ねの頻度
  crosstalk : 全風洞 0% の中で 1 風洞ずつ 78% に。止めている風洞に他の風洞の風が届くか
  hub       : 全風洞 0% で 5 分。記録中に USB を抜き差しして Enter でマーク
任意スケジュール (--steps): "秒数:ファン=出力,..." を並べる。例:
  --steps 60:1=12 120:1=8 120:1=0      (風洞 1 を 12% 60 秒 → 8% 120 秒 → 0% 120 秒)

出力: diag/<日時>/ に samples.csv (全サンプル)、events.csv、summary.txt、plot.png

注意: 校正 GUI と同時に実行しないこと (同じ CP2112 / ファンを取り合う)。

使い方:
    python anemometer_diag.py --preset drift
    python anemometer_diag.py --preset hub
    python anemometer_diag.py --probe 14A51E --probe 1E1047 --preset crosstalk
"""
import argparse
import csv
import datetime
import os
import re
import statistics
import struct
import subprocess
import sys
import threading
import time

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

import calibrate_anemometer as ca
from anemometer_manager import AnemometerManager, AnemometerRegisters as R
from cp2112_driver import CP2112Device

SAMPLE_INTERVAL = 0.1     # プローブ読出し周期 [s]
RPM_INTERVAL    = 2.0     # ファン回転数の取得周期 [s]
ENABLE_CHECK    = 1.0     # enable レジスタ確認周期 [s] (リセット検出)
REG_MEAS_COUNT  = 2       # POLL ブロック内の meas_count のオフセット (0x2A)
ALL_FANS        = sorted(ca.CALIBRATOR_PROFILES.keys())


# ==========================================================
# スケジュール
# ==========================================================
def preset_steps(name, fans):
    """プリセット → [(秒数, {fan: 出力})]"""
    all_ = lambda p: {f: p for f in fans}
    if name == "drift":
        return [(600, all_(40))]
    if name == "lowspeed":
        return [(60, all_(12)), (120, all_(8)), (120, all_(0))]
    if name == "crosstalk":
        steps = [(60, all_(0))]
        for f in fans:
            steps += [(60, {**all_(0), f: 78}), (60, all_(0))]
        return steps
    if name == "hub":
        return [(300, all_(0))]
    raise ValueError(name)


def parse_steps(texts):
    """ "60:1=12,2=0" → (60, {1: 12, 2: 0}) のリスト。"""
    steps = []
    for t in texts:
        m = re.fullmatch(r"(\d+(?:\.\d+)?):(.+)", t.strip())
        if not m:
            raise ValueError(f"step の書式が不正: {t!r} (例 60:1=12,2=0)")
        fans = {}
        for kv in m.group(2).split(","):
            f, p = kv.split("=")
            fans[int(f)] = int(p)
        steps.append((float(m.group(1)), fans))
    return steps


def step_label(fans):
    return " ".join(f"F{f}={p}%" for f, p in sorted(fans.items()))


# ==========================================================
# ファン (liquidctl)。失敗を握りつぶさず event として記録する
# ==========================================================
class Fans:
    def __init__(self, log_event):
        self.log_event = log_event
        self.cmd = {}   # fan -> 指令値 [%]
        self.rpm = {}   # fan -> 直近の回転数 [rpm]

    def set(self, fan, power):
        t0 = time.time()
        r = subprocess.run(["liquidctl", "--match", "quadro", "set", f"fan{fan}",
                            "speed", str(power)], capture_output=True, text=True)
        dt = time.time() - t0
        if r.returncode == 0:
            self.cmd[fan] = power
            self.log_event("fan_set", f"fan{fan}={power}% ({dt:.1f}s)")
        else:
            self.log_event("FAN_SET_FAILED",
                           f"fan{fan}={power}%: {(r.stderr or r.stdout).strip()[:120]}")

    def poll_rpm(self):
        r = subprocess.run(["liquidctl", "--match", "quadro", "status"],
                           capture_output=True, text=True)
        if r.returncode != 0:
            self.log_event("FAN_STATUS_FAILED", (r.stderr or r.stdout).strip()[:120])
            return
        for f, rpm in re.findall(r"Fan (\d) speed\s+(\d+)\s+rpm", r.stdout):
            self.rpm[int(f)] = int(rpm)


# ==========================================================
# プローブ読出し (1 プローブ 1 スレッド)
# ==========================================================
class ProbeReader(threading.Thread):
    def __init__(self, path, dev_id, rec):
        super().__init__(daemon=True)
        self.path, self.dev_id, self.rec = path, dev_id, rec
        self.sensor = None

    def _open(self):
        s = AnemometerManager(path=self.path)
        if not s.open():
            return None
        s.set_enable(True)
        s.set_filter_n(ca.FILTER_N)
        return s

    def run(self):
        rec = self.rec
        self.sensor = self._open()
        if self.sensor is None:
            rec.event("PROBE_OPEN_FAILED", self.dev_id)
            return
        last_mc = None
        last_enable_chk = 0.0
        failing = False
        next_t = time.time()
        while not rec.stop.is_set():
            now = time.time()
            raw = None
            try:
                raw = self.sensor.read_i2c_block(R.REG_POLL_BASE, R.POLL_BLOCK_SIZE)
            except Exception as e:
                rec.event("READ_EXCEPTION", f"{self.dev_id}: {e}")

            if raw is None:
                if not failing:
                    rec.event("READ_FAIL_START", self.dev_id)
                    failing = True
                rec.sample(now, self.dev_id, ok=False)
                # アダプタが再列挙された場合に備えて開き直す
                try:
                    self.sensor.close()
                except Exception:
                    pass
                time.sleep(1.0)
                s = self._open()
                if s is not None:
                    self.sensor = s
                    rec.event("ADAPTER_REOPENED", f"{self.dev_id} (熱線を再 ON)")
                next_t = time.time()
                continue
            if failing:
                rec.event("READ_FAIL_END", self.dev_id)
                failing = False

            status1 = raw[R.POLL_OFS_STATUS1]
            mc = raw[REG_MEAS_COUNT]
            vel, volt = struct.unpack('<2f', raw[R.POLL_OFS_VALUE:R.POLL_OFS_VALUE + 8])
            # meas_count は毎秒 +5 で 255 の次は 0。大きく巻き戻ったら MCU リセット
            if last_mc is not None and mc < last_mc and last_mc < 240:
                rec.event("PROBE_RESET", f"{self.dev_id}: meas_count {last_mc}->{mc}")
            last_mc = mc
            rec.sample(now, self.dev_id, ok=True, status1=status1, mc=mc,
                       volt=volt, vel=vel)

            if now - last_enable_chk >= ENABLE_CHECK:
                last_enable_chk = now
                en = self.sensor.get_enable()
                if en is False:
                    # リセット直後は熱線 OFF (enable=0) で起動する。状況を記録して再 ON
                    rec.event("PROBE_ENABLE_LOST", f"{self.dev_id} (熱線 OFF を検出 → 再 ON)")
                    self.sensor.set_enable(True)
                    self.sensor.set_filter_n(ca.FILTER_N)

            next_t += SAMPLE_INTERVAL
            time.sleep(max(0.0, next_t - time.time()))

    def shutdown(self):
        if self.sensor is not None:
            try:
                self.sensor.set_enable(False)
                self.sensor.close()
            except Exception:
                pass


# ==========================================================
# 記録
# ==========================================================
class Recorder:
    def __init__(self, fans_used):
        self.t0 = time.time()
        self.stop = threading.Event()
        self.lock = threading.Lock()
        self.samples = []   # dict
        self.events = []    # (t, kind, detail)
        self.fans = Fans(self.event)
        self.fans_used = fans_used
        self.step_idx = -1

    def rel(self, t):
        return t - self.t0

    def event(self, kind, detail=""):
        t = time.time()
        with self.lock:
            self.events.append((self.rel(t), kind, detail))
        mark = "!!" if kind.isupper() else "  "
        print(f"{mark} [{self.rel(t):7.1f}s] {kind}: {detail}")

    def sample(self, t, dev, ok, status1=None, mc=None, volt=None, vel=None):
        row = {"t": round(self.rel(t), 3), "probe": dev, "ok": int(ok),
               "status1": status1, "meas_count": mc,
               "voltage_mV": None if volt is None else round(volt * 1000, 2),
               "velocity": None if vel is None else round(vel, 4),
               "step": self.step_idx}
        for f in self.fans_used:
            row[f"fan{f}_cmd"] = self.fans.cmd.get(f)
            row[f"fan{f}_rpm"] = self.fans.rpm.get(f)
        with self.lock:
            self.samples.append(row)


def find_probes(wanted):
    """接続中の CP2112 を走査し [(path, dev_id)] を返す。wanted が空なら全部。"""
    found = []
    for d in CP2112Device.list_devices():
        m = AnemometerManager(path=d["path"])
        if not m.open():
            continue
        try:
            did = m.get_device_id()
        finally:
            m.close()
        if did is not None:
            found.append((d["path"], f"{did:06X}"))
    if wanted:
        wanted = {w.upper() for w in wanted}
        missing = wanted - {d for _, d in found}
        if missing:
            print(f"WARNING: 見つからないプローブ: {', '.join(sorted(missing))}")
        found = [(p, d) for p, d in found if d in wanted]
    return found


# ==========================================================
# 解析
# ==========================================================
def analyze(rec, steps, step_times, settle, out_dir):
    lines = []
    out = lines.append
    probes = sorted({s["probe"] for s in rec.samples})
    out(f"記録時間 {rec.rel(time.time()):.0f} s / プローブ {', '.join(probes)}")

    ev_kinds = {}
    for _, k, _ in rec.events:
        if k.isupper():
            ev_kinds[k] = ev_kinds.get(k, 0) + 1
    out("\n=== 異常イベント ===")
    out("  なし" if not ev_kinds else
        "  " + ", ".join(f"{k} x{n}" for k, n in sorted(ev_kinds.items())))
    for t, k, d in rec.events:
        if k.isupper():
            out(f"  {t:7.1f}s  {k}  {d}")

    out(f"\n=== ステップ別 (各ステップ開始から {settle:.0f} 秒以降を集計) ===")
    for i, ((dur, fans), (s0, s1)) in enumerate(zip(steps, step_times)):
        out(f"\n[{i}] {s0:6.0f}-{s1:6.0f}s  {step_label(fans)}")
        for f in sorted(fans):
            rpms = [s[f"fan{f}_rpm"] for s in rec.samples
                    if s0 + settle <= s["t"] < s1 and s[f"fan{f}_rpm"] is not None]
            if rpms:
                stall = "  <-- 指令 > 0% なのに停止" if fans[f] > 0 and min(rpms) == 0 else ""
                out(f"    fan{f}: {min(rpms)}-{max(rpms)} rpm{stall}")
        for p in probes:
            ss = [s for s in rec.samples if s["probe"] == p and s["ok"]
                  and s0 + settle <= s["t"] < s1
                  and not (s["status1"] & (1 << R.VAL_IDX_VOLTAGE))]
            if len(ss) < 10:
                out(f"    {p}: 有効サンプル不足 ({len(ss)})")
                continue
            v = [s["voltage_mV"] for s in ss]
            pairs = [(s["t"] - ss[0]["t"], s["voltage_mV"] / 1000) for s in ss]
            bad = ca.outlier_bins(pairs)
            # 先頭/末尾の平均の差 = ステップ内のドリフト (窓は 30 秒、短いステップは 1/3)
            w = min(30.0, (ss[-1]["t"] - ss[0]["t"]) / 3)
            head = [s["voltage_mV"] for s in ss if s["t"] < ss[0]["t"] + w]
            tail = [s["voltage_mV"] for s in ss if s["t"] > ss[-1]["t"] - w]
            drift = statistics.mean(tail) - statistics.mean(head)
            out(f"    {p}: 平均 {statistics.mean(v):7.1f} mV  中央値 {statistics.median(v):7.1f}"
                f"  std {statistics.stdev(v):5.1f}  範囲 {min(v):.0f}-{max(v):.0f}"
                f"  跳ね区間 {len(bad)}/{ca.n_bins(pairs)}  ドリフト {drift:+.1f} mV")

    marks = [(t, d) for t, k, d in rec.events if k == "marker"]
    if marks:
        out("\n=== マーカー前後の電圧変化 (直前 5 秒の平均からの最大偏差、直後 10 秒) ===")
        for t, d in marks:
            out(f"  {t:7.1f}s  {d}")
            for p in probes:
                pre = [s["voltage_mV"] for s in rec.samples if s["probe"] == p and s["ok"]
                       and t - 5 <= s["t"] < t]
                post = [s for s in rec.samples if s["probe"] == p and t <= s["t"] < t + 10]
                fails = sum(1 for s in post if not s["ok"])
                vals = [s["voltage_mV"] for s in post if s["ok"]]
                if pre and vals:
                    base = statistics.mean(pre)
                    dev = max(vals, key=lambda x: abs(x - base)) - base
                    out(f"    {p}: {dev:+7.1f} mV" + (f"  読出し失敗 {fails}" if fails else ""))

    text = "\n".join(lines)
    with open(os.path.join(out_dir, "summary.txt"), "w", encoding="utf-8") as f:
        f.write(text + "\n")
    print("\n" + text)


def plot(rec, steps, step_times, out_dir):
    probes = sorted({s["probe"] for s in rec.samples})
    fig, axes = plt.subplots(len(probes) + 1, 1, sharex=True,
                             figsize=(12, 2.4 * (len(probes) + 1)))
    for ax, p in zip(axes, probes):
        ss = [s for s in rec.samples if s["probe"] == p and s["ok"]]
        ax.plot([s["t"] for s in ss], [s["voltage_mV"] for s in ss], lw=0.7)
        fails = [s["t"] for s in rec.samples if s["probe"] == p and not s["ok"]]
        if fails:
            ax.plot(fails, [ax.get_ylim()[0]] * len(fails), "rx", ms=4, label="read fail")
        ax.set_ylabel(f"{p}\n[mV]")
        ax.grid(alpha=0.3)
    ax = axes[-1]
    for f in rec.fans_used:
        ss = [s for s in rec.samples if s[f"fan{f}_rpm"] is not None]
        ax.plot([s["t"] for s in ss], [s[f"fan{f}_rpm"] for s in ss], lw=0.9, label=f"fan{f}")
    ax.set_ylabel("rpm"); ax.set_xlabel("time [s]"); ax.legend(loc="upper right", fontsize=7)
    ax.grid(alpha=0.3)
    for a in axes:
        for i, (s0, s1) in enumerate(step_times):
            if i % 2:
                a.axvspan(s0, s1, color="0.92", zorder=0)
    for t, k, d in rec.events:
        if k == "marker":
            for a in axes:
                a.axvline(t, color="g", ls="--", lw=1)
        elif k.isupper():
            # 異常イベントは該当する欄にだけ引く (プローブ固有 / ファン / その他は全欄)
            owner = [a for a, p in zip(axes, probes) if d.startswith(p)]
            if not owner:
                owner = [axes[-1]] if k.startswith("FAN") else list(axes)
            for a in owner:
                a.axvline(t, color="r", lw=1)
    axes[0].set_title("gray/white = steps, green = marker, red = abnormal event")
    fig.tight_layout()
    fig.savefig(os.path.join(out_dir, "plot.png"), dpi=110)
    plt.close(fig)


def write_csv(rec, out_dir):
    if rec.samples:
        with open(os.path.join(out_dir, "samples.csv"), "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(rec.samples[0].keys()))
            w.writeheader()
            w.writerows(rec.samples)
    with open(os.path.join(out_dir, "events.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["t", "kind", "detail"])
        w.writerows(rec.events)


# ==========================================================
# main
# ==========================================================
def main():
    ap = argparse.ArgumentParser(description="風速校正の異常の診断記録")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--preset", choices=["drift", "lowspeed", "crosstalk", "hub"])
    g.add_argument("--steps", nargs="+", help='例: 60:1=12 120:1=8 120:1=0')
    ap.add_argument("--fans", type=int, nargs="+", default=ALL_FANS,
                    help=f"プリセットで使うファン番号 (既定 {ALL_FANS})")
    ap.add_argument("--probe", action="append", default=[],
                    help="記録するプローブの device_id (複数可。省略時は接続中の全プローブ)")
    ap.add_argument("--settle", type=float, default=25.0,
                    help="ステップ集計で除く、各ステップ開始直後の秒数 (既定 25)")
    ap.add_argument("--preheat", type=float, default=0.0,
                    help="スケジュール開始前に熱線 ON のまま待つ秒数 (ファン 0%%)")
    args = ap.parse_args()

    steps = preset_steps(args.preset, args.fans) if args.preset else parse_steps(args.steps)
    fans_used = sorted({f for _, fans in steps for f in fans})

    probes = find_probes(args.probe)
    if not probes:
        print("ERROR: プローブが見つかりません")
        return 1
    total = args.preheat + sum(d for d, _ in steps)
    print(f"プローブ: {', '.join(d for _, d in probes)}")
    print(f"ファン  : {', '.join(f'fan{f}' for f in fans_used)}")
    print(f"所要    : {total / 60:.1f} 分  (Enter でマーカー、Ctrl+C で中断)")

    out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "diag",
                           datetime.datetime.now().strftime("%Y%m%d_%H%M%S"))
    os.makedirs(out_dir, exist_ok=True)

    rec = Recorder(fans_used)
    for f in fans_used:
        rec.fans.set(f, 0)
    readers = [ProbeReader(p, d, rec) for p, d in probes]
    for r in readers:
        r.start()
    rec.event("heater_on", "全プローブ enable=1 (熱線 ON)")

    def rpm_loop():
        while not rec.stop.is_set():
            rec.fans.poll_rpm()
            rec.stop.wait(RPM_INTERVAL)
    threading.Thread(target=rpm_loop, daemon=True).start()

    def marker_loop():
        n = 0
        while not rec.stop.is_set():
            try:
                text = input()
            except EOFError:
                return
            n += 1
            rec.event("marker", f"#{n} {text}".strip())
    threading.Thread(target=marker_loop, daemon=True).start()

    step_times = []
    try:
        if args.preheat > 0:
            rec.event("preheat", f"{args.preheat:.0f} s")
            time.sleep(args.preheat)
        for i, (dur, fans) in enumerate(steps):
            rec.step_idx = i
            s0 = rec.rel(time.time())
            rec.event("step", f"[{i}] {step_label(fans)} {dur:.0f}s")
            for f, p in fans.items():
                if rec.fans.cmd.get(f) != p:
                    rec.fans.set(f, p)
            time.sleep(max(0.0, dur - (rec.rel(time.time()) - s0)))
            step_times.append((s0, rec.rel(time.time())))
    except KeyboardInterrupt:
        rec.event("interrupted", "Ctrl+C")
        if len(step_times) < len(steps) and rec.step_idx >= 0:
            step_times.append((step_times[-1][1] if step_times else 0.0, rec.rel(time.time())))
    finally:
        rec.stop.set()
        for f in fans_used:
            rec.fans.set(f, 0)
        for r in readers:
            r.join(timeout=3)
            r.shutdown()

    write_csv(rec, out_dir)
    analyze(rec, steps[:len(step_times)], step_times, args.settle, out_dir)
    plot(rec, steps[:len(step_times)], step_times, out_dir)
    print(f"\n出力: {out_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
