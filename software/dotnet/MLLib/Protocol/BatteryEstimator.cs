namespace MLLib.Protocol;

/// <summary>
/// power-based 電池寿命試算。試験データ (VBAT=2.76V Eneloop, 2026-05-30〜06-01) から
/// 各センサの **消費電力** (mW) を係数化し、現在 VBAT で除算して予想電流を再計算する。
/// 詳細は <c>docs/power_consumption.md §5</c> 参照。
/// </summary>
public static class BatteryEstimator
{
    // ============================================================
    // 係数 (mW)。VBAT=2.76V Eneloop 実測値の power 換算。
    // 値変更時は docs/power_consumption.md §5 も同期更新すること。
    // ============================================================

    /// <summary>常時消費 (logging 中、XBee/USB/Flash 全 OFF)。<see cref="PLedBlinkMw"/> を含む。</summary>
    public const double PBaselineMw = 4.53;

    /// <summary>baseline に含まれる 5sec 周期 LED 点滅の寄与。間引き設定実装時はここを差し引く。</summary>
    public const double PLedBlinkMw = 2.76;

    /// <summary>General カテゴリ active 中 (th_probe measureOnce、~520ms/iter)。</summary>
    public const double PGeneralActiveMw = 17.0;

    /// <summary>Velocity active 中 (熱線 ON、無風時)。</summary>
    public const double PVelocityActiveMw = 124.0;

    /// <summary>Illuminance active 中 (OPT3001 read、瞬間値)。</summary>
    public const double PIlluminanceActiveMw = 0.4;

    /// <summary>th_probe measureOnce 1 回の所要時間 [sec]。</summary>
    public const double TGeneralActiveSec = 0.52;

    /// <summary>風速プローブ熱線立ち上げ時間 [sec] (firmware <c>V_WAKEUP_TIME</c>)。</summary>
    public const double TVelocityWakeupSec = 20.0;

    /// <summary>OPT3001 read 所要 [sec] (推定、微小)。</summary>
    public const double TIlluminanceActiveSec = 0.01;

    // ============================================================
    // 電池側 (新品想定、決め打ち)
    // ============================================================

    /// <summary>想定容量 [mAh]。NiMH 標準、Alkaline ~2500mAh は安全側で同値扱い。</summary>
    public const double BatteryMah = 2000.0;

    /// <summary>安全係数。DC-DC 効率低下や温度劣化を吸収。</summary>
    public const double SafetyFactor = 0.8;

    /// <summary>battery 種別判定閾値 [mV]。新品電池の電圧から推定する表示専用ラベル用。</summary>
    public const int AlkalineDetectThresholdMv = 2850;

    // ============================================================
    // 試算
    // ============================================================

    /// <summary>
    /// 指定設定での消費電力 (mW) を計算。
    /// </summary>
    /// <param name="settings">計測設定 (3 カテゴリ)。</param>
    /// <param name="ledBlinkEnabled">5sec 周期 LED 点滅の有無 (現状 firmware は常時 ON)。</param>
    public static double EstimatePowerMw(Settings settings, bool ledBlinkEnabled = true)
    {
        double p = PBaselineMw;
        if (!ledBlinkEnabled) p -= PLedBlinkMw;

        if (settings.General.Enabled && settings.General.Interval > 0)
        {
            double duty = Math.Min(TGeneralActiveSec / settings.General.Interval, 1.0);
            p += PGeneralActiveMw * duty;
        }

        if (settings.Velocity.Enabled && settings.Velocity.Interval > 0)
        {
            // interval < V_WAKEUP_TIME なら熱線は常時 ON 運用 (duty=1.0)
            double duty = settings.Velocity.Interval < TVelocityWakeupSec
                ? 1.0
                : TVelocityWakeupSec / settings.Velocity.Interval;
            p += PVelocityActiveMw * duty;
        }

        if (settings.Illuminance.Enabled && settings.Illuminance.Interval > 0)
        {
            double duty = Math.Min(TIlluminanceActiveSec / settings.Illuminance.Interval, 1.0);
            p += PIlluminanceActiveMw * duty;
        }

        return p;
    }

    /// <summary>
    /// 観測 VBAT [mV] と消費電力 [mW] から、新品電池での連続計測可能時間を推定。
    /// 計算: I = P/V → 時間 = (容量 × 安全係数) / I
    /// </summary>
    public static TimeSpan EstimateContinuousRuntime(double powerMw, int voltageMv)
    {
        if (voltageMv <= 0 || powerMw <= 0) return TimeSpan.Zero;
        double currentMa = powerMw / (voltageMv / 1000.0);          // mA
        double hours = (BatteryMah * SafetyFactor) / currentMa;     // h
        return TimeSpan.FromHours(hours);
    }

    /// <summary>
    /// 新品電池前提で VBAT から電池種別を推定 (表示用ラベル)。
    /// 寿命試算には使わない。
    /// </summary>
    public static BatteryType DetectType(int voltageMv)
    {
        if (voltageMv <= 0) return BatteryType.Unknown;
        return voltageMv > AlkalineDetectThresholdMv ? BatteryType.Alkaline : BatteryType.NiMH;
    }
}
