"""
OSL (Overlay Sensing Layer) 規格準拠の温湿度+CO2+グローブ温度プローブ
(mlogger_th_sensor) を CP2112 USB-I2C ブリッジ経由で操作する Python クライアント。

仕様: SHASE 2026 第2報 (Togashi 2026) + firmware の i2c_shared_data.h
  - INFO BLOCK (0x00-0x27): device_id / addr_key / new_addr / data_count /
    unit_type[8] / name[16]  ※接続時に 1 回読む
  - POLL BLOCK (0x28-0x4B): status1 / status2 / reserved / value[8] float
    ※毎周期 1 回読む (36 byte 一括取得)
    value[0]=温度 [°C], value[1]=相対湿度 [%RH], value[2]=CO2 [ppm],
    value[3]=グローブ温度 [°C]
  - EXTENSION (0x4C-):
    * 補正係数 (8 float, 製造校正用): t_coef_a/b, rh_coef_a/b, co2_coef_a/b,
      t_glb_coef_a/b
    * STCC4 制御コマンド: stcc4_cmd / stcc4_cmd_arg / stcc4_state / frc_correction

リトルエンディアン、無 alignment、CRC 無し。Status1 の bit i が立っていれば
value[i] は stale / 異常。

計測モデル:
  status2 に 0 を書き込むと子機が single-shot 計測を開始する (pre-trigger)。
  ~500 ms 後に POLL ブロックを読むと全 4 値が更新されている。

校正コマンド (stcc4_cmd 経由):
  0x01 = FRC (perform_forced_recalibration, 30sec 連続測定 + FRC ~35sec)
         arg に target ppm を入れて発行
  0x02 = factory_reset (FRC/ASC 履歴消去, ~90ms)
  0x03 = perform_conditioning (センサ立ち上げ調整, ~22sec)
"""
import struct
import sys
import time
from typing import Any, Dict, List, Optional

from cp2112_driver import CP2112Device


# ============================================================
# OSL register map 定数
# ============================================================
class ThSensorRegisters:
    DEFAULT_I2C_ADDR  = 0x11   # 風速プローブ (0x10) と衝突しない値
    ADDR_KEY_UNLOCK   = 0xA5

    # ---- INFO BLOCK (0x00-0x27) ------------------------------------------
    REG_DEVICE_ID     = 0x00   # uint32 R  : FNV-1a 22bit (0..0x3FFFFE)
    REG_ADDR_KEY      = 0x04   # uint8  W  : アドレス変更鍵 (STOP で自動クリア)
    REG_NEW_ADDR      = 0x05   # uint8  R/W: 新しい I2C アドレス
    REG_DATA_COUNT    = 0x06   # uint8  R  : 有効計測値の数 (本子機は 4)
    REG_UNIT_TYPE     = 0x08   # uint16[8] R: BACnet engineering unit codes
    REG_NAME          = 0x18   # char[16]  R/W: 装置ラベル (NUL 終端)
    NAME_LEN          = 16

    # ---- POLL BLOCK (0x28-0x4B) ------------------------------------------
    REG_POLL_BASE     = 0x28
    POLL_BLOCK_SIZE   = 36     # status1(1) + status2(1) + reserved(2) + value[8](32)
    POLL_OFS_STATUS1  = 0
    POLL_OFS_STATUS2  = 1
    POLL_OFS_VALUE    = 4      # value[0] の先頭バイト
    NUM_VALUES        = 8

    REG_STATUS1       = 0x28   # uint8 R
    REG_STATUS2       = 0x29   # uint8 R/W : 0 を書くと single-shot 計測トリガ
    REG_VALUE         = 0x2C   # float[8] R (LE)

    # ---- EXTENSION 補正係数 (4 計測値 × a/b、EEPROM 永続) ---------------
    REG_T_COEF_A      = 0x4C
    REG_T_COEF_B      = 0x50
    REG_RH_COEF_A     = 0x54
    REG_RH_COEF_B     = 0x58
    REG_CO2_COEF_A    = 0x5C
    REG_CO2_COEF_B    = 0x60
    REG_T_GLB_COEF_A  = 0x64
    REG_T_GLB_COEF_B  = 0x68

    # ---- EXTENSION STCC4 制御コマンド (volatile) -------------------------
    REG_STCC4_CMD       = 0x6C   # uint8 W : コマンド発行 (受理後自動クリア)
    REG_STCC4_CMD_ARG   = 0x6D   # uint16 W: FRC では target ppm
    REG_STCC4_STATE     = 0x6F   # uint8 R : 進行状態
    REG_FRC_CORRECTION  = 0x70   # int16 R : 最後の FRC 補正値 [ppm signed]

    # ---- value 配列インデックス ------------------------------------------
    VAL_IDX_TEMPERATURE = 0    # value[0] = 乾球温度 [°C]
    VAL_IDX_HUMIDITY    = 1    # value[1] = 相対湿度 [%RH]
    VAL_IDX_CO2         = 2    # value[2] = CO2 濃度 [ppm]
    VAL_IDX_GLOBE_TEMP  = 3    # value[3] = グローブ温度 [°C]

    # ---- status1 ビット規約 ----------------------------------------------
    STATUS1_STALE_T     = 0x01
    STATUS1_STALE_RH    = 0x02
    STATUS1_STALE_CO2   = 0x04
    STATUS1_STALE_T_GLB = 0x08
    STATUS1_ALL_STALE   = 0xFF

    # ---- STCC4 コマンド --------------------------------------------------
    STCC4_CMD_NONE             = 0x00
    STCC4_CMD_FRC              = 0x01
    STCC4_CMD_FACTORY_RESET    = 0x02
    STCC4_CMD_CONDITIONING     = 0x03

    # ---- STCC4 状態 ------------------------------------------------------
    STCC4_STATE_IDLE                  = 0x00
    STCC4_STATE_FRC_RUNNING           = 0x01
    STCC4_STATE_FRC_DONE              = 0x02
    STCC4_STATE_FRC_FAIL              = 0x03
    STCC4_STATE_FACTORY_RESET_RUNNING = 0x04
    STCC4_STATE_FACTORY_RESET_DONE    = 0x05
    STCC4_STATE_CONDITIONING_RUNNING  = 0x06
    STCC4_STATE_CONDITIONING_DONE     = 0x07


STATE_NAMES = {
    0x00: "IDLE",
    0x01: "FRC_RUNNING",
    0x02: "FRC_DONE",
    0x03: "FRC_FAIL",
    0x04: "FACTORY_RESET_RUNNING",
    0x05: "FACTORY_RESET_DONE",
    0x06: "CONDITIONING_RUNNING",
    0x07: "CONDITIONING_DONE",
}


# ============================================================
# 主クライアントクラス
# ============================================================
class ThSensorManager(CP2112Device):
    """OSL 共通レジスタ + 温湿度/CO2/グローブ温度プローブ拡張領域の Python クライアント。"""

    def __init__(self,
                 slave_addr: int = ThSensorRegisters.DEFAULT_I2C_ADDR,
                 serial_number: Optional[str] = None):
        super().__init__(slave_addr=slave_addr, serial_number=serial_number)
        self._last_comm_time = 0.0
        # AVR128DB32 子機は通信終了後 ~1sec で POWER-DOWN に落ちる。
        # 950ms 以上空いていたら wakeup pulse を打つ。
        self._keep_alive_threshold = 0.950

    # ------------------------------------------------------------------
    # sleep 対策: 通信ごとに wakeup pulse を打つ (anemometer と同じ)
    # ------------------------------------------------------------------

    def _ensure_wakeup(self) -> None:
        if (time.time() - self._last_comm_time) > self._keep_alive_threshold:
            self._robust_wakeup()
        self._last_comm_time = time.time()

    def _robust_wakeup(self) -> None:
        # CP2112 の状態をリセット
        self._raw_write([0x17, 0x01])
        time.sleep(0.01)
        # アドレスだけ送って AVR にクロックストレッチさせる
        self._raw_write([0x14, self.slave_addr << 1, 0x00])
        time.sleep(0.002)   # SUT(10us) + VREG(24us) + CPU 起動 を待つ
        # CP2112 のバスを解放
        self._raw_write([0x17, 0x01])
        time.sleep(0.01)

    def read_i2c_block(self, offset: int, length: int) -> Optional[bytes]:
        self._ensure_wakeup()
        result = super().read_i2c_block(offset, length)
        if result is not None:
            self._last_comm_time = time.time()
        return result

    def write_i2c_block(self, offset: int, data: List[int]) -> bool:
        self._ensure_wakeup()
        result = super().write_i2c_block(offset, data)
        if result:
            self._last_comm_time = time.time()
        return result

    # ==================================================================
    # INFO BLOCK (機器固有情報、通常は接続時に 1 回読む)
    # ==================================================================

    def get_device_id(self) -> Optional[int]:
        raw = self.read_i2c_block(ThSensorRegisters.REG_DEVICE_ID, 4)
        if raw is None:
            return None
        return struct.unpack('<I', raw)[0]

    def get_data_count(self) -> Optional[int]:
        raw = self.read_i2c_block(ThSensorRegisters.REG_DATA_COUNT, 1)
        if raw is None:
            return None
        return raw[0]

    def get_unit_types(self) -> Optional[List[int]]:
        raw = self.read_i2c_block(ThSensorRegisters.REG_UNIT_TYPE, 16)
        if raw is None:
            return None
        return list(struct.unpack('<8H', raw))

    def get_name(self) -> Optional[str]:
        raw = self.read_i2c_block(ThSensorRegisters.REG_NAME,
                                  ThSensorRegisters.NAME_LEN)
        if raw is None:
            return None
        nul = raw.find(0)
        if nul >= 0:
            raw = raw[:nul]
        return raw.decode('ascii', errors='replace')

    def set_name(self, name: str) -> bool:
        encoded = name.encode('ascii', errors='replace')[:ThSensorRegisters.NAME_LEN - 1]
        padded  = encoded.ljust(ThSensorRegisters.NAME_LEN, b'\x00')
        return self.write_i2c_block(ThSensorRegisters.REG_NAME, list(padded))

    # ==================================================================
    # POLL BLOCK (動的情報)
    # ==================================================================

    def trigger_measurement(self) -> bool:
        """status2 に 0 を書いて子機に single-shot 計測を開始させる。"""
        return self.write_i2c_block(ThSensorRegisters.REG_STATUS2, [0])

    def read_poll_block(self) -> Optional[Dict[str, Any]]:
        raw = self.read_i2c_block(ThSensorRegisters.REG_POLL_BASE,
                                  ThSensorRegisters.POLL_BLOCK_SIZE)
        if raw is None:
            return None
        status1 = raw[ThSensorRegisters.POLL_OFS_STATUS1]
        status2 = raw[ThSensorRegisters.POLL_OFS_STATUS2]
        values  = list(struct.unpack(
            '<8f',
            raw[ThSensorRegisters.POLL_OFS_VALUE:
                ThSensorRegisters.POLL_OFS_VALUE + 32]))
        return {"status1": status1, "status2": status2, "values": values}

    def measure_once(self, settle_ms: int = 600) -> Optional[Dict[str, Any]]:
        """trigger → settle_ms 待機 → POLL 読出しを 1 セットで実行。

        Returns: {"status1": int, "status2": int, "temp_c": float, "rh_pct": float,
                  "co2_ppm": float, "glb_c": float, "t_valid": bool, ...} or None
        """
        if not self.trigger_measurement():
            return None
        time.sleep(settle_ms / 1000.0)
        poll = self.read_poll_block()
        if poll is None:
            return None
        s1 = poll["status1"]
        v  = poll["values"]
        return {
            "status1":   s1,
            "status2":   poll["status2"],
            "temp_c":    v[ThSensorRegisters.VAL_IDX_TEMPERATURE],
            "rh_pct":    v[ThSensorRegisters.VAL_IDX_HUMIDITY],
            "co2_ppm":   v[ThSensorRegisters.VAL_IDX_CO2],
            "glb_c":     v[ThSensorRegisters.VAL_IDX_GLOBE_TEMP],
            "t_valid":   not (s1 & ThSensorRegisters.STATUS1_STALE_T),
            "rh_valid":  not (s1 & ThSensorRegisters.STATUS1_STALE_RH),
            "co2_valid": not (s1 & ThSensorRegisters.STATUS1_STALE_CO2),
            "glb_valid": not (s1 & ThSensorRegisters.STATUS1_STALE_T_GLB),
        }

    # ==================================================================
    # EXTENSION 補正係数
    # ==================================================================

    def _read_float(self, reg: int) -> Optional[float]:
        raw = self.read_i2c_block(reg, 4)
        if raw is None:
            return None
        return struct.unpack('<f', raw)[0]

    def _write_float(self, reg: int, value: float) -> bool:
        return self.write_i2c_block(reg, list(struct.pack('<f', value)))

    def get_coefficients(self) -> Optional[Dict[str, float]]:
        """全 8 係数を一括取得 (4 計測値 × a/b)。"""
        raw = self.read_i2c_block(ThSensorRegisters.REG_T_COEF_A, 32)
        if raw is None:
            return None
        f = struct.unpack('<8f', raw)
        return {
            "t_a":     f[0], "t_b":     f[1],
            "rh_a":    f[2], "rh_b":    f[3],
            "co2_a":   f[4], "co2_b":   f[5],
            "t_glb_a": f[6], "t_glb_b": f[7],
        }

    def set_coefficient(self, kind: str, ab: str, value: float) -> bool:
        """kind = 't'/'rh'/'co2'/'t_glb', ab = 'a'/'b'。"""
        reg_map = {
            ("t",     "a"): ThSensorRegisters.REG_T_COEF_A,
            ("t",     "b"): ThSensorRegisters.REG_T_COEF_B,
            ("rh",    "a"): ThSensorRegisters.REG_RH_COEF_A,
            ("rh",    "b"): ThSensorRegisters.REG_RH_COEF_B,
            ("co2",   "a"): ThSensorRegisters.REG_CO2_COEF_A,
            ("co2",   "b"): ThSensorRegisters.REG_CO2_COEF_B,
            ("t_glb", "a"): ThSensorRegisters.REG_T_GLB_COEF_A,
            ("t_glb", "b"): ThSensorRegisters.REG_T_GLB_COEF_B,
        }
        reg = reg_map.get((kind, ab))
        if reg is None:
            raise ValueError(f"Invalid coefficient kind/ab: {kind}/{ab}")
        return self._write_float(reg, value)

    # ==================================================================
    # STCC4 制御コマンド
    # ==================================================================

    def send_stcc4_command(self, cmd: int, arg: int = 0) -> bool:
        """STCC4 コマンドを発行。arg は uint16 (FRC では target ppm)。

        arg → cmd の順に書く (子機は cmd 受理時点で arg を読むため)。
        """
        if not self.write_i2c_block(ThSensorRegisters.REG_STCC4_CMD_ARG,
                                    [arg & 0xFF, (arg >> 8) & 0xFF]):
            return False
        return self.write_i2c_block(ThSensorRegisters.REG_STCC4_CMD, [cmd])

    def start_frc(self, target_ppm: int) -> bool:
        return self.send_stcc4_command(ThSensorRegisters.STCC4_CMD_FRC, target_ppm)

    def start_factory_reset(self) -> bool:
        return self.send_stcc4_command(ThSensorRegisters.STCC4_CMD_FACTORY_RESET)

    def start_conditioning(self) -> bool:
        return self.send_stcc4_command(ThSensorRegisters.STCC4_CMD_CONDITIONING)

    def get_stcc4_state(self) -> Optional[int]:
        raw = self.read_i2c_block(ThSensorRegisters.REG_STCC4_STATE, 1)
        return raw[0] if raw is not None else None

    def get_frc_correction(self) -> Optional[int]:
        """最後の FRC 補正値 [ppm signed]。"""
        raw = self.read_i2c_block(ThSensorRegisters.REG_FRC_CORRECTION, 2)
        if raw is None:
            return None
        return struct.unpack('<h', raw)[0]

    def wait_for_state(self,
                       target_states: List[int],
                       timeout_s: float = 60.0,
                       poll_interval_s: float = 0.5,
                       verbose: bool = True) -> Optional[int]:
        """target_states のいずれかに遷移するまで polling。タイムアウト時は None。"""
        t_end = time.time() + timeout_s
        last_state: Optional[int] = None
        while time.time() < t_end:
            state = self.get_stcc4_state()
            if state in target_states:
                if verbose:
                    print(f"    -> state = {STATE_NAMES.get(state, hex(state))}")
                return state
            if verbose and state != last_state:
                print(f"    polling... state = {STATE_NAMES.get(state, hex(state))}")
                last_state = state
            time.sleep(poll_interval_s)
        return None

    # ==================================================================
    # I2C アドレス変更
    # ==================================================================

    def change_slave_address(self, new_addr_7bit: int) -> bool:
        if not (0x08 <= new_addr_7bit <= 0x77):
            raise ValueError("new_addr must be a valid 7bit I2C address (0x08-0x77)")
        if not self.write_i2c_block(ThSensorRegisters.REG_ADDR_KEY,
                                    [ThSensorRegisters.ADDR_KEY_UNLOCK]):
            return False
        return self.write_i2c_block(ThSensorRegisters.REG_NEW_ADDR,
                                    [new_addr_7bit])


# ============================================================
# 使用例: 接続〜情報取得〜サンプリング数回
# ============================================================
def _print_info_block(sensor: ThSensorManager) -> None:
    print("=== INFO BLOCK ===")
    did = sensor.get_device_id()
    print(f"  Device ID  : 0x{did:08X}" if did is not None else "  Device ID  : (read failed)")
    print(f"  Name       : {sensor.get_name()!r}")
    print(f"  Data Count : {sensor.get_data_count()}  (期待値 4: T/RH/CO2/T_glb)")
    units = sensor.get_unit_types()
    if units:
        print(f"  Unit Types : T={units[0]} (62=°C), RH={units[1]} (29=%RH), "
              f"CO2={units[2]} (96=ppm), T_glb={units[3]} (62=°C)")


def _print_coefficients(sensor: ThSensorManager) -> None:
    print("=== Coefficients (corrected = a*raw + b) ===")
    c = sensor.get_coefficients()
    if c is None:
        print("  (read failed)")
        return
    print(f"  T     : a={c['t_a']:.4f}  b={c['t_b']:.4f}")
    print(f"  RH    : a={c['rh_a']:.4f}  b={c['rh_b']:.4f}")
    print(f"  CO2   : a={c['co2_a']:.4f}  b={c['co2_b']:.4f}")
    print(f"  T_glb : a={c['t_glb_a']:.4f}  b={c['t_glb_b']:.4f}")


def _sample_loop(sensor: ThSensorManager, count: int = 5, interval_s: float = 2.0) -> None:
    print(f"\n=== Sample {count} times (interval {interval_s:.1f}s) ===")
    print("  [n]  status1  T[°C]   RH[%]   CO2[ppm]  T_glb[°C]")
    for i in range(count):
        m = sensor.measure_once()
        if m is None:
            print(f"  [{i}]  -- read failed --")
        else:
            def fmt(v, valid, w, dp):
                if valid:
                    return f"{v:>{w}.{dp}f}"
                return " " * (w - 4) + "STALE"
            print(f"  [{i}]  0x{m['status1']:02X}     "
                  f"{fmt(m['temp_c'], m['t_valid'], 6, 2)}  "
                  f"{fmt(m['rh_pct'], m['rh_valid'], 6, 2)}  "
                  f"{fmt(m['co2_ppm'], m['co2_valid'], 8, 1)}  "
                  f"{fmt(m['glb_c'], m['glb_valid'], 8, 2)}")
        if i + 1 < count:
            time.sleep(interval_s)


def _diag_i2c(sensor: ThSensorManager, addr: int) -> None:
    """CP2112 の HID プリミティブで一連の I2C transaction を実行し、
    各回の TRANSFER_STATUS を生でデコードして表示する。

    目的:
      - 「実は NACK されていて返ってきたバイトは USB バッファの残骸」かどうかを判別
      - 任意 register pointer に対する READ が物理的に成立しているかを確認
    """
    print("\n=== Low-level CP2112 / I2C diagnostic ===")

    # CP2112 transfer status0 / status1 のデコード辞書
    STATUS0 = {0x00: "IDLE", 0x01: "BUSY", 0x02: "COMPLETE", 0x03: "ERROR"}
    # status1 (status0=BUSY 時)
    BUSY_DETAIL = {0x00: "Address ACKed", 0x01: "Address NACKed",
                   0x02: "Data in progress"}
    # status1 (status0=COMPLETE 時)
    COMPLETE_DETAIL = {0x00: "Timeout addr NACK", 0x01: "Timeout bus not free",
                       0x02: "Arbitration lost", 0x03: "Read incomplete",
                       0x04: "Write incomplete", 0x05: "Succeeded"}

    def _decode_status(res):
        """res = CP2112 TRANSFER_STATUS_RESPONSE (0x16 ...) bytes"""
        if not res or res[0] != 0x16:
            return "(no/invalid status response: " + (repr(res) if res else "None") + ")"
        s0 = res[1]
        s1 = res[2]
        bytes_read = (res[3] << 8) | res[4]
        bytes_written = (res[5] << 8) | res[6]
        s0_name = STATUS0.get(s0, f"0x{s0:02X}")
        if s0 == 0x02:    # COMPLETE
            s1_name = COMPLETE_DETAIL.get(s1, f"0x{s1:02X}")
        elif s0 == 0x01:  # BUSY
            s1_name = BUSY_DETAIL.get(s1, f"0x{s1:02X}")
        else:
            s1_name = f"0x{s1:02X}"
        return f"status0={s0_name}, status1={s1_name}, rd={bytes_read}, wr={bytes_written}"

    def _try_write_read(reg: int, length: int):
        # DATA_WRITE_READ_REQ (0x11): 子機に [reg] を書いて length byte 読む
        sensor._raw_write([
            0x11,                    # DATA_WRITE_READ_REQ
            addr << 1,
            (length >> 8) & 0xFF,
            length & 0xFF,
            0x01,                    # Target Address Length = 1
            reg
        ])
        time.sleep(0.005)            # 完了待ち
        # TRANSFER_STATUS を確認
        sensor._raw_write([0x15, 0x01])  # TRANSFER_STATUS_REQ
        status_res = sensor._raw_read(7, 200)
        status_str = _decode_status(status_res)
        # データ取り出し試行
        sensor._raw_write([0x12, 0x00, length])  # DATA_READ_FORCE_SEND
        data_res = sensor._raw_read(64, 200)
        if data_res and data_res[0] == 0x13:
            actual_len = data_res[2]
            payload = bytes(data_res[3:3+min(actual_len, length)])
        else:
            actual_len = 0
            payload = b''
        return status_str, actual_len, payload

    # 1) アドレスだけ ACK チェック (length=0 write)
    print(f"\n[1] Address-only WRITE to 0x{addr:02X} (probe NACK/ACK)")
    sensor._raw_write([0x14, addr << 1, 0])  # DATA_WRITE_REQ, length 0
    time.sleep(0.003)
    sensor._raw_write([0x15, 0x01])
    res = sensor._raw_read(7, 200)
    print(f"    {_decode_status(res)}")
    print("    → status0=COMPLETE & status1=Succeeded なら slave が ACK 返している")
    print("    → status1=Timeout addr NACK だと slave 不在")

    # 2) 1 byte 書き込み (offset=0x29 status2 に 0 を書く = pre-trigger)
    print(f"\n[2] WRITE 1 byte to 0x{addr:02X} offset 0x29 (status2 trigger)")
    sensor._raw_write([0x14, addr << 1, 2, 0x29, 0x00])  # DATA_WRITE_REQ + offset + value
    time.sleep(0.003)
    sensor._raw_write([0x15, 0x01])
    res = sensor._raw_read(7, 200)
    print(f"    {_decode_status(res)}")

    # 3-7) 各 offset を 1 byte ずつ read
    for label, reg in [("device_id[0]", 0x00), ("new_addr", 0x05),
                       ("data_count", 0x06), ("status1", 0x28),
                       ("stcc4_state", 0x6F)]:
        print(f"\n[3] READ 1 byte at offset 0x{reg:02X} ({label})")
        status_str, n, payload = _try_write_read(reg, 1)
        print(f"    {status_str}")
        if payload:
            print(f"    payload: 0x{payload[0]:02X}")
        else:
            print(f"    payload: (none, len={n})")


def main() -> int:
    import argparse
    p = argparse.ArgumentParser(
        description="mlogger_th_sensor 子機の I2C 動作確認 (CP2112 経由)")
    p.add_argument("--serial", default=None,
                   help="CP2112 のシリアル番号 (省略時は最初のデバイス)")
    p.add_argument("--addr", type=lambda x: int(x, 0),
                   default=ThSensorRegisters.DEFAULT_I2C_ADDR,
                   help=f"子機 I2C アドレス (default 0x{ThSensorRegisters.DEFAULT_I2C_ADDR:02X})")
    p.add_argument("--count", type=int, default=5, help="サンプル回数 (default 5)")
    p.add_argument("--interval", type=float, default=1.0,
                   help="サンプル間隔 [sec] (default 1.0)。子機の single-shot 計測は "
                        "~520ms、Python 側は trigger + 600ms wait + read で ~640ms 必要。"
                        "interval=1.0 で実質 ~1.6sec 周期、interval=0.5 で ~1.1sec 周期")
    p.add_argument("--scan-only", action="store_true",
                   help="INFO/POLL の読出しのみ (校正コマンドは発行しない)")
    p.add_argument("--dump", action="store_true",
                   help="0x00-0x7F の生バイトをまとめて読み出して 16進グリッドで表示 (診断用)")
    p.add_argument("--dump-mode", choices=["block", "bytewise"], default="block",
                   help="--dump の読み方: block=128byte 一括 read, bytewise=1byte ずつ read")
    p.add_argument("--diag", action="store_true",
                   help="CP2112 の TRANSFER_STATUS を厳密に確認する低レベル診断モード "
                        "(I2C 自体が動いているか / NACK/timeout の切り分け用)")
    p.add_argument("--conditioning", action="store_true",
                   help="perform_conditioning を発行 (~22sec)。動作確認用。")
    p.add_argument("--factory-reset", action="store_true",
                   help="factory_reset を発行 (~90ms)。FRC/ASC 履歴消去。")
    p.add_argument("--frc", type=int, metavar="PPM", default=None,
                   help="FRC (forced recalibration) を発行 (~35sec)。基準濃度 [ppm]。")
    args = p.parse_args()

    print(f"=== mlogger_th_sensor I2C connectivity test ===")
    print(f"  CP2112 serial : {args.serial or '(first device)'}")
    print(f"  I2C address   : 0x{args.addr:02X}")

    devices = CP2112Device.list_devices()
    if not devices:
        print("ERROR: No CP2112 devices found.")
        return 1
    print(f"  Found {len(devices)} CP2112 device(s):")
    for d in devices:
        print(f"    - {d['product']} (S/N: {d['serial']})")

    sensor = ThSensorManager(slave_addr=args.addr, serial_number=args.serial)
    if not sensor.open():
        print("ERROR: Could not open CP2112.")
        return 1
    print("CP2112 opened.\n")

    try:
        # まず疎通確認 (1 byte read)
        probe = sensor.read_i2c_block(ThSensorRegisters.REG_DATA_COUNT, 1)
        if probe is None:
            print(f"ERROR: I2C device at 0x{args.addr:02X} did not respond (NACK).")
            print("  - 子機の電源は入っていますか？")
            print("  - SDA/SCL の配線とプルアップ抵抗は接続されていますか？")
            print("  - CP2112 と子機の GND は共通ですか？")
            return 2
        print(f"I2C device at 0x{args.addr:02X} responded (data_count = {probe[0]}).\n")

        if args.diag:
            _diag_i2c(sensor, args.addr)

        if args.dump:
            print("\n=== Raw memory dump 0x00-0x7F ===")
            raw = None
            if args.dump_mode == "block":
                # 128 byte 一括 read。CP2112 は 1 HID report で最大 61 byte なので
                # 内部的に複数の DATA_READ_RESPONSE に分割される。現ドライバは
                # 1 回しか read しないため短い結果しか返らない可能性がある。
                raw = sensor.read_i2c_block(0x00, 128)
                if raw is None or len(raw) < 128:
                    got = 0 if raw is None else len(raw)
                    print(f"  block read returned {got}/128 bytes; switching to bytewise...")
                    args.dump_mode = "bytewise"
            if args.dump_mode == "bytewise":
                # 1 byte ずつ read。register pointer protocol が機能していれば
                # 毎回 fresh な値が返る。stale buffer が出ているならパターンが見える。
                raw_bytes = bytearray(b'\xFF' * 128)
                ok_count = 0
                for off in range(128):
                    b = sensor.read_i2c_block(off, 1)
                    if b is not None and len(b) >= 1:
                        raw_bytes[off] = b[0]
                        ok_count += 1
                raw = bytes(raw_bytes)
                print(f"  bytewise: {ok_count}/128 bytes read successfully")
            # 短い場合は 0xFF パディングして IndexError 回避
            if len(raw) < 128:
                raw = raw + b'\xFF' * (128 - len(raw))
            print("       " + " ".join(f"{c:02X}" for c in range(16)))
            for row in range(0, 128, 16):
                hex_part = " ".join(f"{raw[row+c]:02X}" for c in range(16))
                ascii_part = "".join(chr(raw[row+c]) if 32 <= raw[row+c] < 127 else "."
                                     for c in range(16))
                print(f"  {row:02X}:  {hex_part}  {ascii_part}")
            print()

        _print_info_block(sensor)
        print()
        _print_coefficients(sensor)

        if args.factory_reset:
            print("\n=== factory_reset (STCC4 FRC/ASC 履歴消去) ===")
            sensor.start_factory_reset()
            state = sensor.wait_for_state(
                [ThSensorRegisters.STCC4_STATE_FACTORY_RESET_DONE,
                 ThSensorRegisters.STCC4_STATE_IDLE],
                timeout_s=5.0, poll_interval_s=0.1)
            print(f"  final state = {STATE_NAMES.get(state, str(state))}")

        if args.conditioning:
            print("\n=== perform_conditioning (~22 sec) ===")
            sensor.start_conditioning()
            state = sensor.wait_for_state(
                [ThSensorRegisters.STCC4_STATE_CONDITIONING_DONE,
                 ThSensorRegisters.STCC4_STATE_IDLE],
                timeout_s=30.0, poll_interval_s=1.0)
            print(f"  final state = {STATE_NAMES.get(state, str(state))}")

        if args.frc is not None:
            print(f"\n=== FRC (forced recalibration to {args.frc} ppm, ~35 sec) ===")
            sensor.start_frc(args.frc)
            state = sensor.wait_for_state(
                [ThSensorRegisters.STCC4_STATE_FRC_DONE,
                 ThSensorRegisters.STCC4_STATE_FRC_FAIL],
                timeout_s=60.0, poll_interval_s=1.0)
            print(f"  final state = {STATE_NAMES.get(state, str(state))}")
            corr = sensor.get_frc_correction()
            if corr is not None:
                print(f"  correction  = {corr:+d} ppm")

        if not args.scan_only:
            _sample_loop(sensor, count=args.count, interval_s=args.interval)

    finally:
        sensor.close()
        print("\nClosed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
