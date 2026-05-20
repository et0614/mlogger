using MLLib;
using MLLib.Protocol;
using MLLib.Protocol.Protocols;
using MLServer.BACnet;
using MLServer.Transport;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using XBeeLibrary.Core;
using XBeeLibrary.Core.Models;

namespace MLServer
{
  class Program
  {

    #region 定数宣言

    private const string VERSION = "1.2.0";

    /// <summary>XBEEの上位アドレス</summary>
    private const string HIGH_ADD = "0013A200";

    /// <summary>JSONデータを更新する時間間隔[msec]</summary>
    private const int JSON_REFRESH_SPAN = 10 * 1000;

    /// <summary>Portに接続されたコーディネータXBeeの探索時間間隔[msec]</summary>
    private const int PORT_SCAN_SPAN = 1 * 1000;

    /// <summary>UART通信のボーレート</summary>
    private const int BAUD_RATE = 9600;

    /// <summary>日時の型</summary>
    private const string DT_FORMAT = "yyyy/MM/dd HH:mm:ss";

    /// <summary>XBeeLibrary.Core のバグで ~48500B 受信付近で落ちるため、この閾値で coordinator を再接続</summary>
    private const int REOPEN_BYTES_THRESHOLD = 45000;

    #endregion

    #region クラス変数

    /// <summary>BACnetを使うか否か</summary>
    private static bool useBACnet = false;

    /// <summary>BACnetを使う場合のポート番号（47808~）</summary>
    private static int bacnetPort = 47809;

    /// <summary>BACnet DeviceのLocal End Point IP Address</summary>
    private static string bacEPIPAddress = "127.0.0.1";

    private static MLServerDevice? mlBacDevice;

    /// <summary>温冷感計算のための基準の物理量</summary>
    private static double metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue;

    /// <summary>新しいデータ収集があったか否か</summary>
    private static bool hasNewData = true;

    /// <summary>データ格納用のディレクトリ</summary>
    private static string dataDirectory = "";

    /// <summary>発見された子機 (remote アドレスごとの session)</summary>
    private static readonly ConcurrentDictionary<string, RemoteSession> sessions = new();

    /// <summary>接続できなかったポートリスト</summary>
    private static readonly List<string> excludedPorts = new();

    /// <summary>通信用コーディネータリスト</summary>
    private static readonly ConcurrentDictionary<ZigBeeDevice, xbeeInfo> coordinators = new();

    /// <summary>MLoggerのアドレス-名称対応リスト</summary>
    private static readonly Dictionary<string, string> mlNames = new();

    /// <summary>受信パケット総量[bytes] (coordinator ごと)</summary>
    private static readonly ConcurrentDictionary<ZigBeeDevice, int> packetBytes = new();

    #endregion

    #region メイン処理

    static void Main(string[] args)
    {
      showTitle();

      //データ格納用のディレクトリを作成
      dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

      //温冷感計算のための代謝量[met]と着衣量[clo]を読み込む
      loadInitFile();

      // 診断 sink を Console に向ける (受信 byte / parser 行 を見える化)
      XBeeZigbeeCoordinatorMux.DiagnosticRxSink = (addr, len) =>
        Console.WriteLine($"[mux-rx] {addr.Substring(addr.Length - 8)} {len}B");
      LegacyV3Protocol.DiagnosticLineSink = line =>
        Console.WriteLine($"[v3-line] {line}");
      JsonRpcV4Protocol.DiagnosticSink = msg =>
        Console.WriteLine($"[v4] {msg}");

      //必要に応じてBACnet起動
      Console.WriteLine("BACnet service is " + (useBACnet ? "enabled." : "disabled."));
      if (useBACnet)
      {
        Console.WriteLine("Start the BACnet service. (Local end point = \"" + (bacEPIPAddress == "" ? "0.0.0.0" : bacEPIPAddress) + "\", Exclusive port = \"" + bacnetPort + "\")");
        mlBacDevice = new MLServerDevice(bacnetPort, bacEPIPAddress);
        mlBacDevice.Communicator.StartService();
      }
      Console.WriteLine();

      //MLoggerのアドレス-名称対応リストを読む
      string nFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "mlnames.txt";
      if (File.Exists(nFile))
      {
        using StreamReader sReader = new(nFile);
        string? line;
        while ((line = sReader.ReadLine()) != null && line.Contains(':'))
        {
          line = line.Split("//", StringSplitOptions.None)[0].Trim(); //コメント削除
          string[] bf = line.Split(':');
          if (!mlNames.ContainsKey(HIGH_ADD + bf[0]))
            mlNames.Add(HIGH_ADD + bf[0], bf[1]);
        }
      }

      //定期的にJSONファイルを更新する
      Task.Run(() =>
      {
        while (true)
        {
          if (hasNewData)
          {
            hasNewData = false;
            makeJSONData();
          }
          Thread.Sleep(JSON_REFRESH_SPAN);
        }
      });

      //現在日時更新タスク (日が変わったら全 session に SetTimeAsync)
      updateCurrentDateTime();

      //定期的にコーディネータをスキャン
      scanCoordinators();

      //定期的にコーディネータのイベント登録
      resistEvent();

      //メインスレッドは休む
      while (true) Thread.Sleep(1000);
    }

    private static void showTitle()
    {
      Console.WriteLine("\r\n");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#                     MLServer  verstion " + VERSION + "                          #");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#        Software for logging data transmitted from the M-Logger.       #");
      Console.WriteLine("#                       (https://www.mlogger.jp)                        #");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("\r\n");
    }

    private static void loadInitFile()
    {
      metValue = 1.1; cloValue = 1.0; dbtValue = 25.0; rhdValue = 50.0; velValue = 0.2; mrtValue = 25.0;

      using StreamReader sReader = new(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "setting.ini");
      string? line;
      while ((line = sReader.ReadLine()) != null)
      {
        if (string.IsNullOrEmpty(line)) continue;
        if (line.StartsWith('#')) continue;

        int semi = line.IndexOf(';');
        if (semi >= 0) line = line.Remove(semi);
        string[] st = line.Split('=');
        if (st.Length < 2) continue;
        switch (st[0].Trim())
        {
          case "met":     metValue   = double.Parse(st[1]); break;
          case "clo":     cloValue   = double.Parse(st[1]); break;
          case "dbt":     dbtValue   = double.Parse(st[1]); break;
          case "rhd":     rhdValue   = double.Parse(st[1]); break;
          case "vel":     velValue   = double.Parse(st[1]); break;
          case "mrt":     mrtValue   = double.Parse(st[1]); break;
          case "bacnet":  useBACnet  = bool.Parse(st[1]);   break;
          case "bacport": bacnetPort = int.Parse(st[1]);    break;
          case "bacip":   bacEPIPAddress = st[1].Trim();    break;
        }
      }
    }

    private async static void scanCoordinators()
    {
      while (true)
      {
        //接続可能なポート一覧
        string[] portList = System.IO.Ports.SerialPort.GetPortNames();

        //接続候補のポートを絞り込む
        foreach (string port in portList)
        {
          if (!excludedPorts.Contains(port))
          {
            ZigBeeDevice device = new(new XBeeLibrary.Windows.Connection.Serial.WinSerialPort(port, BAUD_RATE));
            xbeeInfo xInfo = new(port);
            if (coordinators.TryAdd(device, xInfo))
            {
              excludedPorts.Add(port);
              xInfo.connectTask = Task.Run(device.Open);
            }
          }
        }

        await Task.Delay(PORT_SCAN_SPAN);
      }
    }

    private async static void resistEvent()
    {
      while (true)
      {
        foreach (ZigBeeDevice device in coordinators.Keys)
        {
          xbeeInfo xInfo = coordinators[device];
          //接続タスクが終了した場合は結果を表示
          if (!xInfo.resistEvent && xInfo.connectTask != null)
          {
            if (xInfo.connectTask.Status == TaskStatus.RanToCompletion)
            {
              xInfo.resistEvent = true;
              Console.WriteLine(xInfo.portName + ": Connection succeeded. S/N = " + device.XBee64BitAddr.ToString());

              //Mux と PacketReceived を登録
              attachMux(device);
            }
            else if (xInfo.connectTask.Status == TaskStatus.Faulted)
            {
              xInfo.resistEvent = true;
              Console.WriteLine("Failed to connect port " + xInfo.portName);
            }
          }
        }

        await Task.Delay(PORT_SCAN_SPAN);
      }
    }

    /// <summary>coordinator に Mux と PacketReceived を取り付ける。</summary>
    private static void attachMux(ZigBeeDevice device)
    {
      var mux = new XBeeZigbeeCoordinatorMux(device);
      mux.NewRemoteDiscovered += OnNewRemoteDiscovered;
      coordinators[device].mux = mux;

      device.PacketReceived += Device_PacketReceived;
    }

    private static void Device_PacketReceived(object? sender, XBeeLibrary.Core.Events.PacketReceivedEventArgs e)
    {
      if (sender is not ZigBeeDevice coord) return;
      int total = packetBytes.AddOrUpdate(coord, e.ReceivedPacket.PacketLength, (_, v) => v + e.ReceivedPacket.PacketLength);

      //XBeeLibrary.Core のバグ workaround: 受信総量が閾値超えたら coordinator を再接続
      if (total > REOPEN_BYTES_THRESHOLD)
      {
        reopenCoordinator(coord);
      }
    }

    private static void reopenCoordinator(ZigBeeDevice coord)
    {
      while (true)
      {
        try
        {
          coord.PacketReceived -= Device_PacketReceived;
          coord.Close();
          packetBytes[coord] = 0;
          coord.Open();
          coord.PacketReceived += Device_PacketReceived;
          coordinators[coord].mux?.RebindAfterReopen();
          return;
        }
        catch
        {
          Console.WriteLine("Re-connect Error");
        }
      }
    }

    #endregion

    #region 子機 (RemoteSession) 関連の処理

    private static void OnNewRemoteDiscovered(RemoteXBeeDevice rdv)
    {
      string addr = rdv.GetAddressString();

      //親機が見つかった場合には終了
      foreach (ZigBeeDevice cd in coordinators.Keys)
        if (cd.GetAddressString() == addr) return;

      if (sessions.ContainsKey(addr)) return;

      ZigBeeDevice? coord = rdv.GetLocalXBeeDevice() as ZigBeeDevice;
      if (coord == null) return;
      var xInfo = coordinators[coord];
      if (xInfo.mux == null) return;

      //session 構築 (Protocol 検出は async でバックグラウンド実行)
      var session = new RemoteSession(addr, rdv);
      session.Cache.LocalName = mlNames.TryGetValue(addr, out var nm) ? nm : "MLogger_" + addr;
      session.Cache.CloValue = cloValue;
      session.Cache.MetValue = metValue;
      session.Cache.DefaultTemperature = dbtValue;
      session.Cache.DefaultRelativeHumidity = rhdValue;
      session.Cache.DefaultGlobeTemperature = mrtValue;
      session.Cache.DefaultVelocity = velValue;

      if (!sessions.TryAdd(addr, session)) return;
      xInfo.longAddress.Add(addr);

      _ = Task.Run(() => InitSessionAsync(session, xInfo.mux));
    }

    private static async Task InitSessionAsync(RemoteSession session, XBeeZigbeeCoordinatorMux mux)
    {
      // v3 firmware は DTT 配信中に VER probe 応答が遅れることがあり、初回 DetectAsync が
      // タイムアウトしがち。失敗 → 放置だと mux subject に subscriber が居なくなり、以降の
      // DTT バイトが全て捨てられて CSV/BACnet 出力が止まる事象が出る。
      // → 数回リトライし、各試行で新規 Transport (= 新規 subscription) を張り直す。
      const int MaxAttempts = 5;
      const int RetryDelaySec = 5;

      for (int attempt = 1; attempt <= MaxAttempts; attempt++)
      {
        try
        {
          // 既存 Transport があれば破棄してから新規作成 (前回 Detect 失敗時の disposed subscription を捨てる)
          session.Transport?.Dispose();
          session.Transport = new XBeeZigbeeTransport(mux, session.Remote);

          using var detectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
          session.Protocol = await ProtocolFactory.DetectAsync(session.Transport, detectCts.Token);

          Console.WriteLine(session.Cache.LocalName + ": Protocol = " +
            (session.Protocol.Device.ProtocolVersion >= 1 ? "v4 (JSON-RPC)" : "v3 (legacy)") +
            ", FW " + session.Protocol.Device.FirmwareVersion +
            (attempt > 1 ? $" (after {attempt} attempts)" : ""));

          // device.Name (v4 の hello name) を反映 (v3 では LLN から取得)
          if (!string.IsNullOrEmpty(session.Protocol.Device.Name))
            session.Cache.Name = session.Protocol.Device.Name;
          session.Cache.HasCO2LevelSensor = session.Protocol.Device.HasCo2Sensor;

          // 計測設定を一度取得 (best-effort)。Zigbee 経由は frame 帯域が不安定で
          // 応答が遅延しがちなので 15 秒待つ。失敗しても session は継続。
          try
          {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var s = await session.Protocol.GetSettingsAsync(cts.Token);
            session.Cache.ApplySettings(s);
          }
          catch (Exception ex) { Console.WriteLine(session.Cache.LocalName + ": GetSettings failed: " + ex.Message); }

          // Samples 購読: ApplySample → CSV 出力 + BACnet 更新
          session.SamplesSub = session.Protocol.Samples.Subscribe(s => OnSampleReceived(session, s));

          hasNewData = true;
          return; // 成功
        }
        catch (Exception ex)
        {
          Console.WriteLine(session.Cache.LocalName +
            $": Protocol detection failed (attempt {attempt}/{MaxAttempts}): " + ex.Message);
          if (attempt < MaxAttempts)
            await Task.Delay(TimeSpan.FromSeconds(RetryDelaySec));
        }
      }

      Console.WriteLine(session.Cache.LocalName + ": gave up after " + MaxAttempts + " attempts");
    }

    #endregion

    #region 計測値受信処理

    private static void OnSampleReceived(RemoteSession session, Sample s)
    {
      session.Cache.ApplySample(s);
      hasNewData = true;

      Console.WriteLine($"[sample] {session.Cache.LowAddress} " +
        $"dbt={s.DrybulbTemperature:F1} rh={s.RelativeHumidity:F1} " +
        $"glb={s.GlobeTemperature:F1} vel={s.Velocity:F3} " +
        $"ill={s.Illuminance} co2={s.Co2}");

      //CSV 追記
      string fName = dataDirectory + Path.DirectorySeparatorChar + session.Cache.LowAddress + ".csv";
      try
      {
        bool firstCall = !File.Exists(fName);
        using StreamWriter sWriter = new(fName, true, Encoding.UTF8);
        if (firstCall)
          sWriter.WriteLine(
            "Server Timestamp,Client Timestamp,Drybulb temperature[C],Relative humidity[%]," +
            "Globe temperature[C],Velocity[m/s],Illuminance[lux],Forward Compatibility Placeholder," +
            "Voltage for velocity measurement[V],Future Placeholder,Mean radiant temperature[C],WBGT (Indoor)[C],WBGT (Outdoor[C])");

        var cache = session.Cache;
        sWriter.WriteLine(
          DateTime.Now.ToString(DT_FORMAT) + "," +
          ((cache.LastMeasured.Year == 2000 || 2100 < cache.LastMeasured.Year) ? "n/a" : cache.LastMeasured.ToString(DT_FORMAT)) + "," +
          fmtOrNA(s.DrybulbTemperature, "F1") + "," +
          fmtOrNA(s.RelativeHumidity,   "F1") + "," +
          fmtOrNA(s.GlobeTemperature,   "F1") + "," +
          fmtOrNA(s.Velocity,           "F4") + "," +
          fmtOrNA(s.Illuminance,        "F2") + "," +
          "n/a,n/a,n/a," + // v4 Sample に電圧フィールド無し、placeholder
          fmtOrDouble(cache.MeanRadiantTemperature, "F1") + "," +
          fmtOrDouble(cache.WBGT_Indoor,            "F1") + "," +
          fmtOrDouble(cache.WBGT_Outdoor,           "F1"));
      }
      catch
      {
        Console.WriteLine(session.Cache.LocalName + ": Can't access to file.");
      }

      //BACnet device 更新
      if (useBACnet && mlBacDevice != null)
        mlBacDevice.UpdateLogger(session.Cache);
    }

    private static string fmtOrNA(double? v, string fmt) => v.HasValue ? v.Value.ToString(fmt) : "n/a";
    private static string fmtOrNA(int? v, string fmt) => v.HasValue ? v.Value.ToString(fmt) : "n/a";
    private static string fmtOrDouble(double v, string fmt) => double.IsNaN(v) ? "n/a" : v.ToString(fmt);

    #endregion

    #region 現在日時更新関連の処理

    /// <summary>日付変更時に各 session の Protocol.SetTimeAsync で時刻同期。</summary>
    private static void updateCurrentDateTime()
    {
      Task.Run(async () =>
      {
        DateTime prevTime = DateTime.Now;
        while (true)
        {
          if (prevTime.Day != DateTime.Now.Day)
          {
            foreach (var session in sessions.Values)
            {
              if (session.Protocol == null) continue;
              try
              {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await session.Protocol.SetTimeAsync(DateTimeOffset.Now, cts.Token);
                Console.WriteLine(session.Cache.LocalName + ": Time sync OK");
              }
              catch (Exception ex)
              {
                Console.WriteLine(session.Cache.LocalName + ": Time sync failed: " + ex.Message);
              }
            }
          }
          prevTime = DateTime.Now;
          Thread.Sleep(1000);
        }
      });
    }

    #endregion

    #region JSONデータの生成処理

    private static void makeJSONData()
    {
      try
      {
        var options = new JsonSerializerOptions
        {
          Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
          WriteIndented = true,
        };
        // セッション → cache の dict に変換して serialize (HTML dashboard の参照する shape)
        var snapshot = new Dictionary<string, LoggerCache>();
        foreach (var (addr, session) in sessions) snapshot[addr] = session.Cache;
        var json = JsonSerializer.Serialize(snapshot, options);
        using StreamWriter sWriter = new(dataDirectory + Path.DirectorySeparatorChar + "latest.json", false, Encoding.UTF8);
        sWriter.Write(json);
      }
      catch (JsonException e)
      {
        Console.WriteLine(e.Message);
      }
    }

    #endregion

    #region インナークラスの定義

    /// <summary>通信用XBee端末の情報</summary>
    private class xbeeInfo
    {
      public xbeeInfo(string portName) { this.portName = portName; }

      public List<string> longAddress = new();
      public string portName { get; private set; }
      public bool resistEvent { get; set; } = false;
      public Task? connectTask { get; set; }
      public XBeeZigbeeCoordinatorMux? mux { get; set; }
    }

    /// <summary>1 子機ぶんの protocol/transport/cache をまとめる</summary>
    private class RemoteSession
    {
      public string Address { get; }
      public RemoteXBeeDevice Remote { get; }
      public LoggerCache Cache { get; }
      public XBeeZigbeeTransport? Transport { get; set; }
      public IMLProtocol? Protocol { get; set; }
      public IDisposable? SamplesSub { get; set; }

      public RemoteSession(string address, RemoteXBeeDevice remote)
      {
        Address = address;
        Remote = remote;
        // LowAddress は HIGH_ADD を除いた下位アドレス
        string low = address.Length > 8 ? address.Substring(address.Length - 8) : address;
        Cache = new LoggerCache(longAddress: address, lowAddress: low);
      }
    }

    #endregion

  }
}
