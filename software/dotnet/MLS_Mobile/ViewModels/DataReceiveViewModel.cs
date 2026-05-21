using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MLLib.Protocol;
using MLS_Mobile.Services;
using Popolo.Core.ThermalComfort;
using Popolo.Core.Physics;

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

    /// <summary>アプリ全体で共有される現在値モデル (他 Tab がライブ入力として購読)</summary>
    private readonly ILiveMeasurementService _live;

    /// <summary>表示・CSV 用のファイル名 (LocalName 由来)</summary>
    private readonly string _baseName;

    /// <summary>
    /// 風速の Out-Of-Range 表示閾値 [m/s]。これを超えたら "OOR" 表記にする。
    /// v3 firmware は 0-1.5 m/s が物理測定範囲、v4 (poem_velocity_sensor.X) は校正により 5.0 m/s まで拡張。
    /// </summary>
    private readonly double _velocityOorThreshold;

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

    public DataReceiveViewModel(IMLProtocol protocol, ILiveMeasurementService live,
                                string baseName, double clo, double met, bool hasCo2)
    {
        _live     = live;
        _baseName = baseName;
        _cloValue = clo;
        _metValue = met;
        HasCO2LevelSensor = hasCo2;

        // v4 (protocol_version >= 1) は校正範囲 5.0 m/s まで、v3 は従来通り 1.5 m/s
        _velocityOorThreshold = protocol.Device.ProtocolVersion >= 1 ? 5.0 : 1.5;

        // 接続オープン通知 (TabBar バッジ等が反応する)
        _live.SetConnection(true, baseName);

        _samplesSub = System.ObservableExtensions.Subscribe(protocol.Samples, OnSample);
    }

    #endregion

    #region サンプル受信

    private void OnSample(Sample s)
    {
        _lastSample = s;
        var local = s.Timestamp.LocalDateTime;

        // [ObservableProperty] の自動 setter (= SetProperty 経由で個別 PropertyChanged 発火)
        // を使わず field を直接書き換える。OnSample 1 回で 12 個の通知が連鎖していたのを、
        // 末尾の OnPropertyChanged(string.Empty) で全 binding を 1 度だけ refresh させる形に
        // まとめる。これがサンプル受信時の UI カクつきの主要因。
        if (s.DrybulbTemperature is double dbt)
        {
            _drybulbTemperature   = FormatF(dbt, 1);
            _lastCommunicated_DBT = local;
        }
        if (s.RelativeHumidity is double rh)
        {
            _relativeHumdity      = FormatF(rh, 1);
            _lastCommunicated_HMD = local;
        }
        if (s.GlobeTemperature is double glb)
        {
            _globeTemperature     = FormatF(glb, 1);
            _lastCommunicated_GLB = local;
        }
        if (s.Velocity is double vel)
        {
            _velocity             = (_velocityOorThreshold < vel) ? "OOR" : FormatF(vel, 2);
            _lastCommunicated_VEL = local;
        }
        if (s.Illuminance is int ill)
        {
            _illuminance          = ill.ToString(CultureInfo.InvariantCulture);
            _lastCommunicated_ILL = local;
        }
        if (s.Co2 is int co2)
        {
            _cO2Level             = co2.ToString(CultureInfo.InvariantCulture);
            _lastCommunicated_CO2 = local;
        }

        // 共有モデル (内部で 1 回だけ PropertyChanged を発火)
        _live.UpdateFromSample(s);

        // 派生指標を field 直書き (個別通知なし)
        RecalcThermalIndicesNoNotify();

        // 1 度だけ全 binding refresh
        OnPropertyChanged(string.Empty);

        AppendCsvV4(s);
    }

    #endregion

    #region 熱的快適性

    /// <summary>
    /// OnSample 用: 派生指標を field 直書きして、PropertyChanged は個別に発火させない。
    /// 呼び出し側で <c>OnPropertyChanged(string.Empty)</c> をまとめて呼ぶこと。
    /// </summary>
    private void RecalcThermalIndicesNoNotify()
    {
        if (_lastSample is not Sample s) return;

        double dbt = Clamp(s.DrybulbTemperature ?? DEF_TEMP, -10, 40);
        double rhd = Clamp(s.RelativeHumidity ?? DEF_RH, 0, 100);
        double vel = Clamp(s.Velocity ?? DEF_VEL, 0, 2);
        double glb = Clamp(s.GlobeTemperature ?? DEF_GLB, -10, 50);

        double mrt = GetMRT(dbt, glb, vel);
        double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndRelativeHumidity(dbt, rhd, ATM);
        double set = GaggeModel.GetSETStarFromAmbientCondition(dbt, mrt, rhd, vel, CloValue, 58.15 * MetValue, 0);
        double pmv = FangerModel.GetPMV(dbt, mrt, rhd, vel, CloValue, MetValue, 0);
        double ppd = FangerModel.GetPPD(pmv);

        // グローブ温度の 150mm 換算 (JIS B7922, JIS Z8504)
        double glb150 = dbt + (1 + 1.13 * Math.Pow(GLOBE_DIAMETER, -0.4) * Math.Pow(vel, 0.6))
                              / (1 + 2.41 * Math.Pow(vel, 0.6)) * (glb - dbt);

        _meanRadiantTemperature = FormatF(mrt, 1);
        _pMV                    = FormatF(pmv, 2);
        _pPD                    = FormatF(ppd, 1);
        _sETStar                = FormatF(set, 1);
        _wBGT_Indoor            = FormatF(0.7 * wbt + 0.3 * glb150, 1);
        _wBGT_Outdoor           = FormatF(0.7 * wbt + 0.2 * glb150 + 0.1 * dbt, 1);
    }

    /// <summary>
    /// Clo/Met 変化時用: 計算 + 1 回の全 binding refresh。OnSample 経路と違ってサンプルは
    /// 来ていないので、empty 通知で派生指標 (MRT/PMV/PPD/SET*/WBGT) を一括更新する。
    /// </summary>
    private void RecalcThermalIndices()
    {
        RecalcThermalIndicesNoNotify();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// グローブ温度 + 乾球温度 + 気流速度から平均放射温度 [°C] を求める。
    /// ピンポン球 (φ38mm) を想定した強制対流補正付き。
    /// Thermal Comfort 画面 (Live モード) からも参照されるため public static で公開。
    /// </summary>
    public static double GetMRT(double tmp, double glb, double vel)
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

    /// <summary>
    /// CSV file I/O を OnSample (UI スレッド) から外すための排他。
    /// SemaphoreSlim(1,1) で書き込み順序を保証しつつ、Task.Run で実 I/O を背景に逃がす。
    /// これがないと毎サンプルで file open/write/close が UI スレッドを止め、
    /// スクロール中に値受信があると体感的にカクつく。
    /// </summary>
    private readonly SemaphoreSlim _csvLock = new(1, 1);

    private void AppendCsvV4(Sample s)
    {
        // 行内容の組み立ては UI スレッドで完結させる (Memo のスナップショットを撮るため)。
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

        string line     = sb.ToString();
        string fileName = _baseName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

        // 実 I/O は background に投げる (fire-and-forget)。SemaphoreSlim で順序保証。
        _ = AppendLineInBackgroundAsync(fileName, line);
    }

    private async Task AppendLineInBackgroundAsync(string fileName, string line)
    {
        await _csvLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => MLUtility.AppendData(fileName, line)).ConfigureAwait(false);
        }
        finally
        {
            _csvLock.Release();
        }
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
        _live.SetConnection(false, null);
    }
}
