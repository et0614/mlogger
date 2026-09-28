"""
M-Logger 本体の安定性診断 (get_diag) を USB-CDC で読む。

使い方:
    python get_diag.py            # COM ポート自動検出
    python get_diag.py COM5       # 明示指定
    python get_diag.py COM5 60    # 60 秒ごとに繰り返し表示 (長時間運転中の監視用)
    python get_diag.py COM5 --wdt-test
        # WDT の動作確認: 本体をわざと止めて約 8 秒後の WDT 再起動を待ち、
        # 再起動後の reset_flags に WDRF が立つかを確認する
    python get_diag.py COM5 300 --zigbee-logging
        # Zigbee + USB 出力でロギングを開始し、300 秒ごとに表示する (Zigbee 送信の長時間試験用)。
        # Zigbee だけのロギングでは本体が USB を処理しないため、USB も出力先に入れて
        # 計測中も読めるようにする (XBee のスリープ動作は Zigbee だけの場合と同じ)。
        # Ctrl+C でロギングを止めて終了する

stack_free_min: 起動以来、スタックが最も深く伸びた時点の残り RAM [byte]
reset_flags   : 起動時のリセット要因
"""
import json
import sys
import time

import serial
import serial.tools.list_ports

BAUD_RATE = 115200
RESET_BITS = ["PORF(電源投入)", "BORF(低電圧)", "EXTRF(外部)", "WDRF(WDT)",
              "SWRF(ソフト)", "UPDIRF(UPDI)"]


def open_no_reset(port, timeout=1.5):
    """DTR/RTS を非アサートで open して AVR DU32 の reset 経路を踏まないようにする。"""
    ser = serial.Serial()
    ser.port, ser.baudrate, ser.timeout = port, BAUD_RATE, timeout
    ser.dtr = False
    ser.rts = False
    ser.open()
    return ser


def send_command(ser, command, cmd_id, timeout=3.0, params=None):
    ser.reset_input_buffer()
    msg = {"v": 1, "id": cmd_id, "command": command}
    if params is not None:
        msg["params"] = params
    ser.write((json.dumps(msg) + "\n").encode())
    end = time.time() + timeout
    while time.time() < end:
        line = ser.readline().decode("utf-8", errors="ignore").strip()
        if not line.startswith("{"):
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        if resp.get("id") == cmd_id:
            return resp
    return None


def find_device_port():
    for p in serial.tools.list_ports.comports():
        if "Bluetooth" in (p.description or ""):
            continue
        try:
            with open_no_reset(p.device) as ser:
                time.sleep(1.5)
                r = send_command(ser, "hello", 1, timeout=2.0)
                if r and r.get("result", {}).get("device") == "M-Logger":
                    return p.device
        except (OSError, serial.SerialException):
            pass
    return None


def wdt_test(port):
    """本体をわざと停止させ、WDT で再起動して WDRF が記録されるかを確認する。"""
    with open_no_reset(port) as ser:
        time.sleep(1.5)
        ser.reset_input_buffer()
        ser.write((json.dumps({"v": 1, "id": 900, "command": "get_diag",
                               "params": {"hang": True}}) + "\n").encode())
        print("本体を停止させました。WDT による再起動を待っています (約 8 秒)...")
    # 再起動で USB が再列挙されるので、ポートを開き直せるまで待つ
    time.sleep(12)
    for _ in range(20):
        try:
            with open_no_reset(port) as ser:
                time.sleep(1.5)
                r = send_command(ser, "get_diag", 901)
                if r and "result" in r:
                    flags = r["result"]["reset_flags"]
                    ok = bool(flags & (1 << 3))
                    print(f"reset_flags 0x{flags:02X} → "
                          + ("OK: WDT で再起動しました" if ok else "NG: WDRF が立っていません"))
                    return 0 if ok else 1
        except (OSError, serial.SerialException):
            pass
        time.sleep(1)
    print("NG: 再起動後に応答がありません")
    return 1


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    port = args[0] if args else find_device_port()
    interval = float(args[1]) if len(args) > 1 else 0
    if not port:
        print("M-Logger が見つかりません")
        return 1
    if "--wdt-test" in sys.argv:
        return wdt_test(port)
    zigbee_logging = "--zigbee-logging" in sys.argv
    with open_no_reset(port) as ser:
        time.sleep(1.5)
        if zigbee_logging:
            # 時刻が未設定 (電源投入直後は 2000 年) だと計測開始時刻 (start_ts) より前と
            # 判定されて計測が始まらないので、先に時刻を合わせる
            r = send_command(ser, "set_time", 1, params={"ts": int(time.time())})
            if not r or "result" not in r:
                print(f"set_time 失敗: {r}")
                return 1
            r = send_command(ser, "start_logging", 2, params={
                "transports": {"zigbee": True, "ble": False, "flash": False, "usb": True},
                "mode": "once"})
            if not r or "result" not in r:
                print(f"start_logging 失敗: {r}")
                return 1
            print("Zigbee + USB 出力でロギングを開始しました (Ctrl+C で停止して終了)")
        n = 0
        try:
            while True:
                n += 1
                r = send_command(ser, "get_diag", 100 + n)
                if not r or "result" not in r:
                    print(f"get_diag 失敗: {r}")
                    return 1
                res = r["result"]
                flags = res["reset_flags"]
                names = [b for i, b in enumerate(RESET_BITS) if flags & (1 << i)] or ["なし"]
                zb = ""
                if "zb_tx_status" in res:
                    zb = (f"   zigbee tx {res['zb_tx_status']} (fail {res['zb_tx_fail']}"
                          + (f", last 0x{res['zb_tx_last_fail']:02X}" if res['zb_tx_fail'] else "") + ")")
                print(f"{time.strftime('%H:%M:%S')}  stack_free_min {res['stack_free_min']:5d} B"
                      f"   reset_flags 0x{flags:02X} ({', '.join(names)}){zb}")
                if interval <= 0:
                    return 0
                time.sleep(interval)
        except KeyboardInterrupt:
            pass
        finally:
            if zigbee_logging:
                send_command(ser, "stop_logging", 3)
                print("ロギングを停止しました")
    return 0


if __name__ == "__main__":
    sys.exit(main())
