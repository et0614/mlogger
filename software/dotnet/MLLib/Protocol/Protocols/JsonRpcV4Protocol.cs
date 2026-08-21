using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json.Nodes;
using MLLib.Protocol.Transport;

namespace MLLib.Protocol.Protocols;

/// <summary>
/// v4 ファーム (JSON Lines / JSON-RPC 風) との通信実装。
/// <see cref="CreateAsync"/> で hello probe 込みでインスタンス化する。
/// </summary>
public sealed class JsonRpcV4Protocol : IMLProtocol
{
    private readonly ISerialTransport _transport;
    private readonly IDisposable _rxSubscription;
    private readonly LineBuffer _lineBuffer = new();

    private int _nextId;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _pending = new();

    // イベントストリーム
    private readonly Subject<Sample> _samples = new();
    private readonly Subject<ReadyEvent> _ready = new();
    private readonly Subject<Co2CalibrationProgress> _co2 = new();
    private readonly Subject<TimeSyncRequest> _timeSyncRequests = new();

    // dump 用のバイナリモード状態
    private byte[]? _dumpBuffer;
    private int _dumpBytesRead;
    private int _dumpRemaining;
    private TaskCompletionSource? _dumpBytesReceived;
    // 進捗報告先 (block 内の受信バイト数を OnBytesReceived から呼ぶ用)
    private Action<int>? _dumpProgress;
    // _lineBuffer の排他 (受信スレッドの Append と DumpBlockAsync の Reset が競合するため)
    private readonly object _lineLock = new();
    // dump 応答の id (OnLine が同期的に binary mode へ切替える際の照合用)。-1 は dump 待機無し。
    // BLE では JSON header の直後に binary chunk が到着する場合があり、DumpAsync 側で
    // 非同期に _dumpRemaining を立てる従来方式だと race で binary bytes が line buffer に
    // 流れて捨てられるため、OnLine 内で同期的に _dumpBuffer/_dumpRemaining を確定する。
    private int _dumpPendingId = -1;

    private DeviceInfo? _device;

    // ============================================================
    // 構築・破棄
    // ============================================================
    private JsonRpcV4Protocol(ISerialTransport transport)
    {
        _transport = transport;
        _rxSubscription = _transport.Received.Subscribe(OnBytesReceived);
    }

    /// <summary>
    /// 接続済みの transport を渡し、内部で hello を実行して DeviceInfo を取得する。
    /// 失敗時は protocol を Dispose してから例外を再 throw。
    /// </summary>
    public static async Task<JsonRpcV4Protocol> CreateAsync(ISerialTransport transport, CancellationToken ct = default)
    {
        var p = new JsonRpcV4Protocol(transport);
        try
        {
            p._device = await p.HelloAsync(ct).ConfigureAwait(false);
            return p;
        }
        catch
        {
            p.Dispose();
            throw;
        }
    }

    /// <summary>
    /// hello probe を打たずに instance を生成する (passive observer 用)。
    /// 既に sleep に入っている子機からの sample event 等を観測するのに使う。
    /// <see cref="DeviceInfo"/> は推測値で埋める (Name / FirmwareVersion は呼び出し側が
    /// 既知の値を渡せれば良いが、無ければ "unknown")。
    /// </summary>
    public static JsonRpcV4Protocol CreatePassive(
        ISerialTransport transport,
        string deviceName = "MLogger",
        string hardwareId = "")
    {
        var p = new JsonRpcV4Protocol(transport);
        p._device = new DeviceInfo(
            Device:          "M-Logger",
            FirmwareVersion: "unknown",
            ProtocolVersion: 1,
            HardwareId:      hardwareId,
            Name:            deviceName,
            IsLogging:       true,  // passive モードに入る = 通常は既にロギング中
            HasCo2Sensor:    true); // v4 firmware は CO2 標準搭載
        p._isLogging = true;
        return p;
    }

    /// <summary>診断: 誰が Dispose を呼んだか log</summary>
    public static Action<string>? DisposeTraceSink { get; set; }

    public void Dispose()
    {
        DisposeTraceSink?.Invoke("JsonRpcV4Protocol.Dispose stack: " + System.Environment.StackTrace.Replace("\r\n", " | ").Replace("\n", " | "));
        _rxSubscription.Dispose();

        // 保留中の応答を全てキャンセル
        foreach (var (_, tcs) in _pending)
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(JsonRpcV4Protocol)));
        }
        _pending.Clear();
        _dumpBytesReceived?.TrySetCanceled();

        _samples.OnCompleted();
        _samples.Dispose();
        _ready.OnCompleted();
        _ready.Dispose();
        _co2.OnCompleted();
        _co2.Dispose();
        _timeSyncRequests.OnCompleted();
        _timeSyncRequests.Dispose();
    }

    public DeviceInfo Device =>
        _device ?? throw new InvalidOperationException("hello not yet completed");

    // ロギング状態のローカル cache。 hello 応答で初期化、 start/stop 成功と
    // ready event で更新される。
    private bool _isLogging;
    public bool IsLogging => _isLogging;

    public IObservable<Sample> Samples => _samples.AsObservable();
    public IObservable<ReadyEvent> ReadyHeartbeats => _ready.AsObservable();
    public IObservable<Co2CalibrationProgress> Co2CalibrationUpdates => _co2.AsObservable();
    public IObservable<TimeSyncRequest> TimeSyncRequests => _timeSyncRequests.AsObservable();

    // ============================================================
    // コマンド送信の共通基盤
    // ============================================================
    /// <summary>
    /// Optional diagnostic sink for sent/received JSON lines. Set by the host app
    /// (e.g. MLUtility.WriteLog) to capture RPC traffic into the in-app log.
    /// </summary>
    public static Action<string>? DiagnosticSink { get; set; }

    private async Task<JsonNode?> CallAsync(string command, JsonNode? @params, CancellationToken ct)
    {
        int id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        using var registration = ct.Register(() =>
        {
            if (_pending.TryRemove(id, out var t)) t.TrySetCanceled(ct);
        });

        var envelope = new JsonObject
        {
            ["v"] = 1,
            ["id"] = id,
            ["command"] = command,
        };
        if (@params is not null) envelope["params"] = @params;

        var json = envelope.ToJsonString();
        DiagnosticSink?.Invoke($"TX id={id} cmd={command} len={json.Length}: {json}");
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _transport.SendAsync(bytes, ct).ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    private static T RequireResult<T>(JsonNode? node) where T : class =>
        node as T ?? throw new InvalidDataException($"expected {typeof(T).Name}, got: {node?.ToJsonString() ?? "null"}");

    // ============================================================
    // 受信処理
    // ============================================================
    // 最終受信時刻 [Environment.TickCount64]。dump block 再試行前の静穏待ちに使う。
    private long _lastRxTicks;

    private void OnBytesReceived(ReadOnlyMemory<byte> data)
    {
        _lastRxTicks = Environment.TickCount64;
        int offset = 0;
        while (offset < data.Length)
        {
            if (_dumpRemaining > 0 && _dumpBuffer is not null)
            {
                int toRead = Math.Min(_dumpRemaining, data.Length - offset);
                data.Span.Slice(offset, toRead).CopyTo(_dumpBuffer.AsSpan(_dumpBytesRead));
                _dumpBytesRead += toRead;
                _dumpRemaining -= toRead;
                offset += toRead;
                _dumpProgress?.Invoke(_dumpBytesRead);
                if (_dumpRemaining == 0) _dumpBytesReceived?.TrySetResult();
            }
            else
            {
                // dump response 受信時に OnLine が _dumpRemaining を立てたら即座に
                // 行解釈を中断し、残りバイトを dump buffer 経路に流す。これをしないと
                // JSON header と binary chunk が同一 BLE notification に乗ったときに
                // binary が line buffer に詰まって捨てられる。
                int consumed;
                lock (_lineLock)
                {
                    consumed = _lineBuffer.Append(
                        data.Span[offset..],
                        OnLine,
                        () => _dumpRemaining > 0);
                }
                offset += consumed;
            }
        }
    }

    private void OnLine(string line)
    {
        DiagnosticSink?.Invoke($"RX len={line.Length}: {line}");
        JsonNode? root;
        try { root = JsonNode.Parse(line); }
        catch (Exception ex)
        {
            DiagnosticSink?.Invoke($"RX parse FAIL: {ex.Message}");
            return;
        }
        if (root is not JsonObject obj) return;

        // 応答 (id 有り)
        if (obj.TryGetPropertyValue("id", out var idNode) && idNode is not null)
        {
            int id = idNode.GetValue<int>();

            // dump 応答 = header と同時に binary mode へ同期的に切替える (BLE 用 race 回避)。
            // DumpAsync が _dumpPendingId を立てた状態でこの id の result が届くと、
            // count/record_size から buffer を確保し _dumpRemaining を立てる。これを
            // OnLine 内で行うことで、同一 BLE notification に同梱された binary chunk
            // も正しく dump buffer に流れる。
            if (id == _dumpPendingId
                && obj.TryGetPropertyValue("result", out var dumpResNode)
                && dumpResNode is JsonObject dumpResult)
            {
                int count   = dumpResult["count"]?.GetValue<int>() ?? 0;
                int recSize = dumpResult["record_size"]?.GetValue<int>() ?? 0;
                int total   = count * recSize;
                _dumpBuffer    = new byte[total];
                _dumpBytesRead = 0;
                _dumpRemaining = total;
                // total == 0 の edge case (record 0 件) は DumpAsync 側で扱う。
            }

            if (!_pending.TryRemove(id, out var tcs)) return;

            if (obj.TryGetPropertyValue("error", out var errNode) && errNode is JsonObject err)
            {
                string code = err["code"]?.GetValue<string>() ?? "";
                string message = err["message"]?.GetValue<string>() ?? "";
                tcs.TrySetException(new MLProtocolException(code, message));
            }
            else if (obj.TryGetPropertyValue("result", out var resNode))
            {
                tcs.TrySetResult(resNode);
            }
            else
            {
                tcs.TrySetException(new InvalidDataException("response missing both result and error"));
            }
            return;
        }

        // イベント (event 有り)
        if (obj.TryGetPropertyValue("event", out var evNode) && evNode is not null)
        {
            string evName = evNode.GetValue<string>();
            var ts = obj["ts"] is JsonNode tsNode
                ? DateTimeOffset.FromUnixTimeSeconds(tsNode.GetValue<long>())
                : DateTimeOffset.UtcNow;
            var data = obj["data"] as JsonObject;

            switch (evName)
            {
                case "smp":
                    if (data is not null) _samples.OnNext(ParseSample(data, ts));
                    break;
                case "ready":
                    if (data is not null)
                    {
                        var re = ParseReadyEvent(data, ts);
                        _isLogging = re.IsLogging;  // 自発状態変化に追従
                        _ready.OnNext(re);
                    }
                    break;
                case "co2_calibration_progress":
                    if (data is not null) _co2.OnNext(ParseCo2Progress(data, ts));
                    break;
                case "dump_end":
                    // block-pull 方式では完了判定は受信バイト数で行うため未使用。
                    // (firmware は各 block 送信後にも送出してくるが無視して良い。
                    //  遅延到着した前 block の dump_end が次 block と交錯しても無害)
                    break;
                case "time_sync_request":
                    int windowSec = data?["window_s"]?.GetValue<int>() ?? 30;
                    _timeSyncRequests.OnNext(new TimeSyncRequest(
                        Timestamp:      DateTimeOffset.UtcNow,
                        DeviceTime:     ts,
                        WindowDuration: TimeSpan.FromSeconds(windowSec)));
                    break;
            }
        }
    }

    // ============================================================
    // 各コマンド実装
    // ============================================================
    private async Task<DeviceInfo> HelloAsync(CancellationToken ct)
    {
        var result = RequireResult<JsonObject>(await CallAsync("hello", null, ct));
        var info = new DeviceInfo(
            Device:          result["device"]?.GetValue<string>() ?? "",
            FirmwareVersion: result["firmware_version"]?.GetValue<string>() ?? "",
            ProtocolVersion: result["protocol_version"]?.GetValue<int>() ?? 0,
            HardwareId:      result["hardware_id"]?.GetValue<string>() ?? "",
            Name:            result["name"]?.GetValue<string>() ?? "",
            IsLogging:       result["logging"]?.GetValue<bool>() ?? false,
            HasCo2Sensor:    true);     // v4 ハードは CO2 センサ標準搭載 (HCS コマンド廃止)
        _isLogging = info.IsLogging;  // ローカル cache を hello の値で初期化
        return info;
    }

    public async Task<Settings> GetSettingsAsync(CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(await CallAsync("get_settings", null, ct));
        return ParseSettings(result);
    }

    public async Task<Settings> SetSettingsAsync(SettingsPatch patch, CancellationToken ct = default)
    {
        // v4 wire は 3 カテゴリ (general/velocity/illuminance)。本 protocol 実装は
        // 内部 model (6 センサ) との変換を担う。MAUI 側 v4 UI は t_dry/humidity/t_glb/co2
        // を常に同値で構成して patch に詰める前提なので、ここでは t_dry を優先的に
        // general の値として採用 (なければ humidity → t_glb → co2 の順にフォールバック)。
        var generalPatch = patch.DrybulbTemperature
                        ?? patch.RelativeHumidity
                        ?? patch.GlobeTemperature
                        ?? patch.Co2;

        var p = new JsonObject();
        if (generalPatch      is not null) p["general"]     = BuildSensorPatch(generalPatch);
        if (patch.Velocity    is not null) p["velocity"]    = BuildSensorPatch(patch.Velocity);
        if (patch.Illuminance is not null) p["illuminance"] = BuildSensorPatch(patch.Illuminance);
        if (patch.StartTime   is not null) p["start_ts"]    = patch.StartTime.Value.ToUnixTimeSeconds();

        var result = RequireResult<JsonObject>(await CallAsync("set_settings", p, ct));
        return ParseSettings(result);
    }

    public async Task<CorrectionFactors> GetCorrectionAsync(CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(await CallAsync("get_correction", null, ct));
        return ParseCorrectionFactors(result);
    }

    public async Task<CorrectionFactors> SetCorrectionAsync(CorrectionFactorsPatch patch, CancellationToken ct = default)
    {
        var p = new JsonObject();
        if (patch.DrybulbTemperature is not null) p["t_dry"]       = BuildCorrectionPatch(patch.DrybulbTemperature);
        if (patch.RelativeHumidity   is not null) p["humidity"]    = BuildCorrectionPatch(patch.RelativeHumidity);
        if (patch.GlobeTemperature   is not null) p["t_glb"]       = BuildCorrectionPatch(patch.GlobeTemperature);
        if (patch.Illuminance        is not null) p["illuminance"] = BuildCorrectionPatch(patch.Illuminance);
        if (patch.Velocity           is not null) p["velocity"]    = BuildCorrectionPatch(patch.Velocity);

        var result = RequireResult<JsonObject>(await CallAsync("set_correction", p, ct));
        return ParseCorrectionFactors(result);
    }

    public async Task<string> SetNameAsync(string name, CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(await CallAsync("set_name", new JsonObject { ["name"] = name }, ct));
        return result["name"]?.GetValue<string>() ?? name;
    }

    public async Task<DateTimeOffset> SetTimeAsync(DateTimeOffset time, CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(
            await CallAsync("set_time", new JsonObject { ["ts"] = time.ToUnixTimeSeconds() }, ct));
        return DateTimeOffset.FromUnixTimeSeconds(result["ts"]?.GetValue<long>() ?? 0);
    }

    public async Task<BatteryInfo> GetBatteryAsync(CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(await CallAsync("get_battery", null, ct));
        return new BatteryInfo(
            VoltageMv: result["voltage_mv"]?.GetValue<int>() ?? 0,
            IsLow:     result["low_battery"]?.GetValue<bool>() ?? false);
    }

    public async Task<ProbeInfo> GetProbeInfoAsync(CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(await CallAsync("get_probe_info", null, ct));
        return new ProbeInfo(
            ThProbe:       ParseProbePort(result["th_probe"]?.AsObject()),
            VelocityProbe: ParseProbePort(result["velocity_probe"]?.AsObject()));
    }

    private static ProbePortInfo ParseProbePort(JsonObject? o)
    {
        if (o is null) return new ProbePortInfo(false, null, null, 0);
        return new ProbePortInfo(
            Connected: o["connected"]?.GetValue<bool>() ?? false,
            DeviceId:  o["device_id"]?.GetValue<string>(),
            Name:      o["name"]?.GetValue<string>(),
            DataCount: o["data_count"]?.GetValue<int>() ?? 0);
    }

    public async Task StartLoggingAsync(LoggingConfig config, CancellationToken ct = default)
    {
        var tx = new JsonObject
        {
            ["zigbee"] = config.Tx.Zigbee,
            ["ble"]    = config.Tx.Ble,
            ["flash"]  = config.Tx.Flash,
            ["usb"]    = config.Tx.Usb,
        };
        var p = new JsonObject
        {
            ["transports"] = tx,
            ["mode"]       = config.Mode == LoggingMode.AutoRestart ? "auto_restart" : "once",
        };
        await CallAsync("start_logging", p, ct);
        _isLogging = true;  // 成功した場合のみここに到達
    }

    public async Task StopLoggingAsync(CancellationToken ct = default)
    {
        await CallAsync("stop_logging", null, ct);
        _isLogging = false;
    }

    /// <summary>
    /// 診断用 echo (firmware ph_echo を直叩き)。size 文字の 'x' を含む応答を返させ、
    /// 返却された size を返す。
    /// </summary>
    public Task<int> EchoAsync(int size, CancellationToken ct = default)
        => EchoAsync(size, 0, ct);

    /// <summary>
    /// padBytes > 0 ならリクエスト側にも "pad" キーで filler 文字を埋めて MAUI 側 TX
    /// チャンキングを意図的に発生させる (set_settings の TX サイズを模擬)。
    /// firmware ph_echo は未知のキーは無視するので副作用なし。
    /// </summary>
    public async Task<int> EchoAsync(int size, int padBytes, CancellationToken ct = default)
    {
        var p = new JsonObject { ["size"] = size };
        if (padBytes > 0) p["pad"] = new string('p', padBytes);
        var result = RequireResult<JsonObject>(await CallAsync("echo", p, ct));
        return result["size"]?.GetValue<int>() ?? -1;
    }

    public async Task ClearDataAsync(CancellationToken ct = default)
        => await CallAsync("clear_data", null, ct);

    public async Task CalibrateCo2Async(Co2CalibrationMode mode, int targetPpm, CancellationToken ct = default)
    {
        string modeStr = mode switch
        {
            Co2CalibrationMode.Forced  => "forced",
            Co2CalibrationMode.Factory => "factory",
            Co2CalibrationMode.Reset   => "reset",
            _ => throw new ArgumentException($"unknown Co2CalibrationMode: {mode}"),
        };
        var p = new JsonObject { ["mode"] = modeStr };
        // reset 以外は target_ppm が必要
        if (mode != Co2CalibrationMode.Reset) p["target_ppm"] = targetPpm;
        await CallAsync("calibrate_co2", p, ct);
    }

    public async Task<DumpResult> GetCountAsync(CancellationToken ct = default)
    {
        var header = RequireResult<JsonObject>(await CallAsync("get_count", null, ct));
        int count       = header["count"]?.GetValue<int>() ?? 0;
        int recordSize  = header["record_size"]?.GetValue<int>() ?? 0;
        string format   = header["format"]?.GetValue<string>() ?? "";
        return new DumpResult(count, recordSize, format, ReadOnlyMemory<byte>.Empty);
    }

    // ============================================================
    // dump (block-pull 方式)
    //
    // BLE/Zigbee の binary stream は fire-and-forget でフレーム欠落があり得る
    // (XBee 内部バッファ overflow による silent drop)。欠落はストリーム途中で
    // 起きるため「受信済みバイト数からの単純 resume」はデータ破損を招く。
    // そこで全件を一気に流させるのではなく、小 block (DumpBlockRecords 件) を
    // dump {from, limit} で順に pull し、期待バイト数が揃わなかった block だけ
    // を再要求する。block 単位の要求-応答なのでペーシングが閉ループ化し、
    // 欠落時も block 1 個の再試行 (~1-2 sec) で済む。
    // ============================================================

    /// <summary>
    /// 1 block あたりのレコード数。22B/record × 1000 = 22KB。
    /// firmware 側の UART flow-control 対応 (xb_write) によりストリーム自体が
    /// ほぼ無損失になったため、block は「万一の欠落時の再取得単位 + 進捗の区切り」
    /// でしかない。小さくしすぎると block 間の要求-応答ポーズ (~0.3 sec) が
    /// 増えて体感が悪化する (100 で細切れ感の指摘あり 2026-08-21)。
    /// </summary>
    private const int DumpBlockRecords = 1000;

    /// <summary>block 再試行の上限回数 (初回含む)。</summary>
    private const int DumpBlockMaxAttempts = 4;

    /// <summary>
    /// block attempt の失敗判定: 受信が完全に途絶えてからこの時間で attempt を
    /// 打ち切る (sliding timeout — バイトが流れ続けている限り切らない)。
    /// block サイズに依存しないので大 block でも安全、途絶時は素早く再試行に入る。
    /// </summary>
    private static readonly TimeSpan DumpBlockStallTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// 失敗 block の再試行前に要求する受信静穏時間。失敗 attempt の遅延バイナリが
    /// 吐き切られる前に再要求すると、遅延バイトが新 block のバッファに混入して
    /// 「バイト数は揃うが中身が破損」する恐れがあるため、この時間受信が無いことを
    /// 確認してから再要求する。
    /// </summary>
    private static readonly TimeSpan DumpRetryQuietWindow = TimeSpan.FromMilliseconds(700);

    /// <summary>静穏待ちの上限 (受信が続いても諦めて再試行に進む)。</summary>
    private static readonly TimeSpan DumpRetryQuietMaxWait = TimeSpan.FromSeconds(5);

    /// <summary>直近 <see cref="DumpRetryQuietWindow"/> の間に受信が無くなるまで待つ。</summary>
    private async Task WaitForRxQuietAsync(CancellationToken ct)
    {
        long start = Environment.TickCount64;
        while (true)
        {
            long silence = Environment.TickCount64 - _lastRxTicks;
            if (silence >= (long)DumpRetryQuietWindow.TotalMilliseconds) return;
            if (Environment.TickCount64 - start >= (long)DumpRetryQuietMaxWait.TotalMilliseconds) return;
            await Task.Delay(100, ct).ConfigureAwait(false);
        }
    }

    public async Task<DumpResult> DumpAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // 総件数・record_size は get_count で確定 (dump は logging 停止中のみ許可される
        // ため、転送中に件数が変わることはない)。
        var header = await GetCountAsync(ct).ConfigureAwait(false);
        int total   = header.RecordCount;
        int recSize = header.RecordSize;
        if (total <= 0 || recSize <= 0)
            return new DumpResult(0, recSize, header.Format, ReadOnlyMemory<byte>.Empty);

        var all = new byte[total * recSize];
        int done = 0;   // 取得済みレコード数
        while (done < total)
        {
            int n = Math.Min(DumpBlockRecords, total - done);
            int baseBytes = done * recSize;
            byte[]? block = null;

            for (int attempt = 1; attempt <= DumpBlockMaxAttempts && block is null; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                if (attempt > 1)
                    await WaitForRxQuietAsync(ct).ConfigureAwait(false);
                try
                {
                    using var blockCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    blockCts.CancelAfter(DumpBlockStallTimeout);
                    block = await DumpBlockAsync(
                        done, n,
                        bytes =>
                        {
                            // 受信がある限り stall timeout を再アーム。
                            // (attempt 終了後に遅延バイトが届いた場合 blockCts は
                            //  破棄済みのことがあるため ODE は握りつぶす)
                            try { blockCts.CancelAfter(DumpBlockStallTimeout); }
                            catch (ObjectDisposedException) { }
                            progress?.Report(baseBytes + bytes);
                        },
                        blockCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // block 単体のタイムアウト (フレーム欠落など) → 再要求
                    DiagnosticSink?.Invoke($"dump block from={done} n={n} attempt={attempt} timed out, retrying");
                }
            }

            if (block is null)
                throw new MLProtocolException("dump_incomplete",
                    $"dump block from={done} count={n} failed after {DumpBlockMaxAttempts} attempts");

            block.CopyTo(all.AsSpan(baseBytes));
            done += n;
            progress?.Report(done * recSize);
        }

        return new DumpResult(total, recSize, header.Format, all);
    }

    /// <summary>
    /// dump {from, limit} を 1 回発行し、その block の binary (limit × record_size バイト)
    /// を受信して返す。期待バイト数が揃わない場合は ct のタイムアウトで
    /// OperationCanceledException になる (呼び出し側が再試行)。
    /// </summary>
    private async Task<byte[]> DumpBlockAsync(int from, int count, Action<int>? progress, CancellationToken ct)
    {
        // CallAsync を使わず、id を先に確定してから _dumpPendingId に登録する。これで
        // header response 受信時に OnLine が同期的に _dumpBuffer/_dumpRemaining を立て、
        // 同一 BLE notification 内の binary chunk (BLE では JSON と binary が混在しうる)
        // も dump buffer に正しく流れる。
        int id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        _dumpPendingId = id;

        _dumpBytesReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dumpProgress      = progress;

        using var registration = ct.Register(() =>
        {
            if (_pending.TryRemove(id, out var t)) t.TrySetCanceled(ct);
            _dumpBytesReceived?.TrySetCanceled();
        });

        try
        {
            // 前 attempt の遅延バイナリ残骸 (改行を含まない不完全 "行") が line buffer に
            // 残っていると、今回の header JSON がそれに連結されて parse 不能になる。
            // リクエスト送信前に捨てて再同期する。dump シーケンス中に正規の行が
            // 部分受信状態でいることはほぼ無い (60 sec 周期の ready heartbeat 程度で、
            // 欠けても実害なし)。
            lock (_lineLock) _lineBuffer.Reset();

            // 1) dump コマンドを送信 (id は予約済み)
            var envelope = new JsonObject
            {
                ["v"]       = 1,
                ["id"]      = id,
                ["command"] = "dump",
                ["params"]  = new JsonObject { ["from"] = from, ["limit"] = count },
            };
            var json = envelope.ToJsonString();
            DiagnosticSink?.Invoke($"TX id={id} cmd=dump len={json.Length}: {json}");
            await _transport.SendAsync(Encoding.UTF8.GetBytes(json + "\n"), ct).ConfigureAwait(false);

            // 2) header (JSON) 受信待ち。OnLine が同期的に _dumpBuffer/_dumpRemaining を立てる。
            var resp = await tcs.Task.ConfigureAwait(false);
            if (resp is not JsonObject header)
                throw new InvalidDataException("dump header was not a JSON object");
            int gotCount = header["count"]?.GetValue<int>() ?? 0;
            int gotFrom  = header["from"]?.GetValue<int>() ?? 0;
            if (gotFrom != from || gotCount != count)
                throw new InvalidDataException(
                    $"dump block mismatch: requested from={from} count={count}, got from={gotFrom} count={gotCount}");

            // 3) バイナリ受信完了待ち (期待バイト数が揃うまで)
            await _dumpBytesReceived.Task.ConfigureAwait(false);

            // 4) dump_end イベントは待たない (完了はバイト数で判定済み。イベント行の
            //    欠落・遅延到着の影響を受けないようにするため)。
            return _dumpBuffer ?? Array.Empty<byte>();
        }
        finally
        {
            _dumpPendingId = -1;
            _dumpBuffer = null;
            _dumpBytesRead = 0;
            _dumpRemaining = 0;
            _dumpBytesReceived = null;
            _dumpProgress = null;
        }
    }

    // ============================================================
    // パース/シリアライズ ヘルパ
    // ============================================================
    private static JsonObject BuildSensorPatch(SensorSettingPatch p)
    {
        var o = new JsonObject();
        if (p.Enabled.HasValue)  o["enabled"]  = p.Enabled.Value;
        if (p.Interval.HasValue) o["interval"] = p.Interval.Value;
        return o;
    }

    private static JsonObject BuildCorrectionPatch(CorrectionCoefficientsPatch p)
    {
        var o = new JsonObject();
        if (p.A.HasValue) o["a"] = p.A.Value;
        if (p.B.HasValue) o["b"] = p.B.Value;
        return o;
    }

    private static SensorSetting ParseSensorSetting(JsonObject obj) => new(
        Enabled:  obj["enabled"]?.GetValue<bool>() ?? false,
        Interval: obj["interval"]?.GetValue<uint>() ?? 0);

    private static Settings ParseSettings(JsonObject result)
    {
        // v4 wire の general (= 温湿度+グローブ温度+CO2 一括) を、内部 6 センサ model の
        // t_dry/humidity/t_glb/co2 の 4 つに同値で fan-out する。MAUI 側 v4 UI は
        // t_dry のみを表示してこの値を編集する設計。
        var general     = ParseSensorSetting((JsonObject)result["general"]!);
        var velocity    = ParseSensorSetting((JsonObject)result["velocity"]!);
        var illuminance = ParseSensorSetting((JsonObject)result["illuminance"]!);
        return new Settings(
            DrybulbTemperature: general,
            RelativeHumidity:   general,
            GlobeTemperature:   general,
            Velocity:           velocity,
            Illuminance:        illuminance,
            Co2:                general,
            StartTime:          DateTimeOffset.FromUnixTimeSeconds(result["start_ts"]?.GetValue<long>() ?? 0));
    }

    private static CorrectionCoefficients ParseCorrectionPair(JsonObject obj) => new(
        A: obj["a"]?.GetValue<float>() ?? 1.0f,
        B: obj["b"]?.GetValue<float>() ?? 0.0f);

    private static CorrectionFactors ParseCorrectionFactors(JsonObject result) => new(
        DrybulbTemperature: ParseCorrectionPair((JsonObject)result["t_dry"]!),
        RelativeHumidity:   ParseCorrectionPair((JsonObject)result["humidity"]!),
        GlobeTemperature:   ParseCorrectionPair((JsonObject)result["t_glb"]!),
        Illuminance:        ParseCorrectionPair((JsonObject)result["illuminance"]!),
        Velocity:           ParseCorrectionPair((JsonObject)result["velocity"]!));

    private static Sample ParseSample(JsonObject data, DateTimeOffset ts)
    {
        return new Sample(
            Timestamp:              ts,
            DrybulbTemperature:     data["t"]?.GetValue<double>(),
            RelativeHumidity:       data["h"]?.GetValue<double>(),
            GlobeTemperature:       data["g"]?.GetValue<double>(),
            Velocity:               data["v"]?.GetValue<double>(),
            Illuminance:            data["l"]?.GetValue<int>(),
            Co2:                    data["c"]?.GetValue<int>(),
            WarmupCategories:       ParseStringArray(data["wu"]),
            DisconnectedCategories: ParseStringArray(data["dc"]),
            VelocityVoltage:        data["vv"]?.GetValue<int>());
    }

    /// <summary>data の wu/dc 配列を List&lt;string&gt; に変換。null / 非配列なら null。</summary>
    private static List<string>? ParseStringArray(JsonNode? node)
    {
        if (node is not JsonArray arr) return null;
        var list = new List<string>(arr.Count);
        foreach (var item in arr)
        {
            if (item?.GetValue<string>() is string s) list.Add(s);
        }
        return list;
    }

    private static ReadyEvent ParseReadyEvent(JsonObject data, DateTimeOffset ts) => new(
        Timestamp: ts,
        Uptime:    TimeSpan.FromSeconds(data["uptime_s"]?.GetValue<long>() ?? 0),
        IsLogging: data["logging"]?.GetValue<bool>() ?? false);

    private static Co2CalibrationProgress ParseCo2Progress(JsonObject data, DateTimeOffset ts)
    {
        var state = data["state"]?.GetValue<string>() switch
        {
            "pass" => Co2CalibrationState.Pass,
            "fail" => Co2CalibrationState.Fail,
            _      => Co2CalibrationState.Measuring,
        };
        return new Co2CalibrationProgress(
            Timestamp:     ts,
            Remaining:     TimeSpan.FromSeconds(data["remaining_s"]?.GetValue<int>() ?? 0),
            State:         state,
            CorrectionPpm: (short)(data["correction_ppm"]?.GetValue<int>() ?? 0),
            CurrentPpm:    data["current_ppm"]?.GetValue<int>() ?? 0);
    }
}
