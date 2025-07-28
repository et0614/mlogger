using Popolo.HumanBody;
using System.Text.RegularExpressions;

using System.Text.Json.Serialization;
using Popolo.ThermophysicalProperty;

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

    /// <summary>日時更新受信イベント</summary>
    public event EventHandler? UpdateCurrentTimeReceivedEvent;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>受信データ</summary>
    private string receivedData = "";

    [JsonIgnore]
    /// <summary>未処理のコマンドがあるか</summary>
    public bool HasCommand { get { return NextCommand != ""; } }

    [JsonIgnore]
    /// <summary>次のコマンドを取得する</summary>
    public string NextCommand { get; private set; } = "";

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

    #region インスタンスメソッド

    /// <summary>受信データを追加する</summary>
    /// <param name="data"></param>
    public void AddReceivedData(string data)
    {
      //最終通信日時を更新
      LastCommunicated = DateTime.Now;

      //データを追加
      receivedData += data;

      //データ受信イベントを通知
      DataReceivedEvent?.Invoke(this, EventArgs.Empty);

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

    /// <summary>熱的快適性の指標（PMV,PPD,SET*）を更新する</summary>
    private void updateThermalIndices()
    {
      if (CalcThermalIndices)
      {
        double dbt = double.IsNaN(DrybulbTemperature.LastValue) ? DefaultTemperature : Math.Max(-10, Math.Min(40, DrybulbTemperature.LastValue));
        double rhd = double.IsNaN(RelativeHumdity.LastValue) ? DefaultRelativeHumidity : Math.Max(0, Math.Min(100, RelativeHumdity.LastValue));
        double vel = double.IsNaN(Velocity.LastValue) ? DefaultVelocity : Math.Max(0, Math.Min(2, Velocity.LastValue));
        double glb = double.IsNaN(GlobeTemperature.LastValue) ? DefaultGlobeTemperature : Math.Max(-10, Math.Min(50, GlobeTemperature.LastValue));

        MeanRadiantTemperature = getMRT(dbt, glb, vel);
        double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndRelativeHumidity
          (dbt, rhd, ATM);

        SETStar = TwoNodeModel.GetSETStarFromAmbientCondition
          (dbt, MeanRadiantTemperature, rhd, vel, CloValue, 58.15 * MetValue, 0);
        PMV = ThermalComfort.GetPMV(dbt, MeanRadiantTemperature, rhd, vel, CloValue, MetValue, 0);
        PPD = ThermalComfort.GetPPD(PMV);

        //グローブ温度を150mmに換算する(JISB7922, JISZ8504)
        double glb150 = dbt + (1 + 1.13 * Math.Pow(GLOBE_DIAMETER, -0.4) * Math.Pow(vel, 0.6)) / (1 + 2.41 * Math.Pow(vel, 0.6)) * (glb - dbt);
        WBGT_Indoor = 0.7 * wbt + 0.3 * glb150;
        WBGT_Outdoor = 0.7 * wbt + 0.2 * glb150 + 0.1 * dbt;
      }
    }

    /// <summary>放射温度[C]を計算する</summary>
    /// <param name="tmp"></param>
    /// <param name="glb"></param>
    /// <param name="vel"></param>
    /// <returns></returns>
    private static double getMRT(double tmp, double glb, double vel)
    {
      const double EPS = 0.95; //ピンポン球の放射率[-]
      const double SIG = 5.67e-8;
      const double ES = EPS * SIG;

      //ISO 7726:Ergonomics of the thermal environment
      double hc1 = 1.4 * Math.Pow(Math.Abs(tmp - glb) / GLOBE_DIAMETER, 0.25);
      double hc2 = 6.3 * Math.Pow(vel, 0.6) / Math.Pow(GLOBE_DIAMETER, 0.4);
      double glbK = glb + 273.15;
      return Math.Pow(Math.Max(0, Math.Pow(glbK, 4) + Math.Max(hc1, hc2) / ES * (glb - tmp)), 0.25) - 273.15;
    }

    #endregion

    #region 受信データの処理

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
          //計測値受信
          case "DTT":
            CurrentStatus = Status.Measuring;
            solveDTT();
            break;

          //設定値変更
          case "CMS":
            solveMS();
            break;

          //設定値受信
          case "LMS":
            solveMS();
            break;

          //Dummy
          case "DMY":
            //未実装
            break;

          //コマンド待ち
          case "WFC":
            //初期化済の場合
            if (CurrentStatus == Status.Initializing && VersionLoaded && MeasuringSettingLoaded)
              CurrentStatus = Status.WaitingForCommand;
            //計測中が解除された場合
            else if (CurrentStatus == Status.Measuring || CurrentStatus == Status.StartMeasuring)
              CurrentStatus = Status.WaitingForCommand;

            //校正完了とみなす
            VelocityCalibrationTime = TemperatureCalibrationTime = 0;

            //イベント通知
            WaitingForCommandMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //計測開始
          case "STL":
            CurrentStatus = Status.StartMeasuring;

            //イベント通知
            StartMeasuringMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //バージョン受信
          case "VER":
            solveVER();
            break;

          //補正係数設定
          case "SCF":
            solveCF();
            break;

          //補正係数受信
          case "LCF":
            solveCF();
            break;

          //風速特性係数設定
          case "SVC":
            solveVC();
            break;

          //風速特性係数受信
          case "LVC":
            solveVC();
            break;

          //ロギング終了命令
          case "ENL":
            //イベント通知
            EndMeasuringMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //名称受信
          case "LLN":
            solveLN();
            break;

          //名称変更
          case "CLN":
            solveLN();
            break;

          //風速電圧校正開始
          case "SCV":
            Velocity.LastMeasureTime = DateTime.Now;
            VelocityVoltage = double.Parse(NextCommand.Remove(0, 4).TrimEnd('\r'));
            CalibratingVoltageReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //風速電圧校正終了
          case "ECV":
            EndCalibratingVoltageMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //風速自動校正
          case "CBV":
            VelocityCalibrationTime = int.Parse(NextCommand.Remove(0, 4).TrimEnd('\r'));
            VelocityAutoCalibrationReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //温度自動校正
          case "CBT":
            TemperatureCalibrationTime = int.Parse(NextCommand.Remove(0, 4).TrimEnd('\r'));
            TemperatureAutoCalibrationReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //CO2濃度センサの有無
          case "HCS":
            HasCO2LevelSensor = NextCommand.Remove(0, 4).TrimEnd('\r') == "1";
            HasCO2LevelSensorReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;

          //CO2濃度校正
          case "CCL":
            string[] bf = NextCommand.Remove(0, 4).TrimEnd('\r').Split(',');
            int rmTime = int.Parse(bf[0]);
            bool success = true;
            int correction = 0;
            if (rmTime == 0)
            {
              success = bf[1] == "success";
              correction = int.Parse(bf[2]);
            }

            HasCO2LevelSensor = NextCommand.Remove(0, 4).TrimEnd('\r') == "1";
            CalibratingCO2LevelReceivedEvent?.Invoke(this, new CalibratingCO2SensorLevelEventArgs(rmTime, success, correction));
            break;

          //日時更新
          case "UCT":
            UpdateCurrentTimeReceivedEvent?.Invoke(this, EventArgs.Empty);
            break;
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
      finally
      {
        //エラーが起きてもとにかく次のコマンドへ移行
        SkipCommand();
      }
    }

    /// <summary>測定値受信コマンド（DTT）を処理する</summary>
    private void solveDTT()
    {
      try
      {
        string[] buff = NextCommand.Substring(4).Split(',');
        string[] mmdd = buff[1].Split('/');
        string[] hhMMss = buff[2].Split(':');
        DateTime now = new DateTime(
          int.Parse(buff[0]), int.Parse(mmdd[0]), int.Parse(mmdd[1]),
          int.Parse(hhMMss[0]), int.Parse(hhMMss[1]), int.Parse(hhMMss[2]), DateTimeKind.Local);
        double temperature = (buff[3] == "n/a") ? double.NaN : double.Parse(buff[3]);
        double humidity = (buff[4] == "n/a") ? double.NaN : double.Parse(buff[4]);
        double globe = (buff[5] == "n/a") ? double.NaN : double.Parse(buff[5]);
        double velocity = (buff[6] == "n/a") ? double.NaN : double.Parse(buff[6]);
        double illuminance = (buff[7] == "n/a") ? double.NaN : double.Parse(buff[7]);
        GlobeTemperatureVoltage = (buff[8] == "n/a") ? double.NaN : double.Parse(buff[8]);
        VelocityVoltage = (buff[9] == "n/a") ? double.NaN : double.Parse(buff[9]);
        double gpVoltage1 = (buff[10] == "n/a") ? double.NaN : double.Parse(buff[10]);
        double gpVoltage2 = (buff[11] == "n/a") ? double.NaN : double.Parse(buff[11]);
        double gpVoltage3 = (buff[12] == "n/a") ? double.NaN : double.Parse(buff[12]);
        //CO2濃度がある場合
        double co2Level = double.NaN;
        if(13 < buff.Length) co2Level = (buff[13] == "n/a") ? double.NaN : double.Parse(buff[13]);

        //最後の計測日時
        LastMeasured = now;

        //最新の値を保存
        if (!double.IsNaN(temperature))
        {
          DrybulbTemperature.LastMeasureTime = RelativeHumdity.LastMeasureTime = now;
          DrybulbTemperature.LastValue = temperature;
          RelativeHumdity.LastValue = humidity;
        }
        if (!double.IsNaN(globe))
        {
          GlobeTemperature.LastMeasureTime = now;
          GlobeTemperature.LastValue = globe;
        }
        if (!double.IsNaN(velocity))
        {
          Velocity.LastMeasureTime = now;
          Velocity.LastValue = velocity;
        }
        if (!double.IsNaN(illuminance))
        {
          Illuminance.LastMeasureTime = now;
          Illuminance.LastValue = illuminance;
        }
        if (!double.IsNaN(gpVoltage1))
        {
          GeneralVoltage1.LastMeasureTime = now;
          GeneralVoltage1.LastValue = gpVoltage1;
        }
        if (!double.IsNaN(gpVoltage2))
        {
          GeneralVoltage2.LastMeasureTime = now;
          GeneralVoltage2.LastValue = gpVoltage2;
        }
        if (!double.IsNaN(gpVoltage3))
        {
          GeneralVoltage3.LastMeasureTime = now;
          GeneralVoltage3.LastValue = gpVoltage3;
        }
        if (!double.IsNaN(co2Level))
        {
          CO2Level.LastMeasureTime = now;
          CO2Level.LastValue = co2Level;
        }

        //熱的快適性指標を計算する
        updateThermalIndices();

        //イベント通知
        MeasuredValueReceivedEvent?.Invoke(this, EventArgs.Empty);
      }
      //通信の問題で不正な文字が送信されるような場合に備える
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }

    /// <summary>測定設定コマンド（CMS, LMS）を処理する</summary>
    private void solveMS()
    {
      string[] buff = NextCommand.Substring(4, NextCommand.Length - 4).Split(',');

      //計測真偽
      DrybulbTemperature.Measure = RelativeHumdity.Measure = (buff[0] == "1");
      GlobeTemperature.Measure = (buff[2] == "1");
      Velocity.Measure = (buff[4] == "1");
      Illuminance.Measure = (buff[6] == "1");
      GeneralVoltage1.Measure = (buff[9] == "1");
      GeneralVoltage2.Measure = (buff[11] == "1");
      GeneralVoltage3.Measure = (buff[13] == "1");
      MeasureProximity = (buff[15] == "1");
      if(16 < buff.Length) CO2Level.Measure = (buff[16] == "1");

      //計測時間間隔[sec]
      DrybulbTemperature.Interval = RelativeHumdity.Interval = int.Parse(buff[1]);
      GlobeTemperature.Interval = int.Parse(buff[3]);
      Velocity.Interval = int.Parse(buff[5]);
      Illuminance.Interval = int.Parse(buff[7]);
      GeneralVoltage1.Interval = int.Parse(buff[10]);
      GeneralVoltage2.Interval = int.Parse(buff[12]);
      GeneralVoltage3.Interval = int.Parse(buff[14]);
      if(17 < buff.Length) CO2Level.Interval = int.Parse(buff[17]);

      //計測開始日時
      StartMeasuringDateTime = GetDateTimeFromUTime(long.Parse(buff[8]));

      //測定設定読み込み済フラグをON
      MeasuringSettingLoaded = true;
      if (VersionLoaded) CurrentStatus = Status.WaitingForCommand;

      //イベント通知
      MeasurementSettingReceivedEvent?.Invoke(this, EventArgs.Empty);
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
      if (MeasuringSettingLoaded) CurrentStatus = Status.WaitingForCommand;

      //イベント通知
      VersionReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>補正係数設定コマンド(SCF,LCF)を処理する</summary>
    private void solveCF()
    {
      string[] buff = NextCommand.Substring(4).Split(',');

      if (!double.TryParse(buff[0], out double dbtA)) dbtA = 1.0;
      DrybulbTemperature.CorrectionFactorA = dbtA;

      if (!double.TryParse(buff[1], out double dbtB)) dbtB = 0.0;
      DrybulbTemperature.CorrectionFactorB = dbtB;

      if (!double.TryParse(buff[2], out double hmdA)) hmdA = 1.0;
      RelativeHumdity.CorrectionFactorA = hmdA;

      if (!double.TryParse(buff[3], out double hmdB)) hmdB = 0.0;
      RelativeHumdity.CorrectionFactorB = hmdB;

      if (!double.TryParse(buff[4], out double glbA)) glbA = 1.0;
      GlobeTemperature.CorrectionFactorA = glbA;

      if (!double.TryParse(buff[5], out double glbB)) glbB = 0.0;
      GlobeTemperature.CorrectionFactorB = glbB;

      if (!double.TryParse(buff[6], out double luxA)) luxA = 1.0;
      Illuminance.CorrectionFactorA = luxA;

      if (!double.TryParse(buff[7], out double luxB)) luxB = 0.0;
      Illuminance.CorrectionFactorB = luxB;

      if (!double.TryParse(buff[8], out double velA)) velA = 1.0;
      Velocity.CorrectionFactorA = velA;

      if (!double.TryParse(buff[9], out double velB)) velB = 0.0;
      Velocity.CorrectionFactorB = velB;

      if (!double.TryParse(buff[10], out double vel0)) vel0 = 1.45;
      VelocityMinVoltage = vel0;

      //イベント通知
      CorrectionFactorsReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>風速特性係数設定コマンドを処理する</summary>
    private void solveVC()
    {
      string[] buff = NextCommand.Substring(4).Split(',');

      if (!double.TryParse(buff[0], out double vel0)) vel0 = 1.45;
      VelocityMinVoltage = vel0;

      if (!double.TryParse(buff[1], out double cfA)) cfA = 79.744;
      VelocityCharacteristicsCoefA = cfA;

      if (!double.TryParse(buff[2], out double cfB)) cfB = -12.029;
      VelocityCharacteristicsCoefB = cfB;

      if (!double.TryParse(buff[3], out double cfC)) cfC = 2.356;
      VelocityCharacteristicsCoefC = cfC;

      //イベント通知
      VelocityCharateristicsReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>ロガー名称設定コマンド()を処理する</summary>
    private void solveLN()
    {
      Name = NextCommand.Remove(0, 4).TrimEnd('\r');
      Name = Name.TrimEnd();

      //イベント通知
      LoggerNameReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 送信コマンド作成処理

    /// <summary>測定開始コマンドをつくる</summary>
    /// <param name="useZigbee">Zigbeeで出力するか</param>
    /// <param name="useBluetooth">Bluetoothで出力するか</param>
    /// <param name="useSDCard">SDカードに書き出すか否か</param>
    /// <returns>測定開始コマンド</returns>
    public static string MakeStartMeasuringCommand(bool useZigbee, bool useBluetooth, bool useSDCard)
    {
      return MakeStartMeasuringCommand(useZigbee, useBluetooth, useSDCard, false);
    }

    /// <summary>測定開始コマンドをつくる</summary>
    /// <param name="useZigbee">Zigbeeで出力するか</param>
    /// <param name="useBluetooth">Bluetoothで出力するか</param>
    /// <param name="useSDCard">SDカードに書き出すか否か</param>
    /// <returns>測定開始コマンド</returns>
    public static string MakeStartMeasuringCommand(bool useZigbee, bool useBluetooth, bool useSDCard, bool permanentMode)
    {
      //tffはxbee-on,bluetooth-off,sdcard-off
      return "\rSTL" +
        String.Format("{0:D10}", GetUnixTime(DateTime.Now)) +
        (permanentMode ? "e" : (useZigbee ? "t" : "f")) +
        ((useBluetooth && !permanentMode) ? "t" : "f") +
        ((useSDCard && !permanentMode) ? "t\r" : "f\r");
    }

    /// <summary>計測設定コマンドをつくる</summary>
    /// <param name="startDTime"></param>
    /// <param name="measureTH"></param>
    /// <param name="intervalTH"></param>
    /// <param name="measureGlb"></param>
    /// <param name="intervalGlb"></param>
    /// <param name="measureVel"></param>
    /// <param name="intervalVel"></param>
    /// <param name="measureIll"></param>
    /// <param name="intervalIll"></param>
    /// <param name="measureGPV1"></param>
    /// <param name="intervalGPV1"></param>
    /// <param name="measureGPV2"></param>
    /// <param name="intervalGPV2"></param>
    /// <param name="measureGPV3"></param>
    /// <param name="intervalGPV3"></param>
    /// <param name="measureProx"></param>
    /// <returns></returns>
    public static string MakeChangeMeasuringSettingCommand(
      DateTime startDTime,
      bool measureTH, int intervalTH,
      bool measureGlb, int intervalGlb,
      bool measureVel, int intervalVel,
      bool measureIll, int intervalIll,
      bool measureGPV1, int intervalGPV1,
      bool measureGPV2, int intervalGPV2,
      bool measureGPV3, int intervalGPV3,
      bool measureProx,
      bool measureCO2, int intervalCO2)
    {
      return "\rCMS"
        + (measureTH ? "t" : "f") + string.Format("{0,5}", intervalTH)
        + (measureGlb ? "t" : "f") + string.Format("{0,5}", intervalGlb)
        + (measureVel ? "t" : "f") + string.Format("{0,5}", intervalVel)
        + (measureIll ? "t" : "f") + string.Format("{0,5}", intervalIll)
        + String.Format("{0, 10}", GetUnixTime(startDTime).ToString("F0")) //UNIX時間を10桁（空白埋め）で送信
        + (measureGPV1 ? "t" : "f") + string.Format("{0,5}", intervalGPV1)
        + (measureGPV2 ? "t" : "f") + string.Format("{0,5}", intervalGPV2)
        + (measureGPV3 ? "t" : "f") + string.Format("{0,5}", intervalGPV3)
        + (measureProx ? "t" : "f") 
        + (measureCO2 ? "t" : "f") + string.Format("{0,5}", intervalCO2)
        + "\r";
    }

    /// <summary>補正係数設定コマンドをつくる</summary>
    /// <param name="dbtA">乾球温度の補正係数A（y=Ax+B）</param>
    /// <param name="dbtB">乾球温度の補正係数B（y=Ax+B）</param>
    /// <param name="hmdA">相対湿度の補正係数A（y=Ax+B）</param>
    /// <param name="hmdB">相対湿度の補正係数B（y=Ax+B）</param>
    /// <param name="glbA">グローブ温度の補正係数A（y=Ax+B）</param>
    /// <param name="glbB">グローブ温度の補正係数B（y=Ax+B）</param>
    /// <param name="luxA">照度の補正係数A（y=Ax+B）</param>
    /// <param name="luxB">照度の補正係数B（y=Ax+B）</param>
    /// <param name="velA">風速の補正係数A（y=Ax+B）</param>
    /// <param name="velB">風速の補正係数B（y=Ax+B）</param>
    /// <param name="vel0">無風風速の電圧[V]</param>
    /// <returns>補正係数設定コマンド</returns>
    public static string MakeCorrectionFactorsSettingCommand(
      double dbtA, double dbtB,
      double hmdA, double hmdB,
      double glbA, double glbB,
      double luxA, double luxB,
      double velA, double velB, double vel0)
    {
      return "\rSCF" +
        string.Format("{0, 4}", (1000 * dbtA).ToString("F0")) +
        string.Format("{0, 4}", (100 * dbtB).ToString("F0")) +
        string.Format("{0, 4}", (1000 * hmdA).ToString("F0")) +
        string.Format("{0, 4}", (100 * hmdB).ToString("F0")) +
        string.Format("{0, 4}", (1000 * glbA).ToString("F0")) +
        string.Format("{0, 4}", (100 * glbB).ToString("F0")) +
        string.Format("{0, 4}", (1000 * luxA).ToString("F0")) +
        string.Format("{0, 4}", (1 * luxB).ToString("F0")) +
        string.Format("{0, 4}", (1000 * velA).ToString("F0")) +
        string.Format("{0, 4}", (1000 * velB).ToString("F0")) +
        string.Format("{0, 4}", (1000 * vel0).ToString("F0") + "\r");
    }

    /// <summary>風速特性係数設定コマンドをつくる</summary>
    /// <param name="vel0">無風風速の電圧[V]</param>
    /// <param name="cCoefA">特性係数A</param>
    /// <param name="cCoefB">特性係数B</param>
    /// <param name="cCoefC">特性係数C</param>
    /// <returns>風速特性係数設定コマンド</returns>
    public static string MakeVelocityCharateristicsSettingCommand(
      double vel0, double cCoefA, double cCoefB, double cCoefC)
    {
      return "\rSVC" +
        string.Format("{0, 4}", (1000 * vel0).ToString("F0")) +
        string.Format("{0, 7}", (1000 * cCoefA).ToString("F0")) +
        string.Format("{0, 7}", (1000 * cCoefB).ToString("F0")) +
        string.Format("{0, 7}", (1000 * cCoefC).ToString("F0") + "\r");
    }

    /// <summary>バージョン取得コマンドをつくる</summary>
    /// <returns>バージョン取得コマンド</returns>
    public static string MakeGetVersionCommand()
    {
      return "\rVER\r";
    }

    /// <summary>計測設定取得コマンドをつくる</summary>
    /// <returns>計測設定取得コマンド</returns>
    public static string MakeLoadMeasuringSettingCommand()
    {
      return "\rLMS\r";
    }

    /// <summary>補正係数取得コマンドをつくる</summary>
    /// <returns>補正係数取得コマンド</returns>
    public static string MakeLoadCorrectionFactorsCommand()
    {
      return "\rLCF\r";
    }

    /// <summary>風速特性係数取得コマンドをつくる</summary>
    /// <returns>風速特性係数取得コマンド</returns>
    public static string MakeLoadVelocityCharateristicsCommand()
    {
      return "\rLVC\r";
    }

    /// <summary>測定終了コマンドをつくる</summary>
    /// <returns>測定終了コマンド</returns>
    public static string MakeEndLoggingCommand()
    {
      return "\rENL\r";
    }

    /// <summary>名称設定コマンドをつくる</summary>
    /// <param name="name">名称</param>
    /// <returns>名称設定コマンド</returns>
    /// <exception cref="Exception"></exception>
    public static string MakeChangeLoggerNameCommand(string name)
    {
      //半角文字のみとする 
      Regex re = new Regex(@"[^a-zA-Z\w\d\s]"); // 「英数字,半角スペース,アンダースコア」以外  
      name = re.Replace(name, "");

      //20字以内に縮めた上で右端を半角スペースで埋める
      if (20 < name.Length) name = name.Substring(0, 20);
      return "\rCLN" + name.PadRight(20) + '\r';
    }

    /// <summary>名称取得コマンドをつくる</summary>
    /// <returns>名称取得コマンド</returns>
    public static string MakeLoadLoggerNameCommand()
    {
      return "\rLLN\r";
    }

    /// <summary>自動風速校正コマンドをつくる</summary>
    /// <param name="sec">校正時間[sec]</param>
    /// <returns>自動風速校正コマンド</returns>
    public static string MakeAutoVelocityCalibrationCommand(int sec)
    {
      return "\rCBV" + Math.Max(1, Math.Min(99999, sec)) + "\r";
    }

    /// <summary>自動温度校正コマンドをつくる</summary>
    /// <param name="sec">校正時間[sec]</param>
    /// <returns>自動温度校正コマンド</returns>
    public static string MakeAutoTemperatureCalibrationCommand(int sec)
    {
      return "\rCBT" + Math.Max(1, Math.Min(99999, sec)) + "\r";
    }

    /// <summary>風速校正開始コマンドをつくる</summary>
    /// <returns>風速校正開始コマンド</returns>
    public static string MakeStartCalibratingVoltageCommand()
    {
      return "\rSCV\r";
    }

    /// <summary>風速校正終了コマンドをつくる</summary>
    /// <returns>風速校正終了コマンド</returns>
    public static string MakeEndCalibratingVoltageCommand()
    {
      return "\rECV\r";
    }

    /// <summary>現在日時更新コマンドをつくる</summary>
    /// <returns>現在日時更新コマンド</returns>
    public static string MakeUpdateCurrentTimeCommand()
    {
      return MakeUpdateCurrentTimeCommand(DateTime.Now);
    }

    /// <summary>CO2濃度計の有無確認コマンドをつくる</summary>
    /// <returns>CO2濃度計の有無確認コマンド</returns>
    public static string MakeHasCO2LevelSensorCommand()
    {
      return "\rHCS\r";
    }

    /// <summary>CO2濃度校正コマンドをつくる</summary>
    /// <param name="referenceCO2Level">基準のCO2濃度[ppm]</param>
    /// <returns>CO2濃度校正コマンド</returns>
    public static string MakeCalibrateCO2LevelCommand(int referenceCO2Level)
    {
      referenceCO2Level = Math.Max(400, Math.Min(10000, referenceCO2Level));
      return "\rCCL" + referenceCO2Level.ToString("F0") + "\r";
    }

    /// <summary>現在日時更新コマンドをつくる</summary>
    /// <param name="cTime">現在日時</param>
    /// <returns>現在日時更新コマンド</returns>
    public static string MakeUpdateCurrentTimeCommand(DateTime cTime)
    {
      return "\rUCT" +
        String.Format("{0:D10}", GetUnixTime(cTime)) + "\r";
    }

    #endregion

    #region staticメソッド

    /// <summary>日時からUNIX時間を求める</summary>
    /// <param name="dTime">日時</param>
    /// <returns>UNIX時間</returns>
    /// <remarks>計測器内部ではUTC=0で時刻を管理する</remarks>
    public static long GetUnixTime(DateTime dTime)
    {
      DateTime dtNow = new DateTime(dTime.Year, dTime.Month, dTime.Day, dTime.Hour, dTime.Minute, dTime.Second, DateTimeKind.Utc);
      return (long)(dtNow - UNIX_EPOCH).TotalSeconds;
    }

    /// <summary>UNIX時間から日時を求める</summary>
    /// <param name="unixTime">UNIX時間</param>
    /// <returns>日時</returns>
    /// <remarks>計測器内部ではUTC=0で時刻を管理する</remarks>
    public static DateTime GetDateTimeFromUTime(long unixTime)
    {
      DateTime dtNow = UNIX_EPOCH.AddSeconds(unixTime);
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

      public CalibratingCO2SensorLevelEventArgs(int remainingTime, bool calibrationSucceeded, int correctionCO2Level)
      {
        RemainingTime = remainingTime;
        CalibrationSucceeded = calibrationSucceeded;
        CorrectionCO2Level = correctionCO2Level;
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
