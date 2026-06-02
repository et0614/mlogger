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
    private TaskCompletionSource<int>? _dumpEndReceived;
    // 進捗報告先 (DumpAsync の IProgress<int> を OnBytesReceived から呼ぶ用)
    private IProgress<int>? _dumpProgress;
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
        _dumpEndReceived?.TrySetCanceled();

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
    private void OnBytesReceived(ReadOnlyMemory<byte> data)
    {
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
                _dumpProgress?.Report(_dumpBytesRead);
                if (_dumpRemaining == 0) _dumpBytesReceived?.TrySetResult();
            }
            else
            {
                // dump response 受信時に OnLine が _dumpRemaining を立てたら即座に
                // 行解釈を中断し、残りバイトを dump buffer 経路に流す。これをしないと
                // JSON header と binary chunk が同一 BLE notification に乗ったときに
                // binary が line buffer に詰まって捨てられる。
                int consumed = _lineBuffer.Append(
                    data.Span[offset..],
                    OnLine,
                    () => _dumpRemaining > 0);
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
                    int sent = data?["sent"]?.GetValue<int>() ?? 0;
                    _dumpEndReceived?.TrySetResult(sent);
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

    public async Task<DumpResult> DumpAsync(IProgress<int>? progress = null, CancellationToken ct = default)
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
        _dumpEndReceived   = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dumpProgress      = progress;

        using var registration = ct.Register(() =>
        {
            if (_pending.TryRemove(id, out var t)) t.TrySetCanceled(ct);
            _dumpBytesReceived?.TrySetCanceled();
            _dumpEndReceived?.TrySetCanceled();
        });

        try
        {
            // 1) dump コマンドを送信 (id は予約済み)
            var envelope = new JsonObject
            {
                ["v"]       = 1,
                ["id"]      = id,
                ["command"] = "dump",
            };
            var json = envelope.ToJsonString();
            DiagnosticSink?.Invoke($"TX id={id} cmd=dump len={json.Length}: {json}");
            await _transport.SendAsync(Encoding.UTF8.GetBytes(json + "\n"), ct).ConfigureAwait(false);

            // 2) header (JSON) 受信待ち。OnLine が同期的に _dumpBuffer/_dumpRemaining を立てる。
            var resp = await tcs.Task.ConfigureAwait(false);
            if (resp is not JsonObject header)
                throw new InvalidDataException("dump header was not a JSON object");
            int count       = header["count"]?.GetValue<int>() ?? 0;
            int recordSize  = header["record_size"]?.GetValue<int>() ?? 0;
            string format   = header["format"]?.GetValue<string>() ?? "";

            // 3) バイナリ受信完了待ち (0件なら即解決)
            if (count * recordSize == 0) _dumpBytesReceived.TrySetResult();
            await _dumpBytesReceived.Task.ConfigureAwait(false);

            // 4) dump_end イベント待ち
            await _dumpEndReceived.Task.ConfigureAwait(false);

            var data = _dumpBuffer ?? Array.Empty<byte>().AsMemory();
            return new DumpResult(count, recordSize, format, data);
        }
        finally
        {
            _dumpPendingId = -1;
            _dumpBuffer = null;
            _dumpBytesRead = 0;
            _dumpRemaining = 0;
            _dumpBytesReceived = null;
            _dumpEndReceived = null;
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
            DisconnectedCategories: ParseStringArray(data["dc"]));
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
