"""
M-Logger v4 USB-CDC diagnostic listener.

Reads `# ...` diag lines emitted by the firmware (diag_usb.c) and prints them
with PC-side timestamps. Non-diag lines (JSON-RPC responses, etc.) are also
shown for context.

Usage:
    python ble_trace.py             # auto-detect COM port (probes hello)
    python ble_trace.py COM5        # explicit COM port

Run this on the PC connected to the M-Logger main board via USB-C while
operating the M-Logger over BLE from a phone / MAUI app. Every BLE frame the
firmware sees, every TX status returned by the XBee, every JSON dispatch, etc.
appears here in real time.

Hex preview shows the first 96 bytes (or 200 for DISPATCH_DATA). Anything
beyond is truncated with `...`.
"""
import serial
import serial.tools.list_ports
import sys
import time
from datetime import datetime

BAUD_RATE = 115200


def open_no_reset(port, baud=BAUD_RATE, timeout=1.5):
    """DTR/RTS を非アサートで open して AVR DU32 の reset を抑制。"""
    ser = serial.Serial()
    ser.port = port
    ser.baudrate = baud
    ser.timeout = timeout
    ser.dtr = False
    ser.rts = False
    ser.open()
    return ser


def find_device_port():
    """Probe each COM port with a v4 hello over USB-CDC, return the responder."""
    import json
    probe = (json.dumps({"v": 1, "id": 1, "command": "hello"}) + '\n').encode("utf-8")
    print("Scanning ports for M-Logger over USB-CDC...")
    for p in serial.tools.list_ports.comports():
        if "Bluetooth" in (p.description or ""):
            continue
        try:
            with open_no_reset(p.device, timeout=1.5) as s:
                time.sleep(1.0)
                s.reset_input_buffer()
                s.write(probe)
                end = time.time() + 2.0
                while time.time() < end:
                    line = s.readline().decode("utf-8", errors="ignore").strip()
                    if not line:
                        continue
                    if '"M-Logger"' in line and '"result"' in line:
                        print(f"  Found M-Logger on {p.device}")
                        return p.device
        except (OSError, serial.SerialException):
            continue
    return None


def ts():
    return datetime.now().strftime("%H:%M:%S.%f")[:-3]


def classify(line: str) -> str:
    """Return a short tag for the line type."""
    if line.startswith("# "):
        # diag line. classify by first token after "# "
        rest = line[2:].split(" ", 1)[0]
        return f"[{rest:<14}]"
    if line.startswith("{"):
        return "[JSON         ]"
    return "[OTHER        ]"


def main(port):
    print(f"Listening on {port} @ {BAUD_RATE} (Ctrl+C to stop)\n")
    try:
        with open_no_reset(port, timeout=0.2) as s:
            buf = b""
            while True:
                chunk = s.read(512)
                if chunk:
                    buf += chunk
                    while b"\n" in buf:
                        raw, buf = buf.split(b"\n", 1)
                        line = raw.decode("utf-8", errors="replace").rstrip("\r")
                        if not line:
                            continue
                        print(f"{ts()} {classify(line)} {line}")
    except KeyboardInterrupt:
        print("\nStopped.")
    except serial.SerialException as e:
        print(f"Serial error: {e}")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        port = sys.argv[1]
    else:
        port = find_device_port()
    if port:
        main(port)
    else:
        print("No M-Logger found. Pass COM port explicitly: python ble_trace.py COMx")
