namespace MLLib.Protocol;

/// <summary>
/// 子機からの ready ハートビート (v4 ready イベント / v3 WFC を統合表現)。
/// 非ロギング時 60秒毎 (v4) または 7秒毎 (v3 WFC) に送出される。
/// </summary>
public sealed record ReadyEvent(
    DateTimeOffset Timestamp,
    TimeSpan       Uptime,
    bool           IsLogging);
