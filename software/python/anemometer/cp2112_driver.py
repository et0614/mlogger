import hid
import time
from enum import IntEnum

class CP2112Report(IntEnum):
    """CP2112 HID Report IDs"""
    # Configuration
    RESET_DEVICE             = 0x01
    GPIO_CONFIG              = 0x02
    
    # Data Transfer
    DATA_READ_REQ            = 0x10
    DATA_WRITE_READ_REQ      = 0x11
    DATA_READ_FORCE_SEND     = 0x12
    DATA_READ_RESPONSE       = 0x13
    DATA_WRITE_REQ           = 0x14
    
    # Status/Control
    TRANSFER_STATUS_REQ      = 0x15
    TRANSFER_STATUS_RESPONSE = 0x16
    CANCEL_TRANSFER          = 0x17


class I2CStatus(IntEnum):
    """Report ID 0x16 (TRANSFER_STATUS_RESPONSE) の res[1] ステータスコード"""
    IDLE           = 0x00
    BUSY           = 0x01
    COMPLETE       = 0x02
    ERROR          = 0x03
    

class CP2112Device:
    """CP2112チップを使用したI2C通信の基底クラス"""
    
    # region 定数宣言,初期化処理

    VID = 0x10C4    # Vendor ID
    PID = 0xEA90    # Product ID

    def __init__(self, slave_addr, serial_number):
        self.__dev = hid.device()
        self.slave_addr = slave_addr
        self.serial_number = serial_number
        self.is_open = False

    #endregion

    # region with-exitの実装

    def __enter__(self):
        self.open()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()

    # endregion

    # region CP2112の操作

    def open(self):
        try:
            if self.serial_number is None:
                self.__dev.open(self.VID, self.PID)
            else:
                self.__dev.open(self.VID, self.PID, self.serial_number)
            self.__dev.write([
                CP2112Report.CANCEL_TRANSFER, 
                0x01]) # Transfer Reset
            self.is_open = True
            return True
        except Exception as e:
            print(f"Device open failed: {e}")
            self.is_open = False
            return False # 失敗を返す
        

    def close(self):
        if self.is_open:
            self.__dev.close()
            self.is_open = False


    def _raw_write(self, data: list):
        """CP2112に書き込む"""
        return self.__dev.write(data)


    def _raw_read(self, length: int, timeout: int):
        """CP2112を読み込む"""
        return self.__dev.read(length, timeout)


    # endregion

    # region I2C通信処理

    def read_i2c_block(self, offset, length, retries=2):
        """低レイヤーI2C読み込み (CP2112コマンド処理)"""
        for attempt in range(retries + 1):
            try:
                self.__dev.write([
                    CP2112Report.DATA_WRITE_READ_REQ,
                    self.slave_addr << 1,
                    (length >> 8) & 0xFF,
                    length & 0xFF,
                    0x01, # Target Address Length (通常1)
                    offset])

                # ポーリング
                for _ in range(15):
                    self.__dev.write([
                        CP2112Report.TRANSFER_STATUS_REQ, 
                        0x01])
                    res = self.__dev.read(64, 100)
                    if (res and 
                        res[0] == CP2112Report.TRANSFER_STATUS_RESPONSE and 
                        res[1] == I2CStatus.COMPLETE):
                        break
                    time.sleep(0.001)

                # データ取得
                self.__dev.write([
                    CP2112Report.DATA_READ_FORCE_SEND, 
                    0x00, 
                    length])
                data_res = self.__dev.read(64, 500)
                if data_res and data_res[0] == CP2112Report.DATA_READ_RESPONSE:
                    return bytes(data_res[3:3+length]) # bytes型で返す
                    
            except Exception as e:
                if attempt == retries: return None
                time.sleep(0.001)
        return None


    def write_i2c_block(self, offset, data: list, retries=2):
        """低レイヤーI2C書き込み"""
        write_data = [
            CP2112Report.DATA_WRITE_REQ, 
            self.slave_addr << 1, 
            len(data) + 1,   # DataLen (Offset + Data)
            offset,          # 書き込み開始位置
        ] + list(data)

        for attempt in range(retries + 1):
            try:
                self.__dev.write(write_data)
                # ステータスポーリング
                for _ in range(15):
                    self.__dev.write([
                        CP2112Report.TRANSFER_STATUS_REQ,
                        0x01])
                    res = self.__dev.read(64, 100)
                    if (res and 
                        res[0] == CP2112Report.TRANSFER_STATUS_RESPONSE and 
                        res[1] == I2CStatus.COMPLETE):
                        return True
                    time.sleep(0.001)
            except Exception as e:
                if attempt == retries:
                    print(f"I2C Write Fatal Error: {e}")
                    return False
            
            if attempt < retries:
                time.sleep(0.001) # リトライ前の待機
        return False


    def read_register_with_crc(self, offset, data_len, retries=2):
        """
        [データnバイト + CRC1バイト] 形式の読み込みを行う共通関数
        """
        for attempt in range(retries + 1):
            raw = self.read_i2c_block(offset, data_len + 1)
            
            if raw:
                data_body = raw[:-1]
                chk_crc = raw[-1]
                if self.calc_crc8(data_body) == chk_crc:
                    return data_body
            
            if attempt < retries:
                time.sleep(0.001)

        return None
    

    def write_register_with_crc(self, offset, data: bytes, retries=2):
        """
        [データnバイト + CRC1バイト] を計算して書き込む共通関数
        """
        crc = self.calc_crc8(data)
        # データ(bytes)をリスト化して末尾にCRCを追加
        payload = list(data) + [crc]

        for attempt in range(retries + 1):
            if self.write_i2c_block(offset, payload):
                return True
            
            if attempt < retries:
                time.sleep(0.001)

        return False

    # endregion

    # region その他の処理

    @staticmethod
    def calc_crc8(data: bytes) -> int:
        """CRC8 (Polynomial: 0x31)"""
        crc = 0xFF
        for byte in data:
            crc ^= byte
            for _ in range(8):
                if crc & 0x80:
                    crc = ((crc << 1) ^ 0x31) & 0xFF
                else:
                    crc = (crc << 1) & 0xFF
        return crc


    @classmethod
    def list_devices(cls):
        """
        接続されているすべてのCP2112デバイスの情報を取得し、リストで返す。
        """
        # デバイスの列挙
        raw_devices = hid.enumerate(CP2112Device.VID, CP2112Device.PID)
        
        devices = []
        for dev in raw_devices:
            # パスのデコード処理
            path = dev.get('path')
            info = {
                'product': dev.get('product_string'),
                'manufacturer': dev.get('manufacturer_string'),
                'serial': dev.get('serial_number'),
                'path': path.decode('utf-8') if isinstance(path, bytes) else path
            }
            devices.append(info)
        
        return devices
    
    # endregion


    # --- テスト用メインコード ---
if __name__ == "__main__":    
    print("=== CP2112 Driver Connectivity Test ===")
    devices = CP2112Device.list_devices()
    
    if not devices:
        print("No CP2112 devices found.")
    else:
        # 検出されたデバイスを表示
        print(f"{'No.':<3} | {'Product Name':<20} | {'Serial Number':<15} | {'Path'}")
        print("-" * 80)
        for i, dev in enumerate(devices, 1):
            print(f"{i:<3} | {dev['product']:<20} | {dev['serial']:<15} | {dev['path']}")

        # 最初のデバイスで接続テスト
        target = devices[0]
        print(f"Testing device: {target['product']} (S/N: {target['serial']})")
        
        with CP2112Device(slave_addr=0x10, serial_number=target['serial']) as device:
            if device.is_open:
                print("Connection established successfully.")
                # CRCテスト
                test_bytes = b'\x12\x34\x56'
                print(f"CRC8 test for {test_bytes.hex()}: 0x{device.calc_crc8(test_bytes):02X}")
                
                # I2C疎通確認 (レジスタ0x00を1バイト読み込み)
                res = device.read_i2c_block(0x00, 1)
                status_str = f"Read Result: 0x{res.hex()}" if res else "Read Failed (NACK/No Slave)"
                print(status_str)
            else:
                print("Failed to open device.")