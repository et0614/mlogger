import serial
import serial.tools.list_ports
import time
import sys

# ==========================================
# 設定エリア
# ==========================================
TARGET_ID_CMD = b'WHO\n'    # 送信する識別コマンド
TARGET_ID_RESP = "M_LOGGER" # 期待するレスポンス
BAUD_RATE = 115200          # ボーレート
TIMEOUT_SEC = 2.0           # 応答待ちタイムアウト
# ==========================================

def find_device_port():
    """
    利用可能なCOMポートを走査し、所定のコマンドに応答するデバイスを探す
    """
    print("Scanning ports...")
    ports = list(serial.tools.list_ports.comports())
    
    for p in ports:
        try:
            print(f"Checking {p.device}...", end="", flush=True)

            # 対策: Bluetoothポートと予想されるポートはスキップ
            if "Bluetooth" in p.description:
                print(" Skipped (Likely Bluetooth).")
                continue
            
            # ポートを開いてみる（タイムアウトは短めに設定）
            with serial.Serial(p.device, BAUD_RATE, timeout=1.5) as ser:
                # 接続安定待ち（Arduino系などDTRリセットがかかる場合のみ必要だが、今回は短めに）
                time.sleep(1.5) 
                ser.reset_input_buffer()
                
                # 識別コマンド送信
                ser.write(TARGET_ID_CMD)
                
                # 応答確認
                line = ser.readline().decode('utf-8', errors='ignore').strip()

                if TARGET_ID_RESP in line:
                    print(" Found!")
                    return p.device
                else:
                    print(" No response or wrong ID.")
                    
        except (OSError, serial.SerialException):
            print(" Failed to open.")
            continue
            
    return None

def run_version_check():
    # 有効なポートを探索
    if len(sys.argv) > 1:
        COM_PORT = sys.argv[1]
    else:
        found_port = find_device_port()
        if found_port:
            COM_PORT = found_port
            print(f"Device detected at: {COM_PORT}")
        else:
            print("Error: Target device not found.")
            return

    ser = None
    try:
        # シリアルポートを開く
        print(f"Connecting to {COM_PORT}...")
        ser = serial.Serial(COM_PORT, BAUD_RATE, timeout=TIMEOUT_SEC)
        
        time.sleep(2.0) # 接続安定待ち
        ser.reset_input_buffer()

        # コマンド送信
        cmd_str = 'CLR'
        # コマンド送信 (改行コード付与)
        ser.write(f'{cmd_str}\n'.encode())
        print(f"Sent command: {cmd_str}")

        start_time = time.time()
        
        while True:
            # タイムアウト自衛 (無限ループ防止)
            if time.time() - start_time > TIMEOUT_SEC:
                print("Timeout: Target response not found.")
                break

            # 1行読み取り
            line_bytes = ser.readline()
            if not line_bytes:
                continue

            # デコードして空白除去
            line_str = line_bytes.decode('utf-8', errors='ignore').strip()

            # 空行は無視
            if not line_str:
                continue

            print("-" * 30)
            print(f"Echo: {line_str}")
            print("-" * 30)
            break

    except serial.SerialException as e:
        print(f"Serial Error: {e}")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        if ser and ser.is_open:
            ser.close()

if __name__ == "__main__":
    if len(sys.argv) > 1:
        COM_PORT = sys.argv[1]
    run_version_check()