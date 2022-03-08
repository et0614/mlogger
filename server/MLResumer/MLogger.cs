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

    /// <summary>名称を取得する</summary>
    public string Name { get; private set; }

    /// <summary>初回の保存か否か</summary>
    public bool IsFirstSave { get; set; } = true;

    /// <summary>LongAddress（10進数）を設定・取得する</summary>
    public ulong LongAddress { get; private set; }

    /// <summary>LongAddressの下位アドレス（16進数）を取得する</summary>
    public string LowAddress { get { return GetLowAddress(LongAddress); } }

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

    #region コンストラクタ

    public MLogger(ulong longAddress)
    {
      LongAddress = longAddress;
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
      if(3 <= vers.Length)
        Version_Revision = int.Parse(vers[2]);
    }

    /// <summary>コマンドを受け取る</summary>
    /// <returns>コマンド</returns>
    public string GetCommand()
    {
      if (!receivedData.Contains("\r")) return "";
      return receivedData.Substring(0, receivedData.IndexOf('\r'));
    }

    /// <summary>コマンドを1つ削除する</summary>
    public void RemoveCommand()
    {
      if (receivedData.Contains("\r"))
        receivedData = receivedData.Remove(0, receivedData.IndexOf('\r') + 1);
    }

    public void SolveDTT(string dtt, out DateTime now,
      out double temperature, out double humidity,
      out double globeV, out double globe,
      out double velocityV, out double velocity,
      out double illuminance)
    {
      try
      {
        string[] buff = dtt.Substring(4).Split(',');
        now = GetDateTimeFromUTime(long.Parse(buff[0]));
        temperature = buff[1] == "n/a" ? double.NaN : 0.1 * int.Parse(buff[1]);
        humidity = buff[2] == "n/a" ? double.NaN : 0.1 * int.Parse(buff[2]);
        globeV = buff[3] == "n/a" ? double.NaN : double.Parse(buff[3]);
        velocityV = buff[4] == "n/a" ? double.NaN : double.Parse(buff[4]);
        illuminance = buff[5] == "n/a" ? double.NaN : double.Parse(buff[5]);

        //電圧を状態値に変換
        globe = (globeV - 0.4) / 0.0195; //MCP9701
        //velVの変換処理
        double bff = Math.Max(0, velocityV / MinVoltage_Velocity - 1.0);
        velocity = bff * (2.3595 + bff * (-12.029 + bff * 79.744));

        //補正
        temperature = CFactorA_Temperature * temperature + CFactorB_Temperature;
        humidity = CFactorA_RHumidity * humidity + CFactorB_RHumidity;
        globe = CFactorA_Globe * globe + CFactorB_Globe;
        velocity = CFactorA_Velocity * velocity + CFactorB_Velocity;
        illuminance = CFactorA_Illuminance * illuminance + CFactorB_Illuminance;

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
      }
      //通信の問題で不正な文字が送信されるような場合に備える
      catch
      {
        now = UNIX_EPOCH;
        temperature = humidity = globeV = globe = velocityV = velocity = illuminance = double.NaN;
      }
    }

    public void GetThermalIndecies(
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
    public static long GetUnixTime(DateTime dTime)
    { return (long)(dTime.ToUniversalTime() - UNIX_EPOCH).TotalSeconds; }

    /// <summary>UNIX時間から日時を求める</summary>
    /// <param name="unixTime">UNIX時間</param>
    /// <returns>日時</returns>
    public static DateTime GetDateTimeFromUTime(long unixTime)
    { return UNIX_EPOCH.AddSeconds(unixTime).ToLocalTime(); }

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
        ml.GetThermalIndecies(metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue, out double setstar, out double pmv, out double ppd);
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
        ml.GetThermalIndecies(metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue, out double setstar, out double pmv, out double ppd);
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
