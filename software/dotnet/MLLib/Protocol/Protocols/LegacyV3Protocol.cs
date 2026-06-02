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

    /// <summary>line-level 診断 sink (各受信行を opt-in でログ出力)。MLServer などで使う。</summary>
    public static Action<string>? DiagnosticLineSink { get; set; }

    // ============================================================
    // 自動リトライ設定 (旧 firmware の取りこぼし対策, "P2")
    // ============================================================
    // 第1期 (3.3.17) / 第2期 (3.3.20) firmware は UART RX ISR の中で
    // append_command → solve_command (EEPROM 書込み含む) まで実行するため、
    // 処理が長くなると hardware FIFO (2 byte) overrun で host から来たバイトを
    // ロストする。これは LMS/CMS/SCF 等の通常通信でも発生し得る。
    //
    // host 側の救済策として、各 SendAsync を per-attempt timeout で
    // 区切り、応答が返らなければコマンドを再送する。SendAsync は元々
    // 先頭に \r を付けて送るので、再送 1 発目で firmware cmdBuff も flush される。
    //
    // 第3期 (3.3.40) 以降は ring buffer + 軽 ISR にリファクタされ取りこぼしが
    // 発生しないので、retry は実質的に発火しない (正常応答が per-attempt
    // timeout 内に必ず返る) → 出荷数最多の 3.3.40 機の UX には影響しない。

    /// <summary>各 attempt の応答待ち上限。これを超えると次の attempt へ。
    /// 正常応答は数十〜数百ms (EEPROM 書込み系でも ~100ms) なので、1.5s は安全側の余裕。</summary>
    public static TimeSpan SendRetryPerAttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>1 回の SendAsync で試す最大回数 (初回 + リトライ含む)。</summary>
    public static int SendRetryMaxAttempts { get; set; } = 3;

    /// <summary>attempt 間の待機時間。firmware / BLE スタックが落ち着く猶予。</summary>
    public static TimeSpan SendRetryInterAttemptDelay { get; set; } = TimeSpan.FromMilliseconds(150);

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

    // v3 でも IsLogging を提供 (start/stop での local 更新のみ、初期は false)
    private bool _isLogging;
    public bool IsLogging => _isLogging;

    public IObservable<Sample> Samples => _samples.AsObservable();
    public IObservable<ReadyEvent> ReadyHeartbeats => _ready.AsObservable();
    public IObservable<Co2CalibrationProgress> Co2CalibrationUpdates => _co2.AsObservable();

    // v3 firmware は time_sync_request 未対応のため empty observable を返す
    public IObservable<TimeSyncRequest> TimeSyncRequests => System.Reactive.Linq.Observable.Empty<TimeSyncRequest>();

    private LegacyV3Protocol(ISerialTransport transport)
    {
        _transport = transport;
        _rxSubscription = _transport.Received.Subscribe(OnBytesReceived);
    }

    /// <summary>
    /// VER/LLN/HCS probe を打たずに instance を生成する (passive observer 用)。
    /// 既に sleep に入っている子機からの DTT 等を観測するのに使う。
    /// </summary>
    public static LegacyV3Protocol CreatePassive(
        ISerialTransport transport,
        string deviceName = "MLogger")
    {
        var p = new LegacyV3Protocol(transport);
        p._device = new DeviceInfo(
            Device:          "M-Logger",
            FirmwareVersion: "unknown",
            ProtocolVersion: 0,        // v3
            HardwareId:      "",
            Name:            deviceName,
            IsLogging:       true,     // 通常は既にロギング中
            HasCo2Sensor:    false);   // probe してないので保守的に false
        p._isLogging = true;
        return p;
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

            // CO2 センサ機能は 3.3.20 で追加された (HCS コマンド・CMS/LMS の CO2 フィールド・DTT の CO2 フィールド)。
            // 3.3.19 以前は HCS 未対応で SendAsync が retry 後に timeout 例外で抜けるが、これは
            // 5s 弱の無駄な待ちになるので version で先回り skip する。
            bool hasCo2 = false;
            bool tryHcs = !Version.TryParse(versionStr, out var fwVer) || fwVer >= new Version(3, 3, 20);
            if (tryHcs)
            {
                try
                {
                    var hcs = await p.SendAsync("HCS\r", "HCS:", ct).ConfigureAwait(false);
                    hasCo2 = hcs.Substring(4).TrimStart().StartsWith("t", StringComparison.OrdinalIgnoreCase);
                }
                catch { /* HCS 非対応のエッジケース、false のまま */ }
            }

            p._device = new DeviceInfo(
                Device:          "M-Logger",
                FirmwareVersion: versionStr,
                ProtocolVersion: 0,        // v3 端末を示す
                HardwareId:      "",       // v3 では取得不可
                Name:            nameStr,
                IsLogging:       false,    // v3 に直接の照会コマンド無し、初期 false
                HasCo2Sensor:    hasCo2);
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
    ///
    /// 注: v3 firmware は受信バッファに以前の garbage (v4 hello probe の残骸など) が
    /// あると新コマンドを認識しないため、旧 MLogger.Make*Command パターンに倣って
    /// 自動で先頭に \r を付加し、受信バッファを flush させる。
    ///
    /// 旧 firmware (3.3.20 以前) では UART ISR overrun でコマンドがロストし得るので、
    /// <see cref="SendRetryPerAttemptTimeout"/> 以内に応答が無ければ最大
    /// <see cref="SendRetryMaxAttempts"/> 回まで再送する。SendOnceAsync が常に
    /// 先頭 \r を付けるので、再送だけで firmware cmdBuff の garbage も flush される。
    /// caller の <paramref name="ct"/> が cancel されたら retry せず即 bubble up。
    /// </summary>
    private async Task<string> SendAsync(string cmd, string expectedPrefix, CancellationToken ct)
    {
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int maxAttempts = Math.Max(1, SendRetryMaxAttempts);
            Exception? lastEx = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(SendRetryPerAttemptTimeout);
                try
                {
                    return await SendOnceAsync(cmd, expectedPrefix, attemptCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt < maxAttempts)
                {
                    // per-attempt timeout: 旧 firmware で ISR overrun → コマンドがロストの可能性が高い。
                    // 次回 SendOnceAsync 先頭 \r が firmware cmdBuff も flush するので、
                    // ここでは firmware が現在処理中の何かを終えるまで少し待つだけ。
                    lastEx = new TimeoutException(
                        $"{expectedPrefix} no response within {SendRetryPerAttemptTimeout.TotalMilliseconds:F0}ms (attempt {attempt}/{maxAttempts})");
                    DiagnosticLineSink?.Invoke($"[retry] {expectedPrefix} attempt {attempt} timed out, retrying");
                    await Task.Delay(SendRetryInterAttemptDelay, ct).ConfigureAwait(false);
                }
            }
            throw lastEx ?? new TimeoutException($"{expectedPrefix} failed after {maxAttempts} attempts");
        }
        finally { _commandLock.Release(); }
    }

    /// <summary>1 attempt 分の生送受信。retry / timeout は呼び出し側 (<see cref="SendAsync"/>) が管理する。</summary>
    private async Task<string> SendOnceAsync(string cmd, string expectedPrefix, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPrefix = expectedPrefix;
        _pendingResponse = tcs;

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        await _transport.SendAsync(Ascii.GetBytes("\r" + cmd), ct).ConfigureAwait(false);
        try { return await tcs.Task.ConfigureAwait(false); }
        finally { _pendingResponse = null; _pendingPrefix = null; }
    }

    /// <summary>応答を期待しないコマンド送信 (leading \r は SendAsync と同様に自動付加)。</summary>
    private async Task SendNoReplyAsync(string cmd, CancellationToken ct)
    {
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try { await _transport.SendAsync(Ascii.GetBytes("\r" + cmd), ct).ConfigureAwait(false); }
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
        DiagnosticLineSink?.Invoke(line.TrimEnd('\r', '\n'));

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

    public Task<BatteryInfo> GetBatteryAsync(CancellationToken ct = default)
    {
        // v3 firmware は battery voltage 取得コマンドを持たない (v4 で新設)
        throw new MLProtocolException(MLProtocolErrorCodes.UnknownCommand, "get_battery is v4-only");
    }

    public Task<DumpResult> GetCountAsync(CancellationToken ct = default)
    {
        // v3 firmware は count 取得コマンドを持たない (v4 で新設)。
        // v3 のスマホ dump は元々サポート外なので親機側でガードする想定。
        throw new MLProtocolException(MLProtocolErrorCodes.UnknownCommand, "get_count is v4-only");
    }

    public async Task<DateTimeOffset> SetTimeAsync(DateTimeOffset time, CancellationToken ct = default)
    {
        // v3 firmware は RTC を timezone 不明な naive 時刻として扱い、DTT 応答も
        // YMDHMS をそのまま local time として吐く。よってここでは「現在の local 成分を
        // UTC として encode した unix 秒」を送る必要がある (旧 MLogger.GetUnixTime の hack)。
        // 単純に time.ToUnixTimeSeconds() (= 実 UTC) を送ると firmware が UTC で動き
        // DTT YMDHMS が UTC になり、PC 側 ParseDtt (YMDHMS を local とみなす) で
        // タイムゾーン分のズレが出る (JST なら 9h 過去にずれる)。
        var local = time.LocalDateTime;
        var localAsUtc = DateTime.SpecifyKind(local, DateTimeKind.Utc);
        var unix = new DateTimeOffset(localAsUtc).ToUnixTimeSeconds();
        await SendAsync($"UCT{unix:D10}\r", "UCT", ct);
        return DateTimeOffset.FromUnixTimeSeconds(unix);
    }

    public async Task StopLoggingAsync(CancellationToken ct = default)
    {
        await SendAsync("ENL\r", "ENL", ct);
        _isLogging = false;
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
        // v3 firmware STL フォーマット: STL{10桁unix}{zigbee}{ble}{sd}\r
        // - zigbee: 't' / 'f' / 'e' ('e' = endless = AutoRestart かつ Zigbee 有効)
        // - ble:    't' / 'f'
        // - sd:     't' / 'f' (v4 でいう flash)
        // v3 には USB 出力が無いので Tx.Usb は無視する。フラグは 3 文字固定で、
        // 4 文字目を送ると firmware が \r の位置を誤認して STL コマンド全体が
        // 無効化される (MLServer Zigbee 受信が止まる原因だった)。
        // permanent モードでは ble/sd は強制 'f'。
        bool permanent = config.Mode == LoggingMode.AutoRestart;
        char zig = permanent ? 'e' : (config.Tx.Zigbee ? 't' : 'f');
        char ble = (!permanent && config.Tx.Ble)   ? 't' : 'f';
        char sd  = (!permanent && config.Tx.Flash) ? 't' : 'f';
        // 重要: v3 firmware STL handler は受信した unix 秒で内部 currentTime を
        // 上書きする (UCT で同期した時刻もリセットされる)。よって SetTimeAsync と
        // 同じく「local 成分を UTC として encode した unix 秒」を送る必要がある。
        // ここで実 UTC を送ると後続 DTT が UTC YMDHMS になり、host 側 ParseDtt
        // (YMDHMS を local とみなす) で JST 9h 過去にずれる。
        var localNow = DateTimeOffset.Now.LocalDateTime;
        var localAsUtc = DateTime.SpecifyKind(localNow, DateTimeKind.Utc);
        var now = new DateTimeOffset(localAsUtc).ToUnixTimeSeconds();
        await SendAsync($"STL{now:D10}{zig}{ble}{sd}\r", "STL", ct);
        _isLogging = true;
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
        // "(LMS|CMS):m_th,i_th,m_glb,i_glb,m_vel,i_vel,m_ill,i_ill,start_dt,m_AD1,i_AD1,m_AD2,i_AD2,m_AD3,i_AD3,m_Prox[,m_co2,i_co2]"
        // 3.3.19 以前は CO2 無し (16 fields)、3.3.20+ は CO2 有り (18 fields)。
        var body = line.Substring(line.IndexOf(':') + 1).TrimEnd('\r', '\n');
        var f = body.Split(',');
        if (f.Length < 16) throw new InvalidDataException($"unexpected LMS/CMS field count: {f.Length}");
        bool mTh   = f[0] == "1";   uint iTh   = uint.Parse(f[1], CultureInfo.InvariantCulture);
        bool mGlb  = f[2] == "1";   uint iGlb  = uint.Parse(f[3], CultureInfo.InvariantCulture);
        bool mVel  = f[4] == "1";   uint iVel  = uint.Parse(f[5], CultureInfo.InvariantCulture);
        bool mIll  = f[6] == "1";   uint iIll  = uint.Parse(f[7], CultureInfo.InvariantCulture);
        long startDt = long.Parse(f[8], CultureInfo.InvariantCulture);
        bool mCo2; uint iCo2;
        if (f.Length >= 18)
        {
            mCo2 = f[16] == "1";
            iCo2 = uint.Parse(f[17], CultureInfo.InvariantCulture);
        }
        else
        {
            mCo2 = false; iCo2 = 0;
        }
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
        if (mode == Co2CalibrationMode.Reset)
        {
            // v3 firmware は factory_reset 単独コマンドを持たない (v4 で新設)
            throw new MLProtocolException(MLProtocolErrorCodes.UnknownCommand, "Co2 factory reset is v4-only");
        }
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
        // "DTT:yyyy,MM/dd,HH:mm:ss,t_dry,humidity,t_glb,velocity,illuminance,glb_volt(0),vel_volt,n/a,n/a,n/a[,co2]"
        // 3.3.19 以前は CO2 無し (13 fields)、3.3.20+ は CO2 有り (14 fields)。
        var body = line.Substring(4).TrimEnd('\r', '\n');
        var f = body.Split(',');
        if (f.Length < 13) return null;

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
            Co2:                f.Length >= 14 ? I(13) : null);
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
