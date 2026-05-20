namespace MLLib.Protocol;

/// <summary>
/// 子機からの時刻同期要求 (v4 time_sync_request イベント)。
///
/// 子機は長期計測中に RTC drift 補正を目的として、24 時間ごと (初回は最初に到来する
/// 深夜 0:00) に本イベントを送出する。送出直後から <see cref="WindowDuration"/> の間、
/// 無線を awake 維持して <c>set_time</c> コマンドの到着を待つ。
///
/// 受信側 (MLServer 想定) は本イベントを受けたら即座に
/// <see cref="IMLProtocol.SetTimeAsync"/> を呼ぶ。それで子機の RTC が更新され、
/// 同期カウンタリセット → sleep 復帰。window 内に応答が無ければ子機は sleep に戻り
/// 次回 24 時間後に再試行する。
///
/// v3 firmware には実装されていない (LegacyV3Protocol.TimeSyncRequests は空の
/// observable を返す)。
/// </summary>
/// <param name="Timestamp">受信側 (PC) で本イベントを受け取った時刻。</param>
/// <param name="DeviceTime">子機の現在 RTC (UTC)。 drift 量の観測に使える。</param>
/// <param name="WindowDuration">子機が wake を維持し set_time 受信を待つ時間。</param>
public sealed record TimeSyncRequest(
    DateTimeOffset Timestamp,
    DateTimeOffset DeviceTime,
    TimeSpan       WindowDuration);
