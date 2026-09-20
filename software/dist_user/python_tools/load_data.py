"""
Download M-Logger v4 flash-recorded data over USB-CDC and save it as CSV.

Usage:
    python load_data.py                # auto-detect COM port
    python load_data.py COM5           # explicit COM port
    python load_data.py COM5 -o out.csv
    python load_data.py COM5 --all     # include all generations (default: latest only)

Record layout (22 bytes, struct format "<BIBIhhHHHH>"):
    uint8  gen          : data generation number
    uint32 ts           : UNIX seconds
    uint8  valid_flags  : bit flags (see below)
    uint32 illuminance  : Lux x 10
    int16  temp_dry     : degC x 100
    int16  temp_globe   : degC x 100
    uint16 humidity     : % x 100
    uint16 wind_speed   : m/s x 10000
    uint16 voltage      : mV (velocity sensor)
    uint16 co2_ppm      : ppm

valid_flags bit assignments:
    bit 0: illuminance / bit 1: t_dry / bit 2: t_glb / bit 3: humidity
    bit 4: wind_speed  / bit 5: voltage / bit 6: co2_ppm
"""
import argparse
import csv
import datetime
import json
import struct
import sys
import time

import serial
import serial.tools.list_ports


BAUD_RATE       = 115200
CONNECT_TIMEOUT = 1.5
DUMP_TIMEOUT    = 30.0       # extended timeout during the bulk transfer


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

RECORD_FORMAT = "<BIBIhhHHHH"  # struct format = 22 bytes (LE / no alignment)
RECORD_SIZE   = struct.calcsize(RECORD_FORMAT)   # = 22
# The firmware ("docs/protocol_v4.md") reports "<BIBIhhHHHH>" including a
# trailing '>'. That trailing '>' is not a byte-order re-specification
# (struct only looks at the first character), so it is meaningless; accept
# it as-is when comparing against the firmware-reported format.
RECORD_FORMAT_FW = RECORD_FORMAT + ">"

# valid_flags bits
FLAG_ILLUMINANCE = 1 << 0
FLAG_TEMP_DRY    = 1 << 1
FLAG_TEMP_GLOBE  = 1 << 2
FLAG_HUMIDITY    = 1 << 3
FLAG_WIND_SPEED  = 1 << 4
FLAG_VOLTAGE     = 1 << 5
FLAG_CO2_PPM     = 1 << 6


# ============================================================
# COM port detection
# ============================================================
def find_device_port():
    """Probe the available COM ports with hello and look for an M-Logger."""
    print("Scanning ports...")
    probe = (json.dumps({"v": 1, "id": 1, "command": "hello"}) + '\n').encode('utf-8')
    for p in serial.tools.list_ports.comports():
        try:
            print(f"  Checking {p.device}...", end="", flush=True)
            if "Bluetooth" in p.description:
                print(" Skipped (Bluetooth).")
                continue
            with open_no_reset(p.device) as ser:
                time.sleep(1.5)
                ser.reset_input_buffer()
                ser.write(probe)
                # Scan multiple lines until the response arrives. If the
                # firmware pushes a ready event or diag lines first, a single
                # readline would miss the reply.
                end = time.time() + 2.0
                found = False
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
                        found = True
                        return p.device
                if not found:
                    print(" No M-Logger response.")
        except (OSError, serial.SerialException):
            print(" Failed to open.")
    return None


# ============================================================
# Single-line JSON request
# ============================================================
def send_command(ser, payload, timeout=3.0):
    """Wait up to `timeout` seconds for the response whose id matches the
    payload. Event lines (ready etc., which carry no id) are skipped."""
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


# ============================================================
# dump
# ============================================================
def dump_records(ser):
    """
    Run the dump command and return (header_dict, raw_bytes, end_event_dict).
    Returns (None, None, None) on failure.
    """
    ser.reset_input_buffer()
    dump_id = 1000
    msg = json.dumps({"v": 1, "id": dump_id, "command": "dump"}) + '\n'
    ser.write(msg.encode('utf-8'))

    # 1) Header JSON: wait for the matching id (ready events etc. may be
    #    interleaved)
    hdr = None
    end = time.time() + 3.0
    while time.time() < end:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not line:
            continue
        try:
            cand = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(cand, dict) and cand.get("id") == dump_id:
            hdr = cand
            break
    if hdr is None:
        print("[ERROR] dump header not received within 3s")
        return None, None, None
    if "result" not in hdr:
        print(f"[ERROR] dump rejected: {hdr}")
        return None, None, None

    count    = hdr["result"].get("count", 0)
    rec_size = hdr["result"].get("record_size", RECORD_SIZE)
    fmt      = hdr["result"].get("format", RECORD_FORMAT)
    print(f"  count={count}, record_size={rec_size}, format={fmt}")

    if rec_size != RECORD_SIZE or fmt not in (RECORD_FORMAT, RECORD_FORMAT_FW):
        print(f"[WARN] firmware reports {rec_size}B/{fmt!r} but client expects "
              f"{RECORD_SIZE}B/{RECORD_FORMAT_FW!r} — parser may misinterpret data")

    # 2) Binary stream (chunked receive + progress bar)
    total = count * rec_size
    print(f"  receiving {total} bytes...")
    data = b""
    if total > 0:
        original_timeout = ser.timeout
        ser.timeout = 0.5   # short per-chunk wait; overall bounded by deadline
        chunk_size = 1024
        buf = bytearray()
        deadline = time.time() + DUMP_TIMEOUT
        last_pct = -1
        try:
            while len(buf) < total and time.time() < deadline:
                remaining = total - len(buf)
                chunk = ser.read(min(chunk_size, remaining))
                if chunk:
                    buf.extend(chunk)
                pct = len(buf) * 100 // total
                if pct != last_pct:
                    last_pct = pct
                    bar_len = 30
                    filled  = bar_len * len(buf) // total
                    bar     = "#" * filled + "-" * (bar_len - filled)
                    print(f"\r  [{bar}] {pct:3d}%  {len(buf):>8}/{total} B",
                          end="", flush=True)
        finally:
            ser.timeout = original_timeout
        print()  # finish the progress-bar line
        data = bytes(buf)
        if len(data) != total:
            print(f"[WARN] expected {total}B, got {len(data)}B (timeout?)")

    # 3) dump_end event: scan multiple lines as above
    end_ev = None
    deadline = time.time() + 3.0
    while time.time() < deadline:
        end_line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not end_line:
            continue
        try:
            cand = json.loads(end_line)
        except json.JSONDecodeError:
            continue
        if isinstance(cand, dict) and cand.get("event") == "dump_end":
            end_ev = cand
            break
    if end_ev is None:
        print("[WARN] dump_end not received within 3s")

    return hdr["result"], data, end_ev


# ============================================================
# Record decoding
# ============================================================
def decode_record(raw):
    """Unpack one record's bytes and return a dict of physical values."""
    (gen, ts, flags, illum, t_dry, t_glb,
     humid, wind, volt, co2) = struct.unpack(RECORD_FORMAT, raw)
    return {
        "gen":         gen,
        "ts":          ts,
        "iso_time":    datetime.datetime.fromtimestamp(ts).isoformat(timespec='seconds'),
        "illuminance": (illum   / 10.0)    if flags & FLAG_ILLUMINANCE else None,
        "t_dry":       (t_dry   / 100.0)   if flags & FLAG_TEMP_DRY    else None,
        "t_glb":       (t_glb   / 100.0)   if flags & FLAG_TEMP_GLOBE  else None,
        "humidity":    (humid   / 100.0)   if flags & FLAG_HUMIDITY    else None,
        "wind_speed":  (wind    / 10000.0) if flags & FLAG_WIND_SPEED  else None,
        "voltage":     volt                if flags & FLAG_VOLTAGE     else None,
        "co2":         co2                 if flags & FLAG_CO2_PPM     else None,
    }


# ============================================================
# CSV output
# ============================================================
CSV_COLUMNS = ["iso_time", "ts", "gen",
               "t_dry", "humidity", "t_glb",
               "wind_speed", "voltage", "illuminance", "co2"]
CSV_UNITS   = ["",       "[s]", "",
               "[C]",     "[%]",  "[C]",
               "[m/s]",   "[mV]", "[lx]", "[ppm]"]


def write_csv(filename, records, device_info, header_meta):
    with open(filename, "w", newline="", encoding="utf-8") as f:
        # Keep metadata as comment lines (CSV parsers commonly skip '#')
        if device_info:
            f.write(f"# device         : {device_info.get('device')}\n")
            f.write(f"# firmware       : {device_info.get('firmware_version')}\n")
            f.write(f"# hardware_id    : {device_info.get('hardware_id')}\n")
            f.write(f"# name           : {device_info.get('name')}\n")
        if header_meta:
            f.write(f"# total_records  : {header_meta.get('count')}\n")
            f.write(f"# record_size   : {header_meta.get('record_size')}\n")
            f.write(f"# struct_format : {header_meta.get('format')}\n")
        f.write(f"# dumped_at      : "
                f"{datetime.datetime.now().isoformat(timespec='seconds')}\n")

        writer = csv.writer(f)
        writer.writerow(CSV_COLUMNS)
        writer.writerow(CSV_UNITS)
        for r in records:
            writer.writerow([
                r["iso_time"], r["ts"], r["gen"],
                "" if r["t_dry"]       is None else f"{r['t_dry']:.2f}",
                "" if r["humidity"]    is None else f"{r['humidity']:.2f}",
                "" if r["t_glb"]       is None else f"{r['t_glb']:.2f}",
                "" if r["wind_speed"]  is None else f"{r['wind_speed']:.4f}",
                "" if r["voltage"]     is None else r["voltage"],
                "" if r["illuminance"] is None else f"{r['illuminance']:.1f}",
                "" if r["co2"]         is None else r["co2"],
            ])


# ============================================================
# main
# ============================================================
def main():
    ap = argparse.ArgumentParser(description="Dump M-Logger v4 flash data to CSV.")
    ap.add_argument("port", nargs="?", default=None,
                    help="COM port (auto-detect if omitted)")
    ap.add_argument("-o", "--output", default=None,
                    help="output CSV filename (default: mlogger_<hwid>_<ts>.csv)")
    ap.add_argument("--all", action="store_true",
                    help="also recover cleared data")
    args = ap.parse_args()

    port = args.port or find_device_port()
    if not port:
        print("Error: M-Logger not found.")
        return 1

    print(f"Connecting to {port}...")
    try:
        with open_no_reset(port) as ser:
            time.sleep(2.0)

            # Device info
            hello = send_command(ser, {"v": 1, "id": 1, "command": "hello"})
            if not hello or "result" not in hello:
                print(f"[ERROR] hello failed: {hello}")
                return 1
            device_info = hello["result"]
            print(f"  device   : {device_info.get('device')} v{device_info.get('firmware_version')}")
            print(f"  hardware : {device_info.get('hardware_id')}")
            print(f"  name     : {device_info.get('name')!r}")
            if device_info.get("logging"):
                print("[ERROR] the device is recording — download is not possible")
                return 1

            # Run the dump
            print("\nDumping records...")
            header_meta, raw_data, end_ev = dump_records(ser)
            if header_meta is None:
                return 1
            if end_ev:
                print(f"  dump_end: data={end_ev.get('data')}")

    except serial.SerialException as e:
        print(f"Serial error: {e}")
        return 1

    # Decode
    nrec = len(raw_data) // RECORD_SIZE
    print(f"\nDecoding {nrec} records...")
    records = [decode_record(raw_data[i * RECORD_SIZE:(i + 1) * RECORD_SIZE])
               for i in range(nrec)]

    # Generation filter
    if records and not args.all:
        latest_gen = max(r["gen"] for r in records)
        before     = len(records)
        records    = [r for r in records if r["gen"] == latest_gen]
        print(f"  filtered: gen={latest_gen} → {len(records)}/{before} records")
    elif args.all:
        gens = sorted({r["gen"] for r in records})
        print(f"  including all generations: {gens}")

    # Output filename
    if args.output:
        out = args.output
    else:
        hwid = device_info.get("hardware_id", "unknown")
        stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        out = f"mlogger_{hwid}_{stamp}.csv"

    write_csv(out, records, device_info, header_meta)
    print(f"\nWrote {len(records)} records to {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
