using Plugin.BLE.Abstractions.Contracts;
using System.Text;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

using MLLib;

namespace MLS_Mobile
{
  /// <summary>ユーティリティクラス</summary>
  public static class MLUtility
  {

    #region 定数宣言

    /// <summary>データフォルダの名称</summary>
    private const string DATA_DIR_NAME = "DATA";

    /// <summary>SDカード使用に関する初期設定ファイル</summary>
    private const string SD_F_NAME = "sd.ini";

    /// <summary>MLogger付属のXBeeのパスワード</summary>
    private const string ML_PASS = "ml_pass";

    #endregion

    #region staticプロパティ

    /// <summary>SDカードが有効か否か</summary>
    public static bool SDCardEnabled
    {
      get
      {
        string sdFPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + SD_F_NAME;
        using (StreamReader sReader = new StreamReader(sdFPath, Encoding.UTF8))
        {
          return sReader.ReadToEnd() == "1";
        }
      }
      set
      {
        string sdFPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + SD_F_NAME;
        using (StreamWriter sWriter = new StreamWriter(sdFPath, false))
        {
          sWriter.Write(value ? "1" : "0");
        }
      }
    }

    /// <summary>ロガー付属のXBeeを取得する</summary>
    public static ZigBeeBLEDevice LoggerSideXBee { get; private set; }

    /// <summary>ロガーを取得する</summary>
    public static MLogger Logger { get; private set; }

    #endregion

    #region XBee通信関連の処理

    /// <summary>MLoggerのXBeeと接続する</summary>
    /// <param name="device"></param>
    public static void StartXBeeCommunication(IDevice device)
    {
      //通信中のXBeeがある場合は接続を閉じる
      EndXBeeCommunication();

      if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        LoggerSideXBee = new ZigBeeBLEDevice(device.Id.ToString(), ML_PASS);
      else LoggerSideXBee = new ZigBeeBLEDevice(device, ML_PASS);

      //XBeeをOpen
      LoggerSideXBee.Open();

      //イベント処理用ロガーを用意
      Logger = new MLogger(LoggerSideXBee.GetAddressString());
      Logger.LocalName = device.Name;

      //イベント登録      
      LoggerSideXBee.SerialDataReceived += LoggerSideXBee_SerialDataReceived;
    }

    /// <summary>MLoggerのXbeeとの接続を解除する</summary>
    public static void EndXBeeCommunication()
    {
      //通信中のXBeeがある場合は接続を閉じる
      if (LoggerSideXBee != null)
      {
        //イベントを解除する
        LoggerSideXBee.SerialDataReceived -= LoggerSideXBee_SerialDataReceived;

        //開いていれば別スレッドで閉じる
        if (LoggerSideXBee.IsOpen)
        {
          ZigBeeBLEDevice clsBee = LoggerSideXBee;
          Task.Run(() =>
          {
            try
            {
              clsBee.Close();
            }
            catch { }
          });
        }
      }

      Logger = null;
    }

    /// <summary>シリアルデータ受信時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void LoggerSideXBee_SerialDataReceived
      (object sender, SerialDataReceivedEventArgs e)
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

    #endregion

    #region データ入出力処理

    /// <summary>データ保存ディレクトリを用意する</summary>
    public static void InitDirAndFiles()
    {
      string dFolder = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME;
      if (!Directory.Exists(dFolder))
        Directory.CreateDirectory(dFolder);

      string sdFPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + SD_F_NAME;
      if (!File.Exists(sdFPath))
      {
        using (StreamWriter sWriter = new StreamWriter(sdFPath, false))
        {
          sWriter.Write("0");
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
    /// <returns>データファイルの内容</returns>
    public static string LoadDataFile(string fileName)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;

      //先頭の1000行を読み込む
      int lines = 0;
      StringBuilder sBuilder = new StringBuilder();
      using (StreamReader sReader = new StreamReader(filePath, Encoding.UTF8))
      {
        string buff;
        while ((buff = sReader.ReadLine()) != null && lines < 1000)
        {
          sBuilder.AppendLine(buff);
          lines++;
        }
        return sBuilder.ToString();
      }
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

  }
}
