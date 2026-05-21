"""
OSL (Overlay Sensing Layer) 規格準拠の風速センサ子機 (poem_velocity_sensor) を
CP2112 USB-I2C ブリッジ経由で操作する Python クライアント。

仕様: SHASE 2026 第2報 (Togashi 2026) + firmware の i2c_shared_data.h
   - INFO BLOCK (0x00-0x27): device_id / addr_key / new_addr / data_count /
     unit_type[8] / name[16]  ※接続時に 1 回読む
   - POLL BLOCK (0x28-0x4B): status1 / status2 / reserved / value[8] float
     ※毎周期 1 回読む (36 byte 一括取得)
   - EXTENSION (0x4C-): enable / filter_n / coefA[5] / coefB[5] (本子機固有)

リトルエンディアン、無 alignment、CRC 無し。Status1 の bit i が立っていれば
value[i] は stale / 異常。
"""
import struct
import sys
import time
from typing import Any, Dict, List, Optional

from cp2112_driver import CP2112Device


# ============================================================
# OSL register map 定数
# ============================================================
class AnemometerRegisters:
    DEFAULT_I2C_ADDR  = 0x10
    ADDR_KEY_UNLOCK   = 0xA5

    # ---- INFO BLOCK (0x00-0x27, 走査時に 1 回読む) -----------------------
    REG_DEVICE_ID     = 0x00   # uint32 R  : FNV-1a 22bit (0..0x3FFFFE)
    REG_ADDR_KEY      = 0x04   # uint8  W  : アドレス変更鍵 (STOP で自動クリア)
    REG_NEW_ADDR      = 0x05   # uint8  R/W: 新しい I2C アドレス
    REG_DATA_COUNT    = 0x06   # uint8  R  : 有効計測値の数 (本子機は 2)
    REG_UNIT_TYPE     = 0x08   # uint16[8] R: BACnet engineering unit codes
    REG_NAME          = 0x18   # char[16]  R/W: 装置ラベル (NUL 終端)
    NAME_LEN          = 16

    # ---- POLL BLOCK (0x28-0x4B, 毎周期読む) ------------------------------
    REG_POLL_BASE     = 0x28
    POLL_BLOCK_SIZE   = 36     # status1(1) + status2(1) + reserved(2) + value[8](32)
    POLL_OFS_STATUS1  = 0
    POLL_OFS_STATUS2  = 1
    POLL_OFS_VALUE    = 4      # value[0] の先頭バイト
    NUM_VALUES        = 8

    REG_STATUS1       = 0x28   # uint8 R
    REG_STATUS2       = 0x29   # uint8 R/W
    REG_VALUE         = 0x2C   # float[8] R (LE)

    # ---- EXTENSION (0x4C-、本子機固有) ----------------------------------
    REG_EXT_ENABLE    = 0x4C   # uint8 R/W : 風速計回路の起動フラグ
    REG_EXT_FILTER_N  = 0x4D   # uint8 R/W : 平滑化フィルタ係数 (0~20)
    REG_EXT_COEF_A    = 0x4E   # float[5] R/W
    REG_EXT_COEF_B    = 0x62   # float[5] R/W
    EXT_COEF_LEN      = 5

    # ---- value 配列インデックス (本子機の物理量割当) --------------------
    VAL_IDX_VELOCITY  = 0      # m/s
    VAL_IDX_VOLTAGE   = 1      # V


# ============================================================
# 主クライアントクラス
# ============================================================
class AnemometerManager(CP2112Device):
    """OSL 共通レジスタ + 風速計拡張領域の Python クライアント。"""

    def __init__(self,
                 slave_addr: int = AnemometerRegisters.DEFAULT_I2C_ADDR,
                 serial_number: Optional[str] = None):
        super().__init__(slave_addr=slave_addr, serial_number=serial_number)
        self._last_comm_time = 0.0
        self._keep_alive_threshold = 0.950   # AVR 子機の sleep idle 直前を狙う

    # ------------------------------------------------------------------
    # sleep 対策: 通信ごとに wakeup pulse を打つ
    # ------------------------------------------------------------------

    def _ensure_wakeup(self) -> None:
        """最後の通信から閾値以上空いていたら wakeup を打つ。"""
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
        """FNV-1a 22bit Device ID (BACnet Object Instance 互換, 0..0x3FFFFE)。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_DEVICE_ID, 4)
        if raw is None:
            return None
        return struct.unpack('<I', raw)[0]

    def get_data_count(self) -> Optional[int]:
        """有効計測値の数 (本子機の出荷状態では 2 = velocity + voltage)。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_DATA_COUNT, 1)
        if raw is None:
            return None
        return raw[0]

    def get_unit_types(self) -> Optional[List[int]]:
        """各 value のシリアル順 BACnet engineering unit code (uint16[8])。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_UNIT_TYPE, 16)
        if raw is None:
            return None
        return list(struct.unpack('<8H', raw))

    def get_name(self) -> Optional[str]:
        """装置ラベル (最大 15 文字 ASCII)。NUL までを返す。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_NAME,
                                  AnemometerRegisters.NAME_LEN)
        if raw is None:
            return None
        nul = raw.find(0)
        if nul >= 0:
            raw = raw[:nul]
        return raw.decode('ascii', errors='replace')

    def set_name(self, name: str) -> bool:
        """装置ラベルを設定 (15 文字 ASCII まで、超過分は切り捨て)。EEPROM 永続。"""
        encoded = name.encode('ascii', errors='replace')[:AnemometerRegisters.NAME_LEN - 1]
        padded  = encoded.ljust(AnemometerRegisters.NAME_LEN, b'\x00')
        return self.write_i2c_block(AnemometerRegisters.REG_NAME, list(padded))

    # ==================================================================
    # POLL BLOCK (動的情報、毎周期読む)
    # ==================================================================

    def read_poll_block(self) -> Optional[Dict[str, Any]]:
        """
        POLL BLOCK (0x28-0x4B, 36 byte) を 1 トランザクションで読む。
        Returns: {"status1": int, "status2": int, "values": List[float]} or None
        """
        raw = self.read_i2c_block(AnemometerRegisters.REG_POLL_BASE,
                                  AnemometerRegisters.POLL_BLOCK_SIZE)
        if raw is None:
            return None
        status1 = raw[AnemometerRegisters.POLL_OFS_STATUS1]
        status2 = raw[AnemometerRegisters.POLL_OFS_STATUS2]
        values  = list(struct.unpack('<8f',
                                     raw[AnemometerRegisters.POLL_OFS_VALUE:
                                         AnemometerRegisters.POLL_OFS_VALUE + 32]))
        return {"status1": status1, "status2": status2, "values": values}

    def get_status1(self) -> Optional[int]:
        """異常フラグ (bit i = value[i] が stale / 異常)。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_STATUS1, 1)
        return raw[0] if raw is not None else None

    def get_status2(self) -> Optional[int]:
        """計測値更新カウンタ (親機が clear して更新を待つ用途)。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_STATUS2, 1)
        return raw[0] if raw is not None else None

    def clear_status2(self) -> bool:
        return self.write_i2c_block(AnemometerRegisters.REG_STATUS2, [0])

    def get_values(self) -> Optional[List[float]]:
        """value[0..7] を float のリストで取得 (32 byte 一括 read)。"""
        raw = self.read_i2c_block(AnemometerRegisters.REG_VALUE, 32)
        if raw is None:
            return None
        return list(struct.unpack('<8f', raw))

    # ==================================================================
    # value 配列の個別 alias (本子機固有割当)
    # ==================================================================

    def get_velocity(self) -> Optional[float]:
        """value[0] = 風速 [m/s]。"""
        values = self.get_values()
        return values[AnemometerRegisters.VAL_IDX_VELOCITY] if values else None

    def get_voltage(self) -> Optional[float]:
        """value[1] = 風速計回路の生電圧 [V]。"""
        values = self.get_values()
        return values[AnemometerRegisters.VAL_IDX_VOLTAGE] if values else None

    def is_value_valid(self, idx: int) -> bool:
        """status1 の bit idx が立っていなければ True (= 値は信頼できる)。"""
        s = self.get_status1()
        return s is not None and not (s & (1 << idx))

    # ==================================================================
    # EXTENSION (本子機固有 R/W)
    # ==================================================================

    def get_enable(self) -> Optional[bool]:
        raw = self.read_i2c_block(AnemometerRegisters.REG_EXT_ENABLE, 1)
        return (raw[0] != 0) if raw is not None else None

    def set_enable(self, enable: bool) -> bool:
        return self.write_i2c_block(AnemometerRegisters.REG_EXT_ENABLE,
                                    [1 if enable else 0])

    def get_filter_n(self) -> Optional[int]:
        raw = self.read_i2c_block(AnemometerRegisters.REG_EXT_FILTER_N, 1)
        return raw[0] if raw is not None else None

    def set_filter_n(self, n: int) -> bool:
        return self.write_i2c_block(AnemometerRegisters.REG_EXT_FILTER_N,
                                    [max(0, min(20, n))])

    def get_coefficients_a(self) -> Optional[List[float]]:
        raw = self.read_i2c_block(AnemometerRegisters.REG_EXT_COEF_A,
                                  4 * AnemometerRegisters.EXT_COEF_LEN)
        if raw is None:
            return None
        return list(struct.unpack('<5f', raw))

    def set_coefficients_a(self, coefs: List[float]) -> bool:
        if len(coefs) != AnemometerRegisters.EXT_COEF_LEN:
            raise ValueError(f"coefs must have length {AnemometerRegisters.EXT_COEF_LEN}")
        return self.write_i2c_block(AnemometerRegisters.REG_EXT_COEF_A,
                                    list(struct.pack('<5f', *coefs)))

    def get_coefficients_b(self) -> Optional[List[float]]:
        raw = self.read_i2c_block(AnemometerRegisters.REG_EXT_COEF_B,
                                  4 * AnemometerRegisters.EXT_COEF_LEN)
        if raw is None:
            return None
        return list(struct.unpack('<5f', raw))

    def set_coefficients_b(self, coefs: List[float]) -> bool:
        if len(coefs) != AnemometerRegisters.EXT_COEF_LEN:
            raise ValueError(f"coefs must have length {AnemometerRegisters.EXT_COEF_LEN}")
        return self.write_i2c_block(AnemometerRegisters.REG_EXT_COEF_B,
                                    list(struct.pack('<5f', *coefs)))

    # ==================================================================
    # I2C アドレス変更 (OSL Addr Key + New Addr スキーム)
    # ==================================================================

    def change_slave_address(self, new_addr_7bit: int) -> bool:
        """
        Addr Key (0xA5) を書き込んだ直後に New Addr を書く (連続 STOP 内、と仕様で
        定義されているが本実装では別 STOP でも poem_velocity_sensor は受け付ける)。
        EEPROM に保存され、新アドレスは即時 SADDR にも反映される。
        """
        if not (0x08 <= new_addr_7bit <= 0x77):
            raise ValueError("new_addr must be a valid 7bit I2C address (0x08-0x77)")
        if not self.write_i2c_block(AnemometerRegisters.REG_ADDR_KEY,
                                    [AnemometerRegisters.ADDR_KEY_UNLOCK]):
            return False
        return self.write_i2c_block(AnemometerRegisters.REG_NEW_ADDR,
                                    [new_addr_7bit])


# ============================================================
# 使用例: 接続〜情報取得〜サンプリング数回
# ============================================================
if __name__ == "__main__":
    serial_num = sys.argv[1] if len(sys.argv) > 1 else None
    sensor = AnemometerManager(serial_number=serial_num)

    sensor.open()
    if not sensor.is_open:
        print("Could not open device. Check CP2112 connection / serial number.")
        sys.exit(1)

    try:
        print("=== INFO BLOCK ===")
        print(f"Device ID  : {sensor.get_device_id()}")
        print(f"Name       : {sensor.get_name()!r}")
        print(f"Data Count : {sensor.get_data_count()}")
        print(f"Unit Types : {sensor.get_unit_types()}")

        print("\n=== Enable + sample 5 times (1 Hz) ===")
        sensor.set_enable(True)
        sensor.set_filter_n(6)
        time.sleep(1.0)        # 平滑化フィルタ立ち上げ

        for i in range(5):
            poll = sensor.read_poll_block()
            if poll is None:
                print(f"  [{i}] read failed")
            else:
                s1 = poll["status1"]
                v  = poll["values"]
                vel  = v[AnemometerRegisters.VAL_IDX_VELOCITY]
                volt = v[AnemometerRegisters.VAL_IDX_VOLTAGE]
                print(f"  [{i}] status1=0x{s1:02X}  "
                      f"vel={vel:7.3f} m/s  volt={volt:6.3f} V")
            time.sleep(1.0)

        print("\n=== Coefficients ===")
        print(f"  A: {sensor.get_coefficients_a()}")
        print(f"  B: {sensor.get_coefficients_b()}")

    finally:
        sensor.close()
        print("\nClosed.")
