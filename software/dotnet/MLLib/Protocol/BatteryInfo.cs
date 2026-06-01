namespace MLLib.Protocol;

/// <summary>
/// firmware <c>get_battery</c> 応答。
/// VBAT (AVR64DU32 PD3 = ADC0 AIN3 経由、VDD 基準 8 サンプル積算) と
/// firmware による low-battery 判定フラグ。
/// </summary>
public sealed record BatteryInfo(
    int VoltageMv,
    bool IsLow);

/// <summary>
/// 電池種別 (新品電圧から判定する表示用ラベル)。
/// 寿命試算自体は <see cref="BatteryEstimator"/> が電圧から計算するため、本値は表示用途のみ。
/// </summary>
public enum BatteryType
{
    /// <summary>判定不能 (測定値なし等)。</summary>
    Unknown,
    /// <summary>Alkaline 乾電池 (新品 ~3.0V / 1.5V/cell × 2)。</summary>
    Alkaline,
    /// <summary>NiMH 二次電池 (満充電 ~2.7V / 1.35V/cell × 2)。</summary>
    NiMH,
}
