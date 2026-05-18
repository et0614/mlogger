namespace MLLib.Protocol;

/// <summary>線形補正 y = a*x + b の係数ペア。</summary>
public sealed record CorrectionCoefficients(float A, float B);

/// <summary>全 5 センサの補正係数 (CO2 は補正対象外)。</summary>
public sealed record CorrectionFactors(
    CorrectionCoefficients DrybulbTemperature,
    CorrectionCoefficients RelativeHumidity,
    CorrectionCoefficients GlobeTemperature,
    CorrectionCoefficients Illuminance,
    CorrectionCoefficients Velocity);

/// <summary>補正係数ペアの PATCH (a / b それぞれ null で現状維持)。</summary>
public sealed record CorrectionCoefficientsPatch(float? A = null, float? B = null);

/// <summary>
/// <see cref="IMLProtocol.SetCorrectionAsync"/> 用の PATCH。
/// 指定したセンサだけ更新。各センサ内も a / b それぞれ PATCH 可。
/// </summary>
public sealed record CorrectionFactorsPatch
{
    public CorrectionCoefficientsPatch? DrybulbTemperature        { get; init; }
    public CorrectionCoefficientsPatch? RelativeHumidity { get; init; }
    public CorrectionCoefficientsPatch? GlobeTemperature      { get; init; }
    public CorrectionCoefficientsPatch? Illuminance { get; init; }
    public CorrectionCoefficientsPatch? Velocity    { get; init; }
}

/// <summary>
/// 補正係数の許容範囲 (firmware protocol_handlers.c の CORRECTIONS テーブルと同期)。
/// </summary>
public static class CorrectionRanges
{
    // すべてのセンサで A は 0.800〜1.200
    public const float AMin = 0.800f;
    public const float AMax = 1.200f;

    public static (float min, float max) BRange(string sensor) => sensor switch
    {
        nameof(CorrectionFactors.DrybulbTemperature) => (-3.00f,  3.00f),
        nameof(CorrectionFactors.RelativeHumidity)   => (-9.99f,  9.99f),
        nameof(CorrectionFactors.GlobeTemperature)   => (-3.00f,  3.00f),
        nameof(CorrectionFactors.Illuminance)        => (-999f,   999f),
        nameof(CorrectionFactors.Velocity)           => (-0.500f, 0.500f),
        _ => throw new ArgumentOutOfRangeException(nameof(sensor), sensor, "unknown sensor"),
    };
}
