#!/usr/bin/env python3
"""th_calibrator 治具の USB-CDC JSON コマンドクライアント。

使い方:
    python thc_client.py hello                  # ポート自動検出して hello
    python thc_client.py ping
    python thc_client.py mux_scan               # 64 スロットスキャン (既定 addr=0x11)
    python thc_client.py mux_scan --addr 0x70   # TCA9548A 自体の応答確認
    python thc_client.py status                 # get_status
    python thc_client.py set_time               # PC 時刻を書き込む
    python thc_client.py raw '{"command":"echo","params":{"n":42}}'
    python thc_client.py --port COM5 hello      # ポート指定

依存: pyserial  (pip install pyserial)
"""

import argparse
import json
import os
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
    while time.time() < end:
        line = ser.readline().decode("utf-8", errors="replace").strip()
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


def render_monitor_frame(vals, cycle):
    """th_read_all の vals (64 要素、(mux,ch) 順) を基板 ch01-ch64 順に並べ替え、
    3 枚の 8x8 グリッドで描画した文字列を返す。
    vals[m*8+c] = [t_c100, rh_100, co2_ppm, status1, status2, stcc4_state] or None。
    値要素の None は stale。セル表示は状態で塗り分ける:
      ----  = 子機不在 (I2C NACK)
      cond  = STCC4 conditioning 実行中 (電源投入後 ~22 秒、正常な待ち状態)
      wait  = トリガ未処理 or 計測中 (status2=0)
      ERR!  = 子機は居るが子機内で STCC4 通信全滅 (status1=0xFF)
      stal  = その値だけ stale (センサ部分異常)"""
    # 旧ファーム (3 要素 [t,h,co2]) の応答でも落ちないように 6 要素へ正規化する。
    # status 系が無い場合は None 埋め (状態表示は stal に倒れる)。
    vals = [None if v is None else (list(v) + [None] * 6)[:6] for v in vals]

    # (mux,ch) 順 → 基板 ch01-ch64 順へ並べ替えて表示する
    bvals = to_board_order(vals)

    def cell(bidx, kind):
        v = bvals[bidx]
        if v is None:
            return "  ---- "
        t, h, c, s1, s2, st4 = v
        val = {"t": t, "h": h, "c": c}[kind]
        if val is not None:
            if kind == "t":
                return f"{val / 100:6.2f} "
            if kind == "h":
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
        f"detected {n_present}/64  ready {n_ready}  cond {n_cond}   (Ctrl+C で終了)",
        "  ---- =不在  cond =warmup中(~22s)  wait =計測中  ERR! =子機内STCC4全滅  stal =部分異常",
        "",
    ]
    for title, kind in (("温度 [°C]", "t"), ("湿度 [%RH]", "h"), ("CO2 [ppm]", "c")):
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
    try:
        while True:
            t0 = time.time()
            send_command(ser, "th_trigger", timeout=10.0)
            time.sleep(1.1)   # 子機の single-shot 計測 (~500ms) + マージン
            resp = send_command(ser, "th_read_all", timeout=15.0)
            cycle += 1
            frame = render_monitor_frame(resp["vals"], cycle)
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
