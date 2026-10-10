"""
M-Logger 出荷前試験スクリプト (USB-CDC 経由)。

温湿度・グローブ温度・CO2・照度・風速の各センサとフラッシュメモリを一通り
動作させ、結果を web/inspection/reports/<hardware_id>.json に記録する。

試験手順:
  1. hello で個体情報 (name / hardware_id / FW version) を取得
  1b. XBee の設定値 (AP/SM/BD/BT/CE/PAN ID) を期待値と比較
  1c. BLE: PC の Bluetooth から携帯アプリと同じ手順で接続・認証し、コマンドが通るか
  1d. Zigbee: 同じ PC に挿した親機 XBee から Zigbee でコマンドを送り、応答が返るか
  2. get_battery で電池電圧を確認
  3. 設定を退避し、全センサ有効 / interval=1sec に変更
  4. set_time で RTC 設定 (フラッシュ記録の前提条件)
  5. start_logging (usb + flash) で計測開始
  6. ウォームアップ完了 (wu 消滅) を待つ (CO2 conditioning ~25sec + 風速加熱 ~10sec)
  7. N サンプル収集し、各チャネルの出現率とレンジを検査
     (dc フラグ = プローブ切断は即 FAIL)
  8. stop_logging → dump でフラッシュから実データを読み返して検証
  9. clear_data + 設定復元で出荷状態に戻す
 10. 結果を表示し JSON 保存。全項目 PASS なら exit 0

判定レンジは室内 (照明あり・常温・無風〜微風) での試験を前提とする。
照度センサを覆ったまま試験すると illuminance が FAIL するので注意。

無線の試験には次が必要:
  - pip install digi-xbee bleak cryptography
  - 試験 PC に Zigbee 親機 (MLServer と同じ設定の XBee、Coordinator) を挿しておく。
    MLServer は止めておく (親機のポートを使うため)
  - 試験 PC の Bluetooth を有効にし、本体にスマホを接続しない (接続中はアドバタイズしない)

Usage:
    python factory_test.py                  # auto-detect COM port
    python factory_test.py COM3
    python factory_test.py COM3 --coord COM5  # 親機のポートを指定 (省略時は自動検出)
    python factory_test.py --id 1234        # 試験冒頭で名称を MLogger_1234 に設定
                                            # (firmware が XBee の BLE 名にも反映する)
"""
import asyncio
import hashlib
import json
import os
import struct
import sys
import time
from datetime import datetime

from ble_trace import open_no_reset, find_device_port

# ============================================================
# 試験パラメータ
# ============================================================
SCRIPT_VERSION = "1.4"  # 記録 JSON に埋める試験スクリプト版数

SAMPLE_COUNT      = 15    # ウォームアップ後に収集するサンプル数
WARMUP_TIMEOUT_S  = 90    # ウォームアップ完了待ちの上限 [sec]
PRESENCE_RATIO    = 0.8   # 各チャネルの最低出現率 (欠測許容 20%)
DC_FAIL_S         = 5     # dc (切断) 表示がこの秒数継続したら即 FAIL

RANGES = {
    "t":  (-10.0, 50.0),    # 乾球温度 [C]
    "h":  (5.0, 95.0),      # 相対湿度 [%]
    "g":  (-10.0, 50.0),    # グローブ温度 [C]
    "c":  (300, 10000),     # CO2 [ppm]
    "l":  (1, 200000),      # 照度 [lx] (照明のある室内前提。0 は素子カバー or 故障)
    "v":  (0.0, 10.0),      # 風速 [m/s] (無風で 0.000 は正常)
    "vv": (50, 1800),      # 熱線ブリッジ電圧 [mV] (静穏気流の室内で 1.8 V を超えるのは不良 = 2 V 張り付き等)
}
# 下限を最小値ではなく平均値で判定するチャネル。熱線ブリッジ電圧は加熱直後などに
# 一時的に小さくなることがあるため (0 V 張り付きは平均で検出できる)
MEAN_LOWER_BOUND = {"vv"}
BATTERY_RANGE_MV = (2000, 3500)

# 本体 XBee の設定の期待値 (get_radio_info のキー: 値)。main.c 冒頭の XBee 設定と対応
XBEE_EXPECTED = {
    "ap": 1,   # API mode (エスケープなし)
    "sm": 1,   # Pin Hibernate
    "bd": 7,   # 115200 bps
    "bt": 1,   # BLE 有効
    "ce": 0,   # End Device
}

COORD_BAUD      = 9600  # 親機 XBee の baud (MLServer と同じ)
ZIGBEE_WAIT_S   = 60    # 本体が親機のネットワークに参加して応答するまで待つ上限 [sec]

RECORD_FORMAT = "<BIBIhhHHHH"  # SensorData_t (22 bytes)
RECORD_SIZE = struct.calcsize(RECORD_FORMAT)

# 成績の保存先。公開サイトのソース (リポジトリの web/) に直接書き出し、git で管理する。
# ファイル名は <hardware_id>.json。
REPORTS_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                            "..", "..", "..", "web", "inspection", "reports"))

_next_id = [100]


# ============================================================
# 通信ユーティリティ
# ============================================================
def _readline_obj(ser):
    """1 行読んで JSON なら dict を返す。diag(#)/空行/非 JSON は None。"""
    line = ser.readline().decode("utf-8", errors="ignore").strip()
    if not line or not line.startswith("{"):
        return None
    try:
        return json.loads(line)
    except json.JSONDecodeError:
        return None


def send_cmd(ser, command, params=None, timeout=5.0):
    """コマンド送信し、同 id の応答を返す。途中の smp 等の event は読み飛ばす。"""
    _next_id[0] += 1
    req = {"v": 1, "id": _next_id[0], "command": command}
    if params is not None:
        req["params"] = params
    ser.write((json.dumps(req) + "\n").encode("utf-8"))
    end = time.time() + timeout
    while time.time() < end:
        obj = _readline_obj(ser)
        if obj and obj.get("id") == _next_id[0]:
            return obj
    return None


def cmd_result(ser, command, params=None, timeout=5.0):
    res = send_cmd(ser, command, params, timeout)
    if res is None:
        raise RuntimeError(f"{command}: no response")
    if "error" in res:
        raise RuntimeError(f"{command}: error response {res['error']}")
    return res["result"]


# ============================================================
# 結果記録
# ============================================================
class Report:
    def __init__(self):
        self.items = []

    def add(self, item, passed, detail):
        self.items.append({"item": item, "pass": bool(passed), "detail": detail})
        print(f"  [{'PASS' if passed else 'FAIL'}] {item}: {detail}")

    def all_pass(self):
        return all(i["pass"] for i in self.items)


# ============================================================
# 試験本体
# ============================================================
def collect_samples(ser, report):
    """ウォームアップ完了を待ってから SAMPLE_COUNT サンプル収集して返す。"""
    print(f"ウォームアップ待ち (最大 {WARMUP_TIMEOUT_S}sec。CO2 conditioning ~25sec + 風速加熱 ~10sec)...")
    samples = []
    warmed = False
    dc_since = {}     # category -> 最初に dc を見た時刻
    t_end = time.time() + WARMUP_TIMEOUT_S
    last_note = ""

    while time.time() < t_end:
        obj = _readline_obj(ser)
        if not obj or obj.get("event") != "smp":
            continue
        data = obj.get("data", {})

        # 切断検知: 一定時間継続したら即 FAIL で打ち切り
        now = time.time()
        for cat in data.get("dc", []):
            dc_since.setdefault(cat, now)
            if now - dc_since[cat] >= DC_FAIL_S:
                name = {"g": "T/RH/CO2/Globe probe", "v": "Velocity probe"}.get(cat, cat)
                report.add("Probe connection", False, f"{name} disconnected (dc={cat})")
                return None
        for cat in list(dc_since):
            if cat not in data.get("dc", []):
                del dc_since[cat]

        wu = data.get("wu", [])
        note = f"warmup 中: {wu}" if wu else "warmup 完了、サンプル収集中"
        if note != last_note:
            print(f"  {note}")
            last_note = note

        if not warmed:
            # wu が消え、主要キー (t と v) が実際に載り始めたら収集開始
            if not wu and "t" in data and "v" in data:
                warmed = True
            else:
                continue

        samples.append(data)
        if len(samples) >= SAMPLE_COUNT:
            report.add("Probe connection", True, "no disconnection")
            return samples

    report.add("Probe connection", False,
               f"warmup not completed within {WARMUP_TIMEOUT_S}s "
               f"(collected {len(samples)}/{SAMPLE_COUNT})")
    return None


def check_channels(samples, report):
    """各チャネルの出現率とレンジを検査。"""
    n = len(samples)
    channel_names = {
        "t": "T/RH sensor (dry-bulb)",
        "h": "T/RH sensor (humidity)",
        "g": "Globe temperature sensor",
        "c": "CO2 sensor",
        "l": "Illuminance sensor",
        "v": "Velocity sensor (air speed)",
        "vv": "Velocity sensor (bridge voltage)",
    }
    stats = {}
    for key, name in channel_names.items():
        vals = [s[key] for s in samples if key in s]
        lo, hi = RANGES[key]
        if len(vals) < n * PRESENCE_RATIO:
            report.add(name, False, f"insufficient valid samples {len(vals)}/{n}")
            continue
        vmin, vmax = min(vals), max(vals)
        vmean = sum(vals) / len(vals)
        stats[key] = {"n": len(vals), "min": vmin, "mean": round(vmean, 3), "max": vmax}
        in_range = lo <= (vmean if key in MEAN_LOWER_BOUND else vmin) and vmax <= hi
        report.add(name, in_range,
                   f"n={len(vals)}/{n} min={vmin} mean={round(vmean, 2)} max={vmax}"
                   + ("" if in_range else f" (allowed {lo}-{hi})"))
    return stats


def _attempt_dump(ser, count, rec_size, session_start_ts):
    """dump 1 回分の実行と検証。(ok, detail) を返す。"""
    # dump: JSON ヘッダ行 → バイナリ count*rec_size B → dump_end イベント行
    _next_id[0] += 1
    req = {"v": 1, "id": _next_id[0], "command": "dump"}
    ser.reset_input_buffer()
    ser.write((json.dumps(req) + "\n").encode("utf-8"))
    header = None
    end = time.time() + 5.0
    while time.time() < end:
        obj = _readline_obj(ser)
        if obj and obj.get("id") == _next_id[0]:
            header = obj
            break
    if not header or "result" not in header:
        return False, f"no/invalid dump header: {header}"

    total = count * rec_size
    blob = b""
    end = time.time() + 30.0
    while len(blob) < total and time.time() < end:
        chunk = ser.read(total - len(blob))
        if chunk:
            blob += chunk
    if len(blob) < total:
        return False, f"binary underrun {len(blob)}/{total} bytes"

    # dump_end を確認 (少し待つ)
    got_end = False
    end = time.time() + 5.0
    while time.time() < end:
        obj = _readline_obj(ser)
        if obj and obj.get("event") == "dump_end":
            got_end = True
            break

    # レコード検証: 世代一定・timestamp 単調非減少・valid_flags 非ゼロ・試験時刻と整合
    gens, ts_list, flag_ok = set(), [], True
    for i in range(count):
        gen, ts, flags, ill, tdry, tglb, hum, wind, volt, co2 = \
            struct.unpack(RECORD_FORMAT, blob[i * rec_size:(i + 1) * rec_size])
        gens.add(gen)
        ts_list.append(ts)
        if flags == 0:
            flag_ok = False
    monotonic = all(a <= b for a, b in zip(ts_list, ts_list[1:]))
    ts_sane = all(abs(t - session_start_ts) < 3600 for t in ts_list)
    ok = len(gens) == 1 and monotonic and flag_ok and ts_sane and got_end
    detail = (f"read back {count} records: gen={sorted(gens)} ts_monotonic={monotonic} "
              f"flags_nonzero={flag_ok} ts_sane={ts_sane} dump_end={got_end}")
    return ok, detail


def verify_flash(ser, report, session_start_ts):
    """dump でフラッシュから実データを読み返し、レコード構造を検証。

    60 秒周期の ready ハートビートの diag 行がバイナリ受信に割り込むと
    稀に検証が乱れるため、失敗時は 1 回だけリトライする。
    """
    res = cmd_result(ser, "get_count")
    count = res["count"]
    rec_size = res["record_size"]
    if count == 0:
        report.add("Flash memory", False, "0 records written")
        return
    if rec_size != RECORD_SIZE:
        report.add("Flash memory", False, f"record_size mismatch: {rec_size} != {RECORD_SIZE}")
        return

    ok, detail = _attempt_dump(ser, count, rec_size, session_start_ts)
    if not ok:
        print(f"  dump 検証失敗 ({detail}) → リトライ")
        time.sleep(1.0)
        ok, detail = _attempt_dump(ser, count, rec_size, session_start_ts)
    report.add("Flash memory", ok, detail)


def collect_identity(ser, report):
    """プローブ ID / XBee MAC / 補正係数など、出荷記録用の個体情報を収集。"""
    identity = {}

    # プローブ INFO BLOCK (device_id / name / data_count)
    probes = cmd_result(ser, "get_probe_info")
    identity["probes"] = probes
    th = probes.get("th_probe", {})
    vel = probes.get("velocity_probe", {})
    report.add("TH probe info",
               th.get("connected") and th.get("device_id") not in (None, "000000"),
               f"id={th.get('device_id')} name={th.get('name')} n={th.get('data_count')}"
               if th.get("connected") else "not connected")
    report.add("Velocity probe info",
               vel.get("connected") and vel.get("device_id") not in (None, "000000"),
               f"id={vel.get('device_id')} name={vel.get('name')} n={vel.get('data_count')}"
               if vel.get("connected") else "not connected")

    # XBee モジュール (64bit MAC / firmware version)
    res = send_cmd(ser, "get_radio_info", timeout=5.0)
    if res and "result" in res:
        identity["radio"] = res["result"]
        report.add("XBee module", True,
                   f"mac={res['result'].get('xbee_mac')} fw={res['result'].get('xbee_fw')}")
    else:
        identity["radio"] = None
        report.add("XBee module", False, f"no response/error: {res}")

    # 補正係数 (出荷時の校正状態の記録。判定はしない)
    identity["correction"] = cmd_result(ser, "get_correction")
    return identity


# ============================================================
# 無線 (XBee 設定 / BLE / Zigbee)
# ============================================================
# ------------------------------------------------------------
# BLE (XBee BLE API: SRP 認証 + AES-CTR 暗号化 + User Data Relay)
# ------------------------------------------------------------
# 携帯アプリ (MLS_Mobile/MLUtility.cs の ML_PASS) と同じ XBee の BLE パスワード
BLE_PASSWORD = "ml_pass"
SRP_USER = "apiservice"

# XBee BLE API サービス
API_SERVICE_UUID  = "53da53b9-0447-425a-b9ea-9837505eb59a"
API_REQUEST_UUID  = "7dddca00-3e05-4651-9254-44074792c590"   # write
API_RESPONSE_UUID = "f9279ee9-2cd0-410c-81cc-adf11e4e5aea"   # indicate

FRAME_UNLOCK_REQ  = 0x2C
FRAME_UNLOCK_RESP = 0xAC
FRAME_RELAY_REQ   = 0x2D
FRAME_RELAY_OUT   = 0xAD
RELAY_IF_SERIAL   = 0x00

BLE_SCAN_S = 15
BLE_CMD_TIMEOUT_S  = 5.0

# SRP-6a 1024 bit group (RFC 5054)。XBee の BLE unlock はこれと SHA-256 を使う
SRP_N = int(
    "EEAF0AB9ADB38DD69C33F80AFA8FC5E86072618775FF3C0B9EA2314C"
    "9C256576D674DF7496EA81D3383B4813D692C6E0E0D5D8E250B98BE4"
    "8E495C1D6089DAD15DC7D7B46154D6B6CE8EF4AD69B15D4982559B29"
    "7BCF1885C529F566660E57EC68EDBC3C05726CC02FD4CBF4976EAA9A"
    "FD5138FE8376435B9FC61D2FC0EB06E3", 16)
SRP_G = 2
SRP_N_LEN = 128

UNLOCK_ERRORS = {
    0x80: "unable to offer B (bad A)",
    0x81: "incorrect payload length",
    0x82: "bad proof of key (password mismatch)",
    0x83: "resource allocation error",
    0x84: "step out of sequence",
}


# ============================================================
# SRP-6a (クライアント側)
# ============================================================
def _b(n):
    """整数を最小長のビッグエンディアンのバイト列に (0 は 1 バイト)。"""
    return n.to_bytes(max(1, (n.bit_length() + 7) // 8), "big")


def _h(*parts):
    m = hashlib.sha256()
    for p in parts:
        m.update(p)
    return m.digest()


def _hi(*parts):
    return int.from_bytes(_h(*parts), "big")


class SrpClient:
    def __init__(self, user, password):
        self.user = user.encode()
        self.password = password.encode()
        while True:
            self.a = int.from_bytes(os.urandom(32), "big")
            self.A = pow(SRP_G, self.a, SRP_N)
            if self.A.bit_length() > (SRP_N_LEN - 1) * 8:   # 128 バイトちょうどになる A を使う
                break

    def a_bytes(self):
        return self.A.to_bytes(SRP_N_LEN, "big")

    def process_challenge(self, salt, b_bytes):
        """salt と B から M1 を計算して返す。B が不正なら None。"""
        B = int.from_bytes(b_bytes, "big")
        if B % SRP_N == 0:
            return None
        A_b, B_b = _b(self.A), _b(B)
        u = _hi(A_b, B_b)
        k = _hi(_b(SRP_N), _b(SRP_G))
        # x = H(s | H(I | ":" | P))。salt は digi-xbee の verifier 生成と同じく整数化して扱う
        x = _hi(_b(int.from_bytes(salt, "big")), _h(self.user, b":", self.password))
        S = pow((B - k * pow(SRP_G, x, SRP_N)) % SRP_N, self.a + u * x, SRP_N)
        self.K = _h(_b(S))
        h_xor = bytes(i ^ j for i, j in zip(_h(_b(SRP_N)), _h(_b(SRP_G))))
        self.M1 = _h(h_xor, _h(self.user), salt, A_b, B_b, self.K)
        self.M2 = _h(A_b, self.M1, self.K)
        return self.M1


# ============================================================
# API フレーム
# ============================================================
def build_frame(data):
    return bytes([0x7E, len(data) >> 8, len(data) & 0xFF]) + data + bytes([0xFF - (sum(data) & 0xFF)])


class FrameParser:
    """バイトストリームから API フレームのデータ部を取り出す (チェックサム不一致は捨てる)。"""
    def __init__(self):
        self.buf = bytearray()

    def feed(self, data):
        self.buf += data
        frames = []
        while True:
            start = self.buf.find(0x7E)
            if start < 0:
                self.buf.clear()
                break
            del self.buf[:start]
            if len(self.buf) < 3:
                break
            length = (self.buf[1] << 8) | self.buf[2]
            if len(self.buf) < length + 4:
                break
            body = bytes(self.buf[3:3 + length])
            cs = self.buf[3 + length]
            del self.buf[:length + 4]
            if (sum(body) + cs) & 0xFF == 0xFF:
                frames.append(body)
        return frames


class XBeeBleLink:
    def __init__(self, client):
        self.client = client
        self.parser = FrameParser()
        self.frames = asyncio.Queue()
        self.enc = None      # 本体 XBee へ送るフレームの暗号化
        self.dec = None      # 本体 XBee から届くフレームの復号
        self.chunk = 20

    def _on_indicate(self, _sender, data):
        data = bytes(data)
        if self.dec is not None:
            data = self.dec.update(data)
        for f in self.parser.feed(data):
            self.frames.put_nowait(f)

    async def start(self):
        mtu = getattr(self.client, "mtu_size", 23) or 23
        self.chunk = max(20, min(mtu - 3, 240))
        await self.client.start_notify(API_RESPONSE_UUID, self._on_indicate)

    async def send(self, data):
        raw = build_frame(data)
        if self.enc is not None:
            raw = self.enc.update(raw)
        for i in range(0, len(raw), self.chunk):
            await self.client.write_gatt_char(API_REQUEST_UUID, raw[i:i + self.chunk], response=True)

    async def recv(self, frame_type, timeout):
        end = time.monotonic() + timeout
        while True:
            left = end - time.monotonic()
            if left <= 0:
                return None
            try:
                f = await asyncio.wait_for(self.frames.get(), left)
            except asyncio.TimeoutError:
                return None
            if f and f[0] == frame_type:
                return f

    async def unlock(self, password):
        srp = SrpClient(SRP_USER, password)
        await self.send(bytes([FRAME_UNLOCK_REQ, 1]) + srp.a_bytes())
        r = await self.recv(FRAME_UNLOCK_RESP, 5.0)
        if r is None:
            raise RuntimeError("unlock: no response to step 1")
        if r[1] != 2:
            raise RuntimeError(f"unlock step 1: {UNLOCK_ERRORS.get(r[1], hex(r[1]))}")
        salt, b_bytes = r[2:6], r[6:6 + SRP_N_LEN]
        m1 = srp.process_challenge(salt, b_bytes)
        if m1 is None:
            raise RuntimeError("unlock: invalid B from device")
        await self.send(bytes([FRAME_UNLOCK_REQ, 3]) + m1)
        r = await self.recv(FRAME_UNLOCK_RESP, 5.0)
        if r is None:
            raise RuntimeError("unlock: no response to step 3")
        if r[1] != 4:
            raise RuntimeError(f"unlock step 3: {UNLOCK_ERRORS.get(r[1], hex(r[1]))}")
        m2, tx_nonce, rx_nonce = r[2:34], r[34:46], r[46:58]
        if m2 != srp.M2:
            raise RuntimeError("unlock: device proof (M2) mismatch")
        # 以降のフレームは AES-256-CTR で暗号化される。カウンタは nonce(12B) + 1 から始まる。
        # TX/RX はクライアント側から見た名前で、送信に TX nonce、受信に RX nonce を使う (実機で確認)
        from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
        ctr = (1).to_bytes(4, "big")
        self.enc = Cipher(algorithms.AES(srp.K), modes.CTR(tx_nonce + ctr)).encryptor()
        self.dec = Cipher(algorithms.AES(srp.K), modes.CTR(rx_nonce + ctr)).decryptor()

    async def command(self, req_id, command, timeout=BLE_CMD_TIMEOUT_S):
        """User Data Relay で本体マイコンに JSON コマンドを送り、同 id の応答を返す。"""
        line = json.dumps({"v": 1, "id": req_id, "command": command}) + "\n"
        await self.send(bytes([FRAME_RELAY_REQ, 0, RELAY_IF_SERIAL]) + line.encode())
        text = ""
        end = time.monotonic() + timeout
        while True:
            f = await self.recv(FRAME_RELAY_OUT, end - time.monotonic())
            if f is None:
                return None
            text += f[2:].decode("utf-8", errors="ignore")
            while "\n" in text:
                one, text = text.split("\n", 1)
                one = one.strip()
                if not one.startswith("{"):
                    continue
                try:
                    obj = json.loads(one)
                except json.JSONDecodeError:
                    continue
                if obj.get("id") == req_id:
                    return obj


async def ble_run_test(addr, add):
    """BLE で接続・認証し、hello / get_battery が通るかを試験する。各項目は add(項目, 合否, 詳細) に記録。"""
    from bleak import BleakClient, BleakScanner

    print(f"BLE デバイスを探しています ({addr}、最大 {BLE_SCAN_S} 秒)...")
    found = await BleakScanner.find_device_by_address(addr, timeout=BLE_SCAN_S)
    if not found:
        add("BLE advertising", False, f"{addr} not found")
        return
    rssi = getattr(found, "rssi", None)
    add("BLE advertising", True, f"name={found.name}" + (f" rssi={rssi} dBm" if rssi is not None else ""))

    try:
        async with BleakClient(found, timeout=20.0) as client:
            add("BLE connect", True, f"mtu={getattr(client, 'mtu_size', '?')}")
            link = XBeeBleLink(client)
            await link.start()
            try:
                await link.unlock(BLE_PASSWORD)
                add("BLE unlock", True, "password accepted")
            except Exception as e:
                add("BLE unlock", False, str(e))
                return

            res = await link.command(1, "hello")
            if res and "result" in res:
                r = res["result"]
                add("BLE command (hello)", True,
                    f"name={r.get('name')} hw={r.get('hardware_id')} FW {r.get('firmware_version')}")
            else:
                add("BLE command (hello)", False, f"no response: {res}")
                return

            res = await link.command(2, "get_battery")
            if res and "result" in res:
                add("BLE command (get_battery)", True, f"{res['result'].get('voltage_mv')} mV")
            else:
                add("BLE command (get_battery)", False, f"no response: {res}")
    except Exception as e:
        add("BLE connect", False, f"{type(e).__name__}: {e}")


def open_coordinator(port, exclude_port):
    """親機 XBee (Coordinator) を開いて返す。port 省略時は自動検出。見つからなければ None。"""
    from digi.xbee.devices import ZigBeeDevice
    import serial.tools.list_ports

    if port:
        candidates = [port]
    else:
        candidates = [p.device for p in serial.tools.list_ports.comports()
                      if p.device != exclude_port and "Bluetooth" not in (p.description or "")]
    for dev in candidates:
        coord = ZigBeeDevice(dev, COORD_BAUD)
        try:
            coord.open()
            if coord.get_parameter("CE") == b"\x01":
                print(f"親機 XBee: {dev}")
                return coord
        except Exception:
            pass
        if coord.is_open():
            coord.close()
    return None


def check_xbee_settings(radio, coord, report):
    """本体 XBee の設定値を期待値と比較する。PAN ID は親機と一致すること。"""
    if not radio:
        report.add("XBee settings", False, "get_radio_info unavailable")
        return
    bad = [f"{k}={radio.get(k)} (expected {v})"
           for k, v in XBEE_EXPECTED.items() if radio.get(k) != v]
    detail = ", ".join(f"{k}={radio.get(k)}" for k in XBEE_EXPECTED)
    pan = radio.get("pan_id")
    if coord is not None:
        # 親機の設定 PAN ID (ID) と、ID=0 のとき自動で選んだ実際の PAN ID (OP) の
        # どちらかに一致すれば良い。本体の ID=0 は「どの PAN にも参加する」設定
        coord_pans = {int(coord.get_parameter(p).hex(), 16) for p in ("ID", "OP")}
        if pan is None or (int(pan, 16) != 0 and int(pan, 16) not in coord_pans):
            bad.append(f"pan_id={pan} (coordinator {', '.join(f'{p:X}' for p in coord_pans)})")
    detail += f", pan_id={pan}"
    report.add("XBee settings", not bad, "; ".join(bad) if bad else detail)


def check_ble(radio, report):
    """携帯アプリと同じ経路で BLE 通信を試験する: アドバタイズ → 接続 → パスワード認証
    → User Data Relay で hello / get_battery を送り応答を確認。"""
    try:
        import bleak  # noqa: F401
        import cryptography  # noqa: F401
    except ImportError as e:
        report.add("BLE advertising", False, f"{e} (pip install bleak cryptography)")
        return
    mac = (radio or {}).get("ble_mac")
    if not mac or len(mac) != 12:
        report.add("BLE advertising", False, f"BLE MAC unavailable: {mac}")
        return
    addr = ":".join(mac[i:i + 2] for i in range(0, 12, 2)).upper()
    try:
        asyncio.run(ble_run_test(addr, report.add))
    except Exception as e:
        report.add("BLE link", False, f"{type(e).__name__}: {e}")


def check_zigbee(radio, coord, report):
    """親機 XBee から本体へ Zigbee でコマンドを送り、応答が返るかを確認する。
    本体は非ロギング中は XBee を起こしたままにしているので、そのまま届く。"""
    if coord is None:
        report.add("Zigbee link", False, "coordinator XBee not found (stop MLServer / use --coord)")
        return
    mac = (radio or {}).get("xbee_mac")
    if not mac:
        report.add("Zigbee link", False, "XBee MAC unavailable")
        return
    from digi.xbee.devices import RemoteZigBeeDevice
    from digi.xbee.models.address import XBee64BitAddress

    remote = RemoteZigBeeDevice(coord, XBee64BitAddress.from_hex_string(mac))
    print(f"Zigbee で応答を確認しています (ネットワーク参加待ち含め最大 {ZIGBEE_WAIT_S} 秒)...")
    t0 = time.time()
    last_err = ""
    cmd_id = 9000
    while time.time() - t0 < ZIGBEE_WAIT_S:
        cmd_id += 1
        req = json.dumps({"v": 1, "id": cmd_id, "command": "get_battery"}) + "\n"
        try:
            coord.send_data(remote, req.encode())
        except Exception as e:   # 本体がまだネットワークに参加していない等
            last_err = str(e)
            time.sleep(3)
            continue
        # 応答は 150 B ごとに分割されて届くことがあるので改行までつなげる
        buf = ""
        end = time.time() + 5
        while time.time() < end:
            try:
                msg = coord.read_data_from(remote, timeout=1)
            except Exception:
                msg = None
            if msg is None:
                continue
            buf += msg.data.decode("utf-8", errors="ignore")
            while "\n" in buf:
                line, buf = buf.split("\n", 1)
                try:
                    obj = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if obj.get("id") == cmd_id and "result" in obj:
                    report.add("Zigbee link", True,
                               f"response in {time.time() - t0:.1f}s "
                               f"(voltage_mv={obj['result'].get('voltage_mv')})")
                    return
        last_err = "no response"
    report.add("Zigbee link", False, f"no response within {ZIGBEE_WAIT_S}s ({last_err})")


def main(port, device_id=None, coord_port=None):
    report = Report()
    device = {}
    stats = {}
    identity = {}
    original_settings = None
    logging_started = False

    with open_no_reset(port, timeout=0.5) as ser:
        time.sleep(1.0)
        ser.reset_input_buffer()

        # --- 1. 個体情報 ---
        info = cmd_result(ser, "hello")
        device = {k: info.get(k) for k in ("name", "hardware_id", "firmware_version", "protocol_version")}
        print(f"個体: {device['name']}  hw={device['hardware_id']}  FW {device['firmware_version']}")
        if info.get("logging"):
            print("ロギング中だったため停止します")
            cmd_result(ser, "stop_logging")
            time.sleep(1.0)
            ser.reset_input_buffer()

        # --- 1a. 個体番号の付与 (--id 指定時のみ) ---
        # set_name は EEPROM の名称に加えて XBee の BLE アドバタイズ名 (BI) にも
        # firmware 側で反映される。BLE 広告名の確実な反映は電源再投入後。
        if device_id is not None:
            new_name = f"MLogger_{device_id:04d}"
            res = cmd_result(ser, "set_name", {"name": new_name})
            ok = res.get("name") == new_name
            report.add("Device name set", ok, f"{device['name']} -> {res.get('name')}")
            print(f"名称設定: {device['name']} -> {res.get('name')}")
            device["name"] = res.get("name")
            time.sleep(0.5)  # firmware 側の XBee BI/WR 適用 (~200ms) を跨がない

        try:
            # --- 1b. プローブ ID / XBee MAC / 補正係数 ---
            identity = collect_identity(ser, report)

            # --- 1c. 無線: XBee 設定値 / BLE 接続・コマンド応答 / Zigbee 応答 ---
            radio = identity.get("radio")
            try:
                coord = open_coordinator(coord_port, port)
            except ImportError:
                print("  [WARN] digi-xbee が未インストール (pip install digi-xbee)")
                coord = None
            try:
                check_xbee_settings(radio, coord, report)
                check_ble(radio, report)
                check_zigbee(radio, coord, report)
            finally:
                if coord is not None:
                    coord.close()

            # --- 2. 電池電圧 ---
            bat = cmd_result(ser, "get_battery")
            mv = bat["voltage_mv"]
            lo, hi = BATTERY_RANGE_MV
            report.add("Battery voltage", lo <= mv <= hi, f"{mv} mV (allowed {lo}-{hi})")
            device["battery_mv"] = mv

            # --- 3. 設定退避 → 試験用設定 ---
            original_settings = cmd_result(ser, "get_settings")
            cmd_result(ser, "set_settings", {
                "general":     {"enabled": True, "interval": 1},
                "velocity":    {"enabled": True, "interval": 1},
                "illuminance": {"enabled": True, "interval": 1},
            })

            # --- 4. RTC 設定 (フラッシュ記録の前提) ---
            session_start_ts = int(time.time())
            cmd_result(ser, "set_time", {"ts": session_start_ts})

            # --- 5. 計測開始 (USB でライブ観測 + フラッシュ書き込み) ---
            cmd_result(ser, "start_logging", {
                "transports": {"usb": True, "flash": True, "zigbee": False, "ble": False},
                "mode": "once",
            })
            logging_started = True

            # --- 6-7. サンプル収集とチャネル検査 ---
            samples = collect_samples(ser, report)
            if samples:
                stats = check_channels(samples, report)

            # --- 8. 停止してフラッシュ読み返し検証 ---
            cmd_result(ser, "stop_logging")
            logging_started = False
            time.sleep(1.0)
            ser.reset_input_buffer()
            verify_flash(ser, report, session_start_ts)

        finally:
            # --- 9. 出荷状態へ復帰 (途中失敗でも必ず実行) ---
            try:
                if logging_started:
                    send_cmd(ser, "stop_logging")
                    time.sleep(1.0)
                    ser.reset_input_buffer()
                send_cmd(ser, "clear_data")
                if original_settings:
                    send_cmd(ser, "set_settings", {
                        "general":     original_settings["general"],
                        "velocity":    original_settings["velocity"],
                        "illuminance": original_settings["illuminance"],
                    })
            except Exception as e:  # 復元失敗は報告のみ (試験判定には含めない)
                print(f"  [WARN] 出荷状態への復帰に失敗: {e}")

    # --- 10. 結果保存 ---
    overall = report.all_pass()
    result = {
        "test": "factory_test",
        "script_version": SCRIPT_VERSION,
        "timestamp": datetime.now().isoformat(timespec="seconds"),
        "device": device,
        "probes": identity.get("probes"),
        "radio": identity.get("radio"),
        "correction": identity.get("correction"),
        "settings_shipped": original_settings,
        "overall": "PASS" if overall else "FAIL",
        "items": report.items,
        "channel_stats": stats,
        "judge_ranges": {"channels": RANGES, "battery_mv": BATTERY_RANGE_MV},
    }
    # 保存名は Web 公開仕様に合わせて hardware_id のみ (例: 911759D0.json)。
    # 再試験は上書き。
    hwid = device.get("hardware_id", "noid")
    os.makedirs(REPORTS_DIR, exist_ok=True)
    path = os.path.join(REPORTS_DIR, f"{hwid}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print()
    print(f"総合判定: {'PASS' if overall else 'FAIL'}")
    print(f"記録: {path}")
    print(f"公開: コミット後に web/build_site.py で組み立てて配置 → "
          f"https://www.mlogger.jp/inspection/viewer.html?id={hwid}")
    return 0 if overall else 1


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser(description="M-Logger 出荷前試験")
    ap.add_argument("port", nargs="?", default=None, help="COM ポート (省略時は自動検出)")
    ap.add_argument("--id", type=int, metavar="NNNN", default=None,
                    help="4 桁の個体番号。指定すると試験冒頭で名称を MLogger_NNNN に設定"
                         " (XBee の BLE 名にも反映)")
    ap.add_argument("--coord", metavar="PORT", default=None,
                    help="Zigbee 親機 XBee の COM ポート (省略時は自動検出)")
    args = ap.parse_args()
    if args.id is not None and not (0 <= args.id <= 9999):
        print("--id は 0-9999 の範囲で指定してください")
        sys.exit(2)
    port = args.port or find_device_port()
    if not port:
        print("No M-Logger found. Pass COM port explicitly: python factory_test.py COMx")
        sys.exit(2)
    try:
        sys.exit(main(port, args.id, args.coord))
    except RuntimeError as e:
        print(f"[ABORT] {e}")
        sys.exit(1)
