using MLLib;
using System.Text;

using Plugin.BLE.Abstractions.Contracts;
using DigiIoT.Maui.Devices.XBee;

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

    #region 列挙型定義

    /// <summary>接続先のデバイス</summary>
    public enum MLDevice
    {
      /// <summary>接続なし</summary>
      None,
      /// <summary>MLogger</summary>
      MLogger,
      /// <summary>MLTransciever</summary>
      MLTransciever
    }

    #endregion

    #region static変数・プロパティ

    /// <summary>MLoggerのLowAddressと名称の対応リスト</summary>
    private static Dictionary<string, string> mlNames = new Dictionary<string, string>();

    /// <summary>接続先のデバイス</summary>
    public static MLDevice ConnectedDevice { get; private set; } = MLDevice.None;

    /// <summary>接続中のXBeeデバイスを設定・取得する</summary>
    public static XBeeBLEDevice ConnectedXBee { get; set; }

    /// <summary>接続されたトランシーバを取得する</summary>
    public static MLTransceiver Transceiver { get; private set; }

    /// <summary>接続されたMLoggerを取得する</summary>
    public static MLogger Logger { get; private set; }

    #endregion

    #region XBee接続切断処理

    public static string OpenXbee(IDevice device)
    {
      //通信中のXBeeがある場合は接続を閉じる
      CloseXbee();

      if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        ConnectedXBee = new XBeeBLEDevice(device.Id.ToString(), ML_PASS);
      else ConnectedXBee = new XBeeBLEDevice(device, ML_PASS);

      //XBeeをOpen
      ConnectedXBee.Connect();
      ConnectedXBee.SerialDataReceived += ConnectedXBee_SerialDataReceived;

      //接続先:MLogger
      if (device.Name.StartsWith("MLogger_"))
      {
        ConnectedDevice = MLDevice.MLogger;
        Logger = new MLogger(ConnectedXBee.GetAddressString());
        Logger.LocalName = Logger.XBeeName = device.Name;
        SaveMLName(Logger.LowAddress, Logger.XBeeName); //LowAddressと名称の対応を保存
        return Logger.LowAddress;
      }
      //接続先:MLTransceiver
      else if (device.Name.StartsWith("MLTransceiver"))
      {
        ConnectedDevice = MLDevice.MLTransciever;
        Transceiver = new MLTransceiver(ConnectedXBee.GetAddressString());
        return Transceiver.LowAddress;
      }
      else return "";
    }

    public static void CloseXbee()
    {
      //通信中のXBeeがある場合は接続を閉じる
      if (ConnectedXBee != null && ConnectedXBee.IsConnected)
      {
        Task.Run(() =>
        {
          try
          {
            ConnectedXBee.Disconnect();
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
          }
        });
      }
      ConnectedDevice = MLDevice.None;
      Transceiver = null;
      Logger = null;
    }

    private static void ConnectedXBee_SerialDataReceived
    (object sender, XBeeLibrary.Core.Events.Relay.SerialDataReceivedEventArgs e)
    {
      if (ConnectedDevice == MLDevice.MLogger)
      {
        Logger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

        //コマンド処理
        while (Logger.HasCommand)
        {
          try
          {
            Logger.SolveCommand();
          }
          catch { }
        }
      }
      else if (ConnectedDevice == MLDevice.MLTransciever)
      {
        Transceiver.AddReceivedData(Encoding.ASCII.GetString(e.Data));

        //コマンド処理
        while (Transceiver.HasCommand)
        {
          try
          {
            Transceiver.SolveCommand();
          }
          catch { }
        }
      }
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
      else if (ConnectedDevice == MLDevice.MLTransciever && Transceiver != null) 
        return Transceiver.GetLogger(lowAddress);
      else return null;
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

    public static void WriteLog(string logMessage)
    {
      // 既存のログが上限を超えているか確認
      ManageLogFileSize();

      // ログをファイルに追加
      string logPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + LOG_FILE_NAME;
      using (StreamWriter sw = new StreamWriter(logPath, true))
      {
        sw.WriteLine($"{DateTime.Now}: {logMessage}");
      }
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

    #region 係数推定用関数

    /// <summary>計測値3点から風量と電圧の関係式の係数を計算する</summary>
    /// <remarks>
    /// vel = A * vtg_n^3 + B * vtg_n^2 + C * vtg_n
    /// vtg_n = vtg / refVtg - 1.0
    /// </remarks>
    /// <param name="vel1">風速1[m/s]</param>
    /// <param name="vel2">風速2[m/s]</param>
    /// <param name="vel3">風速3[m/s]</param>
    /// <param name="refVtg">0m/sの基準電圧[V]</param>
    /// <param name="vtg1">風速1に対する電圧[V]</param>
    /// <param name="vtg2">風速2に対する電圧[V]</param>
    /// <param name="vtg3">風速3に対する電圧[V]</param>
    /// <param name="cfA">出力:係数A</param>
    /// <param name="cfB">出力:係数B</param>
    /// <param name="cfC">出力:係数C</param>
    /// <returns>係数推定が成功したか否か</returns>
    public static bool EstimateCoefs(
      double vel1, double vel2, double vel3,
      double refVtg, double vtg1, double vtg2, double vtg3,
      out double cfA, out double cfB, out double cfC)
    {
      cfA = cfB = cfC = 0;

      if (vel1 == vel2 || vel1 == vel3 || vel2 == vel3) return false;
      if (vtg1 == vtg2 || vtg1 == vtg3 || vtg2 == vtg3) return false;
      if (vtg1 < refVtg || vtg2 < refVtg || vtg3 < refVtg) return false;

      //解析的に逆行列を求めて3変数の3元連立方程式を解く
      double c = vtg1 / refVtg - 1;
      double f = vtg2 / refVtg - 1;
      double i = vtg3 / refVtg - 1;

      double b = c * c;
      double a = b * c;
      double e = f * f;
      double d = e * f;
      double h = i * i;
      double g = h * i;

      double detX = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

      cfA = (vel1 * (e * i - f * h) + vel2 * (c * h - b * i) + vel3 * (b * f - c * e)) / detX;
      cfB = (vel1 * (f * g - d * i) + vel2 * (a * i - c * g) + vel3 * (c * d - a * f)) / detX;
      cfC = (vel1 * (d * h - e * g) + vel2 * (b * g - a * h) + vel3 * (a * e - b * d)) / detX;

      return true;
    }

    #endregion

  }
}
