namespace MLLib.Protocol;

public enum Co2CalibrationState
{
    Measuring,
    Pass,
    Fail
}

/// <summary>
/// CO2 校正進捗イベント (v4 co2_calibration_progress / v3 CCL を統合表現)。
/// 校正中 1秒毎に到着し、完了時に State=Pass/Fail で終わる。
/// </summary>
public sealed record Co2CalibrationProgress(
    DateTimeOffset      Timestamp,
    TimeSpan            Remaining,
    Co2CalibrationState State,
    short               CorrectionPpm,   // 完了時 (Pass/Fail) のみ意味あり
    int                 CurrentPpm);
