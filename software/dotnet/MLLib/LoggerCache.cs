using System;
using MLLib.Protocol;
using Popolo.HumanBody;
using Popolo.ThermophysicalProperty;

namespace MLLib;

/// <summary>
/// <see cref="IMLProtocol.Samples"/> 経由で受け取ったセンサ値をキャッシュするクラス。
/// 旧 <see cref="MLogger"/> と互換の <see cref="ImmutableMLogger"/> インタフェースを
/// 実装するため、CSV/JSON/BACnet の出力経路はそのまま動く。
///
/// v3 (LegacyV3Protocol) も v4 (JsonRpcV4Protocol) も同じ <see cref="Sample"/> ストリームを
/// 出すので、protocol 種別によらずこの 1 クラスで扱える。
/// </summary>
public sealed class LoggerCache : ImmutableMLogger
{
    private const double ATM = 101.325;
    private const double GLOBE_DIAMETER = 0.038;

    public LoggerCache(string longAddress, string lowAddress)
    {
        LongAddress = longAddress;
        LowAddress = lowAddress;
    }

    // --- IDs / 名称 ---
    public string LongAddress { get; }
    public string LowAddress { get; }
    public string LocalName { get; set; } = "Unloaded";
    public string XBeeName { get; set; } = "Unloaded";
    public string Name { get; set; } = "Unloaded";
    public bool IsFirstSave => false;
    public DateTime LastCommunicated { get; private set; } = DateTime.MinValue;
    public DateTime LastMeasured { get; private set; } = DateTime.MinValue;
    public int Version_Major { get; set; }
    public int Version_Minor { get; set; }
    public int Version_Revision { get; set; }
    public MLogger.Status CurrentStatus { get; set; } = MLogger.Status.WaitingForCommand;
    public DateTime StartMeasuringDateTime { get; set; }
    public bool MeasuringSettingLoaded { get; set; }
    public bool VersionLoaded { get; set; }
    public int VelocityCalibrationTime => 0;
    public int TemperatureCalibrationTime => 0;
    public double VelocityMinVoltage { get; set; }
    public double VelocityCharacteristicsCoefA { get; set; }
    public double VelocityCharacteristicsCoefB { get; set; }
    public double VelocityCharacteristicsCoefC { get; set; }
    public bool HasCO2LevelSensor { get; set; }
    public int CO2CalibrationTime => 0;

    // --- 計測値 ---
    public MLogger.MeasurementInfo DrybulbTemperature { get; } = new();
    public MLogger.MeasurementInfo RelativeHumdity   { get; } = new();
    public MLogger.MeasurementInfo GlobeTemperature  { get; } = new();
    public double GlobeTemperatureVoltage { get; private set; }
    public MLogger.MeasurementInfo Velocity          { get; } = new();
    public double VelocityVoltage { get; private set; }
    public MLogger.MeasurementInfo Illuminance       { get; } = new();
    public MLogger.MeasurementInfo GeneralVoltage1   { get; } = new();
    public MLogger.MeasurementInfo GeneralVoltage2   { get; } = new();
    public MLogger.MeasurementInfo GeneralVoltage3   { get; } = new();
    public MLogger.MeasurementInfo CO2Level          { get; } = new();
    public bool MeasureProximity => false;

    // --- 熱的快適性入力 ---
    public bool CalcThermalIndices { get; set; } = true;
    public double MetValue { get; set; } = 1.1;
    public double CloValue { get; set; } = 1.0;
    public double DefaultTemperature { get; set; } = 25.0;
    public double DefaultRelativeHumidity { get; set; } = 50.0;
    public double DefaultVelocity { get; set; } = 0.1;
    public double DefaultGlobeTemperature { get; set; } = 25.0;

    // --- 熱的快適性出力 ---
    public double MeanRadiantTemperature { get; private set; } = double.NaN;
    public double PMV { get; private set; } = double.NaN;
    public double PPD { get; private set; } = double.NaN;
    public double SETStar { get; private set; } = double.NaN;
    public double WBGT_Outdoor { get; private set; } = double.NaN;
    public double WBGT_Indoor { get; private set; } = double.NaN;

    // --- ImmutableMLogger 互換: events と HasCommand/NextCommand (LoggerCache では未使用) ---
#pragma warning disable CS0067 // インタフェース整合のための宣言、LoggerCache 内では fire しないので未使用警告を抑制
    public event EventHandler? DataReceivedEvent;
    public event EventHandler? WaitingForCommandMessageReceivedEvent;
    public event EventHandler? MeasuredValueReceivedEvent;
    public event EventHandler? MeasurementSettingReceivedEvent;
    public event EventHandler? VersionReceivedEvent;
    public event EventHandler? CorrectionFactorsReceivedEvent;
    public event EventHandler? StartMeasuringMessageReceivedEvent;
    public event EventHandler? EndMeasuringMessageReceivedEvent;
    public event EventHandler? LoggerNameReceivedEvent;
    public event EventHandler? CalibratingVoltageReceivedEvent;
    public event EventHandler? EndCalibratingVoltageMessageReceivedEvent;
    public event EventHandler? VelocityAutoCalibrationReceivedEvent;
    public event EventHandler? TemperatureAutoCalibrationReceivedEvent;
    public event EventHandler? HasCO2LevelSensorReceivedEvent;
    public event EventHandler? CalibratingCO2LevelReceivedEvent;
    public event EventHandler? UpdateCurrentTimeReceivedEvent;
#pragma warning restore CS0067
    public bool HasCommand => false;
    public string NextCommand => "";

    /// <summary>IMLProtocol.Samples 経由で受信した計測値を反映する。</summary>
    public void ApplySample(Sample s)
    {
        var local = s.Timestamp.LocalDateTime;
        LastCommunicated = local;
        LastMeasured = local;

        if (s.DrybulbTemperature is double dbt) Set(DrybulbTemperature, dbt, local);
        if (s.RelativeHumidity   is double rh)  Set(RelativeHumdity,   rh,  local);
        if (s.GlobeTemperature   is double glb) Set(GlobeTemperature,  glb, local);
        if (s.Velocity           is double vel) Set(Velocity,          vel, local);
        if (s.Illuminance        is int    ill) Set(Illuminance,       ill, local);
        if (s.Co2                is int    co2) Set(CO2Level,          co2, local);

        if (CalcThermalIndices) RecomputeThermalIndices(s);

        MeasuredValueReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>外部から呼ばれる用 (Settings 応答などで設定情報を反映)。</summary>
    public void ApplySettings(Settings s)
    {
        DrybulbTemperature.Measure  = s.DrybulbTemperature.Enabled;
        DrybulbTemperature.Interval = (int)s.DrybulbTemperature.Interval;
        RelativeHumdity.Measure     = s.RelativeHumidity.Enabled;
        RelativeHumdity.Interval    = (int)s.RelativeHumidity.Interval;
        GlobeTemperature.Measure    = s.GlobeTemperature.Enabled;
        GlobeTemperature.Interval   = (int)s.GlobeTemperature.Interval;
        Velocity.Measure            = s.Velocity.Enabled;
        Velocity.Interval           = (int)s.Velocity.Interval;
        Illuminance.Measure         = s.Illuminance.Enabled;
        Illuminance.Interval        = (int)s.Illuminance.Interval;
        CO2Level.Measure            = s.Co2.Enabled;
        CO2Level.Interval           = (int)s.Co2.Interval;
        StartMeasuringDateTime      = s.StartTime.LocalDateTime;
        MeasuringSettingLoaded      = true;
        MeasurementSettingReceivedEvent?.Invoke(this, EventArgs.Empty);
    }

    private static void Set(MLogger.MeasurementInfo info, double value, DateTime t)
    {
        info.LastValue = value;          // internal set (LoggerCache は MLLib assembly 内なので OK)
        info.LastMeasureTime = t;
    }

    private void RecomputeThermalIndices(Sample s)
    {
        double dbt = Clamp(s.DrybulbTemperature ?? DefaultTemperature, -10, 40);
        double rhd = Clamp(s.RelativeHumidity   ?? DefaultRelativeHumidity, 0, 100);
        double vel = Clamp(s.Velocity           ?? DefaultVelocity, 0, 2);
        double glb = Clamp(s.GlobeTemperature   ?? DefaultGlobeTemperature, -10, 50);

        double mrt = GetMRT(dbt, glb, vel);
        double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndRelativeHumidity(dbt, rhd, ATM);
        double set = TwoNodeModel.GetSETStarFromAmbientCondition(dbt, mrt, rhd, vel, CloValue, 58.15 * MetValue, 0);
        double pmv = ThermalComfort.GetPMV(dbt, mrt, rhd, vel, CloValue, MetValue, 0);
        double ppd = ThermalComfort.GetPPD(pmv);

        // グローブ温度の 150mm 換算 (JIS B7922, JIS Z8504)
        double glb150 = dbt + (1 + 1.13 * Math.Pow(GLOBE_DIAMETER, -0.4) * Math.Pow(vel, 0.6))
                              / (1 + 2.41 * Math.Pow(vel, 0.6)) * (glb - dbt);

        MeanRadiantTemperature = mrt;
        PMV = pmv;
        PPD = ppd;
        SETStar = set;
        WBGT_Indoor  = 0.7 * wbt + 0.3 * glb150;
        WBGT_Outdoor = 0.7 * wbt + 0.2 * glb150 + 0.1 * dbt;
    }

    private static double GetMRT(double tmp, double glb, double vel)
    {
        const double EPS = 0.95;     // ピンポン球の放射率
        const double SIG = 5.67e-8;
        const double ES = EPS * SIG;
        double hc1 = 1.4 * Math.Pow(Math.Abs(tmp - glb) / GLOBE_DIAMETER, 0.25);
        double hc2 = 6.3 * Math.Pow(vel, 0.6) / Math.Pow(GLOBE_DIAMETER, 0.4);
        double glbK = glb + 273.15;
        return Math.Pow(Math.Max(0, Math.Pow(glbK, 4) + Math.Max(hc1, hc2) / ES * (glb - tmp)), 0.25) - 273.15;
    }

    private static double Clamp(double v, double lo, double hi)
        => Math.Max(lo, Math.Min(hi, v));
}
