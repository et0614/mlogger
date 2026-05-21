import serial
import serial.tools.list_ports

def list_serial_ports():
    """
    システム上の有効なシリアルポートをリストアップし、
    接続テストを行って結果を表示します。
    """
    print("=== COM Port Diagnostic Tool ===")
    
    # システムが認識しているポートを取得
    ports = list(serial.tools.list_ports.comports())
    
    if not ports:
        print("\n[!] 警告: 有効なシリアルポートが1つも見つかりませんでした。")
        print("    -> USBケーブルが正しく接続されているか確認してください。")
        print("    -> データ通信対応のケーブルか確認してください（充電専用は不可）。")
        print("    -> デバイスマネージャーでデバイスが認識されているか確認してください。")
        return

    print(f"\n検出されたポート数: {len(ports)}\n")

    for p in ports:
        print("-" * 40)
        print(f"Port:        {p.device}")
        print(f"Description: {p.description}")
        print(f"HwID:        {p.hwid}") # USBのVID/PIDなど
        
        # 接続テスト
        test_connection(p.device)

    print("-" * 40)
    print("\n診断終了。上記の 'Port' に表示されている番号(COMxx)を")
    print("get_version.py の COM_PORT に設定してください。")

def test_connection(port_name):
    """指定されたポートを開けるかテストする"""
    try:
        # テスト用にポートを開いてすぐに閉じる
        ser = serial.Serial(port_name, baudrate=9600, timeout=1)
        ser.close()
        print(f"Status:      [OK] アクセス可能です")
    except serial.SerialException as e:
        # アクセス拒否などは「他のアプリが使用中」の可能性が高い
        if "Access is denied" in str(e):
            print(f"Status:      [BUSY] 使用中です (他のアプリが開いています)")
        else:
            print(f"Status:      [ERROR] {e}")

if __name__ == "__main__":
    list_serial_ports()