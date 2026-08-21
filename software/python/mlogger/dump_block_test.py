"""
dump の block-pull (from/limit パラメータ) の firmware 実装を USB-CDC 経由で検証する。

使い方:
    python dump_block_test.py            # COM ポート自動検出
    python dump_block_test.py COM3       # 明示指定

検証内容:
    1. get_count で総件数を取得
    2. 全件を 1 回の dump (params 無し) で取得 → 参照データ
    3. 同じ範囲を dump {from, limit} の 100 件 block で分割取得
    4. ヘッダの count/from が要求どおりエコーされること、
       block 連結 == 参照データ (バイト一致) を確認
    5. 端の挙動: from=総件数 (0 件)、from+limit が末尾を跨ぐ (クランプ) を確認

BLE 経路の欠落再送は実機 BLE でしか試験できないが、firmware 側の範囲切り出しが
正しければ MAUI (JsonRpcV4Protocol.DumpAsync) の block-pull はこの上に成立する。
"""
import json
import sys
import time

import serial
import serial.tools.list_ports

BAUD_RATE       = 115200
CONNECT_TIMEOUT = 1.5
RECORD_SIZE     = 22
BLOCK_RECORDS   = 100

_next_id = [100]


def open_no_reset(port, baud=BAUD_RATE, timeout=CONNECT_TIMEOUT):
    """DTR/RTS 非アサートで open (AVR DU32 の reset 経路を踏まない)。"""
    ser = serial.Serial()
    ser.port = port
    ser.baudrate = baud
    ser.timeout = timeout
    ser.dtr = False
    ser.rts = False
    ser.open()
    return ser


def find_device_port():
    probe = (json.dumps({"v": 1, "id": 1, "command": "hello"}) + '\n').encode('utf-8')
    for p in serial.tools.list_ports.comports():
        if "Bluetooth" in p.description:
            continue
        try:
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
                        return p.device
        except (OSError, serial.SerialException):
            pass
    return None


def send_command(ser, command, params=None, timeout=3.0):
    _next_id[0] += 1
    payload = {"v": 1, "id": _next_id[0], "command": command}
    if params is not None:
        payload["params"] = params
    ser.write((json.dumps(payload) + '\n').encode('utf-8'))
    end = time.time() + timeout
    while time.time() < end:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not line:
            continue
        try:
            resp = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(resp, dict) and resp.get("id") == _next_id[0]:
            return resp
    return None


def dump_range(ser, params, timeout=60.0):
    """dump を発行し (header_result, blob) を返す。binary は header の count ぶん読む。
    後続の dump_end イベント行も消費する。"""
    ser.reset_input_buffer()
    resp = send_command(ser, "dump", params)
    if resp is None or "result" not in resp:
        raise RuntimeError(f"dump header error: {resp}")
    result = resp["result"]
    total = result["count"] * result["record_size"]
    blob = b""
    end = time.time() + timeout
    while len(blob) < total and time.time() < end:
        chunk = ser.read(total - len(blob))
        if chunk:
            blob += chunk
    if len(blob) < total:
        raise RuntimeError(f"binary underrun {len(blob)}/{total} bytes")
    # dump_end 行を消費 (0 件でも送出される)
    end = time.time() + 3.0
    while time.time() < end:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if line and '"dump_end"' in line:
            break
    return result, blob


def main():
    port = sys.argv[1] if len(sys.argv) > 1 else find_device_port()
    if not port:
        print("M-Logger が見つかりません")
        sys.exit(1)
    print(f"port: {port}")

    ok = True
    with open_no_reset(port) as ser:
        time.sleep(1.5)
        ser.reset_input_buffer()

        cnt = send_command(ser, "get_count")
        count = cnt["result"]["count"]
        print(f"get_count: {count} records")
        if count == 0:
            print("記録データが 0 件です。ロギングでデータを作ってから再実行してください。")
            sys.exit(1)

        # 参照: 全件を 1 回で
        ref_hdr, ref = dump_range(ser, None)
        print(f"full dump: count={ref_hdr['count']} from={ref_hdr.get('from')} "
              f"({len(ref)} bytes)")
        if ref_hdr.get("from") != 0 or ref_hdr["count"] != count:
            print("  [FAIL] full dump header mismatch")
            ok = False

        # block 分割で同じ範囲を取得
        blob = b""
        done = 0
        while done < count:
            n = min(BLOCK_RECORDS, count - done)
            hdr, b = dump_range(ser, {"from": done, "limit": n})
            if hdr.get("from") != done or hdr["count"] != n:
                print(f"  [FAIL] block header echo mismatch: req from={done} n={n} "
                      f"got from={hdr.get('from')} count={hdr['count']}")
                ok = False
                break
            blob += b
            done += n
        print(f"block dump: {done}/{count} records ({len(blob)} bytes)")
        if blob != ref:
            print("  [FAIL] block 連結が full dump と一致しません")
            ok = False
        else:
            print("  [PASS] block 連結 == full dump (バイト一致)")

        # 端の挙動: from=count → 0 件
        hdr, b = dump_range(ser, {"from": count, "limit": 10})
        if hdr["count"] == 0 and len(b) == 0:
            print("  [PASS] from=count → 0 件")
        else:
            print(f"  [FAIL] from=count → count={hdr['count']} len={len(b)}")
            ok = False

        # 端の挙動: 末尾跨ぎはクランプ
        frm = max(0, count - 5)
        hdr, b = dump_range(ser, {"from": frm, "limit": 1000})
        expect = count - frm
        if hdr["count"] == expect and len(b) == expect * RECORD_SIZE \
                and b == ref[frm * RECORD_SIZE:]:
            print(f"  [PASS] 末尾跨ぎ limit → {expect} 件にクランプ (末尾一致)")
        else:
            print(f"  [FAIL] 末尾跨ぎ: count={hdr['count']} expect={expect}")
            ok = False

    print()
    print("=== PASS ===" if ok else "=== FAIL ===")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
