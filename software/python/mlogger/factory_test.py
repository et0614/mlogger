"""
M-Logger 出荷前試験スクリプト (USB-CDC 経由)。

温湿度・グローブ温度・CO2・照度・風速の各センサとフラッシュメモリを一通り
動作させ、結果を factory_test_logs/ に JSON で記録する。

試験手順:
  1. hello で個体情報 (name / hardware_id / FW version) を取得
  2. get_battery で電池電圧を確認
  3. 設定を退避し、全センサ有効 / interval=1sec に変更
  4. set_time で RTC 設定 (フラッシュ記録の前提条件)
  5. start_logging (usb + flash) で計測開始
  6. ウォームアップ完了 (wu 消滅) を待つ (CO2 conditioning ~25sec + 風速加熱 ~10sec)
  7. N サンプル収集し、各チャネルの出現率とレンジを検査
     (dc フラグ = プローブ切断は即 FAIL)
  8. stop_logging → dump でフラッシュから実データを読み返して検証
  9. clear_data + 設定復元で出荷状態に戻す
 10. 結果を表示し JSON 保存。全項目 PASS なら exit 0

判定レンジは室内 (照明あり・常温・無風〜微風) での試験を前提とする。
照度センサを覆ったまま試験すると illuminance が FAIL するので注意。

Usage:
    python factory_test.py           # auto-detect COM port
    python factory_test.py COM3
"""
import json
import os
import struct
import sys
import time
from datetime import datetime

from ble_trace import open_no_reset, find_device_port

# ============================================================
# 試験パラメータ
# ============================================================
SCRIPT_VERSION = "1.1"  # 記録 JSON に埋める試験スクリプト版数

SAMPLE_COUNT      = 15    # ウォームアップ後に収集するサンプル数
WARMUP_TIMEOUT_S  = 90    # ウォームアップ完了待ちの上限 [sec]
PRESENCE_RATIO    = 0.8   # 各チャネルの最低出現率 (欠測許容 20%)
DC_FAIL_S         = 5     # dc (切断) 表示がこの秒数継続したら即 FAIL

RANGES = {
    "t":  (-10.0, 50.0),    # 乾球温度 [C]
    "h":  (5.0, 95.0),      # 相対湿度 [%]
    "g":  (-10.0, 50.0),    # グローブ温度 [C]
    "c":  (300, 10000),     # CO2 [ppm]
    "l":  (1, 200000),      # 照度 [lx] (照明のある室内前提。0 は素子カバー or 故障)
    "v":  (0.0, 10.0),      # 風速 [m/s] (無風で 0.000 は正常)
    "vv": (100, 3000),      # 熱線ブリッジ電圧 [mV]
}
BATTERY_RANGE_MV = (2000, 3500)

RECORD_FORMAT = "<BIBIhhHHHH"  # SensorData_t (22 bytes)
RECORD_SIZE = struct.calcsize(RECORD_FORMAT)

LOG_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "factory_test_logs")

_next_id = [100]


# ============================================================
# 通信ユーティリティ
# ============================================================
def _readline_obj(ser):
    """1 行読んで JSON なら dict を返す。diag(#)/空行/非 JSON は None。"""
    line = ser.readline().decode("utf-8", errors="ignore").strip()
    if not line or not line.startswith("{"):
        return None
    try:
        return json.loads(line)
    except json.JSONDecodeError:
        return None


def send_cmd(ser, command, params=None, timeout=5.0):
    """コマンド送信し、同 id の応答を返す。途中の smp 等の event は読み飛ばす。"""
    _next_id[0] += 1
    req = {"v": 1, "id": _next_id[0], "command": command}
    if params is not None:
        req["params"] = params
    ser.write((json.dumps(req) + "\n").encode("utf-8"))
    end = time.time() + timeout
    while time.time() < end:
        obj = _readline_obj(ser)
        if obj and obj.get("id") == _next_id[0]:
            return obj
    return None


def cmd_result(ser, command, params=None, timeout=5.0):
    res = send_cmd(ser, command, params, timeout)
    if res is None:
        raise RuntimeError(f"{command}: no response")
    if "error" in res:
        raise RuntimeError(f"{command}: error response {res['error']}")
    return res["result"]


# ============================================================
# 結果記録
# ============================================================
class Report:
    def __init__(self):
        self.items = []

    def add(self, item, passed, detail):
        self.items.append({"item": item, "pass": bool(passed), "detail": detail})
        print(f"  [{'PASS' if passed else 'FAIL'}] {item}: {detail}")

    def all_pass(self):
        return all(i["pass"] for i in self.items)


# ============================================================
# 試験本体
# ============================================================
def collect_samples(ser, report):
    """ウォームアップ完了を待ってから SAMPLE_COUNT サンプル収集して返す。"""
    print(f"ウォームアップ待ち (最大 {WARMUP_TIMEOUT_S}sec。CO2 conditioning ~25sec + 風速加熱 ~10sec)...")
    samples = []
    warmed = False
    dc_since = {}     # category -> 最初に dc を見た時刻
    t_end = time.time() + WARMUP_TIMEOUT_S
    last_note = ""

    while time.time() < t_end:
        obj = _readline_obj(ser)
        if not obj or obj.get("event") != "smp":
            continue
        data = obj.get("data", {})

        # 切断検知: 一定時間継続したら即 FAIL で打ち切り
        now = time.time()
        for cat in data.get("dc", []):
            dc_since.setdefault(cat, now)
            if now - dc_since[cat] >= DC_FAIL_S:
                name = {"g": "温湿度/CO2/グローブ プローブ", "v": "風速プローブ"}.get(cat, cat)
                report.add("probe_connection", False, f"{name} が切断状態 (dc={cat})")
                return None
        for cat in list(dc_since):
            if cat not in data.get("dc", []):
                del dc_since[cat]

        wu = data.get("wu", [])
        note = f"warmup 中: {wu}" if wu else "warmup 完了、サンプル収集中"
        if note != last_note:
            print(f"  {note}")
            last_note = note

        if not warmed:
            # wu が消え、主要キー (t と v) が実際に載り始めたら収集開始
            if not wu and "t" in data and "v" in data:
                warmed = True
            else:
                continue

        samples.append(data)
        if len(samples) >= SAMPLE_COUNT:
            report.add("probe_connection", True, "切断なし")
            return samples

    report.add("probe_connection", False,
               f"ウォームアップが {WARMUP_TIMEOUT_S}sec 以内に完了しない (収集 {len(samples)}/{SAMPLE_COUNT})")
    return None


def check_channels(samples, report):
    """各チャネルの出現率とレンジを検査。"""
    n = len(samples)
    channel_names = {
        "t": "温湿度センサ (乾球温度)",
        "h": "温湿度センサ (相対湿度)",
        "g": "グローブセンサ",
        "c": "CO2センサ",
        "l": "照度センサ",
        "v": "風速センサ (風速)",
        "vv": "風速センサ (ブリッジ電圧)",
    }
    stats = {}
    for key, name in channel_names.items():
        vals = [s[key] for s in samples if key in s]
        lo, hi = RANGES[key]
        if len(vals) < n * PRESENCE_RATIO:
            report.add(name, False, f"出現率不足 {len(vals)}/{n}")
            continue
        vmin, vmax = min(vals), max(vals)
        vmean = sum(vals) / len(vals)
        stats[key] = {"n": len(vals), "min": vmin, "mean": round(vmean, 3), "max": vmax}
        in_range = lo <= vmin and vmax <= hi
        report.add(name, in_range,
                   f"n={len(vals)}/{n} min={vmin} mean={round(vmean, 2)} max={vmax}"
                   + ("" if in_range else f" (許容 {lo}..{hi})"))
    return stats


def _attempt_dump(ser, count, rec_size, session_start_ts):
    """dump 1 回分の実行と検証。(ok, detail) を返す。"""
    # dump: JSON ヘッダ行 → バイナリ count*rec_size B → dump_end イベント行
    _next_id[0] += 1
    req = {"v": 1, "id": _next_id[0], "command": "dump"}
    ser.reset_input_buffer()
    ser.write((json.dumps(req) + "\n").encode("utf-8"))
    header = None
    end = time.time() + 5.0
    while time.time() < end:
        obj = _readline_obj(ser)
        if obj and obj.get("id") == _next_id[0]:
            header = obj
            break
    if not header or "result" not in header:
        return False, f"dump ヘッダ応答なし/エラー: {header}"

    total = count * rec_size
    blob = b""
    end = time.time() + 30.0
    while len(blob) < total and time.time() < end:
        chunk = ser.read(total - len(blob))
        if chunk:
            blob += chunk
    if len(blob) < total:
        return False, f"バイナリ受信不足 {len(blob)}/{total} bytes"

    # dump_end を確認 (少し待つ)
    got_end = False
    end = time.time() + 5.0
    while time.time() < end:
        obj = _readline_obj(ser)
        if obj and obj.get("event") == "dump_end":
            got_end = True
            break

    # レコード検証: 世代一定・timestamp 単調非減少・valid_flags 非ゼロ・試験時刻と整合
    gens, ts_list, flag_ok = set(), [], True
    for i in range(count):
        gen, ts, flags, ill, tdry, tglb, hum, wind, volt, co2 = \
            struct.unpack(RECORD_FORMAT, blob[i * rec_size:(i + 1) * rec_size])
        gens.add(gen)
        ts_list.append(ts)
        if flags == 0:
            flag_ok = False
    monotonic = all(a <= b for a, b in zip(ts_list, ts_list[1:]))
    ts_sane = all(abs(t - session_start_ts) < 3600 for t in ts_list)
    ok = len(gens) == 1 and monotonic and flag_ok and ts_sane and got_end
    detail = (f"{count} 件読み返し: gen={sorted(gens)} ts単調={monotonic} "
              f"flags非ゼロ={flag_ok} ts整合={ts_sane} dump_end={got_end}")
    return ok, detail


def verify_flash(ser, report, session_start_ts):
    """dump でフラッシュから実データを読み返し、レコード構造を検証。

    60 秒周期の ready ハートビートの diag 行がバイナリ受信に割り込むと
    稀に検証が乱れるため、失敗時は 1 回だけリトライする。
    """
    res = cmd_result(ser, "get_count")
    count = res["count"]
    rec_size = res["record_size"]
    if count == 0:
        report.add("フラッシュメモリ", False, "記録件数 0 (書き込みされていない)")
        return
    if rec_size != RECORD_SIZE:
        report.add("フラッシュメモリ", False, f"record_size 不一致: {rec_size} != {RECORD_SIZE}")
        return

    ok, detail = _attempt_dump(ser, count, rec_size, session_start_ts)
    if not ok:
        print(f"  dump 検証失敗 ({detail}) → リトライ")
        time.sleep(1.0)
        ok, detail = _attempt_dump(ser, count, rec_size, session_start_ts)
    report.add("フラッシュメモリ", ok, detail)


def collect_identity(ser, report):
    """プローブ ID / XBee MAC / 補正係数など、出荷記録用の個体情報を収集。"""
    identity = {}

    # プローブ INFO BLOCK (device_id / name / data_count)
    probes = cmd_result(ser, "get_probe_info")
    identity["probes"] = probes
    th = probes.get("th_probe", {})
    vel = probes.get("velocity_probe", {})
    report.add("THプローブ情報",
               th.get("connected") and th.get("device_id") not in (None, "000000"),
               f"id={th.get('device_id')} name={th.get('name')} n={th.get('data_count')}"
               if th.get("connected") else "未接続")
    report.add("風速プローブ情報",
               vel.get("connected") and vel.get("device_id") not in (None, "000000"),
               f"id={vel.get('device_id')} name={vel.get('name')} n={vel.get('data_count')}"
               if vel.get("connected") else "未接続")

    # XBee モジュール (64bit MAC / firmware version)
    res = send_cmd(ser, "get_radio_info", timeout=5.0)
    if res and "result" in res:
        identity["radio"] = res["result"]
        report.add("XBeeモジュール", True,
                   f"mac={res['result'].get('xbee_mac')} fw={res['result'].get('xbee_fw')}")
    else:
        identity["radio"] = None
        report.add("XBeeモジュール", False, f"応答なし/エラー: {res}")

    # 補正係数 (出荷時の校正状態の記録。判定はしない)
    identity["correction"] = cmd_result(ser, "get_correction")
    return identity


def main(port):
    report = Report()
    device = {}
    stats = {}
    identity = {}
    original_settings = None
    logging_started = False

    with open_no_reset(port, timeout=0.5) as ser:
        time.sleep(1.0)
        ser.reset_input_buffer()

        # --- 1. 個体情報 ---
        info = cmd_result(ser, "hello")
        device = {k: info.get(k) for k in ("name", "hardware_id", "firmware_version", "protocol_version")}
        print(f"個体: {device['name']}  hw={device['hardware_id']}  FW {device['firmware_version']}")
        if info.get("logging"):
            print("ロギング中だったため停止します")
            cmd_result(ser, "stop_logging")
            time.sleep(1.0)
            ser.reset_input_buffer()

        try:
            # --- 1b. プローブ ID / XBee MAC / 補正係数 ---
            identity = collect_identity(ser, report)

            # --- 2. 電池電圧 ---
            bat = cmd_result(ser, "get_battery")
            mv = bat["voltage_mv"]
            lo, hi = BATTERY_RANGE_MV
            report.add("電池電圧", lo <= mv <= hi, f"{mv} mV (許容 {lo}..{hi})")
            device["battery_mv"] = mv

            # --- 3. 設定退避 → 試験用設定 ---
            original_settings = cmd_result(ser, "get_settings")
            cmd_result(ser, "set_settings", {
                "general":     {"enabled": True, "interval": 1},
                "velocity":    {"enabled": True, "interval": 1},
                "illuminance": {"enabled": True, "interval": 1},
            })

            # --- 4. RTC 設定 (フラッシュ記録の前提) ---
            session_start_ts = int(time.time())
            cmd_result(ser, "set_time", {"ts": session_start_ts})

            # --- 5. 計測開始 (USB でライブ観測 + フラッシュ書き込み) ---
            cmd_result(ser, "start_logging", {
                "transports": {"usb": True, "flash": True, "zigbee": False, "ble": False},
                "mode": "once",
            })
            logging_started = True

            # --- 6-7. サンプル収集とチャネル検査 ---
            samples = collect_samples(ser, report)
            if samples:
                stats = check_channels(samples, report)

            # --- 8. 停止してフラッシュ読み返し検証 ---
            cmd_result(ser, "stop_logging")
            logging_started = False
            time.sleep(1.0)
            ser.reset_input_buffer()
            verify_flash(ser, report, session_start_ts)

        finally:
            # --- 9. 出荷状態へ復帰 (途中失敗でも必ず実行) ---
            try:
                if logging_started:
                    send_cmd(ser, "stop_logging")
                    time.sleep(1.0)
                    ser.reset_input_buffer()
                send_cmd(ser, "clear_data")
                if original_settings:
                    send_cmd(ser, "set_settings", {
                        "general":     original_settings["general"],
                        "velocity":    original_settings["velocity"],
                        "illuminance": original_settings["illuminance"],
                    })
            except Exception as e:  # 復元失敗は報告のみ (試験判定には含めない)
                print(f"  [WARN] 出荷状態への復帰に失敗: {e}")

    # --- 10. 結果保存 ---
    overall = report.all_pass()
    result = {
        "test": "factory_test",
        "script_version": SCRIPT_VERSION,
        "timestamp": datetime.now().isoformat(timespec="seconds"),
        "device": device,
        "probes": identity.get("probes"),
        "radio": identity.get("radio"),
        "correction": identity.get("correction"),
        "settings_shipped": original_settings,
        "overall": "PASS" if overall else "FAIL",
        "items": report.items,
        "channel_stats": stats,
        "judge_ranges": {"channels": RANGES, "battery_mv": BATTERY_RANGE_MV},
    }
    # 保存名は Web 公開仕様に合わせて hardware_id のみ (例: 911759D0.json)。
    # 再試験で上書きされるため、履歴は history/ にタイムスタンプ付きでも残す。
    hwid = device.get("hardware_id", "noid")
    os.makedirs(LOG_DIR, exist_ok=True)
    path = os.path.join(LOG_DIR, f"{hwid}.json")
    payload = json.dumps(result, ensure_ascii=False, indent=2)
    with open(path, "w", encoding="utf-8") as f:
        f.write(payload)

    hist_dir = os.path.join(LOG_DIR, "history")
    os.makedirs(hist_dir, exist_ok=True)
    hist_path = os.path.join(hist_dir, f"{hwid}_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")
    with open(hist_path, "w", encoding="utf-8") as f:
        f.write(payload)

    # Web 公開ディレクトリへコピー (存在するもののみ)。
    #  - repo 内 web/factory/reports: HTML の version 管理と対で保持
    #  - Drive 側 (広報用資料/web): 実際にデプロイされるサイトのソース
    repo_reports = os.path.normpath(os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "web", "factory", "reports"))
    drive_reports = (r"C:\Users\etoga\マイドライブ（e.togashi@gmail.com）"
                     r"\研究\ロガー開発4\3.広報用資料\web\factory\reports")
    published = []
    for base in (repo_reports, drive_reports):
        if os.path.isdir(os.path.dirname(base)) or os.path.isdir(base):
            os.makedirs(base, exist_ok=True)
            dst = os.path.join(base, f"{hwid}.json")
            with open(dst, "w", encoding="utf-8") as f:
                f.write(payload)
            published.append(dst)

    print()
    print(f"総合判定: {'PASS' if overall else 'FAIL'}")
    print(f"記録: {path}")
    print(f"履歴: {hist_path}")
    for dst in published:
        print(f"Web公開用: {dst}")
    if published:
        print(f"  デプロイ後 URL: https://www.mlogger.jp/factory/viewer.html?id={hwid}")
    return 0 if overall else 1


if __name__ == "__main__":
    port = sys.argv[1] if len(sys.argv) > 1 else find_device_port()
    if not port:
        print("No M-Logger found. Pass COM port explicitly: python factory_test.py COMx")
        sys.exit(2)
    try:
        sys.exit(main(port))
    except RuntimeError as e:
        print(f"[ABORT] {e}")
        sys.exit(1)
