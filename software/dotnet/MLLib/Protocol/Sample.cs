namespace MLLib.Protocol;

/// <summary>
/// 1回分の計測サンプル (v4 smp イベント / v3 DTT 応答から構築)。
/// 計測対象外/欠測のセンサは null。
///
/// <see cref="WarmupCategories"/> はウォームアップ中のカテゴリ ID 集合 ("g" / "v" / "l")。
/// <see cref="DisconnectedCategories"/> はセンサ切断中のカテゴリ ID 集合。
/// 欠測値が warmup / disconnect / 設定 OFF のいずれによるものかを判別するのに使う。
/// </summary>
public sealed record Sample(
    DateTimeOffset Timestamp,
    double? DrybulbTemperature,        // °C
    double? RelativeHumidity,    // %
    double? GlobeTemperature,      // °C
    double? Velocity,    // m/s
    int?    Illuminance, // lx
    int?    Co2,         // ppm
    IReadOnlyList<string>? WarmupCategories = null,
    IReadOnlyList<string>? DisconnectedCategories = null,
    int?    VelocityVoltage = null);   // mV (風速プローブ熱線電圧、smp の "vv"。異常解析・校正補助用)
