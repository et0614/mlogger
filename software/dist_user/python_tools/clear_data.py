"""
Clear the recorded data on an M-Logger v4 (over USB-CDC).

Usage:
    python clear_data.py             # auto-detect COM port
    python clear_data.py COM5        # explicit COM port
    python clear_data.py -y          # skip the confirmation prompt
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
    """Open the port with DTR/RTS de-asserted so the AVR DU32 reset path is
    not triggered. pyserial asserts DTR/RTS on open by default, which combined
    with the USB CDC reconnect resets the MCU on some hosts."""
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
    ap.add_argument("-y", "--yes", action="store_true",
                    help="skip the confirmation prompt")
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
                print("[ABORT] device is currently logging — stop the measurement first")
                return 1

            count = send_command(ser, {"v": 1, "id": 2, "command": "get_count"})
            if count and "result" in count:
                n = count["result"].get("count", 0)
                print(f"  stored   : {n} records")
                if n == 0:
                    print("Nothing to clear.")
                    return 0

            if not args.yes:
                ans = input("\nClear all recorded data on this device? [y/N]: ")
                if ans.strip().lower() not in ("y", "yes"):
                    print("Cancelled.")
                    return 0

            print("\nSending clear_data...")
            resp = send_command(ser, {"v": 1, "id": 3, "command": "clear_data"},
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

            # Verify: the record counter must now read zero
            count = send_command(ser, {"v": 1, "id": 4, "command": "get_count"})
            if count and "result" in count and count["result"].get("count") == 0:
                print("  OK — data cleared (record count is now 0).")
            else:
                print(f"[WARN] could not verify the cleared state: {count}")

    except serial.SerialException as e:
        print(f"Serial error: {e}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
