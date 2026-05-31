"""
USB-CDC 経由で smp イベントを連続観察するスクリプト。
firmware が本当に 1 sec 周期で smp を送出しているかの timing 確認に使う。

使い方:
    python smp_watch.py                  # ポート自動検出
    python smp_watch.py COM5             # 明示指定
    python smp_watch.py COM5 30          # 30 秒観察 (default 20)
    python smp_watch.py COM5 30 1        # interval=1 で設定して観察 (default 1)

出力例 (interval=1 で 1 sec 周期):
    [t+0.05]  ts=1764649800  dt=---  data={'t': 25.3, 'h': 45.2, 'g': 25.1}
    [t+1.04]  ts=1764649801  dt=1.0  data={...}
    [t+2.04]  ts=1764649802  dt=1.0  data={...}

interval=1 設定なのに dt が 2.0 になっていれば firmware 側で skip が発生している。
data のキー (t/h/g/c/vel/l) が欠落していれば valid_flags が立っていない
(=child の status1 で stale 判定が出ている)。
"""
import json
import sys
import time

import serial
import serial.tools.list_ports


BAUD_RATE = 115200
TIMEOUT_SEC = 0.5
DEFAULT_OBSERVE_SEC = 20
DEFAULT_INTERVAL = 1


def open_no_reset(port, baud=BAUD_RATE, timeout=TIMEOUT_SEC):
    """DTR/RTS 抑制 open (AVR64DU32 reset 回避)"""
    ser = serial.Serial()
    ser.port = port
    ser.baudrate = baud
    ser.timeout = timeout
    ser.dtr = False
    ser.rts = False
    ser.open()
    return ser


def find_device_port():
    """v4 hello で M-Logger の COM ポートを探す"""
    print("Scanning ports for M-Logger...")
    ports = list(serial.tools.list_ports.comports())
    probe = (json.dumps({"v": 1, "id": 1, "command": "hello"}) + '\n').encode('utf-8')

    for p in ports:
        if "Bluetooth" in p.description:
            continue
        try:
            print(f"  Checking {p.device}...", end="", flush=True)
            with open_no_reset(p.device, timeout=1.5) as ser:
                time.sleep(1.5)
                ser.reset_input_buffer()
                ser.write(probe)
                end = time.time() + 2.0
                while time.time() < end:
                    line = ser.readline().decode('utf-8', errors='ignore').strip()
                    if not line:
                        continue
                    try:
                        resp = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if (isinstance(resp, dict)
                            and resp.get("result", {}).get("device") == "M-Logger"):
                        print(" Found!")
                        return p.device
            print(" no response.")
        except (OSError, serial.SerialException):
            print(" failed to open.")
    return None


def send_json(ser, payload, wait_response=True, timeout_s=5.0):
    """JSON コマンドを送って response を待つ。event 行 (smp/ready 等) は skip。

    timeout_s: 応答待ち時間 (default 5sec)。set_settings の応答が大きい場合や、
    firmware が event を吐いている間でも response を取りこぼさないよう余裕を持つ。
    """
    msg = json.dumps(payload) + '\n'
    ser.reset_input_buffer()       # 旧バッファを掃除 (event 残骸など)
    ser.write(msg.encode('utf-8'))
    if not wait_response:
        return None
    expected_id = payload.get("id")
    end = time.time() + timeout_s
    skipped_events = 0
    while time.time() < end:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not line:
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        # event 行は飛ばして command response を待つ
        if isinstance(resp, dict) and resp.get("event"):
            skipped_events += 1
            continue
        # id が一致するレスポンスのみ受理 (前の request の遅延レスポンスを取らない)
        if expected_id is not None and resp.get("id") != expected_id:
            continue
        return resp
    if skipped_events:
        print(f"    (note: {skipped_events} event lines were skipped while waiting)")
    return None


def configure_intervals(ser, interval):
    """全センサを enabled=True, interval=指定値 にセット。

    NOTE: set_settings の JSON 応答は ~290B で USB-CDC TX buffer の都合により
    270B 前後で truncate されるため、応答パースは諦めて get_settings で
    反映を確認する方式を採る。
    """
    print(f"Configuring all sensors to interval={interval}...")
    # 応答パースは諦めて投げっぱなし (firmware 側の処理自体は成功している前提)
    msg = json.dumps({
        "v": 1, "id": 100, "command": "set_settings",
        "params": {
            "t_dry":       {"enabled": True,  "interval": interval},
            "humidity":    {"enabled": True,  "interval": interval},
            "t_glb":       {"enabled": True,  "interval": interval},
            "velocity":    {"enabled": False, "interval": interval},
            "illuminance": {"enabled": False, "interval": interval},
            "co2":         {"enabled": True,  "interval": interval},
        }
    }) + '\n'
    ser.reset_input_buffer()
    ser.write(msg.encode('utf-8'))
    time.sleep(0.8)  # firmware の処理 + EEPROM 書込 (set_settings は EEPROM save あり)
    ser.reset_input_buffer()  # truncated response の残骸を捨てる

    # get_settings で反映を確認 (こちらは応答 ~286B でギリギリ通る)
    resp = send_json(ser, {"v": 1, "id": 101, "command": "get_settings"}, timeout_s=5.0)
    if resp is None or "result" not in resp:
        print(f"  [!] get_settings verify failed: {resp}")
        return False
    r = resp["result"]
    ok = True
    for s in ["t_dry", "humidity", "t_glb", "co2"]:
        if r.get(s, {}).get("interval") != interval:
            print(f"  [!] {s}.interval = {r.get(s, {}).get('interval')} (expected {interval})")
            ok = False
        if not r.get(s, {}).get("enabled"):
            print(f"  [!] {s}.enabled = False (expected True)")
            ok = False
    if ok:
        print(f"  [OK] verified all 4 sensors enabled with interval={interval}")
    return ok


def start_usb_logging(ser):
    print("Starting logging (transport=usb only)...")
    resp = send_json(ser, {
        "v": 1, "id": 200, "command": "start_logging",
        "params": {
            "transports": {"zigbee": False, "ble": False, "flash": False, "usb": True},
            "mode": "once"
        }
    })
    if resp is None or "result" not in resp:
        print(f"  [!] start_logging failed: {resp}")
        return False
    print(f"  [OK]")
    return True


def stop_logging(ser):
    print("\nStopping logging...")
    try:
        ser.reset_input_buffer()
        resp = send_json(ser, {"v": 1, "id": 201, "command": "stop_logging"})
        if resp and "result" in resp:
            print(f"  [OK]")
        else:
            print(f"  [!] stop_logging response: {resp}")
    except serial.SerialException as e:
        print(f"  [!] stop failed: {e}")


def observe_smp(ser, duration_s):
    """smp イベントを duration_s 秒間収集して timing 統計を表示"""
    print(f"\n=== Observing smp events for {duration_s} sec ===")
    print("[host_t+N.NN]  ts(unix)     dt(sec)  data{keys}")
    print("-" * 80)

    t0 = time.time()
    end_t = t0 + duration_s
    smp_events = []
    other_lines = 0

    while time.time() < end_t:
        try:
            line_bytes = ser.readline()
            if not line_bytes:
                continue
            line = line_bytes.decode('utf-8', errors='ignore').strip()
            if not line:
                continue
            try:
                ev = json.loads(line)
            except json.JSONDecodeError:
                other_lines += 1
                continue
            if ev.get("event") != "smp":
                other_lines += 1
                continue

            host_t = time.time() - t0
            ts = ev.get("ts")
            data = ev.get("data", {})
            keys = ",".join(sorted(data.keys()))

            if smp_events:
                prev_ts = smp_events[-1]["ts"]
                prev_host_t = smp_events[-1]["host_t"]
                dt_ts = ts - prev_ts if (ts is not None and prev_ts is not None) else None
                dt_host = host_t - prev_host_t
                dt_str = f"{dt_ts}" if dt_ts is not None else "?"
                print(f"[t+{host_t:5.2f}]  ts={ts}  dt_ts={dt_str}  dt_host={dt_host:.2f}  "
                      f"keys=[{keys}]  data={data}")
            else:
                print(f"[t+{host_t:5.2f}]  ts={ts}  dt_ts=---  dt_host=----  "
                      f"keys=[{keys}]  data={data}")

            smp_events.append({"ts": ts, "host_t": host_t, "data": data})

        except serial.SerialException as e:
            print(f"\n[!] read failed: {e}")
            break

    # 統計
    print("-" * 80)
    print(f"\n=== Summary ===")
    print(f"  total smp events  : {len(smp_events)}")
    print(f"  non-smp lines     : {other_lines}")
    print(f"  duration          : {duration_s} sec")
    if len(smp_events) >= 2:
        # ts 差分
        ts_diffs = []
        for i in range(1, len(smp_events)):
            if smp_events[i]["ts"] is not None and smp_events[i-1]["ts"] is not None:
                ts_diffs.append(smp_events[i]["ts"] - smp_events[i-1]["ts"])
        if ts_diffs:
            from collections import Counter
            cnt = Counter(ts_diffs)
            print(f"  ts(unix) deltas   : {dict(cnt)}")
            avg_ts = sum(ts_diffs) / len(ts_diffs)
            print(f"  avg dt_ts         : {avg_ts:.2f} sec")
            print(f"  expected dt_ts    : (= interval setting)")

        # host 受信間隔
        host_diffs = [smp_events[i]["host_t"] - smp_events[i-1]["host_t"]
                      for i in range(1, len(smp_events))]
        avg_host = sum(host_diffs) / len(host_diffs)
        print(f"  avg dt_host       : {avg_host:.3f} sec (実際の受信間隔)")

        # キー欠落率
        all_keys = set()
        for ev in smp_events:
            all_keys.update(ev["data"].keys())
        print(f"  observed keys     : {sorted(all_keys)}")
        for k in sorted(all_keys):
            present = sum(1 for ev in smp_events if k in ev["data"])
            pct = 100.0 * present / len(smp_events)
            print(f"    '{k}'           : {present}/{len(smp_events)} ({pct:.0f}%)")

    return len(smp_events)


def main():
    port = None
    duration = DEFAULT_OBSERVE_SEC
    interval = DEFAULT_INTERVAL

    if len(sys.argv) > 1:
        port = sys.argv[1]
    if len(sys.argv) > 2:
        duration = int(sys.argv[2])
    if len(sys.argv) > 3:
        interval = int(sys.argv[3])

    if port is None:
        port = find_device_port()
        if not port:
            print("Error: M-Logger not found. Specify COM port explicitly.")
            sys.exit(1)
        print(f"Detected at: {port}")

    print(f"\n=== smp_watch: port={port}, duration={duration}s, interval={interval}s ===\n")

    try:
        with open_no_reset(port, timeout=TIMEOUT_SEC) as ser:
            time.sleep(2.0)
            ser.reset_input_buffer()

            # 既に logging 中なら一旦止める (iPhone/BLE で開始したまま残ってる場合に対応)
            print("Ensuring logging is stopped before reconfigure...")
            send_json(ser, {"v": 1, "id": 99, "command": "stop_logging"}, timeout_s=2.0)
            time.sleep(0.2)

            if not configure_intervals(ser, interval):
                return 1
            if not start_usb_logging(ser):
                return 1

            try:
                observe_smp(ser, duration)
            finally:
                stop_logging(ser)

    except serial.SerialException as e:
        print(f"Serial Error: {e}")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
