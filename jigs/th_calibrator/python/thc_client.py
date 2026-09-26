#!/usr/bin/env python3
"""th_calibrator 治具の USB-CDC JSON コマンドクライアント。

使い方:
    python thc_client.py hello                  # ポート自動検出して hello
    python thc_client.py ping
    python thc_client.py monitor                # 現在値を表示
    python thc_client.py mux_scan               # 64 スロットスキャン (既定 addr=0x11)
    python thc_client.py mux_scan --addr 0x70   # TCA9548A 自体の応答確認
    python thc_client.py status                 # get_status
    python thc_client.py set_time               # PC 時刻を書き込む
    python thc_client.py raw '{"command":"echo","params":{"n":42}}'
    python thc_client.py --port COM5 hello      # ポート指定

校正 (治具 2 台を同時に使う場合は --port で治具ごとに別プロセスで実行):
    python thc_client.py --port COM5 log --out jigA.csv   # 10 sec 毎に記録 (Ctrl+C で終了)
    python thc_client.py --port COM5 set_coef offsets.csv # Excel で求めた B を一斉設定
    python thc_client.py --port COM5 coef_show            # 現在の補正係数一覧
    python thc_client.py --port COM5 frc 1000             # CO2 強制校正 (FRC)

動作検証
    python thc_client.py --port COM5 log --out verify_jigA.csv --interval 60 --duration 2880 --keep-coef    # 60secで2日間, --keep-coefとすれば補正係数は初期化されない

依存: pyserial  (pip install pyserial)
"""

import argparse
import csv
import json
import os
import struct
import sys
import time
from datetime import datetime, timezone

import serial
import serial.tools.list_ports

BAUD_RATE = 115200
TIMEOUT_SEC = 2.0


def open_no_reset(port, baud=BAUD_RATE, timeout=TIMEOUT_SEC):
    """DTR/RTS を非アサートで open して AVR DU32 の reset 経路を踏まないようにする。
    (software/python/mlogger/test_protocol_v4.py と同じパターン)"""
    ser = serial.Serial()
    ser.port = port
    ser.baudrate = baud
    ser.timeout = timeout
    ser.dtr = False
    ser.rts = False
    ser.open()
    return ser


def send_command(ser, command, params=None, cmd_id=1, timeout=5.0):
    """1 コマンド送信して JSON 応答 (同 id) を待つ。応答 dict を返す。"""
    req = {"id": cmd_id, "command": command}
    if params:
        req["params"] = params
    ser.reset_input_buffer()
    ser.write((json.dumps(req) + "\n").encode("utf-8"))

    end = time.time() + timeout
    buf = b""
    while time.time() < end:
        chunk = ser.readline()
        if not chunk:
            continue
        # readline はシリアル timeout (2 sec) で改行前の途中までを返す。th_read_all 等は
        # 治具が I2C を読みながら 1 行を少しずつ送るため、途中で 2 sec 以上止まると行が
        # 分断される。改行が来るまで連結し、完全な 1 行になってから解釈する。
        buf += chunk
        if not buf.endswith(b"\n"):
            continue
        line = buf.decode("utf-8", errors="replace").strip()
        buf = b""
        if not line or not line.startswith("{"):
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        if resp.get("id") == cmd_id:
            return resp
    raise TimeoutError(f"no response for command '{command}'")


def find_device_port():
    """COM ポートを走査し hello に応答するデバイスを探す。"""
    print("Scanning ports...")
    for p in serial.tools.list_ports.comports():
        if "Bluetooth" in p.description:
            continue
        print(f"  Checking {p.device}...", end="", flush=True)
        try:
            with open_no_reset(p.device, timeout=1.0) as ser:
                time.sleep(1.5)  # CDC 列挙直後の安定待ち
                resp = send_command(ser, "hello", timeout=2.0)
                if resp.get("ok"):
                    print(f" Found! (fw={resp.get('fw')})")
                    return p.device
        except (OSError, serial.SerialException, TimeoutError):
            pass
        print(" no")
    return None


STCC4_STATE_CONDITIONING = 6

# ==========================================================
# 基板チャンネル番号 (ch01-ch64) ⇔ (mux, ローカルch) の対応
# ==========================================================
# mux m は基板 ch(8m+1)〜ch(8m+8) を担当し、mux 内のローカル ch c は
# 基板 ch(8m + BOARD_OFFSET[c]) に接続されている (全 mux 共通の規則)。
# 例: mux4 → ch00→ch38, ch01→ch37, ch02→ch39, ch03→ch40,
#             ch04→ch36, ch05→ch35, ch06→ch34, ch07→ch33
BOARD_OFFSET = [6, 5, 7, 8, 4, 3, 2, 1]


def slot_to_board(m, c):
    """(mux, ローカルch) → 基板チャンネル番号 (1-64)"""
    return 8 * m + BOARD_OFFSET[c]


def board_to_slot(b):
    """基板チャンネル番号 (1-64) → (mux, ローカルch)"""
    m = (b - 1) // 8
    c = BOARD_OFFSET.index((b - 1) % 8 + 1)
    return m, c


def to_board_order(vals64):
    """(mux,ch) 順の 64 要素リストを基板 ch01-ch64 順に並べ替える。"""
    out = [None] * 64
    for m in range(8):
        for c in range(8):
            out[slot_to_board(m, c) - 1] = vals64[m * 8 + c]
    return out


def render_monitor_frame(vals, cycle, note=""):
    """th_read_all の vals (64 要素、(mux,ch) 順) を基板 ch01-ch64 順に並べ替え、
    3 枚の 8x8 グリッドで描画した文字列を返す。
    vals[m*8+c] = [t_c100, rh_100, co2_ppm, status1, status2, stcc4_state,
                   glb_c100] or None。
    値要素の None は stale。セル表示は状態で塗り分ける:
      ----  = 子機不在 (I2C NACK)
      cond  = STCC4 conditioning 実行中 (電源投入後 ~22 秒、正常な待ち状態)
      wait  = トリガ未処理 or 計測中 (status2=0)
      ERR!  = 子機は居るが子機内で STCC4 通信全滅 (status1=0xFF)
      stal  = その値だけ stale (センサ部分異常)"""
    # 旧ファーム (3 要素 [t,h,co2] / 6 要素) の応答でも落ちないように 7 要素へ正規化する。
    # status 系・グローブ温度が無い場合は None 埋め (状態表示は stal に倒れる)。
    vals = [None if v is None else (list(v) + [None] * 7)[:7] for v in vals]

    # (mux,ch) 順 → 基板 ch01-ch64 順へ並べ替えて表示する
    bvals = to_board_order(vals)

    def cell(bidx, kind):
        v = bvals[bidx]
        if v is None:
            return "  ---- "
        t, h, c, s1, s2, st4, g = v
        val = {"t": t, "h": h, "c": c, "g": g}[kind]
        if val is not None:
            if kind in ("t", "h", "g"):
                return f"{val / 100:6.2f} "
            return f"{val:5d}  "
        # 値が無い理由を状態から表示
        if st4 == STCC4_STATE_CONDITIONING:
            return "  cond "
        if s2 == 0:
            return "  wait "
        if s1 == 0xFF:
            return "  ERR! "
        return "  stal "

    n_present = sum(1 for v in vals if v is not None)
    n_ready = sum(1 for v in vals
                  if v is not None and all(x is not None for x in v[:3]))
    n_cond = sum(1 for v in vals
                 if v is not None and v[5] == STCC4_STATE_CONDITIONING
                 and any(x is None for x in v[:3]))
    now = datetime.now().strftime("%H:%M:%S")
    lines = [
        f"th_calibrator monitor   {now}   cycle {cycle}   "
        f"detected {n_present}/64  ready {n_ready}  cond {n_cond}   {note}   (Ctrl+C で終了)",
        "  ---- =不在  cond =warmup中(~22s)  wait =計測中  ERR! =子機内STCC4全滅  stal =部分異常",
        "",
    ]
    for title, kind in (("温度 [°C]", "t"), ("湿度 [%RH]", "h"),
                        ("グローブ温度 [°C]", "g"), ("CO2 [ppm]", "c")):
        lines.append(f"--- {title} ---")
        lines.append("            +1      +2      +3      +4      +5      +6      +7      +8")
        for row in range(8):
            base = row * 8  # 基板 ch(base+1)〜ch(base+8)
            cells = "".join(cell(base + j, kind) for j in range(8))
            lines.append(f"  ch{base + 1:02d}-{base + 8:02d} {cells}")
        lines.append("")
    return "\n".join(lines)


def run_monitor(ser, interval):
    """トリガ → 待ち → 一括読み出し → 画面固定描画、を繰り返す。"""
    os.system("")            # Windows コンソールの ANSI エスケープ有効化
    print("\x1b[2J", end="")  # 初回だけ全画面クリア
    cycle = 0
    n_err = 0
    last_vals = None
    try:
        while True:
            t0 = time.time()
            note = ""
            try:
                send_command(ser, "th_trigger", timeout=10.0)
                time.sleep(1.1)   # 子機の single-shot 計測 (~500ms) + マージン
                t_read = time.time()
                resp = send_command(ser, "th_read_all", timeout=15.0)
                last_vals = resp["vals"]
                note = f"read {time.time() - t_read:.1f}s"
            except TimeoutError as e:
                # 1 周期の失敗で落とさない。直前の表示を残して次の周期へ。
                n_err += 1
                note = f"!! {e}"
                if last_vals is None:
                    continue
            cycle += 1
            if n_err:
                note += f"  errors {n_err}"
            frame = render_monitor_frame(last_vals, cycle, note)
            # カーソルを原点へ戻し、各行を消去しながら上書き (スクロールさせない)
            out = "\x1b[H" + frame.replace("\n", "\x1b[K\n") + "\x1b[K\x1b[J"
            print(out, end="", flush=True)
            remain = interval - (time.time() - t0)
            if remain > 0:
                time.sleep(remain)
    except KeyboardInterrupt:
        print("\nstopped.")


STCC4_STATE_NAMES = {
    0: "IDLE", 1: "FRC_RUNNING", 2: "FRC_DONE", 3: "FRC_FAIL",
    4: "FACTORY_RESET_RUNNING", 5: "FACTORY_RESET_DONE",
    6: "CONDITIONING_RUNNING", 7: "CONDITIONING_DONE",
}


def run_frc(ser, ppm, assume_yes, poll_sec=5.0, timeout_sec=180.0):
    """全スロット一括 FRC。開始 → 5 秒毎に進捗ポーリング → 結果一覧を表示する。"""
    if not assume_yes:
        print(f"基準 CO2 濃度 {ppm} ppm で全スロットに FRC を実行します。")
        print("箱内の濃度が安定していることを確認してください。")
        ans = input("実行しますか? [y/N]: ").strip().lower()
        if ans != "y":
            print("中止しました。")
            return

    resp = send_command(ser, "frc_start", {"ppm": ppm}, timeout=30.0)
    if not resp.get("ok"):
        print(f"ERROR: {resp}", file=sys.stderr)
        sys.exit(2)
    accepted = resp["accepted"]
    chs = sorted(slot_to_board(i // 8, i % 8)
                 for i, a in enumerate(accepted) if a == "1")
    print(f"\nFRC 開始: {resp['started']} 台受理 (target {resp['ppm']} ppm)")
    print("対象: " + ", ".join(f"ch{b:02d}" for b in chs))
    print("子機側は 30 秒連続測定 + FRC 適用 (~35 秒)。conditioning 中の子機は"
          "その完了後に開始します。\n")

    t0 = time.time()
    stat = None
    while time.time() - t0 < timeout_sec:
        time.sleep(poll_sec)
        resp = send_command(ser, "frc_status", timeout=30.0)
        stat = resp["stat"]
        n_run = sum(1 for s in stat if s and s[0] in (1, 6))
        n_done = sum(1 for s in stat if s and s[0] == 2)
        n_fail = sum(1 for s in stat if s and s[0] == 3)
        print(f"  t={time.time() - t0:5.1f}s  running {n_run}  "
              f"done {n_done}  fail {n_fail}")
        if n_run == 0:
            break

    print("\n=== FRC 結果 (基板 ch 順) ===")
    for b in range(1, 65):
        m, c = board_to_slot(b)
        idx = m * 8 + c
        if accepted[idx] != "1":
            continue  # 開始時点で不在だったスロットは表示しない
        s = stat[idx] if stat else None
        if s is None:
            print(f"  ch{b:02d}: 応答なし (途中で切断?)")
            continue
        state, corr = s
        name = STCC4_STATE_NAMES.get(state, f"?{state}")
        if state == 2 and corr is not None:
            print(f"  ch{b:02d}: DONE  correction {corr:+d} ppm")
        elif state == 3:
            print(f"  ch{b:02d}: FAIL  (STCC4 が FRC を拒否/失敗)")
        else:
            print(f"  ch{b:02d}: {name} (タイムアウト時点で未完了)")
    print()


# ==========================================================
# 校正 (補正係数 corrected = a * raw + b、子機 EEPROM に保存)
# ==========================================================
# 子機の補正係数領域の並び (各 LE float): t_a, t_b, rh_a, rh_b, co2_a, co2_b, glb_a, glb_b
COEF_ITEMS = ("t", "rh", "co2", "glb")

# 校正対象 (A=1 固定、B のみ設定)。CO2 は FRC で校正するので対象外。
OFFSET_ITEMS = ("t", "rh", "glb")

# B の許容範囲 (これを超える値は Excel 側の誤りとみなして書き込まない)。
# M-Logger 本体側補正の範囲 (protocol_handlers.c CORRECTIONS) と揃える。
OFFSET_LIMITS = {"t": 3.0, "rh": 9.99, "glb": 3.0}


def f2hex(x):
    """float → 子機メモリ上のバイト列 (LE) の 16 進文字列 (8 文字)。"""
    return struct.pack("<f", x).hex()


def decode_coefs(hexstr):
    """coef_get/coef_set 応答の coef (64 文字) → {item: (a, b)}。"""
    vals = struct.unpack("<8f", bytes.fromhex(hexstr))
    return {item: (vals[i * 2], vals[i * 2 + 1]) for i, item in enumerate(COEF_ITEMS)}


def read_probe_ids(ser):
    """64 スロットの Device ID を (mux,ch) 順のリストで返す (不在は None)。"""
    return send_command(ser, "probe_ids", timeout=30.0)["ids"]


def present_slots(ids):
    """子機が居るスロットを基板 ch 順に [(board_ch, mux, ch, dev_id), ...] で返す。"""
    out = []
    for b in range(1, 65):
        m, c = board_to_slot(b)
        dev = ids[m * 8 + c]
        if dev is not None:
            out.append((b, m, c, dev))
    return out


def warn_duplicate_ids(slots):
    """同一治具内で Device ID が重複している子機を警告する (22bit ハッシュの衝突)。
    重複した ID の集合を返す。"""
    seen = {}
    for b, _, _, dev in slots:
        seen.setdefault(dev, []).append(b)
    dups = {dev: chs for dev, chs in seen.items() if len(chs) > 1}
    for dev, chs in dups.items():
        print(f"WARNING: Device ID {dev} が重複: "
              + ", ".join(f"ch{b:02d}" for b in chs), file=sys.stderr)
    return set(dups)


def set_slot_coefs(ser, m, c, dev, items):
    """1 スロットの補正係数を書く。items = {item: (a, b)}。
    Device ID 照合と読み戻し照合は治具側で行う。成功時は読み戻した係数を返す。"""
    params = {"mux": m, "ch": c, "dev": dev}
    for item, (a, b) in items.items():
        params[item] = [f2hex(a), f2hex(b)]
    resp = send_command(ser, "coef_set", params, timeout=5.0)
    if not resp.get("ok"):
        raise RuntimeError(resp.get("error", "unknown_error"))
    return decode_coefs(resp["coef"])


def run_log(ser, port, out_path, interval, duration_min, keep_coef):
    """全スロットの T/RH/グローブ温度/CO2 を interval 秒毎に CSV へ記録する。

    記録開始時に校正対象 (T/RH/グローブ) の係数を A=1, B=0 に戻し、生値を記録する
    (子機は補正後の値を返すため、旧係数が残っていると B が二重に効く)。
    CSV は 3 行のヘッダ (sensor_id / board_ch / item) + データ行。"""
    ids = read_probe_ids(ser)
    slots = present_slots(ids)
    if not slots:
        print("ERROR: 子機が 1 台も見つかりません", file=sys.stderr)
        sys.exit(1)
    print(f"検出: {len(slots)} 台")
    warn_duplicate_ids(slots)

    if not keep_coef:
        print("補正係数をリセット中 (T/RH/グローブ: A=1, B=0)...")
        failed = []
        for b, m, c, dev in slots:
            try:
                set_slot_coefs(ser, m, c, dev, {i: (1.0, 0.0) for i in OFFSET_ITEMS})
            except (RuntimeError, TimeoutError) as e:
                failed.append(f"ch{b:02d}({e})")
        if failed:
            print("ERROR: リセットに失敗: " + ", ".join(failed), file=sys.stderr)
            print("生値が記録できないため中止します。", file=sys.stderr)
            sys.exit(2)
        print("  完了")

    if out_path is None:
        tag = port.replace(os.sep, "_").replace("/", "_")
        out_path = f"thc_log_{tag}_{datetime.now():%Y%m%d_%H%M%S}.csv"

    items = ("t", "rh", "glb", "co2")
    end_time = time.time() + duration_min * 60 if duration_min > 0 else None

    with open(out_path, "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(["sensor_id", ""] + [dev for _, _, _, dev in slots for _ in items])
        w.writerow(["board_ch", ""] + [f"ch{b:02d}" for b, _, _, _ in slots for _ in items])
        w.writerow(["datetime", "elapsed_s"] + [i for _ in slots for i in items])

        print(f"記録開始: {out_path}  (interval {interval} sec"
              + (f", {duration_min} min" if end_time else ", Ctrl+C で終了") + ")")
        t_start = time.time()
        n = 0        # 周期番号 (欠測周期も含む。記録タイミングの基準)
        n_rows = 0   # 書き込んだ行数
        n_err = 0    # 欠測周期数
        try:
            while end_time is None or time.time() < end_time:
                t_row = t_start + n * interval   # 基準時刻からの累積で周期ずれを防ぐ
                wait = t_row - time.time()
                if wait > 0:
                    time.sleep(wait)
                stamp = datetime.now()
                n += 1

                try:
                    send_command(ser, "th_trigger", timeout=10.0)
                    time.sleep(1.1)   # 子機の single-shot 計測 (~500ms) + マージン
                    t_read = time.time()
                    vals = send_command(ser, "th_read_all", timeout=15.0)["vals"]
                    dt_read = time.time() - t_read
                except TimeoutError as e:
                    # 1 周期の失敗で長時間の記録を止めない。その周期の行は書かない
                    # (datetime 列の欠けで後から分かる)。
                    n_err += 1
                    print(f"\n  {stamp:%H:%M:%S}  WARNING: {e} (この周期は欠測)")
                    continue
                if dt_read > 3.0:
                    print(f"\n  {stamp:%H:%M:%S}  WARNING: 読み出しに {dt_read:.1f} sec")

                row = [stamp.strftime("%Y-%m-%d %H:%M:%S"), round(time.time() - t_start, 1)]
                n_ok = 0
                for _, m, c, _ in slots:
                    v = vals[m * 8 + c]
                    v = [None] * 7 if v is None else (list(v) + [None] * 7)[:7]
                    t, h, co2, g = v[0], v[1], v[2], v[6]
                    row += ["" if t is None else f"{t / 100:.2f}",
                            "" if h is None else f"{h / 100:.2f}",
                            "" if g is None else f"{g / 100:.2f}",
                            "" if co2 is None else co2]
                    if None not in (t, h, g):
                        n_ok += 1
                w.writerow(row)
                f.flush()
                n_rows += 1
                print(f"\r  {stamp:%H:%M:%S}  rows {n_rows}  valid {n_ok}/{len(slots)}"
                      + (f"  errors {n_err}" if n_err else "") + "  ",
                      end="", flush=True)
        except KeyboardInterrupt:
            pass
    print(f"\n記録終了: {n_rows} 行 (欠測 {n_err} 周期) → {out_path}")


def load_offsets(path):
    """Excel から書き出した B の CSV を読む。
    列: sensor_id, t_b, rh_b, glb_b (空欄の項目は変更しない)。
    戻り値: {sensor_id: {item: b}}。不正な行があれば全体を中止する。"""
    text = None
    for enc in ("utf-8-sig", "cp932"):
        try:
            with open(path, encoding=enc) as f:
                text = f.read()
            break
        except UnicodeDecodeError:
            continue
    if text is None:
        raise ValueError("文字コードを判別できません (UTF-8 か Shift-JIS で保存してください)")

    reader = csv.DictReader(text.splitlines())
    need = {"sensor_id", "t_b", "rh_b", "glb_b"}
    if reader.fieldnames is None or not need <= {h.strip() for h in reader.fieldnames}:
        raise ValueError(f"ヘッダが不正です。必要な列: {', '.join(sorted(need))}")

    offsets, errors = {}, []
    for lineno, row in enumerate(reader, start=2):
        row = {k.strip(): (v or "").strip() for k, v in row.items() if k}
        if not row["sensor_id"]:
            continue
        try:
            dev = int(float(row["sensor_id"]))
        except ValueError:
            errors.append(f"{lineno} 行目: sensor_id が数値ではありません")
            continue
        if dev in offsets:
            errors.append(f"{lineno} 行目: sensor_id {dev} が重複しています")
            continue
        items = {}
        for item in OFFSET_ITEMS:
            s = row[f"{item}_b"]
            if not s:
                continue
            try:
                b = float(s)
            except ValueError:
                errors.append(f"{lineno} 行目: {item}_b が数値ではありません ({s})")
                continue
            if abs(b) > OFFSET_LIMITS[item]:
                errors.append(f"{lineno} 行目: {item}_b={b} が許容範囲 "
                              f"±{OFFSET_LIMITS[item]} を超えています")
                continue
            items[item] = b
        offsets[dev] = items
    if errors:
        raise ValueError("\n".join(errors))
    return offsets


def run_set_coef(ser, path, assume_yes):
    """CSV の B を、この治具に接続されている子機へ Device ID で照合して書き込む。"""
    try:
        offsets = load_offsets(path)
    except (OSError, ValueError) as e:
        print(f"ERROR: {path}\n{e}", file=sys.stderr)
        sys.exit(1)

    slots = present_slots(read_probe_ids(ser))
    dups = warn_duplicate_ids(slots)
    targets = [s for s in slots if s[3] in offsets and s[3] not in dups]
    unlisted = [s for s in slots if s[3] not in offsets]
    on_jig = {s[3] for s in slots}
    not_here = [dev for dev in offsets if dev not in on_jig]

    print(f"CSV: {len(offsets)} 台分 / この治具: {len(slots)} 台接続")
    print(f"  書き込み対象: {len(targets)} 台")
    if unlisted:
        print(f"  CSV に無い子機 (書き込まない): "
              + ", ".join(f"ch{b:02d}({dev})" for b, _, _, dev in unlisted))
    if not_here:
        print(f"  この治具に無い ID: {len(not_here)} 台 (もう一方の治具の分なら正常)")
    if not targets:
        print("書き込み対象がありません。")
        return

    if not assume_yes:
        ans = input("\n書き込みますか? [y/N]: ").strip().lower()
        if ans != "y":
            print("中止しました。")
            return

    print("\n  ch    sensor_id      t_b     rh_b    glb_b")
    failed = []
    for b, m, c, dev in targets:
        items = {item: (1.0, bval) for item, bval in offsets[dev].items()}
        try:
            got = set_slot_coefs(ser, m, c, dev, items)
        except (RuntimeError, TimeoutError) as e:
            failed.append(b)
            print(f"  ch{b:02d}  {dev:>9}  FAIL ({e})")
            continue
        cells = "".join(f"{got[i][1]:+9.3f}" for i in OFFSET_ITEMS)
        print(f"  ch{b:02d}  {dev:>9}{cells}")

    print(f"\n完了: {len(targets) - len(failed)}/{len(targets)} 台")
    if failed:
        print("失敗: " + ", ".join(f"ch{b:02d}" for b in failed), file=sys.stderr)
        sys.exit(2)


def run_coef_show(ser):
    """接続されている全子機の補正係数を表示する。"""
    slots = present_slots(read_probe_ids(ser))
    warn_duplicate_ids(slots)
    print(f"\n{len(slots)} 台"
          "\n  ch    sensor_id     t (a, b)          rh (a, b)"
          "         glb (a, b)        co2 (a, b)")
    for b, m, c, _ in slots:
        resp = send_command(ser, "coef_get", {"mux": m, "ch": c}, timeout=5.0)
        if not resp.get("ok"):
            print(f"  ch{b:02d}  ERROR ({resp.get('error')})")
            continue
        co = decode_coefs(resp["coef"])
        cells = "".join(f"  {co[i][0]:6.3f} {co[i][1]:+7.3f}  "
                        for i in ("t", "rh", "glb", "co2"))
        print(f"  ch{b:02d}  {resp['dev']:>9}{cells}")
    print()


def print_rst_matrix(matrix):
    """rst_test の 64 文字マトリクスを表示する。
    行 = Low にした RST 番号、列 = 親バス上の 0x70-0x77 の ACK 有無。
    正常配線なら対角線だけ '.' になる。"""
    print("\n        0x70 0x71 0x72 0x73 0x74 0x75 0x76 0x77")
    for m in range(8):
        row = matrix[m * 8:(m + 1) * 8]
        cells = "    ".join("o" if c == "1" else "." for c in row)
        print(f"  RST{m + 1}   {cells}")
    print("\n  (o=ACK あり / .=ACK なし。RSTn の行で '.' になった列が、その RST に繋がる mux)")
    print()


def print_scan_grid(present, addr):
    """mux_scan の 64 文字ビット列を基板 ch01-ch64 順の 8x8 グリッドで表示する。"""
    n = present.count("1")
    bits = to_board_order(list(present))
    print(f"\naddr=0x{addr:02X}  detected: {n}/64")
    print("           +1  +2  +3  +4  +5  +6  +7  +8")
    for row in range(8):
        base = row * 8
        cells = "   ".join("o" if bits[base + j] == "1" else "."
                           for j in range(8))
        print(f"  ch{base + 1:02d}-{base + 8:02d}  {cells}")
    print()


def main():
    ap = argparse.ArgumentParser(description="th_calibrator USB client")
    ap.add_argument("--port", help="COM port (省略時は自動検出)")
    sub = ap.add_subparsers(dest="cmd", required=True)

    sub.add_parser("hello")
    sub.add_parser("ping")
    sub.add_parser("status")
    sub.add_parser("set_time")

    p_scan = sub.add_parser("mux_scan")
    p_scan.add_argument("--addr", type=lambda s: int(s, 0), default=0x11,
                        help="スキャン対象の 7bit I2C アドレス (既定 0x11)")

    sub.add_parser("i2c_scan", help="親バス直接スキャン (0x08-0x77)")
    p_rst = sub.add_parser("rst_test",
                           help="RST 1本ずつ Low にして mux アドレス対応を特定")
    p_rst.add_argument("--mask", type=lambda s: int(s, 0), default=None,
                       help="bit i = RST(i+1) を同時に Low へ (例: RST1+4+8 = 0x89)")

    p_int = sub.add_parser("set_interval")
    p_int.add_argument("sec", type=int, help="計測間隔 [sec]")

    p_mon = sub.add_parser("monitor", help="64 スロットの T/RH/CO2 ライブ表示")
    p_mon.add_argument("--interval", type=float, default=3.0,
                       help="更新周期 [sec] (既定 3.0、下限はトリガ+読出の ~2sec)")

    p_frc = sub.add_parser("frc", help="全スロット一括 CO2 校正 (FRC)")
    p_frc.add_argument("ppm", type=int,
                       help="基準 CO2 濃度 [ppm] (校正済み濃度計の読み値)")
    p_frc.add_argument("--yes", action="store_true", help="確認プロンプトを省略")
    p_frc.add_argument("--timeout", type=float, default=180.0,
                       help="完了待ちの上限 [sec] (既定 180)")

    p_log = sub.add_parser("log", help="校正用: T/RH/グローブ/CO2 を CSV に記録")
    p_log.add_argument("--out", help="出力 CSV (省略時 thc_log_<port>_<日時>.csv)")
    p_log.add_argument("--interval", type=float, default=10.0,
                       help="記録周期 [sec] (既定 10)")
    p_log.add_argument("--duration", type=float, default=0,
                       help="記録時間 [min] (既定 0 = Ctrl+C まで)")
    p_log.add_argument("--keep-coef", action="store_true",
                       help="開始時の係数リセット (A=1,B=0) を行わない")

    p_sc = sub.add_parser("set_coef", help="校正用: CSV の B を一斉設定 (A=1)")
    p_sc.add_argument("csv_path", help="列: sensor_id,t_b,rh_b,glb_b")
    p_sc.add_argument("--yes", action="store_true", help="確認プロンプトを省略")

    sub.add_parser("coef_show", help="全子機の補正係数を表示")

    p_dbg = sub.add_parser("th_debug", help="1 スロットの子機生レジスタ診断")
    p_dbg.add_argument("--bch", type=int,
                       help="基板チャンネル番号 (1-64)。--mux/--ch の代わりに指定可")
    p_dbg.add_argument("--mux", type=int)
    p_dbg.add_argument("--ch", type=int)
    p_dbg.add_argument("--trigger", action="store_true",
                       help="診断の前にトリガを打って 1.2 秒待つ")

    p_raw = sub.add_parser("raw")
    p_raw.add_argument("json_text", help='例: \'{"command":"echo","params":{"n":42}}\'')

    args = ap.parse_args()

    port = args.port or find_device_port()
    if port is None:
        print("ERROR: device not found", file=sys.stderr)
        sys.exit(1)

    with open_no_reset(port) as ser:
        if not args.port:
            pass  # find_device_port 内で疎通済み。直指定時のみ安定待ちを入れる。
        else:
            time.sleep(1.5)

        if args.cmd == "hello":
            resp = send_command(ser, "hello")
        elif args.cmd == "ping":
            resp = send_command(ser, "ping")
        elif args.cmd == "status":
            resp = send_command(ser, "get_status")
        elif args.cmd == "set_time":
            epoch = int(datetime.now(timezone.utc).timestamp())
            resp = send_command(ser, "set_time", {"epoch": epoch})
            print(f"epoch={epoch} ({datetime.fromtimestamp(epoch)})")
        elif args.cmd == "mux_scan":
            # 64 slot × (select + probe + deselect) なので長めの timeout
            resp = send_command(ser, "mux_scan", {"addr": args.addr}, timeout=30.0)
            if resp.get("ok"):
                print_scan_grid(resp["present"], resp["addr"])
        elif args.cmd == "monitor":
            run_monitor(ser, args.interval)
            return
        elif args.cmd == "frc":
            run_frc(ser, args.ppm, args.yes, timeout_sec=args.timeout)
            return
        elif args.cmd == "log":
            run_log(ser, port, args.out, args.interval, args.duration, args.keep_coef)
            return
        elif args.cmd == "set_coef":
            run_set_coef(ser, args.csv_path, args.yes)
            return
        elif args.cmd == "coef_show":
            run_coef_show(ser)
            return
        elif args.cmd == "th_debug":
            if args.bch is not None:
                if not (1 <= args.bch <= 64):
                    print("ERROR: --bch は 1-64", file=sys.stderr)
                    sys.exit(1)
                mux, ch = board_to_slot(args.bch)
            elif args.mux is not None and args.ch is not None:
                mux, ch = args.mux, args.ch
            else:
                print("ERROR: --bch か --mux/--ch を指定", file=sys.stderr)
                sys.exit(1)
            print(f"slot: board ch{slot_to_board(mux, ch):02d} = mux{mux}-ch{ch}")
            if args.trigger:
                send_command(ser, "th_trigger", timeout=10.0)
                time.sleep(1.2)
            resp = send_command(ser, "th_debug",
                                {"mux": mux, "ch": ch}, timeout=10.0)
            if resp.get("ok"):
                s1, s2 = resp["status1"], resp["status2"]
                st = resp["stcc4_state"]
                stcc4_names = STCC4_STATE_NAMES
                print(f"\nstatus1 = 0x{s1:02X} "
                      f"(stale: T={bool(s1 & 1)} RH={bool(s1 & 2)} "
                      f"CO2={bool(s1 & 4)} GLB={bool(s1 & 8)})")
                print(f"status2 = {s2} "
                      f"({'サンプル READY' if s2 == 1 else 'トリガ待ち/計測中'})")
                print(f"stcc4_state = {st} ({stcc4_names.get(st, '?')})")
                print(f"data_count = {resp['data_count']}\n")
                if s2 == 0:
                    print("→ 子機がトリガを処理していない (main loop 停止/未トリガ)")
                elif s1 == 0xFF:
                    print("→ 計測は実行されたが子機内で STCC4 通信が全滅")
                elif st == 6:
                    print("→ conditioning 実行中。電源投入から ~22 秒待てば解消")
        elif args.cmd == "i2c_scan":
            resp = send_command(ser, "i2c_scan", timeout=30.0)
            if resp.get("ok"):
                found = resp.get("found", [])
                print("\nfound: " + (", ".join(f"0x{a:02X}" for a in found)
                                     if found else "(none)") + "\n")
        elif args.cmd == "rst_test":
            if args.mask is not None:
                resp = send_command(ser, "rst_test", {"mask": args.mask},
                                    timeout=30.0)
                if resp.get("ok"):
                    held = [f"RST{i + 1}" for i in range(8)
                            if args.mask & (1 << i)]
                    print(f"\nheld low: {'+'.join(held) or '(none)'}")
                    print("        0x70 0x71 0x72 0x73 0x74 0x75 0x76 0x77")
                    cells = "    ".join(
                        "o" if c == "1" else "." for c in resp["present"])
                    print(f"  bus    {cells}\n")
            else:
                resp = send_command(ser, "rst_test", timeout=30.0)
                if resp.get("ok"):
                    print_rst_matrix(resp["matrix"])
        elif args.cmd == "set_interval":
            resp = send_command(ser, "set_interval", {"sec": args.sec})
        elif args.cmd == "raw":
            req = json.loads(args.json_text)
            resp = send_command(ser, req["command"], req.get("params"),
                                cmd_id=req.get("id", 1))
        else:
            raise AssertionError(args.cmd)

        print(json.dumps(resp, indent=2, ensure_ascii=False))
        if not resp.get("ok"):
            sys.exit(2)


if __name__ == "__main__":
    main()
