namespace MLLib.Protocol;

/// <summary>1センサあたりの計測設定。</summary>
public sealed record SensorSetting(bool Enabled, uint Interval /* sec */);

/// <summary>全 6 センサの計測設定 + 計測開始時刻。</summary>
public sealed record Settings(
    SensorSetting DrybulbTemperature,
    SensorSetting RelativeHumidity,
    SensorSetting GlobeTemperature,
    SensorSetting Velocity,
    SensorSetting Illuminance,
    SensorSetting Co2,
    DateTimeOffset StartTime);

/// <summary>センサ設定の PATCH 表現 (各フィールドが null なら現状維持)。</summary>
public sealed record SensorSettingPatch(bool? Enabled = null, uint? Interval = null);

/// <summary>
/// <see cref="IMLProtocol.SetSettingsAsync"/> 用の PATCH。
/// null のフィールドは現状維持。
/// </summary>
public sealed record SettingsPatch
{
    public SensorSettingPatch? DrybulbTemperature        { get; init; }
    public SensorSettingPatch? RelativeHumidity { get; init; }
    public SensorSettingPatch? GlobeTemperature      { get; init; }
    public SensorSettingPatch? Velocity    { get; init; }
    public SensorSettingPatch? Illuminance { get; init; }
    public SensorSettingPatch? Co2         { get; init; }
    public DateTimeOffset?     StartTime   { get; init; }
}
