using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MLLib
{
  /// <summary>MLogger中継機を管理する</summary>
  public class MLTransceiver
  {

    #region 定数宣言

    /// <summary>XBEE端末の共通上部アドレス</summary>
    private const string HIGH_ADD = "0013A200";

    /// <summary>UNIX時間起点</summary>
    private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>日時の型（一体）</summary>
    private const string DT_FORMAT = "yyyy/MM/dd HH:mm:ss";

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Zigbeeで接続されているMLoggerのリスト（Lowaddress, MLogger）</summary>
    private Dictionary<string, MLogger> mLoggers = new Dictionary<string, MLogger>();

    /// <summary>受信データ</summary>
    private string receivedData = "";

    /// <summary>LongAddress（16進数）を設定・取得する</summary>
    public string LongAddress { get; set; }

    /// <summary>LongAddressの下位アドレス（16進数）を取得する</summary>
    public string LowAddress { get { return LongAddress.Substring(8); } }

    /// <summary>未処理のコマンドがあるか</summary>
    public bool HasCommand { get { return NextCommand != ""; } }

    /// <summary>次のコマンドを取得する</summary>
    public string NextCommand { get; private set; } = "";

    /// <summary>バージョンが読み込み済か否か</summary>
    public bool VersionLoaded { get; private set; } = false;

    /// <summary>バージョン（メジャー）を取得する</summary>
    public int Version_Major { get; private set; } = 0;

    /// <summary>バージョン（マイナー）を取得する</summary>
    public int Version_Minor { get; private set; } = 0;

    /// <summary>バージョン（リビジョン）を取得する</summary>
    public int Version_Revision { get; private set; } = 0;

    #endregion

    #region イベント定義

    /// <summary>新しいMLogger検出イベント</summary>
    public event EventHandler? NewMLoggerDetectedEvent;

    /// <summary>バージョン受信イベント</summary>
    public event EventHandler? VersionReceivedEvent;

    /// <summary>Bluetoothへの転送イベント</summary>
    public event EventHandler? RelayToBluetoothReceivedEvent;

    /// <summary>USBへの転送イベント</summary>
    public event EventHandler? StopRelayToBluetoothReceivedEvent;

    /// <summary>コマンドリレーイベント</summary>
    public event EventHandler? CommandRelayEvent;

    /// <summary>日時更新受信イベント</summary>
    public event EventHandler? UpdateCurrentTimeReceivedEvent;

    #endregion

    #region イベント用プロパティ

    /// <summary>バージョンを受信したか否かを設定・取得する</summary>
    public bool HasVersionReceived { get; set; } = false;

    /// <summary>Bluetoothへの転送を受信したか否かを設定・取得する</summary>
    public bool HasRelayToBluetoothReceived { get; set; } = false;

    /// <summary>USBへの転送を受信したか否かを設定・取得する</summary>
    public bool HasStopRelayToBluetoothReceived { get; set; } = false;

    /// <summary>現在日時変更イベントを受信したか否かを設定・取得する</summary>
    public bool HasUpdateCurrentTimeReceived { get; set; } = false;

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="longAddress">LongAddress（16進数）</param>
    public MLTransceiver(string longAddress)
    {
      LongAddress = longAddress;
    }

    #endregion

    #region コマンド受信関連

    /// <summary>受信データを追加する</summary>
    /// <param name="data"></param>
    public void AddReceivedData(string data)
    {
      //データを追加
      receivedData += data;

      //次のコマンドが無い場合には、移行を試みる
      if (!HasCommand) SkipCommand();
    }

    /// <summary>コマンドを全消去する</summary>
    public void ClearReceivedData()
    {
      receivedData = "";
    }

    /// <summary>コマンドをスキップする</summary>
    public void SkipCommand()
    {
      while (true)
      {
        if (!receivedData.Contains('\r'))
        {
          NextCommand = "";
          return;
        }
        else
        {
          NextCommand = receivedData.Substring(0, receivedData.IndexOf('\r'));
          receivedData = receivedData.Remove(0, receivedData.IndexOf('\r') + 1);
          if (NextCommand != "") return;
        }
      }
    }

    #endregion

    #region コマンド処理関連

    /// <summary>受信データを処理する</summary>
    /// <returns>コマンドが処理できたか否か</returns>
    public void SolveCommand()
    {
      //処理すべきコマンドがなければ終了
      if (!HasCommand) return;

      try
      {
        string cmd = NextCommand.Substring(0, 3);
        switch (cmd)
        {
          //VERsion
          case "VER":
            solveVER();
            break;

          //Relay To bluetooth
          case "RTB":
            HasRelayToBluetoothReceived = true;
            RelayToBluetoothReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //Stop Relay To Bluetooth
          case "SRB":
            HasStopRelayToBluetoothReceived = true;
            StopRelayToBluetoothReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //Command Relay
          case "CRY":
            solveCRY();
            break;

          //Update Current Time
          case "UCT":
            HasUpdateCurrentTimeReceived = true;
            UpdateCurrentTimeReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;
        }
      }
      catch { }
      finally
      {
        //エラーが起きてもとにかく次のコマンドへ移行
        SkipCommand();
      }
    }

    /// <summary>バージョンを処理する</summary>
    private void solveVER()
    {
      string[] vers = NextCommand.Remove(0, 4).Split('.');
      Version_Major = int.Parse(vers[0]);
      Version_Minor = int.Parse(vers[1]);
      if (3 <= vers.Length)
        Version_Revision = int.Parse(vers[2]);

      //バージョン読み込み済フラグをON
      VersionLoaded = true;

      //イベント通知
      VersionReceivedEvent?.Invoke(this, EventArgs.Empty);
      HasVersionReceived = true;
    }

    /// <summary>コマンドリレーを処理する</summary>
    private void solveCRY()
    {
      string lowAddress = NextCommand.Substring(3, 8);
      string command = NextCommand.Substring(11);

      //新規のMLoggerの場合には検出イベントを通知
      if (!mLoggers.ContainsKey(lowAddress))
      {
        MLogger ml = new MLogger(HIGH_ADD + lowAddress);
        mLoggers.Add(ml.LowAddress, ml);
        NewMLoggerDetectedEvent?.Invoke(this, new MLTransceiverEventArgs(ml));
      }

      //コマンド転送・処理
      mLoggers[lowAddress].AddReceivedData(command + '\r');
      while (mLoggers[lowAddress].HasCommand)
      {
        try
        {
          mLoggers[lowAddress].SolveCommand();
        }
        catch { }
      }

      CommandRelayEvent?.Invoke(this, new MLTransceiverEventArgs(mLoggers[lowAddress]));
    }

    #endregion

    #region 送信コマンド作成処理

    /// <summary>バージョン取得コマンドをつくる</summary>
    /// <returns>バージョン取得コマンド</returns>
    public static string MakeGetVersionCommand()
    {
      return "\rVER\r";
    }

    /// <summary>Bluetooth転送コマンドをつくる</summary>
    /// <returns>Bluetooth転送コマンド</returns>
    public static string MakeRelayToBluetoothCommand()
    {
      return "\rRTB\r";
    }

    /// <summary>Bluetooth転送停止コマンドをつくる</summary>
    /// <returns>Bluetooth転送停止コマンド</returns>
    public static string MakeStopRelayToBluetoothCommand()
    {
      return "\rSRB\r";
    }

    /// <summary>リレーコマンドをつくる</summary>
    /// <param name="lowAddress">リレー先のLow Address</param>
    /// <param name="relayedCommand">リレーするコマンド</param>
    /// <returns>リレーコマンド</returns>
    public static string MakeRelayCommand
      (string lowAddress, string relayedCommand)
    {
      return "\rCRY" + lowAddress + relayedCommand.TrimStart().TrimEnd() + "\r";
    }

    /// <summary>現在日時更新コマンドをつくる</summary>
    /// <param name="cTime">現在日時</param>
    /// <returns>現在日時更新コマンド</returns>
    public static string MakeUpdateCurrentTimeCommand(DateTime cTime)
    {
      return "\rUCT" +
        String.Format("{0:D10}", GetUnixTime(cTime)) + "\r";
    }

    /// <summary>日時からUNIX時間を求める</summary>
    /// <param name="dTime">日時</param>
    /// <returns>UNIX時間</returns>
    /// <remarks>計測器内部ではUTC=0で時刻を管理する</remarks>
    public static long GetUnixTime(DateTime dTime)
    {
      DateTime dtNow = new DateTime(dTime.Year, dTime.Month, dTime.Day, dTime.Hour, dTime.Minute, dTime.Second, DateTimeKind.Utc);
      return (long)(dtNow - UNIX_EPOCH).TotalSeconds;
    }

    #endregion

    /// <summary>管理しているMLoggerのリストを取得する</summary>
    /// <returns>管理しているMLoggerのリスト</returns>
    public ImmutableMLogger[] GetMLoggers()
    {
      return mLoggers.Values.ToArray();
    }

    /// <summary>MLoggerを取得する</summary>
    /// <param name="lowAddress">下位アドレス</param>
    /// <returns>MLogger</returns>
    public MLogger? GetLogger(string lowAddress)
    {
      if (mLoggers.ContainsKey(lowAddress)) return mLoggers[lowAddress];
      else return null;
    }
  }


  public class MLTransceiverEventArgs : EventArgs
  {
    public MLTransceiverEventArgs(ImmutableMLogger mLogger)
    {
      Logger = mLogger;
    }

    public ImmutableMLogger Logger { get; set; }
  }

}
