"""
M-Logger device check tool (USB).

Verifies that an M-Logger is connected and working:
  1. Scans serial ports and locates the M-Logger
  2. Shows device info (name, hardware ID, firmware version)
  3. Shows battery voltage
  4. Shows attached probes and the number of stored records
  5. Runs a short live measurement and shows current sensor readings

Usage:
    python check_device.py              # auto-detect COM port
    python check_device.py COM5         # explicit COM port
    python check_device.py --no-live    # skip the live measurement test
"""
import argparse
import json
import sys
import time

import serial
import serial.tools.list_ports


BAUD_RATE       = 115200
CONNECT_TIMEOUT = 1.5
WARMUP_TIMEOUT  = 90.0   # CO2 conditioning ~25 s + velocity heater ~10 s, with margin
LIVE_SAMPLES    = 3      # samples to collect after warm-up completes

# smp event category ids used in "wu" (warming up) / "dc" (disconnected) lists
CATEGORY_NAMES = {
    "g": "T/RH/CO2 probe",
    "v": "velocity probe",
}


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


_next_id = [10]


def send_cmd(ser, command, params=None, timeout=5.0):
    """Send one JSON command and wait for the response with the same id.
    Event lines (smp, ready, ...) arriving in between are skipped."""
    _next_id[0] += 1
    req = {"v": 1, "id": _next_id[0], "command": command}
    if params is not None:
        req["params"] = params
    ser.write((json.dumps(req) + "\n").encode("utf-8"))
    end = time.time() + timeout
    while time.time() < end:
        line = ser.readline().decode("utf-8", errors="ignore").strip()
        if not line or not line.startswith("{"):
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(resp, dict) and resp.get("id") == _next_id[0]:
            return resp
    return None


def cmd_result(ser, command, params=None, timeout=5.0):
    """send_cmd + unwrap 'result'. Returns None on error/timeout."""
    resp = send_cmd(ser, command, params, timeout)
    if resp is None or "result" not in resp:
        return None
    return resp["result"]


# ============================================================
# Port scan (diagnostic listing + M-Logger detection)
# ============================================================
def probe_port(device):
    """Try to open a port and talk v4 hello. Returns (status, hello_result).
    status: 'found' | 'no_response' | 'busy' | 'error'"""
    try:
        with open_no_reset(device) as ser:
            time.sleep(1.5)
            ser.reset_input_buffer()
            hello = cmd_result(ser, "hello", timeout=2.0)
            if hello and hello.get("device") == "M-Logger":
                return "found", hello
            return "no_response", None
    except serial.SerialException as e:
        if "Access is denied" in str(e) or "PermissionError" in str(e):
            return "busy", None
        return "error", None
    except OSError:
        return "error", None


def scan_ports():
    """List all serial ports with a connection test, return the M-Logger port."""
    print("=== Serial port scan ===")
    ports = list(serial.tools.list_ports.comports())

    if not ports:
        print("[!] No serial ports found.")
        print("    -> Check that the USB cable is connected and the device is powered on.")
        print("    -> Check Windows Device Manager for the device.")
        return None

    found = None
    for p in ports:
        print(f"  {p.device}  ({p.description})", end="", flush=True)
        if "Bluetooth" in (p.description or ""):
            print("  -> skipped (Bluetooth)")
            continue
        status, _ = probe_port(p.device)
        if status == "found":
            print("  -> M-Logger found!")
            found = found or p.device
        elif status == "no_response":
            print("  -> opens OK, but no M-Logger response")
        elif status == "busy":
            print("  -> busy (another application is using this port)")
        else:
            print("  -> cannot open")
    return found


# ============================================================
# Live measurement test
# ============================================================
def live_test(ser, velocity_present):
    """Run a short USB-only measurement and print the latest sensor readings.
    Saves the current settings first and restores them afterwards."""
    original = cmd_result(ser, "get_settings")
    if original is None:
        print("[!] get_settings failed — skipping live test")
        return False

    ok = cmd_result(ser, "set_settings", {
        "general":     {"enabled": True, "interval": 1},
        "velocity":    {"enabled": velocity_present, "interval": 1},
        "illuminance": {"enabled": True, "interval": 1},
    })
    if ok is None:
        print("[!] set_settings failed — skipping live test")
        return False

    started = False
    values = {}          # latest value per channel key
    passed = False
    try:
        r = cmd_result(ser, "start_logging", {
            "transports": {"zigbee": False, "ble": False, "flash": False, "usb": True},
            "mode": "once",
        })
        if r is None:
            print("[!] start_logging failed — skipping live test")
            return False
        started = True

        print("Measuring... (sensor warm-up may take up to "
              f"{int(WARMUP_TIMEOUT)} s)")
        deadline    = time.time() + WARMUP_TIMEOUT
        post_warmup = 0
        last_note   = ""
        dc_since    = {}
        old_timeout = ser.timeout
        ser.timeout = 0.5
        try:
            while time.time() < deadline:
                line = ser.readline().decode("utf-8", errors="ignore").strip()
                if not line or not line.startswith("{"):
                    continue
                try:
                    ev = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if not isinstance(ev, dict) or ev.get("event") != "smp":
                    continue
                data = ev.get("data", {})

                # Persistent probe disconnection -> report and stop early
                now = time.time()
                for cat in data.get("dc", []):
                    dc_since.setdefault(cat, now)
                    if now - dc_since[cat] >= 5.0:
                        name = CATEGORY_NAMES.get(cat, cat)
                        print(f"[!] {name} appears to be DISCONNECTED")
                        return False
                for cat in list(dc_since):
                    if cat not in data.get("dc", []):
                        del dc_since[cat]

                wu = data.get("wu", [])
                note = ("  warming up: "
                        + ", ".join(CATEGORY_NAMES.get(c, c) for c in wu)
                        if wu else "  warm-up complete, sampling...")
                if note != last_note:
                    last_note = note
                    print(note)

                for k in ("t", "h", "g", "c", "l", "v"):
                    if k in data:
                        values[k] = data[k]

                if not wu:
                    post_warmup += 1
                    if post_warmup >= LIVE_SAMPLES:
                        passed = True
                        break
        finally:
            ser.timeout = old_timeout
    finally:
        if started:
            send_cmd(ser, "stop_logging", timeout=3.0)
            time.sleep(0.5)
            ser.reset_input_buffer()
        # Restore the settings that were active before the test
        send_cmd(ser, "set_settings", {
            "general":     original["general"],
            "velocity":    original["velocity"],
            "illuminance": original["illuminance"],
        })

    def fmt(key, pattern, unit):
        v = values.get(key)
        return f"{pattern.format(v)} {unit}" if v is not None else "--"

    print("\nLive readings:")
    print(f"  Dry-bulb temperature : {fmt('t', '{:.1f}', 'C')}")
    print(f"  Relative humidity    : {fmt('h', '{:.0f}', '%')}")
    print(f"  Globe temperature    : {fmt('g', '{:.1f}', 'C')}")
    print(f"  CO2                  : {fmt('c', '{:.0f}', 'ppm')}")
    print(f"  Illuminance          : {fmt('l', '{:.1f}', 'lx')}")
    if velocity_present:
        print(f"  Wind speed           : {fmt('v', '{:.3f}', 'm/s')}")

    if not passed:
        print("[!] Warm-up did not complete within the time limit "
              "(readings above may be incomplete)")
    return passed


# ============================================================
# main
# ============================================================
def main():
    ap = argparse.ArgumentParser(description="Check that an M-Logger is connected and working.")
    ap.add_argument("port", nargs="?", default=None,
                    help="COM port (auto-detect if omitted)")
    ap.add_argument("--no-live", action="store_true",
                    help="skip the live measurement test")
    args = ap.parse_args()

    port = args.port or scan_ports()
    if not port:
        print("\nResult: M-Logger NOT found.")
        return 1

    print(f"\nConnecting to {port}...")
    try:
        with open_no_reset(port) as ser:
            time.sleep(2.0)
            ser.reset_input_buffer()

            hello = cmd_result(ser, "hello")
            if hello is None:
                print("[!] hello failed")
                return 1
            print("\nDevice info:")
            print(f"  Device      : {hello.get('device')}")
            print(f"  Name        : {hello.get('name')}")
            print(f"  Hardware ID : {hello.get('hardware_id')}")
            print(f"  Firmware    : {hello.get('firmware_version')}")
            logging_now = bool(hello.get("logging"))
            if logging_now:
                print("  Status      : LOGGING (measurement in progress)")

            bat = cmd_result(ser, "get_battery")
            bat_mv = None
            if bat:
                bat_mv = bat.get("voltage_mv")
                low = " (LOW)" if bat.get("low_battery") else ""
                print(f"  Battery     : {bat_mv} mV{low}")

            def probe_desc(p):
                if not p or not p.get("connected"):
                    return "not detected"
                return f"{p.get('name', '?')} (ID {p.get('device_id', '?')})"

            probes = cmd_result(ser, "get_probe_info")
            velocity_present = False
            if probes:
                vel = probes.get("velocity_probe") or {}
                velocity_present = bool(vel.get("connected"))
                print(f"  T/RH probe  : {probe_desc(probes.get('th_probe'))}")
                print(f"  Vel. probe  : {probe_desc(vel)}")
                if velocity_present and bat_mv is not None and bat_mv < 2800:
                    print("  [!] Battery below 2.8 V — wind speed cannot be "
                          "measured; replace the batteries")

            cnt = cmd_result(ser, "get_count")
            if cnt:
                print(f"  Stored data : {cnt.get('count')} records")

            if args.no_live:
                print("\nLive measurement test skipped (--no-live).")
            elif logging_now:
                print("\nDevice is currently logging — live measurement test "
                      "skipped so the running measurement is not disturbed.")
            else:
                print()
                live_test(ser, velocity_present)

    except serial.SerialException as e:
        print(f"Serial error: {e}")
        return 1

    print("\nResult: M-Logger check finished.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
