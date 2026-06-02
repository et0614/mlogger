"""
USB-CDC で M-Logger に「全センサ 1sec 計測」を指示し、流れてくる smp イベントを
1 件ずつ raw JSON で表示し続けるツール。
センサ抜き差し時にどのような smp が来ているか目視確認する用途。

使い方:
    python smp_monitor.py            # ポート自動検出
    python smp_monitor.py COM5       # ポート明示
    python smp_monitor.py COM5 5     # interval=5sec で計測 (default 1)

実行内容:
    1. M-Logger に接続 (DTR 抑制 open)
    2. 念のため stop_logging
    3. set_settings で全センサ enabled=True, interval 指定
    4. start_logging (USB only, mode=once)
    5. smp イベントだけを raw JSON で 1 件 / 行で print (Ctrl+C まで永続)
    6. 終了時に stop_logging

抜き差し時の期待値:
    接続 OK   : data に t/h/g/c 4 keys, dc/wu なし
    ウォームアップ : data に g のみ, "wu":["g"]
    切断    : data に general 系なし, "dc":["g"]
"""
import json
import sys
import time

import serial
import serial.tools.list_ports


BAUD_RATE = 115200
TIMEOUT_SEC = 0.5
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


def send_json(ser, payload, timeout_s=3.0):
    """JSON コマンド送信 + 応答待ち (event 行は skip)"""
    msg = json.dumps(payload) + '\n'
    ser.reset_input_buffer()
    ser.write(msg.encode('utf-8'))
    expected_id = payload.get("id")
    end = time.time() + timeout_s
    while time.time() < end:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not line:
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(resp, dict) and resp.get("event"):
            continue
        if expected_id is not None and resp.get("id") != expected_id:
            continue
        return resp
    return None


def configure_intervals(ser, interval):
    """全カテゴリを enabled=True, interval=指定値 にセット。

    firmware の set_settings はカテゴリ単位 (general/velocity/illuminance) のみ。
    general は内部で t/h/glb/co2 を一括設定する。
    """
    print(f"Configuring all categories: enabled=True, interval={interval}sec ...")
    resp = send_json(ser, {
        "v": 1, "id": 100, "command": "set_settings",
        "params": {
            "general":     {"enabled": True, "interval": interval},
            "velocity":    {"enabled": True, "interval": interval},
            "illuminance": {"enabled": True, "interval": interval},
        }
    }, timeout_s=5.0)
    if resp is None or "result" not in resp:
        print(f"  [!] set_settings failed: {resp}")
        return False

    r = resp["result"]
    ok = True
    for s in ["general", "velocity", "illuminance"]:
        actual_i = r.get(s, {}).get("interval")
        actual_e = r.get(s, {}).get("enabled")
        if actual_i != interval or actual_e is not True:
            print(f"  [!] {s}: enabled={actual_e}, interval={actual_i} (expected True, {interval})")
            ok = False
    if ok:
        print(f"  [OK] verified general/velocity/illuminance at interval={interval}sec")
    return ok


def start_usb_logging(ser):
    print("Starting USB-only logging...")
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
    print("  [OK]")
    return True


def stop_logging(ser):
    print("\nStopping logging...")
    try:
        resp = send_json(ser, {"v": 1, "id": 201, "command": "stop_logging"}, timeout_s=2.0)
        if resp and "result" in resp:
            print("  [OK]")
        else:
            print(f"  [!] stop response: {resp}")
    except serial.SerialException as e:
        print(f"  [!] stop failed: {e}")


def monitor_smp(ser):
    """Ctrl+C まで smp イベントのみを raw JSON で print"""
    print("\n=== Monitoring smp events (Ctrl+C to quit) ===")
    print("-" * 80)
    while True:
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
                continue
            if isinstance(ev, dict) and ev.get("event") == "smp":
                print(json.dumps(ev, ensure_ascii=False))
        except KeyboardInterrupt:
            print("\n[interrupted]")
            return
        except serial.SerialException as e:
            print(f"\n[!] read failed: {e}")
            return


def main():
    port = None
    interval = DEFAULT_INTERVAL

    if len(sys.argv) > 1:
        port = sys.argv[1]
    if len(sys.argv) > 2:
        interval = int(sys.argv[2])

    if port is None:
        port = find_device_port()
        if not port:
            print("Error: M-Logger not found. Specify COM port explicitly.")
            return 1
        print(f"Detected at: {port}")

    try:
        with open_no_reset(port, timeout=TIMEOUT_SEC) as ser:
            time.sleep(2.0)
            ser.reset_input_buffer()

            # 念のため既存 logging を止める
            send_json(ser, {"v": 1, "id": 99, "command": "stop_logging"}, timeout_s=2.0)
            time.sleep(0.2)

            if not configure_intervals(ser, interval):
                return 1
            if not start_usb_logging(ser):
                return 1

            try:
                monitor_smp(ser)
            finally:
                stop_logging(ser)

    except serial.SerialException as e:
        print(f"Serial Error: {e}")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
