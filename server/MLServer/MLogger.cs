using System;

using Popolo.HumanBody;
using System.Text;

namespace MLServer
{

  public class MLogger
  {

    #region 定数宣言

    /// <summary>UNIX時間起点</summary>
    private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0);

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>受信データ</summary>
    private string receivedData = "";

    /// <summary>名称を設定・取得する</summary>
    public string Name { get; set; }

    /// <summary>初回の保存か否か</summary>
    public bool IsFirstSave { get; set; } = true;

    /// <summary>LongAddress（16進数）を設定・取得する</summary>
    public string LongAddress { get; private set; }

    /// <summary>LongAddressの下位アドレス（16進数）を取得する</summary>
    public string LowAddress { get { return LongAddress.Substring(8); } }

    /// <summary>受信コマンドがあるか否か</summary>
    public bool HasCommand { get { return receivedData.Contains("\r"); } }

    /// <summary>最後の通信日時を設定・取得する</summary>
    public DateTime LastCommunication { get; set; }

    /// <summary>バージョン（メジャー）を取得する</summary>
    public int Version_Major { get; private set; }

    /// <summary>バージョン（マイナー）を取得する</summary>
    public int Version_Minor { get; private set; }

    /// <summary>バージョン（リビジョン）を取得する</summary>
    public int Version_Revision { get; private set; }

    #endregion

    #region 計測値関連のプロパティ

    /// <summary>最新の温湿度計測日時を取得する</summary>
    public DateTime LastMeasureTime_TH { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の放射温度計測日時を取得する</summary>
    public DateTime LastMeasureTime_Glb { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の微風速計測日時を取得する</summary>
    public DateTime LastMeasureTime_Vel { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の照度計測日時を取得する</summary>
    public DateTime LastMeasureTime_Ill { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の汎用電圧1計測日時を取得する</summary>
    public DateTime LastMeasureTime_GV1 { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の汎用電圧2計測日時を取得する</summary>
    public DateTime LastMeasureTime_GV2 { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の汎用電圧3計測日時を取得する</summary>
    public DateTime LastMeasureTime_GV3 { get; private set; } = UNIX_EPOCH;

    /// <summary>最新の温度[C]を取得する</summary>
    public double LastTemperature { get; private set; } = double.NaN;

    /// <summary>最新の相対湿度[%]を取得する</summary>
    public double LastRelativeHumidity { get; private set; } = double.NaN;

    /// <summary>最新のグローブ温度[C]を取得する</summary>
    public double LastGlobeTemperature { get; private set; } = double.NaN;

    /// <summary>最新の微風速[m/s]を取得する</summary>
    public double LastVelocity { get; private set; } = double.NaN;

    /// <summary>最新の照度[lx]を取得する</summary>
    public double LastIlluminance { get; private set; } = double.NaN;

    /// <summary>最新の汎用電圧1[V]を取得する</summary>
    public double LastIGeneralPurposeVoltage1 { get; private set; } = double.NaN;

    /// <summary>最新の汎用電圧2[V]を取得する</summary>
    public double LastIGeneralPurposeVoltage2 { get; private set; } = double.NaN;

    /// <summary>最新の汎用電圧3[V]を取得する</summary>
    public double LastIGeneralPurposeVoltage3 { get; private set; } = double.NaN;

    /// <summary>温度計測値の補正式Ax+Bの補正係数Aを取得する</summary>
    public double CFactorA_Temperature { get; private set; } = 1.0;

    /// <summary>温度計測値の補正式Ax+Bの補正係数Bを取得する</summary>
    public double CFactorB_Temperature { get; private set; } = 0.0;

    /// <summary>相対湿度計測値の補正式Ax+Bの補正係数Aを取得する</summary>
    public double CFactorA_RHumidity { get; private set; } = 1.0;

    /// <summary>相対湿度計測値の補正式Ax+Bの補正係数Bを取得する</summary>
    public double CFactorB_RHumidity { get; private set; } = 0.0;

    /// <summary>グローブ温度計測値の補正式Ax+Bの補正係数Aを取得する</summary>
    public double CFactorA_Globe { get; private set; } = 1.0;

    /// <summary>グローブ温度計測値の補正式Ax+Bの補正係数Bを取得する</summary>
    public double CFactorB_Globe { get; private set; } = 0.0;

    /// <summary>微風速の無風時の電圧[V]を取得する</summary>
    public double MinVoltage_Velocity { get; private set; } = 1.45;

    /// <summary>微風速計測値の補正式Ax+Bの補正係数Aを取得する</summary>
    public double CFactorA_Velocity { get; private set; } = 1.0;

    /// <summary>微風速計測値の補正式Ax+Bの補正係数Bを取得する</summary>
    public double CFactorB_Velocity { get; private set; } = 0.0;

    /// <summary>照度計測値の補正式Ax+Bの補正係数Aを取得する</summary>
    public double CFactorA_Illuminance { get; private set; } = 1.0;

    /// <summary>照度計測値の補正式Ax+Bの補正係数Bを取得する</summary>
    public double CFactorB_Illuminance { get; private set; } = 0.0;

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

    public MLogger(string longAddress)
    {
      LongAddress = longAddress;
      Name = LowAddress;
    }

    public void InitCFactors(string initString)
    {
      string[] bf = initString.Split(',');
      Name = bf[0];
      CFactorA_Temperature = double.Parse(bf[1]);
      CFactorB_Temperature = double.Parse(bf[2]);
      CFactorA_RHumidity = double.Parse(bf[3]);
      CFactorB_RHumidity = double.Parse(bf[4]);
      CFactorA_Globe = double.Parse(bf[5]);
      CFactorB_Globe = double.Parse(bf[6]);
      MinVoltage_Velocity = double.Parse(bf[7]);
      CFactorA_Velocity = double.Parse(bf[8]);
      CFactorB_Velocity = double.Parse(bf[9]);
      CFactorA_Illuminance = double.Parse(bf[10]);
      CFactorB_Illuminance = double.Parse(bf[11]);
    }

    public string MakeCFString()
    {
      return Name +
        "," + CFactorA_Temperature.ToString("F4") + "," + CFactorB_Temperature.ToString("F4") +
        "," + CFactorA_RHumidity.ToString("F4") + "," + CFactorB_RHumidity.ToString("F4") +
        "," + CFactorA_Globe.ToString("F4") + "," + CFactorB_Globe.ToString("F4") +
        "," + MinVoltage_Velocity.ToString("F4") + "," + CFactorA_Velocity.ToString("F4") + "," + CFactorB_Velocity.ToString("F4") +
        "," + CFactorA_Illuminance.ToString("F4") + "," + CFactorB_Illuminance.ToString("F4");
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>受信データを追加する</summary>
    /// <param name="data"></param>
    public void AddReceivedData(string data)
    { receivedData += data; }

    /// <summary>バージョンを設定する</summary>
    /// <param name="version">バージョンを表す文字列(x.x.x)</param>
    public void SetVersion(string version)
    {
      string[] vers = version.Split('.');
      Version_Major = int.Parse(vers[0]);
      Version_Minor = int.Parse(vers[1]);
      if (3 <= vers.Length)
        Version_Revision = int.Parse(vers[2]);
    }

    /// <summary>コマンドを受け取る</summary>
    /// <returns>コマンド</returns>
    public string GetCommand()
    {
      if (!receivedData.Contains("\r")) return null;
      return receivedData.Substring(0, receivedData.IndexOf('\r'));
    }

    /// <summary>コマンドを1つ削除する</summary>
    public void RemoveCommand()
    {
      if (receivedData.Contains("\r"))
        receivedData = receivedData.Remove(0, receivedData.IndexOf('\r') + 1);
    }

    /// <summary>コマンドを全消去する</summary>
    public void ClearCommand()
    {
      receivedData = "";
    }

    public void SolveDTT(string dtt, out DateTime now,
      out double temperature, out double humidity,
      out double globeV, out double globe,
      out double velocityV, out double velocity,
      out double illuminance,
      out double gpVoltage1, out double gpVoltage2, out double gpVoltage3)
    {
      try
      {
        string[] buff = dtt.Substring(4).Split(',');
        string[] mmdd = buff[1].Split('/');
        string[] hhMMss = buff[2].Split(':');
        now = new DateTime(
          int.Parse(buff[0]), int.Parse(mmdd[0]), int.Parse(mmdd[1]),
          int.Parse(hhMMss[0]), int.Parse(hhMMss[1]), int.Parse(hhMMss[2]), DateTimeKind.Local);
        temperature = (buff[3] == "n/a") ? double.NaN : double.Parse(buff[3]);
        humidity = (buff[4] == "n/a") ? double.NaN : double.Parse(buff[4]);
        globe = (buff[5] == "n/a") ? double.NaN : double.Parse(buff[5]);
        velocity = (buff[6] == "n/a") ? double.NaN : double.Parse(buff[6]);
        illuminance = (buff[7] == "n/a") ? double.NaN : double.Parse(buff[7]);
        globeV = (buff[8] == "n/a") ? double.NaN : double.Parse(buff[8]);
        velocityV = (buff[9] == "n/a") ? double.NaN : double.Parse(buff[9]);
        gpVoltage1 = (buff[10] == "n/a") ? double.NaN : double.Parse(buff[10]);
        gpVoltage2 = (buff[11] == "n/a") ? double.NaN : double.Parse(buff[11]);
        gpVoltage3 = (buff[12] == "n/a") ? double.NaN : double.Parse(buff[12]);

        //最新の値を保存
        if (!double.IsNaN(temperature))
        {
          LastMeasureTime_TH = now;
          LastTemperature = temperature;
          LastRelativeHumidity = humidity;
        }
        if (!double.IsNaN(globe))
        {
          LastMeasureTime_Glb = now;
          LastGlobeTemperature = globe;
        }
        if (!double.IsNaN(velocity))
        {
          LastMeasureTime_Vel = now;
          LastVelocity = velocity;
        }
        if (!double.IsNaN(illuminance))
        {
          LastMeasureTime_Ill = now;
          LastIlluminance = illuminance;
        }
        if (!double.IsNaN(gpVoltage1))
        {
          LastMeasureTime_GV1 = now;
          LastIGeneralPurposeVoltage1 = gpVoltage1;
        }
        if (!double.IsNaN(gpVoltage2))
        {
          LastMeasureTime_GV2 = now;
          LastIGeneralPurposeVoltage2 = gpVoltage2;
        }
        if (!double.IsNaN(gpVoltage3))
        {
          LastMeasureTime_GV3 = now;
          LastIGeneralPurposeVoltage3 = gpVoltage3;
        }

        //熱的快適性指標を計算する
        calcThermalIndices();
      }
      //通信の問題で不正な文字が送信されるような場合に備える
      catch
      {
        now = UNIX_EPOCH;
        temperature = humidity = globeV = globe = velocityV = velocity = illuminance 
          = gpVoltage1 = gpVoltage2 = gpVoltage3 = double.NaN;
      }
    }

    /// <summary>補正係数を読み込む</summary>
    /// <param name="command"></param>
    public void LoadCFactors(string command) 
    {
      string[] buff = command.Substring(4).Split(',');

      if (!double.TryParse(buff[0], out double dbtA)) dbtA = 1.0;
      CFactorA_Temperature = dbtA;

      if (!double.TryParse(buff[1], out double dbtB)) dbtB = 0.0;
      CFactorB_Temperature = dbtB;

      if (!double.TryParse(buff[2], out double hmdA)) hmdA = 1.0;
      CFactorA_RHumidity= hmdA;

      if (!double.TryParse(buff[3], out double hmdB)) hmdB = 0.0;
      CFactorB_RHumidity = hmdB;

      if (!double.TryParse(buff[4], out double glbA)) glbA = 1.0;
      CFactorA_Globe = glbA;

      if (!double.TryParse(buff[5], out double glbB)) glbB = 0.0;
      CFactorB_Globe = glbB;

      if (!double.TryParse(buff[6], out double luxA)) luxA = 1.0;
      CFactorA_Illuminance = luxA;

      if (!double.TryParse(buff[7], out double luxB)) luxB = 0.0;
      CFactorB_Illuminance = luxB;

      if (!double.TryParse(buff[8], out double velA)) velA = 1.0;
      CFactorA_Velocity = velA;

      if (!double.TryParse(buff[9], out double velB)) velB = 0.0;
      CFactorB_Velocity = velB;

      if (!double.TryParse(buff[10], out double vel0)) vel0 = 1.45;
      MinVoltage_Velocity = vel0;

    }

    /// <summary>補正係数設定コマンドを作成する</summary>
    /// <returns>補正係数設定コマンド</returns>
    public string MakeSCFCommand()
    {
      return MakeSCFCommand(
        CFactorA_Temperature, CFactorB_Temperature,
        CFactorA_RHumidity, CFactorB_RHumidity,
        CFactorA_Globe, CFactorB_Globe,
        CFactorA_Illuminance, CFactorB_Illuminance,
        CFactorA_Velocity, CFactorB_Velocity, MinVoltage_Velocity
        );
    }

    public static string MakeSCFCommand
      (double dbtA, double dbtB, double hmdA, double hmdB, 
      double glbA, double glbB, double luxA, double luxB, double velA, double velB, double vel0)
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

    private void calcThermalIndices()
    {
      if (CalcThermalIndices)
      {
        double dbt = double.IsNaN(LastTemperature) ? DefaultTemperature : Math.Max(-10, Math.Min(40, LastTemperature));
        double rhd = double.IsNaN(LastRelativeHumidity) ? DefaultRelativeHumidity : Math.Max(0, Math.Min(100, LastRelativeHumidity));
        double vel = double.IsNaN(LastVelocity) ? DefaultVelocity : Math.Max(0, Math.Min(2, LastVelocity));
        double glb = double.IsNaN(LastGlobeTemperature) ? DefaultGlobeTemperature : Math.Max(-10, Math.Min(50, LastGlobeTemperature));

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

    public void GetThermalIndices (
      double met, double clo, double dbt, double rhd, double vel, double mrt,
      out double setstar, out double pmv, out double ppd)
    {
      dbt = double.IsNaN(LastTemperature) ? dbt : Math.Max(-10, Math.Min(50, LastTemperature));
      rhd = double.IsNaN(LastRelativeHumidity) ? rhd : Math.Max(0, Math.Min(100, LastRelativeHumidity));
      vel = double.IsNaN(LastVelocity) ? vel : Math.Max(0, Math.Min(10, LastVelocity));
      mrt = double.IsNaN(LastGlobeTemperature) ? mrt : Math.Max(-10, Math.Min(50, LastGlobeTemperature));

      setstar = TwoNodeModel.GetSETStarFromAmbientCondition
        (dbt, mrt, rhd, vel, clo, 58.15 * met, 0);
      pmv = ThermalComfort.GetPMV(dbt, mrt, rhd, vel, clo, met, 0);
      ppd = ThermalComfort.GetPPD(pmv);
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
      //return UNIX_EPOCH.AddSeconds(unixTime).ToLocalTime(); 
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

    public static string MakeListHTML(
      string baseHTML, MLogger[] mLoggers,
      double metValue, double cloValue, double dbtValue, double rhdValue, double velValue, double mrtValue)
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
        contents.AppendLine("<td class=\"dt_last general\">" + ml.LastCommunication.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"name general\">" + ml.Name + "</td>");
        contents.AppendLine("<td class=\"id general\">" + ml.LowAddress + "</td>");
        //温湿度
        contents.AppendLine("<td class=\"dt_th thlog\">" + ml.LastMeasureTime_TH.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"tmp thlog\">" + ml.LastTemperature.ToString("F1") + "</td>");
        contents.AppendLine("<td class=\"hmd thlog\">" + ml.LastRelativeHumidity.ToString("F1") + "</td>");
        //グローブ温度
        contents.AppendLine("<td class=\"dt_glb glblog\">" + ml.LastMeasureTime_Glb.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"glb glblog\">" + ml.LastGlobeTemperature.ToString("F2") + "</td>");
        //微風速
        contents.AppendLine("<td class=\"dt_vel vellog\">" + ml.LastMeasureTime_Vel.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"vel vellog\">" + (ml.LastVelocity * 100).ToString("F1") + "</td>");
        //照度
        contents.AppendLine("<td class=\"dt_ill illlog\">" + ml.LastMeasureTime_Ill.ToString("M/d HH:mm:ss") + "</td>");
        contents.AppendLine("<td class=\"ill illlog\">" + ml.LastIlluminance.ToString("F2") + "</td>");
        //熱的快適性
        ml.GetThermalIndices(metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue, out double setstar, out double pmv, out double ppd);
        contents.AppendLine("<td class=\"cmft_set cmftlog\">" + setstar.ToString("F1") + "</td>");
        contents.AppendLine("<td class=\"cmft_pmv cmftlog\">" + pmv.ToString("F2") + "</td>");
        contents.AppendLine("<td class=\"cmft_ppd cmftlog\">" + ppd.ToString("F1") + "</td>");
        //データリンク先
        contents.AppendLine("<td class=\"general\"><a href=\"" + ml.LowAddress + ".csv\">" + ml.LowAddress + ".csv</a></td>");
        contents.AppendLine("</tr>");
      }
      baseHTML = baseHTML.Replace("<!--ML_CONTENTS-->", contents.ToString());

      return baseHTML;
    }

    public static string MakeLatestData(
      MLogger[] mLoggers,
      double metValue, double cloValue, double dbtValue, double rhdValue, double velValue, double mrtValue)
    {
      StringBuilder sBuilder = new StringBuilder();
      for (int i = 0; i < mLoggers.Length; i++)
      {
        MLogger ml = mLoggers[i];
        sBuilder.Append(ml.Name);
        //温湿度
        sBuilder.Append("," + ml.LastMeasureTime_TH.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.LastTemperature.ToString("F1"));
        sBuilder.Append("," + ml.LastMeasureTime_TH.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.LastRelativeHumidity.ToString("F1"));
        //グローブ温度
        sBuilder.Append("," + ml.LastMeasureTime_Glb.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.LastGlobeTemperature.ToString("F2"));
        //微風速
        sBuilder.Append("," + ml.LastMeasureTime_Vel.ToString("yyyy/MM/dd HH:mm:ss") + "," + (ml.LastVelocity * 100).ToString("F1"));
        //照度
        sBuilder.Append("," + ml.LastMeasureTime_Ill.ToString("yyyy/MM/dd HH:mm:ss") + "," + ml.LastIlluminance.ToString("F2"));
        //SET*
        ml.GetThermalIndices(metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue, out double setstar, out double pmv, out double ppd);
        sBuilder.Append("," + setstar.ToString("F2"));
        sBuilder.Append("," + pmv.ToString("F2"));
        sBuilder.Append("," + ppd.ToString("F2"));
        sBuilder.AppendLine();
      }
      return sBuilder.ToString();
    }

    #endregion

  }
}
