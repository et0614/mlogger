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

    // dump 用のバイナリモード状態
    private byte[]? _dumpBuffer;
    private int _dumpBytesRead;
    private int _dumpRemaining;
    private TaskCompletionSource? _dumpBytesReceived;
    private TaskCompletionSource<int>? _dumpEndReceived;

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

    public void Dispose()
    {
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
    }

    public DeviceInfo Device =>
        _device ?? throw new InvalidOperationException("hello not yet completed");

    public IObservable<Sample> Samples => _samples.AsObservable();
    public IObservable<ReadyEvent> ReadyHeartbeats => _ready.AsObservable();
    public IObservable<Co2CalibrationProgress> Co2CalibrationUpdates => _co2.AsObservable();

    // ============================================================
    // コマンド送信の共通基盤
    // ============================================================
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

        var bytes = Encoding.UTF8.GetBytes(envelope.ToJsonString() + "\n");
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
                if (_dumpRemaining == 0) _dumpBytesReceived?.TrySetResult();
            }
            else
            {
                _lineBuffer.Append(data.Span[offset..], OnLine);
                offset = data.Length;
            }
        }
    }

    private void OnLine(string line)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(line); }
        catch { return; }
        if (root is not JsonObject obj) return;

        // 応答 (id 有り)
        if (obj.TryGetPropertyValue("id", out var idNode) && idNode is not null)
        {
            int id = idNode.GetValue<int>();
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
                    if (data is not null) _ready.OnNext(ParseReadyEvent(data, ts));
                    break;
                case "co2_calibration_progress":
                    if (data is not null) _co2.OnNext(ParseCo2Progress(data, ts));
                    break;
                case "dump_end":
                    int sent = data?["sent"]?.GetValue<int>() ?? 0;
                    _dumpEndReceived?.TrySetResult(sent);
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
        return new DeviceInfo(
            Device:          result["device"]?.GetValue<string>() ?? "",
            FirmwareVersion: result["firmware_version"]?.GetValue<string>() ?? "",
            ProtocolVersion: result["protocol_version"]?.GetValue<int>() ?? 0,
            HardwareId:      result["hardware_id"]?.GetValue<string>() ?? "",
            Name:            result["name"]?.GetValue<string>() ?? "",
            IsLogging:       result["logging"]?.GetValue<bool>() ?? false);
    }

    public async Task<Settings> GetSettingsAsync(CancellationToken ct = default)
    {
        var result = RequireResult<JsonObject>(await CallAsync("get_settings", null, ct));
        return ParseSettings(result);
    }

    public async Task<Settings> SetSettingsAsync(SettingsPatch patch, CancellationToken ct = default)
    {
        var p = new JsonObject();
        if (patch.DrybulbTemperature is not null) p["t_dry"]       = BuildSensorPatch(patch.DrybulbTemperature);
        if (patch.RelativeHumidity   is not null) p["humidity"]    = BuildSensorPatch(patch.RelativeHumidity);
        if (patch.GlobeTemperature   is not null) p["t_glb"]       = BuildSensorPatch(patch.GlobeTemperature);
        if (patch.Velocity           is not null) p["velocity"]    = BuildSensorPatch(patch.Velocity);
        if (patch.Illuminance        is not null) p["illuminance"] = BuildSensorPatch(patch.Illuminance);
        if (patch.Co2                is not null) p["co2"]         = BuildSensorPatch(patch.Co2);
        if (patch.StartTime          is not null) p["start_ts"]    = patch.StartTime.Value.ToUnixTimeSeconds();

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
    }

    public async Task StopLoggingAsync(CancellationToken ct = default)
        => await CallAsync("stop_logging", null, ct);

    public async Task ClearDataAsync(CancellationToken ct = default)
        => await CallAsync("clear_data", null, ct);

    public async Task CalibrateCo2Async(Co2CalibrationMode mode, int targetPpm, CancellationToken ct = default)
    {
        var p = new JsonObject
        {
            ["mode"]       = mode == Co2CalibrationMode.Factory ? "factory" : "forced",
            ["target_ppm"] = targetPpm,
        };
        await CallAsync("calibrate_co2", p, ct);
    }

    public async Task<DumpResult> DumpAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // 1) dump コマンドを送り、JSON ヘッダ {count, record_size, format} を取得
        var header = RequireResult<JsonObject>(await CallAsync("dump", null, ct));
        int count       = header["count"]?.GetValue<int>() ?? 0;
        int recordSize  = header["record_size"]?.GetValue<int>() ?? 0;
        string format   = header["format"]?.GetValue<string>() ?? "";

        // 2) バイナリ受信モードに遷移
        int total = count * recordSize;
        _dumpBuffer       = new byte[total];
        _dumpBytesRead    = 0;
        _dumpRemaining    = total;
        _dumpBytesReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dumpEndReceived  = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // 3) バイナリ受信完了まで待機 (0件なら即解決)
            if (total == 0) _dumpBytesReceived.TrySetResult();
            using (ct.Register(() =>
            {
                _dumpBytesReceived?.TrySetCanceled();
                _dumpEndReceived?.TrySetCanceled();
            }))
            {
                await _dumpBytesReceived.Task.ConfigureAwait(false);
                // 4) dump_end イベント待ち
                await _dumpEndReceived.Task.ConfigureAwait(false);
            }

            var data = _dumpBuffer;
            return new DumpResult(count, recordSize, format, data);
        }
        finally
        {
            _dumpBuffer = null;
            _dumpBytesRead = 0;
            _dumpRemaining = 0;
            _dumpBytesReceived = null;
            _dumpEndReceived = null;
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

    private static Settings ParseSettings(JsonObject result) => new(
        DrybulbTemperature: ParseSensorSetting((JsonObject)result["t_dry"]!),
        RelativeHumidity:   ParseSensorSetting((JsonObject)result["humidity"]!),
        GlobeTemperature:   ParseSensorSetting((JsonObject)result["t_glb"]!),
        Velocity:           ParseSensorSetting((JsonObject)result["velocity"]!),
        Illuminance:        ParseSensorSetting((JsonObject)result["illuminance"]!),
        Co2:                ParseSensorSetting((JsonObject)result["co2"]!),
        StartTime:          DateTimeOffset.FromUnixTimeSeconds(result["start_ts"]?.GetValue<long>() ?? 0));

    private static CorrectionCoefficients ParseCorrectionPair(JsonObject obj) => new(
        A: obj["a"]?.GetValue<float>() ?? 1.0f,
        B: obj["b"]?.GetValue<float>() ?? 0.0f);

    private static CorrectionFactors ParseCorrectionFactors(JsonObject result) => new(
        DrybulbTemperature: ParseCorrectionPair((JsonObject)result["t_dry"]!),
        RelativeHumidity:   ParseCorrectionPair((JsonObject)result["humidity"]!),
        GlobeTemperature:   ParseCorrectionPair((JsonObject)result["t_glb"]!),
        Illuminance:        ParseCorrectionPair((JsonObject)result["illuminance"]!),
        Velocity:           ParseCorrectionPair((JsonObject)result["velocity"]!));

    private static Sample ParseSample(JsonObject data, DateTimeOffset ts) => new(
        Timestamp:          ts,
        DrybulbTemperature: data["t"]?.GetValue<double>(),
        RelativeHumidity:   data["h"]?.GetValue<double>(),
        GlobeTemperature:   data["g"]?.GetValue<double>(),
        Velocity:           data["vel"]?.GetValue<double>(),
        Illuminance:        data["l"]?.GetValue<int>(),
        Co2:                data["c"]?.GetValue<int>());

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
