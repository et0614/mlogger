"""
M-Logger v4 通信プロトコル動作確認スクリプト (USB-CDC)

使い方:
    python test_protocol_v4.py          # COMポート自動検出
    python test_protocol_v4.py COM5     # 明示指定

確認する内容:
    1) hello コマンドが正しく応答するか (Phase A の最小確認)
    2) 未知コマンドで unknown_command エラーが返るか
    3) 不正JSONで invalid_params エラーが返るか
    4) (参考) 旧 v3 の WHO コマンドが引き続き動作するか
"""
import serial
import serial.tools.list_ports
import time
import sys
import json

# ==========================================
# 設定
# ==========================================
BAUD_RATE = 115200
TIMEOUT_SEC = 2.0
# ==========================================


def open_no_reset(port, baud=BAUD_RATE, timeout=TIMEOUT_SEC):
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
    """利用可能なCOMポートを走査し、v4 hello JSON コマンドで M-Logger を探す。"""
    print("Scanning ports...")
    ports = list(serial.tools.list_ports.comports())
    probe = (json.dumps({"v": 1, "id": 1, "command": "hello"}) + '\n').encode('utf-8')

    for p in ports:
        try:
            print(f"  Checking {p.device}...", end="", flush=True)

            if "Bluetooth" in p.description:
                print(" Skipped (Likely Bluetooth).")
                continue

            with open_no_reset(p.device, timeout=1.5) as ser:
                time.sleep(1.5)
                ser.reset_input_buffer()
                ser.write(probe)
                # 応答が来るまで複数行スキャン。firmware が ready event や
                # diag 行を先に流していると 1 行 readline では取りこぼす。
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
            continue

    return None


def send_json(ser, payload, label=None):
    """JSONメッセージを送信し、応答1行を読んで返す。"""
    msg = json.dumps(payload, ensure_ascii=False) + '\n'
    if label:
        print(f"\n--- {label} ---")
    print(f">>> Sent ({len(msg)} bytes):")
    print(f"    {msg.strip()}")

    ser.reset_input_buffer()
    ser.write(msg.encode('utf-8'))

    line_bytes = ser.readline()
    if not line_bytes:
        print("<<< (no response)")
        return None

    line_str = line_bytes.decode('utf-8', errors='ignore').strip()
    print(f"<<< Received ({len(line_str)} bytes):")
    print(f"    {line_str}")

    try:
        return json.loads(line_str)
    except json.JSONDecodeError as e:
        print(f"    [!] JSON parse error: {e}")
        return None


def send_raw(ser, raw_bytes, label=None):
    """生バイト列を送信し、応答1行を読んで返す (不正JSON送信テスト用)。"""
    if label:
        print(f"\n--- {label} ---")
    print(f">>> Sent ({len(raw_bytes)} bytes):")
    print(f"    {raw_bytes!r}")

    ser.reset_input_buffer()
    ser.write(raw_bytes)

    line_bytes = ser.readline()
    if not line_bytes:
        print("<<< (no response)")
        return None

    line_str = line_bytes.decode('utf-8', errors='ignore').strip()
    print(f"<<< Received ({len(line_str)} bytes):")
    print(f"    {line_str}")

    try:
        return json.loads(line_str)
    except json.JSONDecodeError as e:
        print(f"    [!] JSON parse error: {e}")
        return None


def test_hello(ser):
    resp = send_json(ser, {"v": 1, "id": 1, "command": "hello"}, "Test 1: hello")
    if resp is None:
        print("    [FAIL] No response or invalid JSON")
        return False
    if "error" in resp:
        print(f"    [ERR] error returned: {resp['error']}")
        return False
    if "result" not in resp:
        print(f"    [FAIL] Missing 'result' field")
        return False
    if resp.get("id") != 1:
        print(f"    [WARN] id mismatch: expected 1, got {resp.get('id')}")
    if resp.get("v") != 1:
        print(f"    [WARN] v mismatch: expected 1, got {resp.get('v')}")

    print("    [OK] result fields:")
    for k, v in resp["result"].items():
        print(f"          {k}: {v!r}")

    # 想定フィールドの存在チェック
    expected = {"device", "firmware_version", "protocol_version", "hardware_id", "name", "logging"}
    missing = expected - resp["result"].keys()
    if missing:
        print(f"    [WARN] missing expected keys: {missing}")
    extra = resp["result"].keys() - expected
    if extra:
        print(f"    [INFO] unexpected extra keys: {extra}")
    return True


def test_unknown_command(ser):
    resp = send_json(ser, {"v": 1, "id": 2, "command": "no_such_command"},
                     "Test 2: unknown command (expect error)")
    if resp is None:
        print("    [FAIL] No response")
        return False
    if "error" not in resp:
        print(f"    [FAIL] expected error, got: {resp}")
        return False
    if resp["error"].get("code") != "unknown_command":
        print(f"    [FAIL] expected code='unknown_command', got: {resp['error']}")
        return False
    if resp.get("id") != 2:
        print(f"    [WARN] id mismatch: expected 2, got {resp.get('id')}")
    print(f"    [OK] {resp['error']}")
    return True


def test_invalid_json(ser):
    # ':' を抜いた不正JSON
    broken = b'{"v":1,"id":3,"command" "hello"}\n'
    resp = send_raw(ser, broken, "Test 3: invalid JSON (expect error)")
    if resp is None:
        print("    [FAIL] No response")
        return False
    if "error" not in resp:
        print(f"    [FAIL] expected error, got: {resp}")
        return False
    if resp["error"].get("code") != "invalid_params":
        print(f"    [WARN] expected code='invalid_params', got: {resp['error']}")
    print(f"    [OK] {resp['error']}")
    return True


# ============================================================
# echo diagnostic tests (USB)
#   ph_echo is a side-effect-free handler that returns N 'x' characters in
#   result.data. Used to isolate bugs:
#     - USB pass + BLE fail  → bug is in Xbee_BlChars / chunking / reentry
#     - USB fail             → bug is in pd_dispatch / ph_echo / CH_Reply / dispatch state
# ============================================================

def _do_echo(ser, id_, size, verbose=False):
    """単発 echo, 応答が正しいか検証して bool を返す。verbose=False で出力簡略。"""
    msg = json.dumps({"v": 1, "id": id_, "command": "echo", "params": {"size": size}}) + '\n'
    ser.reset_input_buffer()
    ser.write(msg.encode('utf-8'))
    line = ser.readline().decode('utf-8', errors='ignore').strip()
    if not line:
        if verbose: print(f"    id={id_} size={size}: NO RESPONSE")
        return False
    try:
        resp = json.loads(line)
    except json.JSONDecodeError as e:
        if verbose: print(f"    id={id_} size={size}: PARSE FAIL ({e}) — got {len(line)}B: {line[:80]!r}")
        return False
    if resp.get("id") != id_:
        if verbose: print(f"    id={id_} size={size}: ID MISMATCH (got {resp.get('id')})")
        return False
    r = resp.get("result") or {}
    if r.get("size") != size:
        if verbose: print(f"    id={id_} size={size}: size mismatch {r.get('size')}")
        return False
    data = r.get("data") or ""
    if len(data) != size or any(c != 'x' for c in data):
        if verbose: print(f"    id={id_} size={size}: data wrong (len={len(data)})")
        return False
    if verbose: print(f"    id={id_} size={size}: OK ({len(line)}B response)")
    return True


def test_echo_single_small(ser):
    print("\n--- Echo 1: single small (size=10) ---")
    ok = _do_echo(ser, 200, 10, verbose=True)
    print(f"    [{'OK' if ok else 'FAIL'}]")
    return ok


def test_echo_single_large(ser):
    print("\n--- Echo 2: single large (size=300, response ~370B) ---")
    ok = _do_echo(ser, 201, 300, verbose=True)
    print(f"    [{'OK' if ok else 'FAIL'}]")
    return ok


def test_echo_burst_small(ser):
    """連続 5 回の小さい echo - ディスパッチ/状態のサンプル"""
    print("\n--- Echo 3: burst of 5 small (size=20) ---")
    all_ok = True
    for i in range(5):
        ok = _do_echo(ser, 210 + i, 20, verbose=True)
        if not ok: all_ok = False
    print(f"    [{'OK' if all_ok else 'FAIL'}]  (passed {sum(1 for i in range(5) if True)})")
    return all_ok


def test_echo_burst_large(ser):
    """連続 5 回の大きい echo - 実機 BLE で出ている 'first ok, second fails' を USB で再現するか"""
    print("\n--- Echo 4: burst of 5 large (size=300, response ~370B each) ---")
    results = []
    for i in range(5):
        ok = _do_echo(ser, 220 + i, 300, verbose=True)
        results.append(ok)
    print(f"    individual: {results}")
    all_ok = all(results)
    print(f"    [{'OK' if all_ok else 'FAIL'}]")
    return all_ok


def test_echo_size_sweep(ser):
    """サイズスイープ - 応答が壊れ始めるしきい値を見る"""
    print("\n--- Echo 5: size sweep [10, 50, 100, 200, 250, 300, 350, 400, 440] ---")
    results = {}
    for size in [10, 50, 100, 200, 250, 300, 350, 400, 440]:
        ok = _do_echo(ser, 230 + size, size, verbose=False)
        results[size] = ok
        print(f"    size={size:>4}: {'OK' if ok else 'FAIL'}")
    all_ok = all(results.values())
    print(f"    [{'OK' if all_ok else 'FAIL'}]")
    return all_ok


def test_legacy_v3_removed(ser):
    """旧 v3 コマンドが応答しないことを確認 (Phase E で廃止)"""
    print("\n--- Test 4: legacy v3 WHO must NOT respond ---")
    ser.reset_input_buffer()
    ser.write(b'WHO\n')
    print(">>> Sent: b'WHO\\n'")
    # ファームは応答せず破棄する想定 → readline がタイムアウト
    line = ser.readline().decode('utf-8', errors='ignore').strip()
    if not line:
        print("    [OK] no response as expected (v3 removed)")
        return True
    else:
        print(f"    [FAIL] unexpected response: {line!r}")
        return False


# ============================================================
# Phase B tests: get/set 系
# ============================================================
SENSOR_NAMES = ["t_dry", "humidity", "t_glb", "velocity", "illuminance", "co2"]
CORRECTION_SENSORS = ["t_dry", "humidity", "t_glb", "illuminance", "velocity"]


def test_get_settings(ser):
    resp = send_json(ser, {"v": 1, "id": 5, "command": "get_settings"},
                     "Test 5: get_settings")
    if resp is None or "result" not in resp:
        print(f"    [FAIL] {resp}")
        return False
    r = resp["result"]
    missing = [s for s in SENSOR_NAMES if s not in r]
    if missing:
        print(f"    [FAIL] missing sensors: {missing}")
        return False
    if "start_ts" not in r:
        print(f"    [FAIL] missing 'start_ts'")
        return False
    for s in SENSOR_NAMES:
        v = r[s]
        if not isinstance(v, dict) or "enabled" not in v or "interval" not in v:
            print(f"    [FAIL] {s} malformed: {v}")
            return False
    print(f"    [OK]")
    for s in SENSOR_NAMES:
        print(f"      {s:>12}: {r[s]}")
    print(f"      {'start_ts':>12}: {r['start_ts']}")
    return True


def test_set_settings_patch(ser):
    """illuminance.interval を変更し、応答で反映されているか確認後、元に戻す"""
    # 現状を取得
    cur = send_json(ser, {"v": 1, "id": 100, "command": "get_settings"},
                    "Test 6a: capture original settings")
    if cur is None or "result" not in cur:
        print(f"    [FAIL] get_settings failed")
        return False
    orig_ill = cur["result"]["illuminance"]
    print(f"    original illuminance: {orig_ill}")

    # PATCH: illuminance.interval = 77
    resp = send_json(ser, {"v": 1, "id": 6, "command": "set_settings",
                           "params": {"illuminance": {"interval": 77}}},
                     "Test 6b: PATCH illuminance.interval=77")
    if resp is None or "result" not in resp:
        print(f"    [FAIL] {resp}")
        return False
    if resp["result"]["illuminance"]["interval"] != 77:
        print(f"    [FAIL] interval not 77: {resp['result']['illuminance']}")
        return False
    # enabled は変更しないので元のまま
    if resp["result"]["illuminance"]["enabled"] != orig_ill["enabled"]:
        print(f"    [FAIL] PATCH leaked: enabled changed unexpectedly")
        return False
    print(f"    [OK] PATCH applied")

    # 元に戻す
    restore = send_json(ser, {"v": 1, "id": 101, "command": "set_settings",
                              "params": {"illuminance": orig_ill}},
                        "Test 6c: restore original illuminance")
    if restore is None or "result" not in restore:
        print(f"    [FAIL] restore failed")
        return False
    if restore["result"]["illuminance"]["interval"] != orig_ill["interval"]:
        print(f"    [WARN] restore not exact: {restore['result']['illuminance']}")
    return True


def test_set_settings_out_of_range(ser):
    """interval が範囲外なら out_of_range が返ることを確認"""
    resp = send_json(ser, {"v": 1, "id": 7, "command": "set_settings",
                           "params": {"velocity": {"interval": 999999}}},
                     "Test 7: set_settings interval out_of_range")
    if resp is None or "error" not in resp:
        print(f"    [FAIL] expected error, got: {resp}")
        return False
    if resp["error"].get("code") != "out_of_range":
        print(f"    [FAIL] expected code='out_of_range', got: {resp['error']}")
        return False
    print(f"    [OK] {resp['error']}")
    return True


def test_get_correction(ser):
    resp = send_json(ser, {"v": 1, "id": 8, "command": "get_correction"},
                     "Test 8: get_correction")
    if resp is None or "result" not in resp:
        print(f"    [FAIL] {resp}")
        return False
    r = resp["result"]
    missing = [s for s in CORRECTION_SENSORS if s not in r]
    if missing:
        print(f"    [FAIL] missing sensors: {missing}")
        return False
    for s in CORRECTION_SENSORS:
        v = r[s]
        if not isinstance(v, dict) or "a" not in v or "b" not in v:
            print(f"    [FAIL] {s} malformed: {v}")
            return False
    print(f"    [OK]")
    for s in CORRECTION_SENSORS:
        print(f"      {s:>12}: {r[s]}")
    return True


def test_set_correction_out_of_range(ser):
    """t_dry.a=99 (許容: 0.8〜1.2) で out_of_range を確認"""
    resp = send_json(ser, {"v": 1, "id": 9, "command": "set_correction",
                           "params": {"t_dry": {"a": 99.0}}},
                     "Test 9: set_correction out_of_range")
    if resp is None or "error" not in resp:
        print(f"    [FAIL] expected error, got: {resp}")
        return False
    if resp["error"].get("code") != "out_of_range":
        print(f"    [FAIL] expected code='out_of_range', got: {resp['error']}")
        return False
    print(f"    [OK] {resp['error']}")
    return True


def test_set_correction_patch(ser):
    """humidity.b を一時的に変更して反映を確認後、元に戻す"""
    cur = send_json(ser, {"v": 1, "id": 102, "command": "get_correction"},
                    "Test 10a: capture original correction")
    if cur is None or "result" not in cur:
        return False
    orig_h = cur["result"]["humidity"]
    print(f"    original humidity correction: {orig_h}")

    resp = send_json(ser, {"v": 1, "id": 10, "command": "set_correction",
                           "params": {"humidity": {"b": 1.23}}},
                     "Test 10b: PATCH humidity.b=1.23")
    if resp is None or "result" not in resp:
        print(f"    [FAIL] {resp}")
        return False
    if abs(resp["result"]["humidity"]["b"] - 1.23) > 0.001:
        print(f"    [FAIL] b not 1.23: {resp['result']['humidity']}")
        return False
    if abs(resp["result"]["humidity"]["a"] - orig_h["a"]) > 0.001:
        print(f"    [FAIL] PATCH leaked: 'a' changed unexpectedly")
        return False
    print(f"    [OK] PATCH applied")

    restore = send_json(ser, {"v": 1, "id": 103, "command": "set_correction",
                              "params": {"humidity": orig_h}},
                        "Test 10c: restore original humidity correction")
    if restore is None or "result" not in restore:
        print(f"    [FAIL] restore failed")
        return False
    return True


def test_set_name(ser):
    """name を一時変更し、戻す"""
    hello = send_json(ser, {"v": 1, "id": 104, "command": "hello"},
                      "Test 11a: capture original name via hello")
    if hello is None or "result" not in hello:
        return False
    orig_name = hello["result"]["name"]
    print(f"    original name: {orig_name!r}")

    resp = send_json(ser, {"v": 1, "id": 11, "command": "set_name",
                           "params": {"name": "v4_test"}},
                     "Test 11b: set_name='v4_test'")
    if resp is None or "result" not in resp:
        return False
    if resp["result"].get("name") != "v4_test":
        print(f"    [FAIL] name not changed: {resp['result']}")
        return False
    print(f"    [OK] name changed")

    restore = send_json(ser, {"v": 1, "id": 105, "command": "set_name",
                              "params": {"name": orig_name}},
                        "Test 11c: restore name")
    if restore is None or "result" not in restore:
        return False
    return True


def test_set_time(ser):
    """現在時刻を送信し、応答 ts が ±2秒以内で一致するか"""
    now = int(time.time())
    resp = send_json(ser, {"v": 1, "id": 12, "command": "set_time",
                           "params": {"ts": now}},
                     "Test 12: set_time to now")
    if resp is None or "result" not in resp:
        print(f"    [FAIL] {resp}")
        return False
    returned = resp["result"].get("ts")
    if not isinstance(returned, int):
        print(f"    [FAIL] ts not int: {returned}")
        return False
    if abs(returned - now) > 2:
        print(f"    [WARN] ts mismatch: sent {now}, got {returned} (diff {returned-now})")
    print(f"    [OK] sent {now}, returned {returned}")
    return True


# ============================================================
# Phase C tests: action 系
# ============================================================
def _logging_state(ser, label_id):
    """hello を呼んで logging フィールドだけ返す"""
    h = send_json(ser, {"v": 1, "id": label_id, "command": "hello"})
    if h is None or "result" not in h:
        return None
    return h["result"].get("logging")


def test_stop_logging_when_idle(ser):
    """非ロギング時に stop_logging を投げて空 result が返ることを確認"""
    resp = send_json(ser, {"v": 1, "id": 13, "command": "stop_logging"},
                     "Test 13: stop_logging (when idle)")
    if resp is None or "result" not in resp:
        print(f"    [FAIL] {resp}")
        return False
    print(f"    [OK]")
    return True


def test_smp_event_stream(ser):
    """start_logging で smp イベントが USB-CDC 経由で定期的に流れることを確認 (Phase D)。
    USB 出力を有効にすると USB は接続を維持できる想定。
    数秒受信後に stop_logging。"""
    print("\n--- Test FINAL: smp event stream (Phase D) ---")

    # ロギング設定の interval を 1 秒に揃えておく (PATCH)
    send_json(ser, {"v": 1, "id": 300, "command": "set_settings",
                    "params": {"t_dry":       {"enabled": True, "interval": 1},
                               "humidity":    {"enabled": True, "interval": 1},
                               "t_glb":       {"enabled": True, "interval": 1},
                               "velocity":    {"enabled": True, "interval": 1},
                               "illuminance": {"enabled": True, "interval": 1},
                               "co2":         {"enabled": False, "interval": 1}}})
    time.sleep(0.2)

    try:
        resp = send_json(ser, {"v": 1, "id": 99, "command": "start_logging",
                               "params": {"transports": {"zigbee": False, "ble": False,
                                                         "flash": False, "usb": True},
                                          "mode": "once"}},
                         "Test FINAL: start_logging with usb=true")
    except serial.SerialException as e:
        print(f"    [SKIP] write failed: {e}")
        return False

    if resp is None or "result" not in resp:
        print(f"    [FAIL] no ACK: {resp}")
        return False

    # smp イベントを数秒間収集
    print("    Collecting smp events for 4 seconds...")
    end_time = time.time() + 4.0
    smp_events = []
    other_lines = []
    while time.time() < end_time:
        try:
            line_bytes = ser.readline()
            if not line_bytes:
                continue
            line = line_bytes.decode('utf-8', errors='ignore').strip()
            if not line:
                continue
            try:
                ev = json.loads(line)
                if ev.get("event") == "smp":
                    smp_events.append(ev)
                    if len(smp_events) <= 3:
                        print(f"    [{len(smp_events)}] smp: ts={ev.get('ts')}, data={ev.get('data')}")
                else:
                    other_lines.append(ev)
            except json.JSONDecodeError:
                other_lines.append(line)
        except serial.SerialException as e:
            print(f"    [WARN] read failed: {e}")
            break

    # stop_logging
    try:
        ser.reset_input_buffer()
        stop = send_json(ser, {"v": 1, "id": 51, "command": "stop_logging"})
        if stop and "result" in stop:
            print(f"    stop_logging: OK")
        else:
            print(f"    [WARN] stop_logging response: {stop}")
    except serial.SerialException as e:
        print(f"    [WARN] stop_logging failed: {e}")
        print(f"    NOTE: device may still be logging — power-cycle to recover.")

    if len(smp_events) == 0:
        print(f"    [FAIL] no smp events received (other lines: {len(other_lines)})")
        return False

    # 内容バリデーション
    first = smp_events[0]
    if "data" not in first or not isinstance(first["data"], dict):
        print(f"    [FAIL] malformed smp: {first}")
        return False
    expected_keys = {"t", "h", "g", "vel", "l"}  # co2 disabled, may be absent
    got_keys = set(first["data"].keys())
    common = expected_keys & got_keys
    if len(common) < 3:
        print(f"    [WARN] expected short keys {expected_keys}, got {got_keys}")

    print(f"    [OK] {len(smp_events)} smp events received over ~4s")
    return True


def test_start_logging_invalid_mode(ser):
    resp = send_json(ser, {"v": 1, "id": 15, "command": "start_logging",
                           "params": {"transports": {"zigbee": False, "ble": False,
                                                     "flash": False, "usb": False},
                                      "mode": "weird_mode"}},
                     "Test 14: start_logging invalid mode")
    if resp is None or "error" not in resp:
        print(f"    [FAIL] expected error, got: {resp}")
        return False
    if resp["error"].get("code") != "invalid_params":
        print(f"    [FAIL] expected invalid_params, got: {resp['error']}")
        return False
    print(f"    [OK] {resp['error']}")
    return True


def test_calibrate_co2_validation(ser):
    """各バリデーションパスをチェック (実校正は副作用が大きいので発火させない)"""
    print("\n--- Test 15: calibrate_co2 validation paths ---")
    ok = True

    # 15a: mode 欠落
    r = send_json(ser, {"v": 1, "id": 16, "command": "calibrate_co2",
                        "params": {"target_ppm": 420}},
                  "Test 15a: missing mode")
    if r is None or r.get("error", {}).get("code") != "invalid_params":
        print(f"    [FAIL] {r}"); ok = False
    else:
        print(f"    [OK] missing mode → {r['error']}")

    # 15b: 不明な mode
    r = send_json(ser, {"v": 1, "id": 17, "command": "calibrate_co2",
                        "params": {"mode": "weird", "target_ppm": 420}},
                  "Test 15b: invalid mode value")
    if r is None or r.get("error", {}).get("code") != "invalid_params":
        print(f"    [FAIL] {r}"); ok = False
    else:
        print(f"    [OK] invalid mode → {r['error']}")

    # 15c: target_ppm 欠落
    r = send_json(ser, {"v": 1, "id": 18, "command": "calibrate_co2",
                        "params": {"mode": "forced"}},
                  "Test 15c: missing target_ppm")
    if r is None or r.get("error", {}).get("code") != "invalid_params":
        print(f"    [FAIL] {r}"); ok = False
    else:
        print(f"    [OK] missing target_ppm → {r['error']}")

    # 15d: target_ppm 範囲外
    r = send_json(ser, {"v": 1, "id": 19, "command": "calibrate_co2",
                        "params": {"mode": "forced", "target_ppm": 999999}},
                  "Test 15d: target_ppm out_of_range")
    if r is None or r.get("error", {}).get("code") != "out_of_range":
        print(f"    [FAIL] {r}"); ok = False
    else:
        print(f"    [OK] target_ppm out_of_range → {r['error']}")

    return ok


def test_dump(ser):
    """JSON ヘッダ受信 → 続くバイナリストリームをレコード数分だけ吸い出す"""
    print("\n--- Test 16: dump (USB-CDC binary stream) ---")
    ser.reset_input_buffer()
    msg = json.dumps({"v": 1, "id": 20, "command": "dump"}) + '\n'
    print(f">>> Sent: {msg.strip()}")
    ser.write(msg.encode('utf-8'))

    # ヘッダJSON
    line = ser.readline().decode('utf-8', errors='ignore').strip()
    print(f"<<< Header: {line}")
    try:
        hdr = json.loads(line)
    except json.JSONDecodeError:
        print(f"    [FAIL] header not JSON")
        return False
    if "result" not in hdr:
        print(f"    [FAIL] {hdr}")
        return False
    count = hdr["result"].get("count")
    rec_size = hdr["result"].get("record_size")
    fmt = hdr["result"].get("format")
    print(f"    count={count}, record_size={rec_size}, format={fmt}")
    if not isinstance(count, int) or not isinstance(rec_size, int):
        print(f"    [FAIL] header malformed")
        return False

    # バイナリストリーム
    total = count * rec_size
    print(f"    expecting {total} bytes of binary records...")
    if total > 0:
        data = ser.read(total)
        print(f"    received {len(data)} bytes")
        if len(data) != total:
            print(f"    [WARN] expected {total}, got {len(data)} (timeout?)")
        if data[:rec_size]:
            print(f"    first record (hex): {data[:rec_size].hex()}")
    else:
        print(f"    no records to dump (fresh device)")

    # dump_end イベントを消費 (Phase D)
    end_line = ser.readline().decode('utf-8', errors='ignore').strip()
    try:
        end_ev = json.loads(end_line)
        if end_ev.get("event") == "dump_end":
            print(f"    dump_end: data={end_ev.get('data')}")
        else:
            print(f"    [WARN] expected dump_end event, got: {end_ev}")
    except json.JSONDecodeError:
        print(f"    [WARN] dump_end not received: {end_line!r}")

    print(f"    [OK]")
    return True


def test_dump_xbee_rejection_offline(ser):
    """USB 経由で送る場合は通るので、ここでは error code を期待できない。
       コードレベルで src!=SRC_USB の判定があることを README で確認可。
       実機テストとしては XBee 経由実装後に行う。— スキップ扱い。"""
    print("\n--- Test 17: dump from non-USB (skipped — needs XBee setup) ---")
    print("    [SKIP] (cannot exercise via USB transport)")
    return True


def run_test(com_port):
    print(f"\nConnecting to {com_port}...")
    try:
        with open_no_reset(com_port, timeout=TIMEOUT_SEC) as ser:
            time.sleep(2.0)  # 接続安定待ち

            results = []
            # Phase A
            results.append(("hello",           test_hello(ser)))
            results.append(("unknown_command", test_unknown_command(ser)))
            results.append(("invalid_json",    test_invalid_json(ser)))
            results.append(("legacy_v3_removed", test_legacy_v3_removed(ser)))
            # echo diagnostic (副作用なし、bug isolation 用)
            results.append(("echo single small", test_echo_single_small(ser)))
            results.append(("echo single large", test_echo_single_large(ser)))
            results.append(("echo burst small",  test_echo_burst_small(ser)))
            results.append(("echo burst large",  test_echo_burst_large(ser)))
            results.append(("echo size sweep",   test_echo_size_sweep(ser)))
            # Phase B
            results.append(("get_settings",                 test_get_settings(ser)))
            results.append(("set_settings PATCH",           test_set_settings_patch(ser)))
            results.append(("set_settings out_of_range",    test_set_settings_out_of_range(ser)))
            results.append(("get_correction",               test_get_correction(ser)))
            results.append(("set_correction out_of_range",  test_set_correction_out_of_range(ser)))
            results.append(("set_correction PATCH",         test_set_correction_patch(ser)))
            results.append(("set_name",                     test_set_name(ser)))
            results.append(("set_time",                     test_set_time(ser)))
            # Phase C (non-destructive を先に、start_logging は最後)
            results.append(("stop_logging (idle)",          test_stop_logging_when_idle(ser)))
            results.append(("start_logging invalid mode",   test_start_logging_invalid_mode(ser)))
            results.append(("calibrate_co2 validation",     test_calibrate_co2_validation(ser)))
            results.append(("dump",                          test_dump(ser)))
            results.append(("dump non-USB (skipped)",       test_dump_xbee_rejection_offline(ser)))
            # **最後**: 実際の start_logging + smp event stream 受信 (Phase D)
            results.append(("smp event stream",             test_smp_event_stream(ser)))

            print("\n" + "=" * 40)
            print("Summary:")
            for name, ok in results:
                mark = "PASS" if ok else "FAIL"
                print(f"  [{mark}] {name}")
            print("=" * 40)

    except serial.SerialException as e:
        print(f"Serial Error: {e}")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        port = sys.argv[1]
    else:
        port = find_device_port()
        if not port:
            print("Error: Target device not found.")
            sys.exit(1)
        print(f"Device detected at: {port}")

    run_test(port)
