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

    /// <summary>SDカード使用に関する初期設定ファイル</summary>
    private const string SD_F_NAME = "sd.ini";

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

    #region static変数

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

      //接続先:MLogger
      if (device.Name.StartsWith("MLogger_"))
      {
        ConnectedDevice = MLDevice.MLogger;
        Logger = new MLogger(ConnectedXBee.GetAddressString());
        Logger.LocalName = device.Name;
        ConnectedXBee.SerialDataReceived += ConnectedXBee_SerialDataReceived;
        return Logger.LowAddress;
      }
      //接続先:MLTransceiver
      else if (device.Name.StartsWith("MLTransceiver"))
      {
        ConnectedDevice = MLDevice.MLTransciever;
        Transceiver = new MLTransceiver(ConnectedXBee.GetAddressString());
        ConnectedXBee.SerialDataReceived += ConnectedXBee_SerialDataReceived;
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

  }
}
