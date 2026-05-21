"""
M-Logger v4 のフラッシュ記録データを論理消去する (USB-CDC 経由)。

protocol_v4.md §4.8 の `clear_data` コマンドを送る。論理消去 = 世代番号
インクリメント。旧データは flash 上に物理的には残るが、以降の dump で
load_data.py がデフォルト動作 (最新世代のみ抽出) で除外する。

使い方:
    python clear_data.py             # COM ポート自動検出
    python clear_data.py COM5        # 明示指定
"""
import argparse
import json
import sys
import time

import serial
import serial.tools.list_ports


BAUD_RATE       = 115200
CONNECT_TIMEOUT = 1.5


def open_no_reset(port, baud=BAUD_RATE, timeout=CONNECT_TIMEOUT):
    """DTR/RTS を非アサートで open して AVR DU32 の reset 経路を踏まないようにする。
    pyserial デフォルトでは open 時に DTR/RTS がアサートされ、USB CDC reconnect と
    合わせて MCU reset を引き起こす環境がある。"""
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


def send_command(ser, payload, timeout=3.0):
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
        description="Clear M-Logger v4 stored data (generation increment).")
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

            hello = send_command(ser, {"v": 1, "id": 1, "command": "hello"})
            if not hello or "result" not in hello:
                print(f"[ERROR] hello failed: {hello}")
                return 1
            print(f"  device   : {hello['result'].get('device')} "
                  f"v{hello['result'].get('firmware_version')}")
            print(f"  hardware : {hello['result'].get('hardware_id')}")
            if hello['result'].get("logging"):
                print("[ABORT] device is currently logging — stop logging first")
                return 1

            print("\nSending clear_data...")
            resp = send_command(ser, {"v": 1, "id": 2, "command": "clear_data"},
                                timeout=5.0)
            if resp is None:
                print("[ERROR] no response (timeout)")
                return 1
            if "error" in resp:
                print(f"[ERROR] {resp['error']}")
                return 1
            if "result" not in resp:
                print(f"[ERROR] unexpected response: {resp}")
                return 1
            print("  OK (generation incremented).")

            # === 診断: 同一 session 内で dump の count (= rec_latest) を確認 ===
            # 0 が返れば LC_ClearData() は正常に rec_latest をリセットしている。
            # 0 でなければ firmware 側で rec_latest=0 が効いていない。
            # ここで一度 close → 別 session で 0 以外が返るようなら reboot 仮説。
            print("\n[diag] checking rec_latest in same session...")
            dump_resp = send_command(ser, {"v": 1, "id": 3, "command": "dump"},
                                     timeout=5.0)
            if dump_resp and "result" in dump_resp:
                cnt = dump_resp["result"].get("count")
                print(f"  rec_latest after clear = {cnt}")
                if cnt == 0:
                    print("  → OK: clear_data reset rec_latest. ")
                    print("    If a separate load_data.py run shows count>0,")
                    print("    the close→reopen is rebooting the device (DTR/USB reset).")
                else:
                    print("  → BAD: firmware did not reset rec_latest.")
                # dump はヘッダのみ確認したいが、binary stream + dump_end も
                # 来るので readout を完了させてバッファをクリアしておく。
                rec_size = dump_resp["result"].get("record_size", 22)
                total    = cnt * rec_size
                if total > 0:
                    old_to = ser.timeout
                    ser.timeout = 5.0
                    try:
                        ser.read(total)
                    finally:
                        ser.timeout = old_to
                # dump_end を消費
                end = time.time() + 3.0
                while time.time() < end:
                    line = ser.readline().decode('utf-8', errors='ignore').strip()
                    if line.startswith('{') and '"dump_end"' in line:
                        break
            else:
                print(f"  [WARN] dump diag failed: {dump_resp}")

            print("\nNote: data is logically cleared. Old records remain in flash")
            print("      but load_data.py default mode hides them via gen filter.")
    except serial.SerialException as e:
        print(f"Serial error: {e}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
