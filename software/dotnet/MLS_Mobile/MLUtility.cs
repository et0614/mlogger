using MLLib;
using System.Text;

using Plugin.BLE.Abstractions.Contracts;
using DigiIoT.Maui.Devices.XBee;
using MLLib.Protocol;
using MLS_Mobile.Resources.i18n;
using MLS_Mobile.Services;
using MLS_Mobile.Transport;

namespace MLS_Mobile
{
  /// <summary>ユーティリティクラス</summary>
  public static class MLUtility
  {

    #region 定数宣言

    /// <summary>MLogger付属のXBeeのパスワード</summary>
    private const string ML_PASS = "ml_pass";

    /// <summary>データフォルダの名称</summary>
    private const string DATA_DIR_NAME = "DATA";

    /// <summary>MLoggerのLowAddress-名称対応リスト</summary>
    private const string ML_LIST_NAME = "mlList.txt";

    /// <summary>ログファイルの名称</summary>
    private const string LOG_FILE_NAME = "log.txt";
    
    /// <summary>ログファイルの最大バイト数</summary>
    private const long MAX_LOG_FILE_SIZE = 5 * 1024 * 1024; // 5MB

    /// <summary>ログファイル超過時の削除行数</summary>
    private const int LINES_TRIM = 100;

    #endregion

    #region static変数・プロパティ

    /// <summary>MLoggerのLowAddressと名称の対応リスト</summary>
    private static Dictionary<string, string> mlNames = new Dictionary<string, string>();

    /// <summary>接続先のデバイス</summary>
    public static MLDevice ConnectedDevice { get; private set; } = MLDevice.None;

    /// <summary>接続中のXBeeデバイスを設定・取得する</summary>
    public static XBeeBLEDevice ConnectedXBee { get; set; }

    /// <summary>接続されたMLoggerを取得する</summary>
    public static MLogger Logger { get; private set; }

    /// <summary>
    /// 接続済子機の v4/v3 IMLProtocol。<see cref="DetectProtocolAsync"/> 呼び出し後に
    /// 利用可能になる。CloseXbee で破棄される。
    /// </summary>
    public static IMLProtocol Protocol { get; private set; }

    /// <summary>Protocol の裏で IMLProtocol が使う transport (生成時に保持、CloseXbee で破棄)。</summary>
    private static BleXBeeTransport _bleTransport;

    #endregion

    #region XBee接続切断処理

    public static async Task<string> OpenXbeeAsync(IDevice device)
    {
      //通信中のXBeeがある場合は接続を閉じる (await して完全切断後に新接続を始める)
      await CloseXbeeAsync();

      if (Microsoft.Maui.Devices.DeviceInfo.Current.Platform == DevicePlatform.Android)
        ConnectedXBee = new XBeeBLEDevice(device.Id.ToString(), ML_PASS);
      else ConnectedXBee = new XBeeBLEDevice(device, ML_PASS);

      //XBeeをOpen (BLE GATT 接続は UI スレッドを長時間ブロックするので background へ)
      await Task.Run(() => ConnectedXBee.Connect());
      ConnectedXBee.SerialDataReceived += ConnectedXBee_SerialDataReceived;

      //接続先:MLogger (MLTransceiver サポートは v4 移行で削除)
      if (device.Name.StartsWith("MLogger_"))
      {
        ConnectedDevice = MLDevice.MLogger;
        Logger = new MLogger(ConnectedXBee.GetAddressString());
        Logger.LocalName = Logger.XBeeName = device.Name;
        SaveMLName(Logger.LowAddress, Logger.XBeeName); //LowAddressと名称の対応を保存
        return Logger.LowAddress;
      }
      else return "";
    }

    public static async Task CloseXbeeAsync()
    {
      //v4/v3 protocol 破棄 (XBee 切断より前に行う)
      try { Protocol?.Dispose(); } catch { }
      Protocol = null;
      try { _bleTransport?.Dispose(); } catch { }
      _bleTransport = null;

      //通信中のXBeeがある場合は接続を閉じる。fire-and-forget だと次の Connect と race して
      //失敗するので必ず await する。
      var dev = ConnectedXBee;
      if (dev != null && dev.IsConnected)
      {
        await Task.Run(() =>
        {
          try { dev.Disconnect(); }
          catch (Exception ex) { Console.WriteLine(ex.Message); }
        });
      }
      ConnectedDevice = MLDevice.None;
      Logger = null;
      ConnectedXBee = null;   // 次回の "IsConnected" 判定でゴーストにならないよう null 化
    }

    /// <summary>
    /// 接続済 <see cref="ConnectedXBee"/> から <see cref="BleXBeeTransport"/> を作り、
    /// <see cref="ProtocolFactory.DetectAsync"/> で v4/v3 を自動判定して <see cref="Protocol"/> に格納する。
    ///
    /// 注意: v3 ファームの場合、ProtocolFactory が内部で VER を送るので、その応答は
    /// 既存の SerialDataReceived 静的コールバック経由でも処理され、<see cref="Logger"/> の
    /// Version_* 等が副作用で更新される。これにより既存画面の VersionLoaded 待ちロジックは
    /// 変更不要のまま動作する。
    /// </summary>
    public static async Task DetectProtocolAsync(CancellationToken ct = default)
    {
      if (ConnectedXBee == null || !ConnectedXBee.IsConnected)
        throw new InvalidOperationException("XBee is not connected");
      if (Protocol != null) return; // 既に検出済

      _bleTransport = new BleXBeeTransport(ConnectedXBee);

      // 旧 debug 用 diagnostic sink (JsonRpcV4Protocol.DiagnosticSink,
      // LegacyV3Protocol.DiagnosticLineSink, BleXBeeTransport.DiagnosticRxSink/
      // TxChunkSink/DisposeTraceSink, JsonRpcV4Protocol.DisposeTraceSink) は
      // chunking/timing/race 系の bug 解析用に追加していたが解決済みのため撤去。
      // 将来必要になれば各クラスの static sink プロパティに再 wire するだけで復活可。

      //ProtocolFactory.DetectAsync は v4 hello → v3 VER の順に試す。
      //v4: 2s で即決、v3: 8s 待つ (firmware の BLE 応答遅延に追従)。
      //内側 ProbeTimeout 合計 ~10s に対し外側 ハードリミットは余裕を持って 15s。
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      cts.CancelAfter(TimeSpan.FromSeconds(15));

      try
      {
        // XBee の LowAddress を device key として cache。同じ機体に再接続する際に
        // 前回検出した protocol kind から始められる → 旧 v3 機で v4 hello timeout (~2s)
        // を skip でき、再接続が体感数秒速くなる。stale 時は ProtocolFactory 側で自動 fallback。
        Protocol = await ProtocolFactory.DetectAsync(_bleTransport, Logger?.LowAddress, cts.Token);
        var dev = Protocol.Device;
        WriteLog($"Connected: {(string.IsNullOrEmpty(dev.Name) ? Logger?.LocalName : dev.Name)} " +
          $"({(dev.ProtocolVersion >= 1 ? "v4" : "v3")} FW {dev.FirmwareVersion})");
      }
      catch
      {
        _bleTransport.Dispose();
        _bleTransport = null;
        throw;
      }

      // 注: 子機 RTC の時刻同期 (SetTimeAsync) は DeviceSetting.initInfoV4 で
      // GetSettings の直後に呼ぶ。hello 応答直後に set_time を打つと phone 側
      // BLE スタックが notification 直後の write を取りこぼし、firmware に届かない
      // 事象があったため、UI 遷移 (数秒のクッション) を経てから打つ。
    }

    /// <summary>
    /// 子機の RTC を端末の現在時刻に合わせる (best-effort)。<see cref="DetectProtocolAsync"/>
    /// 直後ではなく、UI 遷移後の落ち着いた時点で呼ぶ。
    ///
    /// これを呼ばないと firmware RTC が初期値 (2029/12/31 等) のまま動き、
    /// Sample/CSV/dump の Client Timestamp が現実時刻と乖離する。
    /// </summary>
    public static async Task SyncDeviceTimeAsync(CancellationToken ct = default)
    {
      if (Protocol == null) return;
      try
      {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        await Protocol.SetTimeAsync(DateTimeOffset.Now, cts.Token);
        WriteLog("[time] SetTimeAsync OK: " + DateTimeOffset.Now);
      }
      catch (Exception ex)
      {
        WriteLog("[time] SetTimeAsync FAIL: " + ex.GetType().Name + " " + ex.Message);
      }
    }

    private static void ConnectedXBee_SerialDataReceived
    (object sender, XBeeLibrary.Core.Events.Relay.SerialDataReceivedEventArgs e)
    {
      //IMLProtocol へ受信データを供給。v3/v4 とも LegacyV3Protocol / JsonRpcV4Protocol が
      //パース・イベント発火を担当する (旧 MLogger.AddReceivedData + SolveCommand 経路は D7 で撤去)。
      _bleTransport?.FeedReceived(e.Data);
    }

    #endregion

    #region MLoggerリストの処理

    /// <summary>MLoggerを取得する</summary>
    /// <param name="lowAddress">下位アドレス</param>
    /// <returns>MLogger</returns>
    public static MLogger GetLogger(string lowAddress)
    {
      if (ConnectedDevice == MLDevice.MLogger && Logger != null && Logger.LowAddress == lowAddress)
        return Logger;
      else return null;
    }

    #endregion

    #region Demo (実機なし UI 確認用)

    /// <summary>
    /// Demo 用: BLE 接続なしに <see cref="DummyMLProtocol"/> を活性化する。
    /// 戻り値の lowAddress を <c>DeviceSetting?mlLowAddress=...</c> に渡せば既存フローで
    /// DataReceive まで進める。実機接続中なら先に切断する。
    /// <paramref name="protocolVersion"/> で v3 (0) / v4 (1) 相当の挙動を切り替え可能。
    /// </summary>
    public static async Task<string> UseDummyProtocolAsync(int protocolVersion = 1)
    {
      await CloseXbeeAsync();

      // LongAddress 16桁 hex 想定、下位 8 桁が LowAddress になる
      Logger = new MLogger("0013A200DEMODEMO");
      // 実機の BLE デバイス名と同形式 ("MLogger_XXXX") にしておかないと
      // LoggingDataList の prefix フィルタ (StartsWith("MLogger_")) を通らず
      // ダミー計測の CSV が一覧に出てこない
      Logger.LocalName = Logger.XBeeName = "MLogger_DEMO";
      Protocol = new DummyMLProtocol(protocolVersion);
      ConnectedDevice = MLDevice.MLogger;

      return Logger.LowAddress;
    }

    #endregion

    #region データ入出力処理

    /// <summary>データ保存ディレクトリを用意する</summary>
    public static void InitDirAndFiles()
    {
      string dFolder = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME;
      if (!Directory.Exists(dFolder))
        Directory.CreateDirectory(dFolder);

      string mlPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + ML_LIST_NAME;
      if (!File.Exists(mlPath))
      {
        using (StreamWriter sWriter = new StreamWriter(mlPath, false))
        {
          sWriter.Write("");
        }
      }

      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      if (!File.Exists(logPath))
      {
        using (StreamWriter sWriter = new StreamWriter(logPath, false))
        {
          sWriter.Write("");
        }
      }
    }

    /// <summary>データファイルリストを取得する</summary>
    /// <returns>データファイルリスト</returns>
    public static string[] GetDataFiles()
    {
      string folder = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME;
      return Directory.GetFiles(folder);
    }

    /// <summary>データファイルの内容を取得する</summary>
    /// <param name="fileName">データファイル名称</param>
    /// <param name="maxLine">読み込む最大行数</param>
    /// <returns>データファイルの内容</returns>
    public static string LoadDataFile(string fileName, int maxLine)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;

      //先頭のmaxLine行を読み込む
      int lines = 1;
      StringBuilder sBuilder = new StringBuilder();
      using (StreamReader sReader = new StreamReader(filePath, Encoding.UTF8))
      {
        string buff;
        while ((buff = sReader.ReadLine()) != null && lines <= maxLine)
        {
          sBuilder.AppendLine(buff);
          lines++;
        }
        return sBuilder.ToString().TrimEnd('\r', '\n');
      }
    }

    /// <summary>データファイルの内容を取得する</summary>
    /// <param name="fileName">データファイル名称</param>
    /// <returns>データファイルの内容</returns>
    public static string LoadDataFile(string fileName)
    {
      return LoadDataFile(fileName, int.MaxValue);
    }

    /// <summary>データファイルに追記する</summary>
    /// <param name="fileName">データファイル名称</param>
    /// <param name="data">追記するデータ</param>
    public static void AppendData(string fileName, string data)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;

      using (StreamWriter sWriter = new StreamWriter(filePath, true))
      {
        sWriter.Write(data);
      }
    }

    public static void DeleteDataFile(string fileName)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;
      if(File.Exists(filePath))
        File.Delete(filePath);
    }

    /// <summary>ファイルサイズ[byte]を取得する</summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static long GetFileSize(string fileName)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;
      if (File.Exists(filePath))
      {
        FileInfo file = new FileInfo(filePath);
        return file.Length;
      }
      return 0;
    }

    #endregion

    #region MLogger名称リスト関連の処理

    /// <summary>MLoggerのLowAddressと名称の対応表を読み込む</summary>
    public static void LoadMLNamesFile()
    {
      string fName = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + ML_LIST_NAME;
      using (StreamReader sReader = new StreamReader(fName, Encoding.UTF8))
      {
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          if (line != "" && line.Contains(','))
          {
            string[] buff = line.Split(',');
            mlNames.Add(buff[0], buff[1]);
          }
        }
      }
    }

    /// <summary>MLoggerのLowAddressと名称の対応表を更新する</summary>
    public static void UpdateMLNamesFile()
    {
      string fName = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + ML_LIST_NAME;
      using (StreamWriter sWriter = new StreamWriter(fName, false, Encoding.UTF8))
      {
        foreach (string lowAdd in mlNames.Keys)
          sWriter.WriteLine(lowAdd + "," + mlNames[lowAdd].Replace(",", ""));
      }
    }

    /// <summary>MLoggerの名称を取得する</summary>
    /// <param name="lowAddress">XBeeの下位アドレス</param>
    /// <returns>MLoggerの名称</returns>
    public static string GetMLName(string lowAddress)
    {
      if (mlNames.ContainsKey(lowAddress)) return mlNames[lowAddress];
      else return "MLogger_new";
    }

    /// <summary>MLoggerの名称を設定する</summary>
    /// <param name="lowAddress">XBeeの下位アドレス</param>
    /// <param name="name">MLoggerの名称</param>
    public static void SaveMLName(string lowAddress, string name)
    {
      //登録済のMLoggerの場合
      if (mlNames.ContainsKey(lowAddress))
      {
        if (mlNames[lowAddress] != name) //現実にはこのケースは発生しないはず。。。
        {
          mlNames[lowAddress] = name;
          UpdateMLNamesFile();
        }
      }
      //未登録のMLoggerの場合
      else
      {
        mlNames.Add(lowAddress, name);
        UpdateMLNamesFile();
      }
    }

    #endregion

    #region Log関連の処理

    public static string ReadLog()
    {
      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      using (StreamReader sr = new StreamReader(logPath))
      {
        return sr.ReadToEnd();
      }
    }

    /// <summary>ログファイルを空にする (LogView の Clear ボタン用)。</summary>
    public static void ClearLog()
    {
      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      File.WriteAllText(logPath, "");
    }

    public static void WriteLog(string logMessage)
    {
      // 既存のログが上限を超えているか確認
      ManageLogFileSize();

      // ログをファイルに追加。改行は CRLF 固定 (Android/iOS の Environment.NewLine は "\n"
      // のため、Windows メールクライアントで開いたとき行が連結して見える)。
      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      using (StreamWriter sw = new StreamWriter(logPath, true))
      {
        sw.NewLine = "\r\n";
        sw.WriteLine($"{DateTime.Now}: {logMessage}");
      }
    }

    /// <summary>
    /// 例外発生時のユーザー向け Alert を統一する。
    ///   - 技術詳細 (型名 + ex.Message + StackTrace 先頭) は WriteLog で LogView に残す
    ///   - 画面には userMessage + 「詳細はログ画面で確認できます。」とだけ表示
    /// これで一般ユーザーには技術詳細を出さず、開発者は LogView で原因追跡できる。
    /// </summary>
    public static async Task ShowErrorAsync(Page page, string userMessage, Exception ex)
    {
      // スタックトレースは長いので先頭 600 文字程度に切り詰めて記録
      string stack = ex.StackTrace ?? "";
      if (stack.Length > 600) stack = stack.Substring(0, 600) + "...";
      WriteLog($"[Error] {userMessage} :: {ex.GetType().Name}: {ex.Message}\r\n{stack}");

      // DisplayAlert は UI スレッド必須。バックグラウンドからの呼び出しでも安全になるよう
      // MainThread 経由で実行する。
      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        await page.DisplayAlert(
          MLSResource.ERR_AlertTitle,
          userMessage + Environment.NewLine + Environment.NewLine + MLSResource.ERR_DetailsInLog,
          "OK");
      });
    }

    private static void ManageLogFileSize()
    {
      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      FileInfo logFile = new FileInfo(logPath);
      if (logFile.Exists && logFile.Length > MAX_LOG_FILE_SIZE)
      {
        // 古い行を削除する
        TrimLogFile();
      }
    }

    private static void TrimLogFile()
    {
      // ファイルのすべての行を読み込む
      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      string[] allLines = File.ReadAllLines(logPath);

      // 指定された行数を削除
      if (allLines.Length > LINES_TRIM)
        File.WriteAllLines(logPath, allLines[LINES_TRIM..]);
      else File.WriteAllText(logPath, "");
    }

    #endregion

  }
}
