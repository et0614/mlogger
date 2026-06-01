namespace MLLib.Protocol;

/// <summary>1カテゴリあたりの計測設定。</summary>
public sealed record SensorSetting(bool Enabled, uint Interval /* sec */);

/// <summary>
/// 計測設定 (v4 から 3 カテゴリに集約)。
/// <list type="bullet">
///   <item><c>General</c> = 温湿度 + グローブ温度 + CO2 (mlogger_th_sensor 子機で一括計測)</item>
///   <item><c>Velocity</c> = 風速</item>
///   <item><c>Illuminance</c> = 照度</item>
/// </list>
/// </summary>
public sealed record Settings(
    SensorSetting General,
    SensorSetting Velocity,
    SensorSetting Illuminance,
    DateTimeOffset StartTime);

/// <summary>センサ設定の PATCH 表現 (各フィールドが null なら現状維持)。</summary>
public sealed record SensorSettingPatch(bool? Enabled = null, uint? Interval = null);

/// <summary>
/// <see cref="IMLProtocol.SetSettingsAsync"/> 用の PATCH。
/// null のフィールドは現状維持。
/// </summary>
public sealed record SettingsPatch
{
    public SensorSettingPatch? General     { get; init; }
    public SensorSettingPatch? Velocity    { get; init; }
    public SensorSettingPatch? Illuminance { get; init; }
    public DateTimeOffset?     StartTime   { get; init; }
}
