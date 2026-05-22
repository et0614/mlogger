"""
M-Logger v4 の内蔵フラッシュ (W25Q256, 32MB) を chip erase で完全初期化する (USB-CDC 経由)。

通常運用では使わない。以下の特殊状況での復旧手段:
  - firmware 書き換え時に EEPROM がクリアされ、EM_generationNumber (= 1) が flash に
    残っている旧データの generation と衝突して dump が異常な件数を返す場合
  - ノイズ等で EEPROM 上の generation 値が壊れた場合

chip erase は約 40〜80 秒の blocking (W25Q256)。処理中は本体の赤 LED が点灯し続ける。
完了後は EM_generationNumber=1 / rec_latest=0 の工場初期化相当の状態になる。

使い方:
    python erase_flash.py             # COM ポート自動検出
    python erase_flash.py COM5        # 明示指定
"""
import argparse
import json
import sys
import time

import serial
import serial.tools.list_ports


BAUD_RATE       = 115200
CONNECT_TIMEOUT = 1.5
ERASE_TIMEOUT   = 300.0   # W25Q256 chip erase は通常 40 秒・最大 80 秒、余裕を持って 300 秒


def open_no_reset(port, baud=BAUD_RATE, timeout=CONNECT_TIMEOUT):
    """DTR/RTS 非アサートで AVR DU32 の reset を抑制して open する。"""
    ser = serial.Serial()
    ser.port = port
    ser.baudrate = baud
    ser.timeout = timeout
    ser.dtr = False
    ser.rts = False
    ser.open()
    return ser


def find_device_port():
    print("Scanning ports...")
    probe = (json.dumps({"v": 1, "id": 1, "command": "hello"}) + '\n').encode('utf-8')
    for p in serial.tools.list_ports.comports():
        try:
            print(f"  Checking {p.device}...", end="", flush=True)
            if "Bluetooth" in (p.description or ""):
                print(" Skipped (Bluetooth).")
                continue
            with open_no_reset(p.device) as ser:
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
                print(" No M-Logger response.")
        except (OSError, serial.SerialException):
            print(" Failed to open.")
    return None


def send_command(ser, payload, timeout):
    msg = json.dumps(payload, ensure_ascii=False) + '\n'
    ser.reset_input_buffer()
    ser.write(msg.encode('utf-8'))
    target_id = payload.get("id")
    end = time.time() + timeout
    while time.time() < end:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not line:
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(resp, dict) and resp.get("id") == target_id:
            return resp
    return None


def main():
    ap = argparse.ArgumentParser(
        description="Chip-erase the M-Logger v4 internal flash (recovery tool).")
    ap.add_argument("port", nargs="?", default=None,
                    help="COM port (auto-detect if omitted)")
    args = ap.parse_args()

    port = args.port or find_device_port()
    if not port:
        print("Error: M-Logger not found.")
        return 1

    print(f"Connecting to {port}...")
    try:
        with open_no_reset(port) as ser:
            time.sleep(2.0)

            hello = send_command(ser, {"v": 1, "id": 1, "command": "hello"}, timeout=3.0)
            if not hello or "result" not in hello:
                print(f"[ERROR] hello failed: {hello}")
                return 1
            print(f"  device   : {hello['result'].get('device')} v{hello['result'].get('firmware_version')}")
            print(f"  hardware : {hello['result'].get('hardware_id')}")
            if hello['result'].get("logging"):
                print("[ABORT] device is currently logging - stop logging first")
                return 1

            print()
            print("This will COMPLETELY ERASE the internal flash (W25Q256).")
            print("All stored records will be lost. Continue? [y/N]: ", end="", flush=True)
            ans = sys.stdin.readline().strip().lower()
            if ans not in ("y", "yes"):
                print("Aborted.")
                return 0

            print(f"\nSending erase_flash (this takes about 40-80 seconds)...")
            print("Red LED on the device will stay ON during the operation.")
            t0 = time.time()
            resp = send_command(ser, {"v": 1, "id": 2, "command": "erase_flash"},
                                timeout=ERASE_TIMEOUT)
            elapsed = time.time() - t0

            if resp is None:
                print(f"[ERROR] no response within {ERASE_TIMEOUT:.0f}s (elapsed {elapsed:.1f}s)")
                return 1
            if "error" in resp:
                print(f"[ERROR] {resp['error']}")
                return 1
            if "result" not in resp:
                print(f"[ERROR] unexpected response: {resp}")
                return 1
            print(f"  OK (elapsed {elapsed:.1f}s, generation reset to 1, rec_latest = 0).")
    except serial.SerialException as e:
        print(f"Serial error: {e}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
