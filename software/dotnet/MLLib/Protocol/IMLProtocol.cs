namespace MLLib.Protocol;

/// <summary>
/// M-Logger 子機と通信するための統一インタフェース。
/// 実装は v4 (JsonRpcV4Protocol) と v3 (LegacyV3Protocol) の2種類。
/// 接続時に <c>ProtocolFactory.DetectAsync</c> がプロトコル種別を hello probe で判定する。
/// </summary>
public interface IMLProtocol : IDisposable
{
    /// <summary>接続済みデバイスの識別情報 (hello/VER 応答キャッシュ)。</summary>
    DeviceInfo Device { get; }

    /// <summary>
    /// 子機がロギング中かどうかの現在の状態 (cached, ローカル更新)。
    /// 初期値は hello 応答の logging field。StartLoggingAsync/StopLoggingAsync の
    /// 成功で更新、ready event の logging field でも追従する。
    /// </summary>
    bool IsLogging { get; }

    // ============================================================
    // コマンド (request/response)
    // ============================================================
    Task<Settings> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>PATCH 形式の設定更新。指定したセンサ/フィールドのみ書き換え。応答は更新後の全状態。</summary>
    Task<Settings> SetSettingsAsync(SettingsPatch patch, CancellationToken ct = default);

    Task<CorrectionFactors> GetCorrectionAsync(CancellationToken ct = default);
    Task<CorrectionFactors> SetCorrectionAsync(CorrectionFactorsPatch patch, CancellationToken ct = default);

    Task<string> SetNameAsync(string name, CancellationToken ct = default);
    Task<DateTimeOffset> SetTimeAsync(DateTimeOffset time, CancellationToken ct = default);

    Task StartLoggingAsync(LoggingConfig config, CancellationToken ct = default);
    Task StopLoggingAsync(CancellationToken ct = default);

    Task ClearDataAsync(CancellationToken ct = default);

    /// <summary>
    /// CO2 校正を開始する。即時 ACK が返り、進捗は <see cref="Co2CalibrationUpdates"/> ストリームで通知。
    /// </summary>
    Task CalibrateCo2Async(Co2CalibrationMode mode, int targetPpm, CancellationToken ct = default);

    /// <summary>USB-CDC 専用。XBee/BLE 経由で呼ぶと <see cref="MLProtocolException"/> (code=unsupported_transport)。</summary>
    Task<DumpResult> DumpAsync(IProgress<int>? progress = null, CancellationToken ct = default);

    // ============================================================
    // イベントストリーム (Rx)
    // ============================================================
    /// <summary>計測サンプル (smp) ストリーム。ロギング中に流れる。</summary>
    IObservable<Sample> Samples { get; }

    /// <summary>ハートビート (ready) ストリーム。非ロギング時 60秒毎。</summary>
    IObservable<ReadyEvent> ReadyHeartbeats { get; }

    /// <summary>CO2 校正進捗 (co2_calibration_progress) ストリーム。</summary>
    IObservable<Co2CalibrationProgress> Co2CalibrationUpdates { get; }

    /// <summary>
    /// 時刻同期要求 (time_sync_request) ストリーム。子機が長期計測中の RTC drift 補正の
    /// ため能動的に同期を要求してくる。受信側 (MLServer 想定) は即座に <see cref="SetTimeAsync"/>
    /// を呼ぶことで、子機の wake window (data.window_s 秒) 内に時刻同期が完了する。
    /// v3 (LegacyV3Protocol) は本機能未対応のため <see cref="System.Reactive.Linq.Observable.Empty{T}()"/> を返す。
    /// </summary>
    IObservable<TimeSyncRequest> TimeSyncRequests { get; }
}
