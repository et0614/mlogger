using System;

using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using XBeeLibrary.Core;

using MLLib;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using XBeeLibrary.Core.Models;
using System.Collections.Concurrent;

namespace MLServer
{
  class Program
  {

    #region 定数宣言

    private const string VERSION = "1.1.4";

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

    #endregion

    #region クラス変数

    /// <summary>温冷感計算のための基準の物理量</summary>
    private static double metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue;

    /// <summary>新しいデータ収集があったか否か</summary>
    private static bool hasNewData = true;

    /// <summary>データ格納用のディレクトリ</summary>
    private static string dataDirectory;

    /// <summary>発見されたMLogger端末のリスト</summary>
    private static ConcurrentDictionary<string, MLogger> mLoggers = new ConcurrentDictionary<string, MLogger>();

    /// <summary>接続できなかったポートリスト</summary>
    private static List<string> excludedPorts = new List<string>();

    /// <summary>通信用コーディネータリスト</summary>
    private static ConcurrentDictionary<ZigBeeDevice, xbeeInfo> coordinators = new ConcurrentDictionary<ZigBeeDevice, xbeeInfo>();

    /// <summary>MLoggerのアドレス-名称対応リスト</summary>
    private static readonly Dictionary<string, string> mlNames = new Dictionary<string, string>();

    #endregion

    #region メイン処理

    static void Main(string[] args)
    {
      showTitle();

      //データ格納用のディレクトリを作成
      dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

      //温冷感計算のための代謝量[met]と着衣量[clo]を読み込む
      loadInitFile(out metValue, out cloValue, out dbtValue, out rhdValue, out velValue, out mrtValue);

      //MLoggerのアドレス-名称対応リストを読む
      string nFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "mlnames.txt";
      if (File.Exists(nFile))
      {
        using (StreamReader sReader = new StreamReader(nFile))
        {
          string line;
          while ((line = sReader.ReadLine()) != null && line.Contains(':'))
          {
            string[] bf = line.Split(':');
            if(!mlNames.ContainsKey(HIGH_ADD + bf[0]))
              mlNames.Add(HIGH_ADD + bf[0], bf[1]);
          }
        }
      }

      //定期的にJSONファイルを更新する
      Task jsonRefreshTask = Task.Run(() =>
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

      //現在日時更新タスク
      updateCurrentDateTime();

      //定期的にコーディネータをスキャン
      scanCoordinators();

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
              coordinators[device].resistEvent = true;
              Console.WriteLine(coordinators[device].portName + ": Connection succeeded." + " S/N = " + device.XBee64BitAddr.ToString());
              device.DataReceived += Device_DataReceived; //データ受信イベント登録
            }
            else if (xInfo.connectTask.Status == TaskStatus.Faulted)
            {
              coordinators[device].resistEvent = true;
              Console.WriteLine("Failed to connect port " + xInfo.portName);
            }
          }
        }
      }
    }

    private static void showTitle()
    {
      Console.WriteLine("\r\n");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#                       MLServer  verstion " + VERSION + "                        #");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#                Software for logging data sent from M-Logger           #");
      Console.WriteLine("#                         (https://www.mlogger.jp)                      #");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("\r\n");
    }

    private static void loadInitFile
      (out double met, out double clo, out double dbt, out double rhd, out double vel, out double mrt)
    {
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
            ZigBeeDevice device = new ZigBeeDevice(new XBeeLibrary.Windows.Connection.Serial.WinSerialPort(port, BAUD_RATE));
            xbeeInfo xInfo = new xbeeInfo(port);
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

    #endregion

    #region コーディネータ接続関連の処理

    private static void addXBeeDevice(RemoteXBeeDevice rdv)
    {
      string add = rdv.GetAddressString();

      //親機が見つかった場合には終了
      foreach (ZigBeeDevice cd in coordinators.Keys)
        if (cd.GetAddressString() == add) return;

      if (mLoggers.ContainsKey(add)) return;

      ZigBeeDevice dv = rdv.GetLocalXBeeDevice() as ZigBeeDevice;
      MLogger ml = new MLogger(add);

      //名前を設定
      if (mlNames.ContainsKey(add)) ml.LocalName = mlNames[add];

      //熱的快適性計算のための情報を設定
      ml.CloValue = cloValue;
      ml.MetValue = metValue;
      ml.DefaultTemperature = dbtValue;
      ml.DefaultRelativeHumidity = rhdValue;
      ml.DefaultGlobeTemperature = mrtValue;
      ml.DefaultVelocity = velValue;

      //イベント登録
      ml.MeasuredValueReceivedEvent += Ml_MeasuredValueReceivedEvent;

      mLoggers.TryAdd(add, ml);

      //子機のアドレスと通信用XBeeを対応付ける
      if (!coordinators[dv].longAddress.Contains(add))
        coordinators[dv].longAddress.Add(add);
    }

    private static void Device_DataReceived(object sender, XBeeLibrary.Core.Events.DataReceivedEventArgs e)
    {
      RemoteXBeeDevice rdv = e.DataReceived.Device;
      string add = rdv.GetAddressString();
      string rcvStr = e.DataReceived.DataString;

      //未登録のノードからの受信の場合
      if (!mLoggers.ContainsKey(add))
        addXBeeDevice(rdv);

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
          Console.WriteLine(mlg.LocalName + ": " + mlg.NextCommand);
          mlg.SolveCommand();
        }
        catch (Exception exc)
        {
          Console.WriteLine(mlg.LocalName + " : " + exc.Message);
          mlg.ClearReceivedData(); //異常終了時はコマンドを全消去する
        }
      }
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
            DateTime.Now.ToString(DT_FORMAT) + "," + //親機の現在日時
            ((ml.LastMeasured.Year == 2000 || 2100 < ml.LastMeasured.Year) ? "n/a" : ml.LastMeasured.ToString(DT_FORMAT)) + "," + //子機の計測日時
            ml.DrybulbTemperature.LastValue.ToString("F2") + "," +
            ml.RelativeHumdity.LastValue.ToString("F2") + "," +
            ml.GlobeTemperature.LastValue.ToString("F2") + "," +
            ml.Velocity.LastValue.ToString("F4") + "," +
            ml.Illuminance.LastValue.ToString("F2") + "," +
            ml.GlobeTemperatureVoltage.ToString("F3") + "," +
            ml.VelocityVoltage.ToString("F3") + "," +
            ml.GeneralVoltage1.LastValue.ToString("F3"));
          //ml.GeneralVoltage1.LastValue.ToString("F3") + "," +
          //ml.GeneralVoltage2.LastValue.ToString("F3") + "," +
          //ml.GeneralVoltage3.LastValue.ToString("F3"));
        }
      }
      catch
      {
        Console.WriteLine(ml.LocalName + ": Can't access to file.");
        return;
      }
    }

    #endregion

    #region 現在日時更新関連の処理

    /// <summary>現在時刻を更新するタスク</summary>
    private static void updateCurrentDateTime()
    {
      Task dtUpdateTask = Task.Run(() =>
      {
        DateTime prevTime = DateTime.Now;
        while (true)
        {
          //日付変更時に現在時刻を再設定
          if (prevTime.Day != DateTime.Now.Day)
          {
            foreach (string key in mLoggers.Keys)
            {
              MLogger ml = mLoggers[key];
              ml.HasUpdateCurrentTimeReceived = false;
              int num = 0;
              //情報が更新されるまで命令を繰り返す
              while (!ml.HasUpdateCurrentTimeReceived)
              {
                try
                {
                  sendCommand(ml.LongAddress, MLogger.MakeUpdateCurrentTimeCommand(DateTime.Now));
                }
                catch { }
                Task.Delay(500);

                //3回失敗したら諦める
                num++;
                if (3 < num) break;
              }
            }
          }
          prevTime = DateTime.Now;
          Thread.Sleep(1000); //1秒お休みあれ
        }
      });
    }

    private static void sendCommand(string longAddress, string command)
    {
      ZigBeeDevice xbee = getXBee(longAddress);
      if (xbee == null) return;
      RemoteXBeeDevice rmdv = xbee.GetNetwork().GetDevice(new XBee64BitAddress(longAddress));
      xbee.SendData(rmdv, Encoding.ASCII.GetBytes(command));
    }

    /// <summary>子機のLongAddressを管理する通信用XBee端末を取得する</summary>
    /// <param name="address">子機のLongAddress</param>
    /// <returns>通信用XBee端末</returns>
    private static ZigBeeDevice getXBee(string address)
    {
      foreach (ZigBeeDevice key in coordinators.Keys)
        if (coordinators[key].longAddress.Contains(address)) return key;
      return null;
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
        var json = JsonSerializer.Serialize(mLoggers, options);
        using (StreamWriter sWriter = new StreamWriter
          (dataDirectory + Path.DirectorySeparatorChar + "latest.json", false, Encoding.UTF8))
        { sWriter.Write(json); }
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
      public xbeeInfo(string portName)
      {
        this.portName = portName;
      }

      public List<string> longAddress = new List<string>();

      public string portName { get; private set; }

      public bool resistEvent { get; set; } = false;

      public Task connectTask { get; set; }
    }

    #endregion

  }
}
