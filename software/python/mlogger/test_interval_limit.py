"""
set_settings の interval 範囲検証テスト (USB-CDC 経由)。

firmware 側で interval_* を uint32_t 化し、protocol 上限 0-99999 [sec] を
歪みなく保持できることの確認用。16bit 時代は 65536 以上が silent wrap
(86400 → 20864)、32768〜65535 が (int) キャストで負値になり毎秒計測に
化けていた。

テスト内容:
  1. get_settings で現在値を退避
  2. interval=100000 → out_of_range エラー (protocol 上限超え)
  3. interval=86400 (1日) → 成功し、読み返しも 86400 (旧: 20864 に化けた)
  4. interval=40000 → 成功 (旧: 負値化で毎秒計測に化けた)
  5. interval=99999 (上限値) → 成功
  6. interval=60 → 成功 (通常値)
  7. 退避した元の interval に復元

Usage:
    python test_interval_limit.py           # auto-detect
    python test_interval_limit.py COM3
"""
import json
import sys
import time

from ble_trace import open_no_reset, find_device_port

_next_id = [0]


def send_cmd(ser, command, params=None, timeout=3.0):
    """コマンドを送り、同 id の応答 (result/error) を返す。event 行は読み飛ばす。"""
    _next_id[0] += 1
    req = {"v": 1, "id": _next_id[0], "command": command}
    if params is not None:
        req["params"] = params
    ser.reset_input_buffer()
    ser.write((json.dumps(req) + "\n").encode("utf-8"))
    end = time.time() + timeout
    while time.time() < end:
        line = ser.readline().decode("utf-8", errors="ignore").strip()
        if not line or not line.startswith("{"):
            continue  # diag (#...) や空行はスキップ
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            continue
        if obj.get("id") == _next_id[0]:
            return obj
    return None


def set_general_interval(ser, interval):
    return send_cmd(ser, "set_settings", {"general": {"interval": interval}})


def expect(name, cond, detail=""):
    status = "PASS" if cond else "FAIL"
    print(f"  [{status}] {name} {detail}")
    return cond


def expect_accepted(ser, ok, interval):
    res = set_general_interval(ser, interval)
    ok &= expect(f"interval={interval} -> accepted",
                 res is not None and res.get("result", {}).get("general", {}).get("interval") == interval,
                 f"got: {res}")
    res = send_cmd(ser, "get_settings")
    ok &= expect(f"get_settings reads back {interval}",
                 res is not None and res.get("result", {}).get("general", {}).get("interval") == interval,
                 f"got: {res}")
    return ok


def main(port):
    ok = True
    with open_no_reset(port, timeout=0.5) as ser:
        time.sleep(1.0)

        # 1. 現在値を退避
        res = send_cmd(ser, "get_settings")
        assert res and "result" in res, f"get_settings failed: {res}"
        original = res["result"]["general"]["interval"]
        print(f"current general.interval = {original}")

        # 2. protocol 上限超え
        res = set_general_interval(ser, 100000)
        ok &= expect("interval=100000 -> out_of_range",
                     res is not None and res.get("error", {}).get("code") == "out_of_range",
                     f"got: {res}")

        # 3-5. 16bit 時代に壊れていた値域が正しく保持されること
        ok = expect_accepted(ser, ok, 86400)   # 1日 (旧: 20864 に wrap)
        ok = expect_accepted(ser, ok, 40000)   # 旧: (int) 負値化で毎秒計測
        ok = expect_accepted(ser, ok, 99999)   # 上限値

        # 6. 通常値
        ok = expect_accepted(ser, ok, 60)

        # 7. 元の値に復元
        res = set_general_interval(ser, original)
        ok &= expect(f"restore interval={original}",
                     res is not None and res.get("result", {}).get("general", {}).get("interval") == original,
                     f"got: {res}")

    print()
    print("ALL PASS" if ok else "SOME TESTS FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    port = sys.argv[1] if len(sys.argv) > 1 else find_device_port()
    if not port:
        print("No M-Logger found. Pass COM port explicitly: python test_interval_limit.py COMx")
        sys.exit(2)
    sys.exit(main(port))
