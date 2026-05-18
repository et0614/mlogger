using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using MLLib.Protocol.Transport;

namespace MLLib.Protocol.Protocols;

/// <summary>
/// v3 ファーム (3文字 ASCII コマンド + CSV 応答) との通信実装。
/// v4 の <see cref="IMLProtocol"/> インタフェースに合わせて旧プロトコルを橋渡しする。
///
/// 制約:
///  - v3 にはリクエスト ID が無いので、応答プレフィックスでマッチさせる必要があり、
///    送信を <see cref="SemaphoreSlim"/> でシリアル化する (一度に1コマンドのみ)。
///  - v3 にネイティブな PATCH は無いので、set_* は GET → 在メモリ更新 → 全項目 CMS/SCF 送信で擬似実装。
///  - v3 では t_dry と humidity が同一フラグ (measure_th) を共有、velocity と illuminance は
///    独立。<see cref="SetSettingsAsync"/> で両方指定された場合 last-write-wins。
///  - v3 の dump は先頭 4byte で件数を LE 送信する形式 (v4 の JSON ヘッダではない)。
/// </summary>
public sealed class LegacyV3Protocol : IMLProtocol
{
    private static readonly Encoding Ascii = Encoding.ASCII;

    private readonly ISerialTransport _transport;
    private readonly IDisposable _rxSubscription;
    private readonly LineBuffer _lineBuffer = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    // 応答待ちは常に1個 (commandLock でシリアル化されているため)
    private TaskCompletionSource<string>? _pendingResponse;
    private string? _pendingPrefix;

    // 自発イベントストリーム
    private readonly Subject<Sample> _samples = new();
    private readonly Subject<ReadyEvent> _ready = new();
    private readonly Subject<Co2CalibrationProgress> _co2 = new();

    // dump 用バイナリ受信
    private byte[]? _dumpBuffer;
    private int _dumpBytesRead;
    private int _dumpRemaining;
    private TaskCompletionSource? _dumpBytesReceived;
    private TaskCompletionSource<int>? _dumpCountReceived;

    private DeviceInfo? _device;

    public DeviceInfo Device => _device ?? throw new InvalidOperationException("VER probe not yet completed");
    public IObservable<Sample> Samples => _samples.AsObservable();
    public IObservable<ReadyEvent> ReadyHeartbeats => _ready.AsObservable();
    public IObservable<Co2CalibrationProgress> Co2CalibrationUpdates => _co2.AsObservable();

    private LegacyV3Protocol(ISerialTransport transport)
    {
        _transport = transport;
        _rxSubscription = _transport.Received.Subscribe(OnBytesReceived);
    }

    public static async Task<LegacyV3Protocol> CreateAsync(ISerialTransport transport, CancellationToken ct = default)
    {
        var p = new LegacyV3Protocol(transport);
        try
        {
            var ver = await p.SendAsync("VER\r", "VER:", ct).ConfigureAwait(false);
            var name = await p.SendAsync("LLN\r", "LLN:", ct).ConfigureAwait(false);
            // ver は "VER:3.5.0\r" 形式、name は "LLN:<name>\r" 形式
            var versionStr = ver.Substring(4).TrimEnd('\r', '\n');
            var nameStr    = name.Substring(4).TrimEnd('\r', '\n').TrimEnd('\0');

            p._device = new DeviceInfo(
                Device:          "M-Logger",
                FirmwareVersion: versionStr,
                ProtocolVersion: 0,        // v3 端末を示す
                HardwareId:      "",       // v3 では取得不可
                Name:            nameStr,
                IsLogging:       false);   // v3 に直接の照会コマンド無し、初期 false
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
        _pendingResponse?.TrySetException(new ObjectDisposedException(nameof(LegacyV3Protocol)));
        _pendingResponse = null;
        _pendingPrefix = null;
        _dumpBytesReceived?.TrySetCanceled();
        _dumpCountReceived?.TrySetCanceled();

        _samples.OnCompleted(); _samples.Dispose();
        _ready.OnCompleted();   _ready.Dispose();
        _co2.OnCompleted();     _co2.Dispose();
        _commandLock.Dispose();
    }

    // ============================================================
    // 送受信の共通基盤
    // ============================================================
    /// <summary>
    /// ASCII コマンドを送り、指定プレフィックスで始まる行が到着するまで待つ。
    /// </summary>
    private async Task<string> SendAsync(string cmd, string expectedPrefix, CancellationToken ct)
    {
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingPrefix = expectedPrefix;
            _pendingResponse = tcs;

            using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            await _transport.SendAsync(Ascii.GetBytes(cmd), ct).ConfigureAwait(false);
            try { return await tcs.Task.ConfigureAwait(false); }
            finally { _pendingResponse = null; _pendingPrefix = null; }
        }
        finally { _commandLock.Release(); }
    }

    /// <summary>応答を期待しないコマンド送信。</summary>
    private async Task SendNoReplyAsync(string cmd, CancellationToken ct)
    {
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try { await _transport.SendAsync(Ascii.GetBytes(cmd), ct).ConfigureAwait(false); }
        finally { _commandLock.Release(); }
    }

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
            else if (_dumpCountReceived is not null && _dumpRemaining == 0 && _dumpBuffer is null)
            {
                // dump の先頭 4byte (件数 LE) を読み込み
                int needed = 4 - _dumpCountBufPos;
                int toRead = Math.Min(needed, data.Length - offset);
                for (int i = 0; i < toRead; i++) ProcessDumpCountByte(data.Span[offset + i]);
                offset += toRead;
            }
            else
            {
                _lineBuffer.Append(data.Span[offset..], OnLine);
                offset = data.Length;
            }
        }
    }

    // dump 件数(LE 4byte)組み立て用
    private byte[] _dumpCountBuf = new byte[4];
    private int _dumpCountBufPos;

    private void ProcessDumpCountByte(byte b)
    {
        _dumpCountBuf[_dumpCountBufPos++] = b;
        if (_dumpCountBufPos == 4)
        {
            int count = BitConverter.ToInt32(_dumpCountBuf, 0);
            _dumpCountReceived?.TrySetResult(count);
            _dumpCountBufPos = 0;
        }
    }

    private void OnLine(string line)
    {
        // 応答行: pending のプレフィックスに一致するなら最優先
        if (_pendingPrefix is not null && line.StartsWith(_pendingPrefix, StringComparison.Ordinal))
        {
            _pendingResponse?.TrySetResult(line);
            return;
        }

        // 自発イベント
        if (line.StartsWith("DTT:", StringComparison.Ordinal))
        {
            var s = ParseDtt(line);
            if (s is not null) _samples.OnNext(s);
            return;
        }
        if (line.StartsWith("WFC", StringComparison.Ordinal))
        {
            _ready.OnNext(new ReadyEvent(DateTimeOffset.UtcNow, TimeSpan.Zero, _device?.IsLogging ?? false));
            return;
        }
        if (line.StartsWith("CCL:", StringComparison.Ordinal))
        {
            var p = ParseCcl(line);
            if (p is not null) _co2.OnNext(p);
            return;
        }
        // HCS: 等のその他は今回は無視 (v4 に該当概念が無いか、内部情報なので非公開)
    }

    // ============================================================
    // 簡易コマンド
    // ============================================================
    public async Task<string> SetNameAsync(string name, CancellationToken ct = default)
    {
        if (name.Length > 20) throw new ArgumentException("name max 20 chars", nameof(name));
        var line = await SendAsync($"CLN{name}\r", "CLN:", ct);
        var newName = line.Substring(4).TrimEnd('\r', '\n').TrimEnd('\0');
        if (_device is not null) _device = _device with { Name = newName };
        return newName;
    }

    public async Task<DateTimeOffset> SetTimeAsync(DateTimeOffset time, CancellationToken ct = default)
    {
        var unix = time.ToUnixTimeSeconds();
        await SendAsync($"UCT{unix:D10}\r", "UCT", ct);
        return DateTimeOffset.FromUnixTimeSeconds(unix);
    }

    public async Task StopLoggingAsync(CancellationToken ct = default)
    {
        await SendAsync("ENL\r", "ENL", ct);
        if (_device is not null) _device = _device with { IsLogging = false };
    }

    public async Task ClearDataAsync(CancellationToken ct = default)
    {
        await SendAsync("CLR\r", "CLR", ct);
    }

    // ============================================================
    // start_logging (STL)
    // ============================================================
    public async Task StartLoggingAsync(LoggingConfig config, CancellationToken ct = default)
    {
        // STL{10桁unix}{zigbee:t/f/e}{ble:t/f}{flash:t/f}{usb:t/f}\r
        // 'e' = endless (auto_restart) かつ Zigbee 有効
        char zig = config.Mode == LoggingMode.AutoRestart ? 'e' : (config.Tx.Zigbee ? 't' : 'f');
        char ble = config.Tx.Ble   ? 't' : 'f';
        char fl  = config.Tx.Flash ? 't' : 'f';
        char usb = config.Tx.Usb   ? 't' : 'f';
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await SendAsync($"STL{now:D10}{zig}{ble}{fl}{usb}\r", "STL", ct);
        if (_device is not null) _device = _device with { IsLogging = true };
    }

    // ============================================================
    // get/set_settings (LMS/CMS)
    // ============================================================
    public async Task<Settings> GetSettingsAsync(CancellationToken ct = default)
    {
        var line = await SendAsync("LMS\r", "LMS:", ct);
        return ParseMeasurementSettings(line);
    }

    public async Task<Settings> SetSettingsAsync(SettingsPatch patch, CancellationToken ct = default)
    {
        // v3 は PATCH を持たないため、現在値を取得 → メモリ上で適用 → 全項目 CMS 送信
        var cur = await GetSettingsAsync(ct);
        var updated = ApplyPatch(cur, patch);
        var cmd = BuildCmsCommand(updated);
        var line = await SendAsync(cmd, "CMS:", ct);
        return ParseMeasurementSettings(line);
    }

    private static Settings ApplyPatch(Settings cur, SettingsPatch p)
    {
        // 注: v3 は m_th が t_dry と humidity 共有。両方指定なら humidity が後勝ち。
        var th  = cur.DrybulbTemperature;
        if (p.DrybulbTemperature is { } a) th = new SensorSetting(a.Enabled ?? th.Enabled, a.Interval ?? th.Interval);
        if (p.RelativeHumidity   is { } b) th = new SensorSetting(b.Enabled ?? th.Enabled, b.Interval ?? th.Interval);

        var glb = cur.GlobeTemperature;
        if (p.GlobeTemperature is { } c) glb = new SensorSetting(c.Enabled ?? glb.Enabled, c.Interval ?? glb.Interval);

        var vel = cur.Velocity;
        if (p.Velocity is { } d) vel = new SensorSetting(d.Enabled ?? vel.Enabled, d.Interval ?? vel.Interval);

        var ill = cur.Illuminance;
        if (p.Illuminance is { } e) ill = new SensorSetting(e.Enabled ?? ill.Enabled, e.Interval ?? ill.Interval);

        var co2 = cur.Co2;
        if (p.Co2 is { } f) co2 = new SensorSetting(f.Enabled ?? co2.Enabled, f.Interval ?? co2.Interval);

        var st = p.StartTime ?? cur.StartTime;

        return new Settings(th, th, glb, vel, ill, co2, st);
    }

    private static string BuildCmsCommand(Settings s)
    {
        // フォーマット (固定長): CMS<t/f>{iiiii}<t/f>{iiiii}<t/f>{iiiii}<t/f>{iiiii}{yyyyyyyyyy}f00000f00000f00000f<t/f>{iiiii}\r
        char TF(bool b) => b ? 't' : 'f';
        string IV(uint v) => Math.Min(v, 99999u).ToString("D5", CultureInfo.InvariantCulture);
        long unix = s.StartTime.ToUnixTimeSeconds();
        // 19 文字の legacy ダミー領域 (AD1/2/3/Prox)
        var sb = new StringBuilder("CMS");
        sb.Append(TF(s.DrybulbTemperature.Enabled));
        sb.Append(IV(s.DrybulbTemperature.Interval));
        sb.Append(TF(s.GlobeTemperature.Enabled));
        sb.Append(IV(s.GlobeTemperature.Interval));
        sb.Append(TF(s.Velocity.Enabled));
        sb.Append(IV(s.Velocity.Interval));
        sb.Append(TF(s.Illuminance.Enabled));
        sb.Append(IV(s.Illuminance.Interval));
        sb.Append(unix.ToString("D10", CultureInfo.InvariantCulture));
        sb.Append("f00000f00000f00000f");
        sb.Append(TF(s.Co2.Enabled));
        sb.Append(IV(s.Co2.Interval));
        sb.Append('\r');
        return sb.ToString();
    }

    private static Settings ParseMeasurementSettings(string line)
    {
        // "(LMS|CMS):m_th,i_th,m_glb,i_glb,m_vel,i_vel,m_ill,i_ill,start_dt,m_AD1,i_AD1,m_AD2,i_AD2,m_AD3,i_AD3,m_Prox,m_co2,i_co2"
        var body = line.Substring(line.IndexOf(':') + 1).TrimEnd('\r', '\n');
        var f = body.Split(',');
        if (f.Length < 18) throw new InvalidDataException($"unexpected LMS/CMS field count: {f.Length}");
        bool mTh   = f[0] == "1";   uint iTh   = uint.Parse(f[1], CultureInfo.InvariantCulture);
        bool mGlb  = f[2] == "1";   uint iGlb  = uint.Parse(f[3], CultureInfo.InvariantCulture);
        bool mVel  = f[4] == "1";   uint iVel  = uint.Parse(f[5], CultureInfo.InvariantCulture);
        bool mIll  = f[6] == "1";   uint iIll  = uint.Parse(f[7], CultureInfo.InvariantCulture);
        long startDt = long.Parse(f[8], CultureInfo.InvariantCulture);
        bool mCo2  = f[16] == "1";  uint iCo2  = uint.Parse(f[17], CultureInfo.InvariantCulture);
        return new Settings(
            DrybulbTemperature: new SensorSetting(mTh, iTh),
            RelativeHumidity:   new SensorSetting(mTh, iTh),   // v3 共有
            GlobeTemperature:   new SensorSetting(mGlb, iGlb),
            Velocity:           new SensorSetting(mVel, iVel),
            Illuminance:        new SensorSetting(mIll, iIll),
            Co2:                new SensorSetting(mCo2, iCo2),
            StartTime:          DateTimeOffset.FromUnixTimeSeconds(startDt));
    }

    // ============================================================
    // get/set_correction (LCF/SCF)
    // ============================================================
    public async Task<CorrectionFactors> GetCorrectionAsync(CancellationToken ct = default)
    {
        var line = await SendAsync("LCF\r", "LCF:", ct);
        return ParseCorrectionResponse(line);
    }

    public async Task<CorrectionFactors> SetCorrectionAsync(CorrectionFactorsPatch patch, CancellationToken ct = default)
    {
        var cur = await GetCorrectionAsync(ct);

        CorrectionCoefficients Apply(CorrectionCoefficients c, CorrectionCoefficientsPatch? p)
            => p is null ? c : new CorrectionCoefficients(p.A ?? c.A, p.B ?? c.B);

        var updated = new CorrectionFactors(
            Apply(cur.DrybulbTemperature, patch.DrybulbTemperature),
            Apply(cur.RelativeHumidity,   patch.RelativeHumidity),
            Apply(cur.GlobeTemperature,   patch.GlobeTemperature),
            Apply(cur.Illuminance,        patch.Illuminance),
            Apply(cur.Velocity,           patch.Velocity));

        // 範囲チェック (v4 と同じ範囲)
        ValidateRange("DrybulbTemperature", updated.DrybulbTemperature);
        ValidateRange("RelativeHumidity",   updated.RelativeHumidity);
        ValidateRange("GlobeTemperature",   updated.GlobeTemperature);
        ValidateRange("Illuminance",        updated.Illuminance);
        ValidateRange("Velocity",           updated.Velocity);

        var cmd = BuildScfCommand(updated);
        var line = await SendAsync(cmd, "SCF:", ct);
        return ParseCorrectionResponse(line);
    }

    private static void ValidateRange(string sensor, CorrectionCoefficients c)
    {
        if (c.A < CorrectionRanges.AMin || c.A > CorrectionRanges.AMax)
            throw new MLProtocolException(MLProtocolErrorCodes.OutOfRange, $"{sensor}.a out of range");
        var (bMin, bMax) = CorrectionRanges.BRange(sensor);
        if (c.B < bMin || c.B > bMax)
            throw new MLProtocolException(MLProtocolErrorCodes.OutOfRange, $"{sensor}.b out of range");
    }

    private static string BuildScfCommand(CorrectionFactors f)
    {
        // 整数 4 桁ずつエンコード (スケール: 一部は 1000、一部は 100、lux.b は 1)
        string D4(int v) => Math.Clamp(v, -999, 9999).ToString("0000;-000", CultureInfo.InvariantCulture);
        var sb = new StringBuilder("SCF");
        sb.Append(D4((int)Math.Round(f.DrybulbTemperature.A * 1000)));
        sb.Append(D4((int)Math.Round(f.DrybulbTemperature.B * 100)));
        sb.Append(D4((int)Math.Round(f.RelativeHumidity.A * 1000)));
        sb.Append(D4((int)Math.Round(f.RelativeHumidity.B * 100)));
        sb.Append(D4((int)Math.Round(f.GlobeTemperature.A * 1000)));
        sb.Append(D4((int)Math.Round(f.GlobeTemperature.B * 100)));
        sb.Append(D4((int)Math.Round(f.Illuminance.A * 1000)));
        sb.Append(D4((int)Math.Round(f.Illuminance.B)));         // lux.b はスケール1
        sb.Append(D4((int)Math.Round(f.Velocity.A * 1000)));
        sb.Append(D4((int)Math.Round(f.Velocity.B * 1000)));
        sb.Append('\r');
        return sb.ToString();
    }

    private static CorrectionFactors ParseCorrectionResponse(string line)
    {
        // "(LCF|SCF):dbtA,dbtB,hmdA,hmdB,glbA,glbB,luxA,luxB,velA,velB,vel0"
        var body = line.Substring(line.IndexOf(':') + 1).TrimEnd('\r', '\n');
        var f = body.Split(',');
        if (f.Length < 10) throw new InvalidDataException($"unexpected LCF/SCF field count: {f.Length}");
        float P(int i) => float.Parse(f[i], CultureInfo.InvariantCulture);
        return new CorrectionFactors(
            DrybulbTemperature: new CorrectionCoefficients(P(0), P(1)),
            RelativeHumidity:   new CorrectionCoefficients(P(2), P(3)),
            GlobeTemperature:   new CorrectionCoefficients(P(4), P(5)),
            Illuminance:        new CorrectionCoefficients(P(6), P(7)),
            Velocity:           new CorrectionCoefficients(P(8), P(9)));
    }

    // ============================================================
    // calibrate_co2 (IC2/CCL)
    // ============================================================
    public async Task CalibrateCo2Async(Co2CalibrationMode mode, int targetPpm, CancellationToken ct = default)
    {
        if (targetPpm < 0 || targetPpm > 65535)
            throw new MLProtocolException(MLProtocolErrorCodes.OutOfRange, "target_ppm must be 0-65535");
        if (mode == Co2CalibrationMode.Factory)
        {
            await SendAsync($"IC2{targetPpm:D5}\r", "IC2", ct);
        }
        else
        {
            // CCL は ACK 無し、応答ストリームとして CCL:... が後続で流れる
            await SendNoReplyAsync($"CCL{targetPpm:D5}\r", ct);
        }
    }

    // ============================================================
    // dump (DMP)
    // ============================================================
    public async Task<DumpResult> DumpAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _dumpCountReceived = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _dumpCountBufPos = 0;

            using (ct.Register(() =>
            {
                _dumpCountReceived?.TrySetCanceled();
                _dumpBytesReceived?.TrySetCanceled();
            }))
            {
                await _transport.SendAsync(Ascii.GetBytes("DMP\r"), ct).ConfigureAwait(false);
                int count = await _dumpCountReceived.Task.ConfigureAwait(false);

                const int recordSize = 18;    // v3 互換
                int total = count * recordSize;
                _dumpBuffer = new byte[total];
                _dumpBytesRead = 0;
                _dumpRemaining = total;
                _dumpBytesReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                if (total == 0) _dumpBytesReceived.TrySetResult();
                await _dumpBytesReceived.Task.ConfigureAwait(false);

                var data = _dumpBuffer;
                _dumpBuffer = null;
                return new DumpResult(count, recordSize, "<BIBIhhHHHH>", data);
            }
        }
        finally
        {
            _dumpBuffer = null;
            _dumpBytesRead = 0;
            _dumpRemaining = 0;
            _dumpBytesReceived = null;
            _dumpCountReceived = null;
            _commandLock.Release();
        }
    }

    // ============================================================
    // 自発イベントのパース
    // ============================================================
    private static Sample? ParseDtt(string line)
    {
        // "DTT:yyyy,MM/dd,HH:mm:ss,t_dry,humidity,t_glb,velocity,illuminance,glb_volt(0),vel_volt,n/a,n/a,n/a,co2"
        var body = line.Substring(4).TrimEnd('\r', '\n');
        var f = body.Split(',');
        if (f.Length < 14) return null;

        // 日時の組み立て (デバイス側ローカル時刻と仮定)
        DateTimeOffset ts;
        try
        {
            // f[0]="yyyy", f[1]="MM/dd", f[2]="HH:mm:ss"
            var year   = int.Parse(f[0], CultureInfo.InvariantCulture);
            var md     = f[1].Split('/');
            var hms    = f[2].Split(':');
            var dt = new DateTime(year,
                int.Parse(md[0], CultureInfo.InvariantCulture),
                int.Parse(md[1], CultureInfo.InvariantCulture),
                int.Parse(hms[0], CultureInfo.InvariantCulture),
                int.Parse(hms[1], CultureInfo.InvariantCulture),
                int.Parse(hms[2], CultureInfo.InvariantCulture),
                DateTimeKind.Unspecified);
            ts = new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
        }
        catch { ts = DateTimeOffset.Now; }

        double? D(int i) => f[i] == "n/a" ? null : double.Parse(f[i], CultureInfo.InvariantCulture);
        int?    I(int i) => f[i] == "n/a" ? null : int.Parse(f[i], CultureInfo.InvariantCulture);

        return new Sample(
            Timestamp:          ts,
            DrybulbTemperature: D(3),
            RelativeHumidity:   D(4),
            GlobeTemperature:   D(5),
            Velocity:           D(6),
            Illuminance:        f[7] == "n/a" ? null : (int)Math.Round(double.Parse(f[7], CultureInfo.InvariantCulture)),
            Co2:                I(13));
    }

    private static Co2CalibrationProgress? ParseCcl(string line)
    {
        // "CCL:remaining,state,correction,current"
        var body = line.Substring(4).TrimEnd('\r', '\n');
        var f = body.Split(',');
        if (f.Length < 4) return null;
        var state = f[1] switch
        {
            "pass"      => Co2CalibrationState.Pass,
            "fail"      => Co2CalibrationState.Fail,
            "measuring" => Co2CalibrationState.Measuring,
            _           => Co2CalibrationState.Measuring,
        };
        return new Co2CalibrationProgress(
            Timestamp:     DateTimeOffset.UtcNow,
            Remaining:     TimeSpan.FromSeconds(int.Parse(f[0], CultureInfo.InvariantCulture)),
            State:         state,
            CorrectionPpm: short.Parse(f[2], CultureInfo.InvariantCulture),
            CurrentPpm:    int.Parse(f[3], CultureInfo.InvariantCulture));
    }
}
