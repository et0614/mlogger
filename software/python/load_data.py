import serial
import serial.tools.list_ports
import time
import struct
import sys
import os
import csv
from datetime import datetime, timezone

# ==========================================
# 設定エリア
# ==========================================
TARGET_ID_CMD = b'WHO\n'    # 送信する識別コマンド
TARGET_ID_RESP = "M_LOGGER" # 期待するレスポンス
BAUD_RATE = 115200
TIMEOUT_SEC = 5.0
OUTPUT_DIR = "csv_output" # 保存先のフォルダ名
MISSING_VAL = "" # 計測値がない場合の表示

# 1レコードのバイト数 (SensorData_t)
RECORD_SIZE = 22

# バイナリフォーマット (Little Endian)
# Header: uint32
HEADER_FMT = '<I'
# Data: Generation(B), Timestamp(I), Valid Flags(B), Lux(I), T_Dry(h), T_Glb(h), Hum(H), Wind(H), Volt(H), CO2(H)
DATA_FMT = '<BIBIhhHHHH'

# ビットフラグ定義
FLAG_ILLUMINANCE = (1 << 0)
FLAG_TEMP_DRY    = (1 << 1)
FLAG_TEMP_GLOBE  = (1 << 2)
FLAG_HUMIDITY    = (1 << 3)
FLAG_WIND_SPEED  = (1 << 4)
FLAG_VOLTAGE     = (1 << 5)
FLAG_CO2_PPM     = (1 << 6)

# CSVのヘッダー行
CSV_HEADER = [
    "Timestamp", "Illuminance", "Temp_Dry", 
    "Temp_Globe", "Humidity", "WindSpeed", "Voltage", "CO2_ppm"
]
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


def print_progress_bar(iteration, total, prefix='', suffix='', decimals=1, length=50, fill='#'):
    """
    コンソールに進捗バーを表示する関数
    """
    percent = ("{0:." + str(decimals) + "f}").format(100 * (iteration / float(total)))
    filled_length = int(length * iteration // total)
    bar = fill * filled_length + '-' * (length - filled_length)
    sys.stdout.write(f'\r{prefix} |{bar}| {percent}% {suffix}')
    sys.stdout.flush()
    if iteration == total:
        print()

def run_dump_tool():
    ser = None
    csv_file = None
    csv_writer = None
    current_date_str = None
    
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

    # 保存用ディレクトリの作成
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)
        print(f"Created output directory: {OUTPUT_DIR}")

    try:
        # シリアルポートを開く
        print(f"Connecting to {COM_PORT}...")
        ser = serial.Serial(COM_PORT, BAUD_RATE, timeout=TIMEOUT_SEC)
        time.sleep(2.0) # 接続安定待ち
        ser.reset_input_buffer()

        # コマンド送信
        ser.write(b'DMP\n')
        print("Sent command: DMP")

        # ヘッダ受信
        header_data = ser.read(4)

        total_records = struct.unpack(HEADER_FMT, header_data)[0]
        print(f"Total records to download: {total_records}")

        if total_records == 0:
            print("No data found.")
            return

        print("-" * 60)
        print_progress_bar(0, total_records, prefix='Progress:', suffix='Complete', length=40)

        # === 時間計測開始 ===
        start_time = time.time()

        # データ受信ループ
        for i in range(total_records):
            raw_data = ser.read(RECORD_SIZE)

            if len(raw_data) != RECORD_SIZE:
                print(f"\nError: Communication interrupted at index {i}.")
                break

            # バイナリ変換
            vals = struct.unpack(DATA_FMT, raw_data)
            gen, ts, flags, lux, t_dry, t_glb, hum, wind, volt, co2 = vals
            
            # タイムスタンプの変換
            dt = datetime.fromtimestamp(ts, timezone.utc)
            date_str = dt.strftime('%Y%m%d')      # ファイル名用
            ts_str = dt.strftime('%H:%M:%S') # CSVデータ用
            
            # --- 日付変更チェックとファイル切り替え処理 ---
            if date_str != current_date_str:
                # 既にファイルが開いていれば閉じる
                if csv_file:
                    csv_file.close()
                
                # 新しいファイルパス
                filename = os.path.join(OUTPUT_DIR, f"{date_str}.csv")
                
                # ファイルを開く (上書きモード 'w' なので必ずヘッダーを書く)
                csv_file = open(filename, 'w', newline='', encoding='utf-8')
                csv_writer = csv.writer(csv_file)
                csv_writer.writerow(CSV_HEADER)
                
                current_date_str = date_str

            # --- CSVへの書き込み ---
            row = [
                ts_str,
                lux / 10.0     if (flags & FLAG_ILLUMINANCE) else MISSING_VAL,
                t_dry / 100.0  if (flags & FLAG_TEMP_DRY)    else MISSING_VAL,
                t_glb / 100.0  if (flags & FLAG_TEMP_GLOBE)  else MISSING_VAL,
                hum / 100.0    if (flags & FLAG_HUMIDITY)    else MISSING_VAL,
                wind / 10000.0 if (flags & FLAG_WIND_SPEED)  else MISSING_VAL,
                volt / 1000.0  if (flags & FLAG_VOLTAGE)     else MISSING_VAL,
                co2            if (flags & FLAG_CO2_PPM)     else MISSING_VAL
            ]
            csv_writer.writerow(row)

            # --- 進捗表示の更新 (100レコードごと、または最後) ---
            if (i + 1) % 100 == 0 or (i + 1) == total_records:
                print_progress_bar(i + 1, total_records, prefix='Progress:', suffix='Complete', length=40)

        # === 時間計測終了 ===
        end_time = time.time()
        elapsed_sec = end_time - start_time

        print("-" * 60)
        print("Download finished successfully.")
        
        # 結果表示
        if elapsed_sec > 0:
            # 分・秒換算
            m, s = divmod(elapsed_sec, 60)
            # 速度計算
            speed_recs = total_records / elapsed_sec
            speed_kbps = (total_records * RECORD_SIZE) / 1024 / elapsed_sec
            
            print(f"Elapsed Time : {int(m)}m {s:.2f}s")
            print(f"Average Speed: {speed_recs:.1f} records/sec ({speed_kbps:.2f} kB/s)")
        else:
            print("Elapsed Time : < 0.01s")

    except serial.SerialException as e:
        print(f"\nSerial Error: {e}")
    except KeyboardInterrupt:
        print("\nAborted by user.")
    except Exception as e:
        print(f"\nUnexpected Error: {e}")
    finally:
        # 後始末
        if csv_file:
            csv_file.close()
        if ser and ser.is_open:
            ser.close()

if __name__ == "__main__":
    if len(sys.argv) > 1:
        COM_PORT = sys.argv[1]
    run_dump_tool()