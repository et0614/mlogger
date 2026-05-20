using System.Text.Json.Serialization;

namespace MLLib
{

  /// <summary>MLoggerを管理する</summary>
  public class MLogger: ImmutableMLogger
  {

    #region 定数宣言

    /// <summary>UNIX時間起点</summary>
    private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>大気圧[kPa]</summary>
    private const double ATM = 101.325;

    /// <summary>グローブ温度計の直径[m]</summary>
    private const double GLOBE_DIAMETER = 0.038;

    #endregion

    #region 列挙型定義

    /// <summary>状態</summary>
    public enum Status
    {
      /// <summary>初期化中</summary>
      Initializing,
      /// <summary>コマンド受信待ち</summary>
      WaitingForCommand,
      /// <summary>計測開始処理中</summary>
      StartMeasuring,
      /// <summary>計測中</summary>
      Measuring
    }

    #endregion

    #region イベント定義

    /// <summary>データ受信イベント</summary>
    public event EventHandler? DataReceivedEvent;

    /// <summary>コマンド待ち通知受信イベント</summary>
    public event EventHandler? WaitingForCommandMessageReceivedEvent;

    /// <summary>測定値受信イベント</summary>
    public event EventHandler? MeasuredValueReceivedEvent;

    /// <summary>測定設定受信イベント</summary>
    public event EventHandler? MeasurementSettingReceivedEvent;

    /// <summary>バージョン受信イベント</summary>
    public event EventHandler? VersionReceivedEvent;

    /// <summary>補正係数受信イベント</summary>
    public event EventHandler? CorrectionFactorsReceivedEvent;

    /// <summary>風速特性係数受信イベント</summary>
    public event EventHandler? VelocityCharateristicsReceivedEvent;

    /// <summary>測定開始通知受信イベント</summary>
    public event EventHandler? StartMeasuringMessageReceivedEvent;

    /// <summary>測定終了通知受信イベント</summary>
    public event EventHandler? EndMeasuringMessageReceivedEvent;

    /// <summary>ロガー名称受信イベント</summary>
    public event EventHandler? LoggerNameReceivedEvent;

    /// <summary>風速電圧校正受信イベント</summary>
    public event EventHandler? CalibratingVoltageReceivedEvent;

    /// <summary>風速電圧校正終了イベント</summary>
    public event EventHandler? EndCalibratingVoltageMessageReceivedEvent;

    /// <summary>風速自動校正受信イベント</summary>
    public event EventHandler? VelocityAutoCalibrationReceivedEvent;

    /// <summary>温度自動校正受信イベント</summary>
    public event EventHandler? TemperatureAutoCalibrationReceivedEvent;

    /// <summary>CO2濃度センサの有無受信イベント</summary>
    public event EventHandler? HasCO2LevelSensorReceivedEvent;

    /// <summary>CO2センサの自動校正受信イベント</summary>
    public event EventHandler? CalibratingCO2LevelReceivedEvent;

    /// <summary>CO2センサの初期化処理受信イベント</summary>
    public event EventHandler? InitializingCO2LevelReceivedEvent;

    /// <summary>日時更新受信イベント</summary>
    public event EventHandler? UpdateCurrentTimeReceivedEvent;

    #endregion

    #region インスタンス変数・プロパティ

    // v3 raw byte parsing は D7 で IMLProtocol (LegacyV3Protocol / JsonRpcV4Protocol)
    // に完全移行したため削除。MLogger は MLS_Mobile での Name / LowAddress 等の
    // データコンテナとして残置 (ImmutableMLogger interface を満たすためのプロパティ
    // と events を declared する)。

    [JsonIgnore]
    /// <summary>未処理のコマンドがあるか (legacy interface 互換、常に false)</summary>
    public bool HasCommand => false;

    [JsonIgnore]
    /// <summary>次のコマンドを取得する (legacy interface 互換、常に空)</summary>
    public string NextCommand => "";

    [JsonPropertyName("name")]
    /// <summary>CLNコマンドで書き換えられる名称を取得する</summary>
    public string Name { get; private set; } = "Unloaded";

    [JsonPropertyName("localName")]
    /// <summary>外部テキストデータで書き換えられる名称を設定・取得する</summary>
    public string LocalName { get; set; }

    [JsonPropertyName("xbeeName")]
    /// <summary>IDとして埋め込まれた名称を設定・取得する</summary>
    public string XBeeName { get; set; }

    [JsonIgnore]
    /// <summary>初回の保存か否か</summary>
    public bool IsFirstSave { get; set; } = true;

    [JsonIgnore]
    /// <summary>LongAddress（16進数）を設定・取得する</summary>
    public string LongAddress { get; set; }

    [JsonPropertyName("lowAddress")]
    /// <summary>LongAddressの下位アドレス（16進数）を取得する</summary>
    public string LowAddress { get { return LongAddress.Substring(8); } }

    [JsonPropertyName("lastCommunicated")]
    /// <summary>最後の通信日時を取得する</summary>
    public DateTime LastCommunicated { get; private set; }

    [JsonIgnore]
    /// <summary>最後の計測日時を取得する</summary>
    public DateTime LastMeasured { get; private set; }

    [JsonIgnore]
    /// <summary>バージョン（メジャー）を取得する</summary>
    public int Version_Major { get; private set; } = 0;

    [JsonIgnore]
    /// <summary>バージョン（マイナー）を取得する</summary>
    public int Version_Minor { get; private set; } = 0;

    [JsonIgnore]
    /// <summary>バージョン（リビジョン）を取得する</summary>
    public int Version_Revision { get; private set; } = 0;

    /// <summary>現在の状態を取得する</summary>
    public Status CurrentStatus { get; private set; } = Status.Initializing;

    [JsonIgnore]
    /// <summary>計測開始日時を取得する</summary>
    public DateTime StartMeasuringDateTime { get; private set; } = new DateTime(2000, 1, 1, 0, 0, 0);

    [JsonIgnore]
    /// <summary>計測設定値が読み込み済か否か</summary>
    public bool MeasuringSettingLoaded { get; private set; } = false;

    [JsonIgnore]
    /// <summary>バージョンが読み込み済か否か</summary>
    public bool VersionLoaded { get; private set; } = false;

    [JsonIgnore]
    /// <summary>風速校正残り時間[sec]を取得する</summary>
    public int VelocityCalibrationTime { get; private set; } = 0;

    [JsonIgnore]
    /// <summary>温度校正残り時間[sec]を取得する</summary>
    public int TemperatureCalibrationTime { get; private set; } = 0;

    [JsonIgnore]
    /// <summary>無風時の電圧[V]を取得する</summary>
    public double VelocityMinVoltage { get; private set; } = 1.45;

    [JsonIgnore]
    /// <summary>風速計の特性係数Aを取得する</summary>
    public double VelocityCharacteristicsCoefA { get; private set; }

    [JsonIgnore]
    /// <summary>風速計の特性係数Bを取得する</summary>
    public double VelocityCharacteristicsCoefB { get; private set; }

    [JsonIgnore]
    /// <summary>風速計の特性係数Cを取得する</summary>
    public double VelocityCharacteristicsCoefC { get; private set; }

    [JsonIgnore]
    /// <summary>CO2濃度センサの有無を取得する</summary>
    public bool HasCO2LevelSensor { get; private set; }

    [JsonIgnore]
    /// <summary>CO2センサの自動校正残時間[sec]を取得する</summary>
    public int CO2CalibrationTime { get; private set; } = 0;

    #endregion

    #region 計測値関連のプロパティ

    [JsonPropertyName("drybulbTemperature")]
    /// <summary>乾球温度計測情報を取得する</summary>
    public MeasurementInfo DrybulbTemperature { get; } = new MeasurementInfo();

    [JsonPropertyName("relativeHumdity")]
    /// <summary>相対湿度計測情報を取得する</summary>
    public MeasurementInfo RelativeHumdity { get; } = new MeasurementInfo();

    [JsonPropertyName("globeTemperature")]
    /// <summary>グローブ温度計測情報を取得する</summary>
    public MeasurementInfo GlobeTemperature { get; } = new MeasurementInfo();

    [JsonIgnore]
    /// <summary>グローブ温度の電圧[V]を取得する</summary>
    public double GlobeTemperatureVoltage { get; private set; }

    [JsonPropertyName("velocity")]
    /// <summary>風速計測情報を取得する</summary>
    public MeasurementInfo Velocity { get; } = new MeasurementInfo();

    [JsonIgnore]
    /// <summary>風速の電圧[V]を取得する</summary>
    public double VelocityVoltage { get; private set; }

    [JsonPropertyName("illuminance")]
    /// <summary>照度計測情報を取得する</summary>
    public MeasurementInfo Illuminance { get; } = new MeasurementInfo();

    [JsonIgnore]
    /// <summary>汎用電圧1計測情報を取得する</summary>
    public MeasurementInfo GeneralVoltage1 { get; } = new MeasurementInfo();

    [JsonIgnore]
    /// <summary>汎用電圧2計測情報を取得する</summary>
    public MeasurementInfo GeneralVoltage2 { get; } = new MeasurementInfo();

    [JsonIgnore]
    /// <summary>汎用電圧3計測情報を取得する</summary>
    public MeasurementInfo GeneralVoltage3 { get; } = new MeasurementInfo();

    [JsonPropertyName("co2Level")]
    /// <summary>CO2濃度計測情報を取得する</summary>
    public MeasurementInfo CO2Level { get; } = new MeasurementInfo();

    [JsonIgnore]
    /// <summary>近接センサ計測の真偽を取得する</summary>
    public bool MeasureProximity { get; private set; } = false;

    #endregion

    #region 熱的快適性関連のプロパティ

    [JsonIgnore]
    /// <summary>熱的快適性指標を計算するか否か</summary>
    public bool CalcThermalIndices { get; set; } = true;

    [JsonPropertyName("metValue")]
    /// <summary>代謝量[met]を設定・取得する</summary>
    public double MetValue { get; set; } = 1.1;

    [JsonPropertyName("cloValue")]
    /// <summary>クロ値[clo]を設定・取得する</summary>
    public double CloValue { get; set; } = 1.0;

    [JsonIgnore]
    /// <summary>計測値がない場合の乾球温度[C]を設定・取得する</summary>
    public double DefaultTemperature { get; set; } = 25;

    [JsonIgnore]
    /// <summary>計測値がない場合の相対湿度[%]を設定・取得する</summary>
    public double DefaultRelativeHumidity { get; set; } = 50;

    [JsonIgnore]
    /// <summary>計測値がない場合の風速[m/s]を設定・取得する</summary>
    public double DefaultVelocity { get; set; } = 0.1;

    [JsonIgnore]
    /// <summary>計測値がない場合のグローブ温度[C]を設定・取得する</summary>
    public double DefaultGlobeTemperature { get; set; } = 25;

    [JsonPropertyName("meanRadiantTemperature")]
    /// <summary>平均放射温度[C]を取得する</summary>
    public double MeanRadiantTemperature { get; private set; }

    [JsonPropertyName("pmv")]
    /// <summary>PMV[-]を取得する</summary>
    public double PMV { get; private set; }

    [JsonPropertyName("ppd")]
    /// <summary>PPD[%]を取得する</summary>
    public double PPD { get; private set; }

    [JsonPropertyName("setStar")]
    /// <summary>SET*[C]を取得する</summary>
    public double SETStar { get; private set; }

    [JsonPropertyName("wbgt_outdoor")]
    /// <summary>屋外のWBGT[C]を取得する</summary>
    public double WBGT_Outdoor { get; private set; }

    [JsonPropertyName("wbgt_indoor")]
    /// <summary>屋内のWBGT[C]を取得する</summary>
    public double WBGT_Indoor { get; private set; }

    #endregion

    #region コンストラクタ

    private MLogger()
      : this("0000000000000000") { }

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="longAddress">LongAddress（16進数）</param>
    public MLogger(string longAddress)
    {
      LongAddress = longAddress;
      LocalName = LowAddress;
      XBeeName = LowAddress;
    }

    #endregion

    // インスタンスメソッド (v3 parsing / 熱的快適性計算) は D7 で削除。
    // 熱的快適性は LoggerCache.RecomputeThermalIndices に移管。
    // v3 parsing は LegacyV3Protocol.OnLine / ParseDtt 等に移管。




    #region staticメソッド

    /// <summary>日時からUNIX時間を求める</summary>
    /// <param name="dTime">日時</param>
    /// <returns>UNIX時間</returns>
    /// <remarks>計測器内部ではUTC=0で時刻を管理する</remarks>
    public static long GetUnixTime(DateTime dTime)
    {
      DateTime dtNow = new DateTime(dTime.Year, dTime.Month, dTime.Day, dTime.Hour, dTime.Minute, dTime.Second, DateTimeKind.Utc);
      long a1 = (long)(dtNow - UNIX_EPOCH).TotalSeconds;
      long a2 = new DateTimeOffset(dTime).ToUnixTimeSeconds();

      return (long)(dtNow - UNIX_EPOCH).TotalSeconds;
    }

    /// <summary>UNIX時間から日時を求める</summary>
    /// <param name="unixTime">UNIX時間</param>
    /// <returns>日時</returns>
    /// <remarks>計測器内部ではUTC=0で時刻を管理する</remarks>
    public static DateTime GetDateTimeFromUTime(long unixTime)
    {
      DateTime dtNow = UNIX_EPOCH.AddSeconds(unixTime);
      DateTime d1 = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, dtNow.Second, DateTimeKind.Local);
      DateTime d2 = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;

      return new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, dtNow.Second, DateTimeKind.Local);
    }

    /// <summary>LongAddressを16進数表記で取得する</summary>
    /// <param name="longAddress">LongAddress（10進数表記）</param>
    /// <returns>LongAddress（16進数表記）</returns>
    public static string GetLongHexAddress(ulong longAddress)
    { return Convert.ToString((int)longAddress, 16).ToUpper(); }

    /// <summary>下位アドレスを取得する</summary>
    /// <param name="adds">アドレス</param>
    /// <returns>下位アドレス</returns>
    public static string GetLowAddress(ulong longAddress)
    { return GetLongHexAddress(longAddress); }

    #endregion

    #region インナークラス定義

    /// <summary>計測情報</summary>
    public class MeasurementInfo
    {
      [JsonPropertyName("measure")]
      /// <summary>計測するか否かを取得する</summary>
      public bool Measure { get; internal set; } = true;

      [JsonIgnore]
      /// <summary>計測時間間隔[sec]を取得する</summary>
      public int Interval { get; internal set; } = 60;

      [JsonPropertyName("lastMeasureTime")]
      /// <summary>最終の計測日時を取得する</summary>
      public DateTime LastMeasureTime { get; internal set; } = UNIX_EPOCH;

      [JsonPropertyName("lastValue")]
      /// <summary>最終の計測値を取得する</summary>
      public double LastValue { get; internal set; }

      [JsonIgnore]
      /// <summary>補正式Ax+Bの補正係数Aを取得する</summary>
      public double CorrectionFactorA { get; internal set; } = 1.0;

      [JsonIgnore]
      /// <summary>補正式Ax+Bの補正係数Bを取得する</summary>
      public double CorrectionFactorB { get; internal set; } = 0.0;
    }

    public class CalibratingCO2SensorLevelEventArgs : EventArgs
    {
      /// <summary>残り校正時間[sec]を取得する</summary>
      public int RemainingTime { get; }

      /// <summary>校正が成功したか否か</summary>
      public bool CalibrationSucceeded { get; }

      /// <summary>校正幅[ppm]を取得する</summary>
      public int CorrectionCO2Level { get; }

      /// <summary>現在のCO2濃度[ppm]を取得する</summary>
      public int CurrentCO2Level { get; }

      public CalibratingCO2SensorLevelEventArgs(int remainingTime, bool calibrationSucceeded, int correctionCO2Level, int currentCO2Level)
      {
        RemainingTime = remainingTime;
        CalibrationSucceeded = calibrationSucceeded;
        CorrectionCO2Level = correctionCO2Level;
        CurrentCO2Level = currentCO2Level;
      }
    }

    #endregion

  }

  /// <summary>読み取り専用のMLogger管理インターフェース</summary>
  public interface ImmutableMLogger
  {

    #region イベント

    /// <summary>データ受信イベント</summary>
    event EventHandler? DataReceivedEvent;

    /// <summary>コマンド待ち通知受信イベント</summary>
    event EventHandler? WaitingForCommandMessageReceivedEvent;

    /// <summary>測定値受信イベント</summary>
    event EventHandler? MeasuredValueReceivedEvent;

    /// <summary>測定設定受信イベント</summary>
    event EventHandler? MeasurementSettingReceivedEvent;

    /// <summary>バージョン受信イベント</summary>
    event EventHandler? VersionReceivedEvent;

    /// <summary>補正係数受信イベント</summary>
    event EventHandler? CorrectionFactorsReceivedEvent;

    /// <summary>測定開始通知受信イベント</summary>
    event EventHandler? StartMeasuringMessageReceivedEvent;

    /// <summary>測定終了通知受信イベント</summary>
    event EventHandler? EndMeasuringMessageReceivedEvent;

    /// <summary>ロガー名称受信イベント</summary>
    event EventHandler? LoggerNameReceivedEvent;

    /// <summary>風速電圧校正受信イベント</summary>
    event EventHandler? CalibratingVoltageReceivedEvent;

    /// <summary>風速電圧校正終了イベント</summary>
    event EventHandler? EndCalibratingVoltageMessageReceivedEvent;

    /// <summary>風速自動校正受信イベント</summary>
    event EventHandler? VelocityAutoCalibrationReceivedEvent;

    /// <summary>温度自動校正受信イベント</summary>
    event EventHandler? TemperatureAutoCalibrationReceivedEvent;

    /// <summary>CO2濃度センサの有無受信イベント</summary>
    event EventHandler? HasCO2LevelSensorReceivedEvent;

    /// <summary>CO2センサの自動校正受信イベント</summary>
    event EventHandler? CalibratingCO2LevelReceivedEvent;

    /// <summary>日時更新受信イベント</summary>
    event EventHandler? UpdateCurrentTimeReceivedEvent;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>未処理のコマンドがあるか</summary>
    public bool HasCommand { get; }

    /// <summary>次のコマンドを取得する</summary>
    string NextCommand { get; }

    /// <summary>CLNコマンドで書き換えられる名称を取得する</summary>
    string Name { get; }

    /// <summary>外部テキストデータで書き換えられる名称を設定・取得する</summary>
    string LocalName { get; }

    /// <summary>IDとして埋め込まれた名称を設定・取得する</summary>
    string XBeeName { get; }

    /// <summary>初回の保存か否か</summary>
    bool IsFirstSave { get; }

    /// <summary>LongAddress（16進数）を取得する</summary>
    string LongAddress { get; }

    /// <summary>LongAddressの下位アドレス（16進数）を取得する</summary>
    string LowAddress { get; }

    /// <summary>最後の通信日時を取得する</summary>
    DateTime LastCommunicated { get; }

    /// <summary>最後の計測日時を取得する</summary>
    DateTime LastMeasured { get; }

    /// <summary>バージョン（メジャー）を取得する</summary>
    int Version_Major { get; }

    /// <summary>バージョン（マイナー）を取得する</summary>
    int Version_Minor { get; }

    /// <summary>バージョン（リビジョン）を取得する</summary>
    int Version_Revision { get; }

    /// <summary>現在の状態を取得する</summary>
    MLogger.Status CurrentStatus { get; }

    /// <summary>計測開始日時を取得する</summary>
    DateTime StartMeasuringDateTime { get; }

    /// <summary>計測設定値が読み込み済か否か</summary>
    bool MeasuringSettingLoaded { get; }

    /// <summary>バージョンが読み込み済か否か</summary>
    bool VersionLoaded { get; }

    /// <summary>風速校正残り時間[sec]を取得する</summary>
    int VelocityCalibrationTime { get; }

    /// <summary>温度校正残り時間[sec]を取得する</summary>
    int TemperatureCalibrationTime { get; }

    /// <summary>無風時の電圧[V]を取得する</summary>
    double VelocityMinVoltage { get; }

    /// <summary>風速計の特性係数Aを取得する</summary>
    double VelocityCharacteristicsCoefA { get; }

    /// <summary>風速計の特性係数Bを取得する</summary>
    double VelocityCharacteristicsCoefB { get; }

    /// <summary>風速計の特性係数Cを取得する</summary>
    double VelocityCharacteristicsCoefC { get; }

    /// <summary>CO2濃度センサの有無を取得する</summary>
    bool HasCO2LevelSensor { get; }

    /// <summary>CO2センサの自動校正残時間[sec]を取得する</summary>
    int CO2CalibrationTime { get; }

    #endregion

    #region 計測値関連のプロパティ

    /// <summary>乾球温度計測情報を取得する</summary>
    MLogger.MeasurementInfo DrybulbTemperature { get; }

    /// <summary>相対湿度計測情報を取得する</summary>
    MLogger.MeasurementInfo RelativeHumdity { get; }

    /// <summary>グローブ温度計測情報を取得する</summary>
    MLogger.MeasurementInfo GlobeTemperature { get; }

    /// <summary>グローブ温度の電圧[V]を取得する</summary>
    double GlobeTemperatureVoltage { get; }

    /// <summary>風速計測情報を取得する</summary>
    MLogger.MeasurementInfo Velocity { get; }

    /// <summary>風速の電圧[V]を取得する</summary>
    double VelocityVoltage { get; }

    /// <summary>照度計測情報を取得する</summary>
    MLogger.MeasurementInfo Illuminance { get; }

    /// <summary>汎用電圧1計測情報を取得する</summary>
    MLogger.MeasurementInfo GeneralVoltage1 { get; }

    /// <summary>汎用電圧2計測情報を取得する</summary>
    MLogger.MeasurementInfo GeneralVoltage2 { get; }

    /// <summary>汎用電圧3計測情報を取得する</summary>
    MLogger.MeasurementInfo GeneralVoltage3 { get; }

    /// <summary>CO2濃度計測情報を取得する</summary>
    MLogger.MeasurementInfo CO2Level { get; }

    /// <summary>近接センサ計測の真偽を取得する</summary>
    bool MeasureProximity { get; }

    #endregion

    #region 熱的快適性関連のプロパティ

    /// <summary>熱的快適性指標を計算するか否か</summary>
    bool CalcThermalIndices { get; }

    /// <summary>代謝量[met]を取得する</summary>
    double MetValue { get; }

    /// <summary>クロ値[clo]を取得する</summary>
    double CloValue { get; }

    /// <summary>計測値がない場合の乾球温度[C]を取得する</summary>
    double DefaultTemperature { get; }

    /// <summary>計測値がない場合の相対湿度[%]を取得する</summary>
    double DefaultRelativeHumidity { get; }

    /// <summary>計測値がない場合の風速[m/s]を取得する</summary>
    double DefaultVelocity { get; }

    /// <summary>計測値がない場合のグローブ温度[C]を取得する</summary>
    double DefaultGlobeTemperature { get; }

    /// <summary>平均放射温度[C]を取得する</summary>
    double MeanRadiantTemperature { get; }

    /// <summary>PMV[-]を取得する</summary>
    double PMV { get; }

    /// <summary>PPD[%]を取得する</summary>
    double PPD { get; }

    /// <summary>SET*[C]を取得する</summary>
    double SETStar { get; }

    /// <summary>屋外のWBGT[C]を取得する</summary>
    public double WBGT_Outdoor { get; }

    /// <summary>屋内のWBGT[C]を取得する</summary>
    public double WBGT_Indoor { get; }

    #endregion

  }
}
