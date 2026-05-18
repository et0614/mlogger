namespace MLLib.Protocol;

/// <summary>
/// 1回分の計測サンプル (v4 smp イベント / v3 DTT 応答から構築)。
/// 計測対象外/欠測のセンサは null。
/// </summary>
public sealed record Sample(
    DateTimeOffset Timestamp,
    double? DrybulbTemperature,        // °C
    double? RelativeHumidity,    // %
    double? GlobeTemperature,      // °C
    double? Velocity,    // m/s
    int?    Illuminance, // lx
    int?    Co2);        // ppm
