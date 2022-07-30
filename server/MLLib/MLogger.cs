using System;

using Popolo.HumanBody;
using System.Text;

using System.Collections.Generic;

namespace MLLib
{

  /// <summary>MLoggerを管理する</summary>
  public class MLogger
  {

    #region 定数宣言

    /// <summary>UNIX時間起点</summary>
    private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0);

    #endregion

    #region 列挙型定義

    /// <summary>状態</summary>
    public enum Status
    {
      /// <summary>コマンド受信待ち</summary>
      WaitingForCommand,
      /// <summary>計測開始処理中</summary>
      StartMeasuring,
      /// <summary>計測中</summary>
      Measuring
    }

    #endregion

    #region イベント定義

    /// <summary>測定値受信イベント</summary>
    public event EventHandler MeasuredValueReceivedEvent;

    /// <summary>測定設定受信イベント</summary>
    public event EventHandler MeasurementSettingReceivedEvent;

    /// <summary>バージョン受信イベント</summary>
    public event EventHandler VersionReceivedEvent;

    /// <summary>補正係数受信イベント</summary>
    public event EventHandler CorrectionFactorsReceivedEvent;

    /// <summary>コマンド待ち通知受信イベント</summary>
    public event EventHandler WaitingForCommandMessageReceivedEvent;

    /// <summary>測定開始通知受信イベント</summary>
    public event EventHandler StartMeasuringMessageReceivedEvent;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>受信データ</summary>
    private string receivedData = "";

    /// <summary>未処理のコマンドがあるか</summary>
    public bool HasCommand { get { return NextCommand != ""; } }

    /// <summary>次のコマンドを取得する</summary>
    public string NextCommand { get; private set; } = "";

    /// <summary>名称を設定・取得する</summary>
    public string Name { get; set; }

    /// <summary>初回の保存か否か</summary>
    public bool IsFirstSave { get; set; } = true;

    /// <summary>LongAddress（16進数）を設定・取得する</summary>
    public string LongAddress { get; set; }

    /// <summary>LongAddressの下位アドレス（16進数）を取得する</summary>
    public string LowAddress { get { return LongAddress.Substring(8); } }

    /// <summary>最後の通信日時を取得する</summary>
    public DateTime LastCommunicated { get; private set; }

    /// <summary>最後の計測日時を取得する</summary>
    public DateTime LastMeasured { get; private set; }

    /// <summary>バージョン（メジャー）を取得する</summary>
    public int Version_Major { get; private set; }

    /// <summary>バージョン（マイナー）を取得する</summary>
    public int Version_Minor { get; private set; }

    /// <summary>バージョン（リビジョン）を取得する</summary>
    public int Version_Revision { get; private set; }

    /// <summary>現在の状態を取得する</summary>
    public Status CurrentStatus { get; private set; } = Status.WaitingForCommand;

    /// <summary>計測開始日時を取得する</summary>
    public DateTime StartMeasuringDateTime { get; private set; } = new DateTime(2000, 1, 1, 0, 0, 0);

    #endregion

    #region 計測値関連のプロパティ

    /// <summary>乾球温度計測情報を取得する</summary>
    public MeasurementInfo DrybulbTemperature { get; } = new MeasurementInfo();

    /// <summary>相対湿度計測情報を取得する</summary>
    public MeasurementInfo RelativeHumdity { get; } = new MeasurementInfo();

    /// <summary>グローブ温度計測情報を取得する</summary>
    public MeasurementInfo GlobeTemperature { get; } = new MeasurementInfo();

    /// <summary>グローブ温度の電圧[V]を取得する</summary>
    public double GlobeTemperatureVoltage { get; private set; }

    /// <summary>風速計測情報を取得する</summary>
    public MeasurementInfo Velocity { get; } = new MeasurementInfo();

    /// <summary>風速の電圧[V]を取得する</summary>
    public double VelocityVoltage { get; private set; }

    /// <summary>照度計測情報を取得する</summary>
    public MeasurementInfo Illuminance { get; } = new MeasurementInfo();

    /// <summary>汎用電圧1計測情報を取得する</summary>
    public MeasurementInfo GeneralVoltage1 { get; } = new MeasurementInfo();

    /// <summary>汎用電圧2計測情報を取得する</summary>
    public MeasurementInfo GeneralVoltage2 { get; } = new MeasurementInfo();

    /// <summary>汎用電圧3計測情報を取得する</summary>
    public MeasurementInfo GeneralVoltage3 { get; } = new MeasurementInfo();

    /// <summary>近接センサ計測の真偽を取得する</summary>
    public bool MeasureProximity { get; private set; } = false;

    /// <summary>微風速の無風時の電圧[V]を取得する</summary>
    public double VelocityMinVoltage { get; private set; } = 1.45;

    #endregion

    #region 熱的快適性関連のプロパティ

    /// <summary>熱的快適性指標を計算するか否か</summary>
    public bool CalcThermalIndices { get; set; } = true;

    /// <summary>代謝量[met]を設定・取得する</summary>
    public double MetValue { get; set; } = 1.1;

    /// <summary>クロ値[clo]を設定・取得する</summary>
    public double CloValue { get; set; } = 1.0;

    /// <summary>計測値がない場合の乾球温度[C]を設定・取得する</summary>
    public double DefaultTemperature { get; set; } = 25;

    /// <summary>計測値がない場合の相対湿度[%]を設定・取得する</summary>
    public double DefaultRelativeHumidity { get; set; } = 50;

    /// <summary>計測値がない場合の風速[m/s]を設定・取得する</summary>
    public double DefaultVelocity { get; set; } = 0.1;

    /// <summary>計測値がない場合のグローブ温度[C]を設定・取得する</summary>
    public double DefaultGlobeTemperature { get; set; } = 25;

    /// <summary>平均放射温度[C]を取得する</summary>
    public double MeanRadiantTemperature { get; private set; }

    /// <summary>PMV[-]を取得する</summary>
    public double PMV { get; private set; }

    /// <summary>PPD[-]を取得する</summary>
    public double PPD { get; private set; }

    /// <summary>SET*[-]を取得する</summary>
    public double SETStar { get; private set; }

    #endregion

    #region コンストラクタ

    public MLogger()
      : this("0000000000000000") { }

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="longAddress">LongAddress（16進数）</param>
    public MLogger(string longAddress)
    {
      LongAddress = longAddress;
      Name = LowAddress;
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
      if (receivedData.Contains('\r'))
      {
        NextCommand = receivedData.Substring(0, receivedData.IndexOf('\r'));
        receivedData = receivedData.Remove(0, receivedData.IndexOf('\r') + 1);
      }
      else NextCommand = "";
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

        SETStar = TwoNodeModel.GetSETStarFromAmbientCondition
          (dbt, MeanRadiantTemperature, rhd, vel, CloValue, 58.15 * MetValue, 0);
        PMV = ThermalComfort.GetPMV(dbt, MeanRadiantTemperature, rhd, vel, CloValue, MetValue, 0);
        PPD = ThermalComfort.GetPPD(PMV);
      }
    }

    /// <summary>放射温度[C]を計算する</summary>
    /// <param name="tmp"></param>
    /// <param name="glb"></param>
    /// <param name="vel"></param>
    /// <returns></returns>
    private static double getMRT(double tmp, double glb, double vel)
    {
      const double DIA = 0.04; //ピンポン球の直径[m]
      const double EPS = 0.95; //ピンポン球の放射率[-]
      const double SIG = 5.67e-8;
      const double ES = EPS * SIG;

      //ISO 7726:Ergonomics of the thermal environment
      double hc1 = 1.4 * Math.Pow(Math.Abs(tmp - glb) / DIA, 0.25);
      double hc2 = 6.3 * Math.Pow(vel, 0.6) / Math.Pow(DIA, 0.4);
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

      string cmd = NextCommand.Substring(0, 3);
      switch (cmd)
      {
        case "DTT":
          CurrentStatus = Status.Measuring;
          solveDTT();
          break;

        case "CMS":
          solveMS();
          break;

        case "LMS":
          solveMS();
          break;

        case "DMY":
          //未実装
          break;

        case "WFC":
          CurrentStatus = Status.WaitingForCommand;

          //イベント通知
          WaitingForCommandMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
          break;

        case "STL":
          CurrentStatus = Status.StartMeasuring;

          //イベント通知
          StartMeasuringMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
          break;

        case "VER":
          solveVER();
          break;

        case "SCF":
          solveCF();
          break;

        case "LCF":
          solveCF();
          break;
      }

      //次のコマンドへ移行
      SkipCommand();
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

        //熱的快適性指標を計算する
        updateThermalIndices();

        //イベント通知
        MeasuredValueReceivedEvent?.Invoke(this, EventArgs.Empty);
      }
      //通信の問題で不正な文字が送信されるような場合に備える
      catch
      {

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

      //計測時間間隔[sec]
      DrybulbTemperature.Interval = RelativeHumdity.Interval = int.Parse(buff[1]);
      GlobeTemperature.Interval = int.Parse(buff[3]);
      Velocity.Interval = int.Parse(buff[5]);
      Illuminance.Interval = int.Parse(buff[7]);
      GeneralVoltage1.Interval = int.Parse(buff[10]);
      GeneralVoltage2.Interval = int.Parse(buff[12]);
      GeneralVoltage3.Interval = int.Parse(buff[14]);

      //計測開始日時
      StartMeasuringDateTime = GetDateTimeFromUTime(long.Parse(buff[8]));

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

      //イベント通知
      VersionReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>補正係数を読み込む</summary>
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

    #endregion

    #region 送信コマンド作成処理

    /// <summary>測定開始コマンドをつくる</summary>
    /// <param name="outputToSDCard">SDカードに書き出すか否か</param>
    /// <returns>測定開始コマンド</returns>
    public static string MakeStartMeasuringCommand(bool outputToSDCard)
    {
      //tffはxbee-on,bluetooth-off,sdcard-off
      return "\rSTL" + String.Format("{0:D10}", GetUnixTime(DateTime.Now)) + (outputToSDCard ? "fft\r" : "ttf\r");
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
      bool measureProx)
    {
      return "CMS"
        + (measureTH ? "t" : "f") + string.Format("{0,5}", intervalTH)
        + (measureGlb ? "t" : "f") + string.Format("{0,5}", intervalGlb)
        + (measureVel ? "t" : "f") + string.Format("{0,5}", intervalVel)
        + (measureIll ? "t" : "f") + string.Format("{0,5}", intervalIll)
        + String.Format("{0, 10}", MLogger.GetUnixTime(startDTime).ToString("F0")) //UNIX時間を10桁（空白埋め）で送信
        + (measureGPV1 ? "t" : "f") + string.Format("{0,5}", intervalGPV1)
        + (measureGPV2 ? "t" : "f") + string.Format("{0,5}", intervalGPV2)
        + (measureGPV3 ? "t" : "f") + string.Format("{0,5}", intervalGPV3)
        + (measureProx ? "t" : "f");
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

    public static string MakeHTMLTable(string baseHTML, MLogger[] mLoggers)
    {
      //編集
      //台数を表示
      baseHTML = baseHTML.Replace("<!--ML_NUMBER-->", mLoggers.Length.ToString());

      //計測値を表示
      StringBuilder contents = new StringBuilder("");
      for (int i = 0; i < mLoggers.Length; i++)
      {
        MLogger ml = mLoggers[i];
        contents.AppendLine("<tr>");
        //一般情報
        contents.AppendLine("<td class=\"dt_last general\">" + ml.LastCommunicated.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"name general\">" + ml.Name + "</td>");
        contents.AppendLine("<td class=\"id general\">" + ml.LowAddress + "</td>");
        //温湿度
        contents.AppendLine("<td class=\"dt_th thlog\">" + ml.DrybulbTemperature.LastMeasureTime.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"tmp thlog\">" + ml.DrybulbTemperature.LastValue.ToString("F1") + "</td>");
        contents.AppendLine("<td class=\"hmd thlog\">" + ml.RelativeHumdity.LastValue.ToString("F1") + "</td>");
        //グローブ温度
        contents.AppendLine("<td class=\"dt_glb glblog\">" + ml.GlobeTemperature.LastMeasureTime.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"glb glblog\">" + ml.GlobeTemperature.LastValue.ToString("F2") + "</td>");
        //微風速
        contents.AppendLine("<td class=\"dt_vel vellog\">" + ml.Velocity.LastMeasureTime.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"vel vellog\">" + (ml.Velocity.LastValue * 100).ToString("F1") + "</td>");
        //照度
        contents.AppendLine("<td class=\"dt_ill illlog\">" + ml.Illuminance.LastMeasureTime.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"ill illlog\">" + ml.Illuminance.LastValue.ToString("F2") + "</td>");
        //熱的快適性指標の計算
        ml.updateThermalIndices();
        contents.AppendLine("<td class=\"cmft_set cmftlog\">" + ml.SETStar.ToString("F1") + "</td>");
        contents.AppendLine("<td class=\"cmft_pmv cmftlog\">" + ml.PMV.ToString("F2") + "</td>");
        contents.AppendLine("<td class=\"cmft_ppd cmftlog\">" + ml.PPD.ToString("F1") + "</td>");
        //データリンク先
        contents.AppendLine("<td class=\"general\"><a href=\"" + ml.LowAddress + ".csv\">" + ml.LowAddress + ".csv</a></td>");
        contents.AppendLine("</tr>");
      }
      baseHTML = baseHTML.Replace("<!--ML_CONTENTS-->", contents.ToString());

      return baseHTML;
    }

    public static string MakeLatestData(MLogger[] mLoggers)
    {
      StringBuilder sBuilder = new StringBuilder();
      for (int i = 0; i < mLoggers.Length; i++)
      {
        MLogger ml = mLoggers[i];
        sBuilder.Append(ml.Name);
        //温湿度
        sBuilder.Append("," + ml.DrybulbTemperature.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.DrybulbTemperature.LastValue.ToString("F1"));
        sBuilder.Append("," + ml.RelativeHumdity.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.RelativeHumdity.LastValue.ToString("F1"));
        //グローブ温度
        sBuilder.Append("," + ml.GlobeTemperature.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.GlobeTemperature.LastValue.ToString("F2"));
        //微風速
        sBuilder.Append("," + ml.Velocity.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss") + "," + (ml.Velocity.LastValue * 100).ToString("F1"));
        //照度
        sBuilder.Append("," + ml.Illuminance.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.Illuminance.LastValue.ToString("F2"));
        //熱的快適性指標の更新
        ml.updateThermalIndices();
        sBuilder.Append("," + ml.SETStar.ToString("F2"));
        sBuilder.Append("," + ml.PMV.ToString("F2"));
        sBuilder.Append("," + ml.PPD.ToString("F2"));
        sBuilder.AppendLine();
      }
      return sBuilder.ToString();
    }

    #endregion

    #region インナークラス定義

    /// <summary>計測情報</summary>
    public class MeasurementInfo
    {
      /// <summary>計測するか否かを取得する</summary>
      public bool Measure { get; internal set; } = true;

      /// <summary>計測時間間隔[sec]を取得する</summary>
      public int Interval { get; internal set; } = 60;

      /// <summary>最終の計測日時を取得する</summary>
      public DateTime LastMeasureTime { get; internal set; } = UNIX_EPOCH;

      /// <summary>最終の計測値を取得する</summary>
      public double LastValue { get; internal set; }

      /// <summary>補正式Ax+Bの補正係数Aを取得する</summary>
      public double CorrectionFactorA { get; internal set; } = 1.0;

      /// <summary>補正式Ax+Bの補正係数Bを取得する</summary>
      public double CorrectionFactorB { get; internal set; } = 0.0;
    }

    #endregion

  }
}
