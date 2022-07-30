using System;

using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using XBeeLibrary.Core;
using XBeeLibrary.Core.Models;

using MLLib;

namespace MLServer
{
  class Program
  {

    #region 定数宣言

    /// <summary>XBEEの上位アドレス</summary>
    private const string UP_ADD = "0013A200";

    /// <summary>HTMLデータを更新する時間間隔[msec]</summary>
    private const int HTML_REFRESH_SPAN = 10 * 1000;

    /// <summary>コーディネータXBee探索時間間隔[msec]</summary>
    private const int XBEE_SCAN_SPAN = 5 * 1000;

    /// <summary>子機の探索時間間隔[msec]</summary>
    private const int ENDDV_SCAN_SPAN = 5 * 1000;

    #endregion

    #region クラス変数

    /// <summary>UART通信のボーレート</summary>
    private static int baudRate;

    /// <summary>受信パケット総量[bytes]</summary>
    private static int pBytes = 0;

    /// <summary>温冷感計算のための基準の物理量</summary>
    private static double metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue;

    /// <summary>新しいデータ収集があったか否か</summary>
    private static bool hasNewData = true;

    /// <summary>データ格納用のディレクトリ</summary>
    private static string dataDirectory;

    /// <summary>発見されたMLogger端末のリスト</summary>
    private static Dictionary<string, MLogger> mLoggers = new Dictionary<string, MLogger>();

    /// <summary>XBee端末と接続されたポート名のリスト</summary>
    private static List<string> connectedPorts = new List<string>();

    /// <summary>接続候補のポートリスト</summary>
    private static List<string> excludedPorts = new List<string>();

    /// <summary>通信用XBee端末リスト</summary>
    private static Dictionary<ZigBeeDevice, xbeeInfo> coordinators = new Dictionary<ZigBeeDevice, xbeeInfo>();

    /// <summary>MLoggerのアドレス-名称対応リスト</summary>
    private static readonly Dictionary<string, string> mlNames = new Dictionary<string, string>();

    #endregion

    #region メイン処理

    static void Main(string[] args)
    {
      //データ格納用のディレクトリを作成
      dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

      //温冷感計算のための代謝量[met]と着衣量[clo]を読み込む
      loadInitFile(out baudRate, out metValue, out cloValue, out dbtValue, out rhdValue, out velValue, out mrtValue);

      //MLoggerのアドレス-名称対応リストを読む
      using (StreamReader sReader = new StreamReader
        (AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "mlnames.txt"))
      {
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          string[] bf = line.Split(':');
          mlNames.Add(UP_ADD + bf[0], bf[1]);
        }
      }

      //定期的にHTMLファイルを更新する
      Task htmlRefreshTask = Task.Run(() =>
      {
        while (true)
        {
          if (hasNewData)
          {
            hasNewData = false;
            makeWebData();
          }
          Thread.Sleep(HTML_REFRESH_SPAN);
        }
      });

      //定期的にXBeeコーディネータを探索する
      scanCoordinator();

      //子機を探す
      scanEndDevice();

      while (true) ;
    }

    private static void loadInitFile
      (out int brate, out double met, out double clo, out double dbt, out double rhd, out double vel, out double mrt)
    {
      brate = 9600;
      met = 1.1;
      clo = 1.0;
      dbt = 25.0;
      rhd = 50.0;
      vel = 0.2;
      mrt = 25.0;

      using (StreamReader sReader = new StreamReader
        (AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "setting.ini"))
      {
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          line = line.Remove(line.IndexOf(';'));
          string[] st = line.Split('=');
          switch (st[0])
          {
            case "baud_rate":
              baudRate = int.Parse(st[1]);
              break;
            case "met":
              metValue = double.Parse(st[1]);
              break;
            case "clo":
              cloValue = double.Parse(st[1]);
              break;
            case "dbt":
              dbtValue = double.Parse(st[1]);
              break;
            case "rhd":
              rhdValue = double.Parse(st[1]);
              break;
            case "vel":
              velValue = double.Parse(st[1]);
              break;
            case "mrt":
              mrtValue = double.Parse(st[1]);
              break;
          }
        }
      }
    }

    private static void scanCoordinator()
    {
      Task xbeeScanTask = Task.Run(() =>
      {
        while (true)
        {
          //各ポートへの接続を試行
          string[] portList = System.IO.Ports.SerialPort.GetPortNames();
          foreach (string pn in excludedPorts)
            if (Array.IndexOf(portList, pn) == -1)
              excludedPorts.Remove(pn);

          for (int i = 0; i < portList.Length; i++)
          {
            if (!connectedPorts.Contains(portList[i]) && !excludedPorts.Contains(portList[i]))
            {
              Task tsk = makeConnectTask(portList[i]);
              tsk.Start();
            }
          }
          Thread.Sleep(XBEE_SCAN_SPAN);
        }
      });
    }

    private static void scanEndDevice()
    {
      //定期的にXBeeコーディネータを探索する
      Task scanEndDeviceTask = Task.Run(() =>
      {
        while (true)
        {
          foreach (ZigBeeDevice coordinator in coordinators.Keys)
          {
            XBeeNetwork net = coordinator.GetNetwork();

            //既に探索中の場合は一旦停止
            if (net.IsDiscoveryRunning) net.StopNodeDiscoveryProcess();

            //探索開始
            net.SetDiscoveryTimeout((int)(ENDDV_SCAN_SPAN * 0.9)); //5秒
            try
            {
              Console.WriteLine("Start scanning end devices...");
              net.StartNodeDiscoveryProcess(); //DiscoveryProcessの二重起動で例外が発生する
            }
            catch (Exception e)
            {
              Console.WriteLine(e.Message);
            }
          }

          Thread.Sleep(ENDDV_SCAN_SPAN);
        }
      });
    }

    #endregion

    #region コーディネータ接続関連の処理

    /// <summary>Portへの接続Taskを生成</summary>
    /// <param name="pName">Port名称</param>
    /// <returns>Portへの接続Task</returns>
    private static Task makeConnectTask(string pName)
    {
      return new Task(() =>
      {
        //通信用XBee端末をOpen
        ZigBeeDevice device = new ZigBeeDevice(new XBeeLibrary.Windows.Connection.Serial.WinSerialPort(pName, baudRate));
        try
        {
          device.Open();
        }
        catch (Exception ex)
        {
          excludedPorts.Add(pName);
          Console.WriteLine(pName + ": " + ex.Message);
          return;
        }
        coordinators.Add(device, new xbeeInfo(pName));
        Console.WriteLine(pName + ": Connection succeeded." + " S/N = " + device.XBee64BitAddr.ToString());

        //イベント登録
        XBeeNetwork net = device.GetNetwork();
        device.GetNetwork().DeviceDiscovered += Net_DeviceDiscovered; //xbeeノード発見イベント
                                                                      //
        device.DataReceived += Device_DataReceived; //データ受信イベント
        device.PacketReceived += Device_PacketReceived;  //パケット総量を捕捉

        connectedPorts.Add(pName);
      });
    }

    private static void Net_DeviceDiscovered(object sender, XBeeLibrary.Core.Events.DeviceDiscoveredEventArgs e)
    {
      //HTML更新フラグを立てる
      hasNewData = true;

      RemoteXBeeDevice rdv = e.DiscoveredDevice;
      ZigBeeDevice dv = rdv.GetLocalXBeeDevice() as ZigBeeDevice;

      //MLoggerリストに追加
      string add = rdv.GetAddressString();
      if (!mLoggers.ContainsKey(add))
      {
        MLogger ml = new MLogger(add);
        
        //名前を設定
        if (mlNames.ContainsKey(add)) ml.Name = mlNames[add];

        //熱的快適性計算のための情報を設定
        ml.CloValue = cloValue;
        ml.MetValue = metValue;
        ml.DefaultTemperature = dbtValue;
        ml.DefaultRelativeHumidity = rhdValue;
        ml.DefaultGlobeTemperature = mrtValue;
        ml.DefaultVelocity = velValue;

        //イベント登録
        ml.MeasuredValueReceivedEvent += Ml_MeasuredValueReceivedEvent;

        mLoggers.Add(add, ml);
      }

      //子機のアドレスと通信用XBeeを対応付ける
      if (!coordinators[dv].longAddress.Contains(add))
        coordinators[dv].longAddress.Add(add);
    }

    private static void Device_DataReceived(object sender, XBeeLibrary.Core.Events.DataReceivedEventArgs e)
    {
      RemoteXBeeDevice rdv = e.DataReceived.Device;
      string add = rdv.GetAddressString();
      string rcvStr = e.DataReceived.DataString;

      if (!mLoggers.ContainsKey(add)) return; //未登録のノードからの受信は無視

      //HTML更新フラグを立てる
      hasNewData = true;

      MLogger mlg = mLoggers[add];

      //受信データを追加
      mlg.AddReceivedData(rcvStr);

      //コマンド処理
      while (mlg.HasCommand)
      {
        try
        {
          Console.WriteLine(mlg.Name + ": " + mlg.NextCommand);
          mlg.SolveCommand();
        }
        catch (Exception exc)
        {
          Console.WriteLine(mlg.Name + " : " + exc.Message);
          mlg.ClearReceivedData(); //異常終了時はコマンドを全消去する
        }
      }

      //受信パケット総量が48500bytesを超えた場合に再接続
      //XBeeLibrary.Coreのバグなのか、48500byteあたりで落ちるため
      //かなりいい加減でデータの取りこぼしが発生しかねない処理。
      if (45000 < pBytes)
      {
        while (true)
        {
          try
          {
            XBeeDevice dvv = (XBeeDevice)e.DataReceived.Device.GetLocalXBeeDevice();

            dvv.DataReceived -= Device_DataReceived;
            dvv.PacketReceived -= Device_PacketReceived;
            dvv.Close();

            pBytes = 0;

            dvv.Open();
            dvv.DataReceived += Device_DataReceived;
            dvv.PacketReceived += Device_PacketReceived;
            return;
          }
          catch
          {
            Console.WriteLine("Re-connect Error");
          }
        }

      }
    }

    private static void Device_PacketReceived(object sender, XBeeLibrary.Core.Events.PacketReceivedEventArgs e)
    {
      //受信パケット総量を加算
      pBytes += e.ReceivedPacket.PacketLength;
    }

    #endregion

    #region コマンド受信イベント発生時の処理

    private static void Ml_MeasuredValueReceivedEvent(object sender, EventArgs e)
    {
      //データ書き出し
      MLogger ml = (MLogger)sender;      
      string fName = dataDirectory + Path.DirectorySeparatorChar + ml.LowAddress + ".csv";

      try
      {
        using (StreamWriter sWriter = new StreamWriter(fName, true, Encoding.UTF8))
        {
          sWriter.WriteLine(
            DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "," + //親機の現在日時
            ml.LastMeasured.ToString("yyyy/MM/dd HH:mm:ss") + "," + //子機の計測日時
            ml.DrybulbTemperature.LastValue.ToString("F2") + "," +
            ml.RelativeHumdity.LastValue.ToString("F2") + "," +
            ml.GlobeTemperatureVoltage.ToString("F3") + "," +
            ml.GlobeTemperature.LastValue.ToString("F2") + "," +
            ml.VelocityVoltage.ToString("F3") + "," +
            ml.Velocity.LastValue.ToString("F4") + "," +
            ml.Illuminance.LastValue.ToString("F2") + "," +
            ml.GeneralVoltage1.LastValue.ToString("F3") + "," +
            ml.GeneralVoltage2.LastValue.ToString("F3") + "," +
            ml.GeneralVoltage3.LastValue.ToString("F3"));
        }
      }
      catch
      {
        Console.WriteLine(ml.Name + ": Can't access to file.");
        return;
      }
    }

    #endregion

    #region WEBサーバーデータの生成処理

    private static void makeWebData()
    {
      MLogger[] loggers = new MLogger[mLoggers.Values.Count];
      mLoggers.Values.CopyTo(loggers, 0);

      string html = MLogger.MakeHTMLTable(Resources.topPage_html, loggers);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "index.htm", false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
      { sWriter.Write(html); }

      string latestData = MLogger.MakeLatestData(loggers);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "latest.txt", false, Encoding.UTF8))
      { sWriter.Write(latestData); }
    }

    #endregion

    #region インナークラスの定義

    /// <summary>通信用XBee端末の情報</summary>
    private class xbeeInfo
    {
      public xbeeInfo(string portName)
      {
        this.portName = portName;
      }

      public List<string> longAddress = new List<string>();

      public string portName { get; private set; }
    }

    #endregion

  }
}
