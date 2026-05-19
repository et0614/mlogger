using System;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using MLLib;
using MLLib.Protocol;
using Popolo.HumanBody;
using Popolo.ThermophysicalProperty;

namespace MLS_Mobile.ViewModels;

/// <summary>
/// DataReceive 画面の ViewModel。IMLProtocol.Samples ストリームから計測サンプルを
/// 受け取り、表示値・熱的快適性指標 (MRT/PMV/PPD/SET*/WBGT) を更新し、CSV へ追記する。
/// v3 ハードは LegacyV3Protocol が DTT 行を Sample に変換するため同じ経路で動く。
/// </summary>
public sealed partial class DataReceiveViewModel : ObservableObject, IDisposable
{
    #region 定数

    /// <summary>大気圧[kPa]</summary>
    private const double ATM = 101.325;

    /// <summary>グローブ温度計の直径[m]</summary>
    private const double GLOBE_DIAMETER = 0.038;

    /// <summary>計測値が無い場合のデフォルト値</summary>
    private const double DEF_TEMP = 25, DEF_RH = 50, DEF_VEL = 0.1, DEF_GLB = 25;

    #endregion

    #region 依存先

    private readonly IDisposable _samplesSub;

    /// <summary>表示・CSV 用のファイル名 (LocalName 由来)</summary>
    private readonly string _baseName;

    /// <summary>最後に受け取ったサンプル (Clo/Met 変更時の再計算用)</summary>
    private Sample? _lastSample;

    #endregion

    #region 計測値の表示プロパティ

    [ObservableProperty] private string _drybulbTemperature = "";
    [ObservableProperty] private string _relativeHumdity = "";  // XAML 既存 typo に追従
    [ObservableProperty] private string _globeTemperature = "";
    [ObservableProperty] private string _velocity = "";
    [ObservableProperty] private string _illuminance = "";
    [ObservableProperty] private string _cO2Level = "";

    [ObservableProperty] private DateTime _lastCommunicated_DBT;
    [ObservableProperty] private DateTime _lastCommunicated_HMD;
    [ObservableProperty] private DateTime _lastCommunicated_GLB;
    [ObservableProperty] private DateTime _lastCommunicated_VEL;
    [ObservableProperty] private DateTime _lastCommunicated_ILL;
    [ObservableProperty] private DateTime _lastCommunicated_CO2;

    [ObservableProperty] private bool _hasCO2LevelSensor;

    #endregion

    #region 演算値の表示プロパティ

    [ObservableProperty] private string _meanRadiantTemperature = "";
    [ObservableProperty] private string _pMV = "";
    [ObservableProperty] private string _pPD = "";
    [ObservableProperty] private string _sETStar = "";
    [ObservableProperty] private string _wBGT_Outdoor = "";
    [ObservableProperty] private string _wBGT_Indoor = "";

    #endregion

    #region Clo / Met (双方向)

    [ObservableProperty] private double _cloValue = 1.0;
    [ObservableProperty] private double _metValue = 1.1;

    partial void OnCloValueChanged(double value) => RecalcThermalIndices();

    partial void OnMetValueChanged(double value) => RecalcThermalIndices();

    #endregion

    #region コンストラクタ

    public DataReceiveViewModel(IMLProtocol protocol, string baseName, double clo, double met, bool hasCo2)
    {
        _baseName = baseName;
        _cloValue = clo;
        _metValue = met;
        HasCO2LevelSensor = hasCo2;

        _samplesSub = System.ObservableExtensions.Subscribe(protocol.Samples, OnSample);
    }

    #endregion

    #region サンプル受信

    private void OnSample(Sample s)
    {
        _lastSample = s;
        var local = s.Timestamp.LocalDateTime;

        if (s.DrybulbTemperature is double dbt)
        {
            DrybulbTemperature = FormatF(dbt, 1);
            LastCommunicated_DBT = local;
        }
        if (s.RelativeHumidity is double rh)
        {
            RelativeHumdity = FormatF(rh, 1);
            LastCommunicated_HMD = local;
        }
        if (s.GlobeTemperature is double glb)
        {
            GlobeTemperature = FormatF(glb, 1);
            LastCommunicated_GLB = local;
        }
        if (s.Velocity is double vel)
        {
            Velocity = (2.00 < vel) ? "OOR" : FormatF(vel, 2);
            LastCommunicated_VEL = local;
        }
        if (s.Illuminance is int ill)
        {
            Illuminance = ill.ToString(CultureInfo.InvariantCulture);
            LastCommunicated_ILL = local;
        }
        if (s.Co2 is int co2)
        {
            CO2Level = co2.ToString(CultureInfo.InvariantCulture);
            LastCommunicated_CO2 = local;
        }

        RecalcThermalIndices();
        AppendCsvV4(s);
    }

    #endregion

    #region 熱的快適性

    /// <summary>最新サンプル + 現在の Clo/Met から MRT/PMV/PPD/SET*/WBGT を更新する</summary>
    private void RecalcThermalIndices()
    {
        if (_lastSample is not Sample s) return;

        double dbt = Clamp(s.DrybulbTemperature ?? DEF_TEMP, -10, 40);
        double rhd = Clamp(s.RelativeHumidity ?? DEF_RH, 0, 100);
        double vel = Clamp(s.Velocity ?? DEF_VEL, 0, 2);
        double glb = Clamp(s.GlobeTemperature ?? DEF_GLB, -10, 50);

        double mrt = GetMRT(dbt, glb, vel);
        double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndRelativeHumidity(dbt, rhd, ATM);
        double set = TwoNodeModel.GetSETStarFromAmbientCondition(dbt, mrt, rhd, vel, CloValue, 58.15 * MetValue, 0);
        double pmv = ThermalComfort.GetPMV(dbt, mrt, rhd, vel, CloValue, MetValue, 0);
        double ppd = ThermalComfort.GetPPD(pmv);

        // グローブ温度の 150mm 換算 (JIS B7922, JIS Z8504)
        double glb150 = dbt + (1 + 1.13 * Math.Pow(GLOBE_DIAMETER, -0.4) * Math.Pow(vel, 0.6))
                              / (1 + 2.41 * Math.Pow(vel, 0.6)) * (glb - dbt);

        MeanRadiantTemperature = FormatF(mrt, 1);
        PMV = FormatF(pmv, 2);
        PPD = FormatF(ppd, 1);
        SETStar = FormatF(set, 1);
        WBGT_Indoor = FormatF(0.7 * wbt + 0.3 * glb150, 1);
        WBGT_Outdoor = FormatF(0.7 * wbt + 0.2 * glb150 + 0.1 * dbt, 1);
    }

    private static double GetMRT(double tmp, double glb, double vel)
    {
        const double EPS = 0.95; // ピンポン球の放射率
        const double SIG = 5.67e-8;
        const double ES = EPS * SIG;

        double hc1 = 1.4 * Math.Pow(Math.Abs(tmp - glb) / GLOBE_DIAMETER, 0.25);
        double hc2 = 6.3 * Math.Pow(vel, 0.6) / Math.Pow(GLOBE_DIAMETER, 0.4);
        double glbK = glb + 273.15;
        return Math.Pow(Math.Max(0, Math.Pow(glbK, 4) + Math.Max(hc1, hc2) / ES * (glb - tmp)), 0.25) - 273.15;
    }

    #endregion

    #region CSV 保存

    /// <summary>外部から設定可能なメモ (XAML Entry と双方向バインド)</summary>
    [ObservableProperty] private string _memo = "";

    private void AppendCsvV4(Sample s)
    {
        string memo = SanitizeMemo(Memo);
        var t = s.Timestamp.LocalDateTime;
        var sb = new StringBuilder();
        sb.Append(t.ToString("yyyy/M/d,HH:mm:ss")).Append(',');
        sb.Append(FormatOrNA(s.DrybulbTemperature, "F1")).Append(',');
        sb.Append(FormatOrNA(s.RelativeHumidity, "F1")).Append(',');
        sb.Append(FormatOrNA(s.GlobeTemperature, "F2")).Append(',');
        sb.Append(FormatOrNA(s.Velocity, "F3")).Append(',');
        sb.Append(FormatOrNA(s.Illuminance, "F2")).Append(',');
        // v4 Sample に電圧フィールドは無いため n/a で埋める (将来必要なら別途追加)
        sb.Append("n/a,n/a,");
        sb.Append(FormatOrNA(s.Co2, "F0")).Append(',');
        sb.Append(memo).Append(Environment.NewLine);

        AppendToFile(sb.ToString());
    }

    private void AppendToFile(string line)
    {
        string fileName = _baseName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
        MLUtility.AppendData(fileName, line);
    }

    private static string SanitizeMemo(string? memo)
        => (memo ?? "").Replace("\r", "").Replace("\n", "").Replace(",", "");

    #endregion

    #region ヘルパ

    private static string FormatF(double value, int digits)
        => double.IsNaN(value) ? "" : value.ToString("F" + digits, CultureInfo.InvariantCulture);

    private static string FormatOrNA(double? v, string fmt)
        => v.HasValue ? v.Value.ToString(fmt, CultureInfo.InvariantCulture) : "n/a";

    private static string FormatOrNA(int? v, string fmt)
        => v.HasValue ? v.Value.ToString(fmt, CultureInfo.InvariantCulture) : "n/a";

    private static double Clamp(double v, double lo, double hi)
        => Math.Max(lo, Math.Min(hi, v));

    #endregion

    public void Dispose()
    {
        _samplesSub?.Dispose();
    }
}
