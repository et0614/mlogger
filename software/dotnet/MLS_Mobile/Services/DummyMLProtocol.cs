using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using MLLib.Protocol;

// MLLib.Protocol.DeviceInfo と Microsoft.Maui.Devices.DeviceInfo の名前衝突を解消
using DeviceInfo = MLLib.Protocol.DeviceInfo;

namespace MLS_Mobile.Services;

/// <summary>
/// 開発・デモ用の偽 IMLProtocol 実装。
/// 実機 BLE 接続なしで DataReceive / Thermal comfort / Moist air などの UI を確認できる。
///
///   - 1 秒ごとにリアルな範囲の Sample を発行 (温度 23-27°C / 湿度 45-65% /
///     グローブ 24-28°C / 風速 0-0.5 m/s / 照度 300-700 lx / CO2 400-1500 ppm)
///   - Get/Set 系コマンドは in-memory state で完結 (副作用なし)
///   - CO2 校正は短い fake シーケンスを CO2CalibrationUpdates に流して完了
///   - DumpAsync は空の DumpResult を返す
///
/// MLoggerScanner の「Open Demo Device」ボタンから MLUtility.UseDummyProtocol() を
/// 経由して活性化する。
/// </summary>
public sealed class DummyMLProtocol : IMLProtocol
{
    private readonly int _protocolVersion;

    public DeviceInfo Device { get; }

    public bool IsLogging { get; private set; } = true;

    // ============================================================
    // in-memory state
    // ============================================================
    private Settings _settings = new(
        DrybulbTemperature: new SensorSetting(true, 60),
        RelativeHumidity:   new SensorSetting(true, 60),
        GlobeTemperature:   new SensorSetting(true, 60),
        Velocity:           new SensorSetting(true, 60),
        Illuminance:        new SensorSetting(true, 60),
        Co2:                new SensorSetting(true, 60),
        StartTime:          DateTimeOffset.Now);

    private CorrectionFactors _corrections = new(
        DrybulbTemperature: new CorrectionCoefficients(1.0f, 0.0f),
        RelativeHumidity:   new CorrectionCoefficients(1.0f, 0.0f),
        GlobeTemperature:   new CorrectionCoefficients(1.0f, 0.0f),
        Illuminance:        new CorrectionCoefficients(1.0f, 0.0f),
        Velocity:           new CorrectionCoefficients(1.0f, 0.0f));

    private string _name = "Demo Device";

    // ============================================================
    // Sample 発行
    // ============================================================
    private readonly Subject<Sample> _samples  = new();
    private readonly Subject<ReadyEvent> _ready = new();
    private readonly Subject<Co2CalibrationProgress> _co2cal = new();
    private readonly IDispatcherTimer? _sampleTimer;
    private readonly Random _rng = new(42);

    public IObservable<Sample> Samples                               => _samples;
    public IObservable<ReadyEvent> ReadyHeartbeats                   => _ready;
    public IObservable<Co2CalibrationProgress> Co2CalibrationUpdates => _co2cal;
    public IObservable<TimeSyncRequest> TimeSyncRequests             => Observable.Empty<TimeSyncRequest>();

    /// <summary>
    /// <paramref name="protocolVersion"/> に 0 を渡すと v3 firmware 相当 (3 letter コマンド)
    /// として振る舞う ⇒ DeviceSetting UI が旧 5 行レイアウト + 電池パネル非表示になる。
    /// 1 以上で v4 firmware 相当 ⇒ 3 行レイアウト + 電池パネル表示。
    /// </summary>
    public DummyMLProtocol(int protocolVersion = 1)
    {
        _protocolVersion = protocolVersion;
        Device = new DeviceInfo(
            Device:           "M-Logger",
            FirmwareVersion:  protocolVersion >= 1 ? "demo (v4)" : "demo (v3.3.20)",
            ProtocolVersion:  protocolVersion,
            HardwareId:       "DEMO0000",
            Name:             "Demo Device",
            IsLogging:        true,
            HasCo2Sensor:     true);

        _sampleTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_sampleTimer != null)
        {
            _sampleTimer.Interval = TimeSpan.FromSeconds(1);
            _sampleTimer.Tick    += (_, _) => EmitSample();
            _sampleTimer.Start();
        }
    }

    private void EmitSample()
    {
        if (!IsLogging) return;

        double Jitter(double center, double half) => center + (_rng.NextDouble() - 0.5) * 2 * half;

        double dbt = Jitter(25.0,  2.0);
        double rh  = Jitter(55.0, 10.0);
        double glb = Jitter(26.0,  2.0);
        double vel = Math.Max(0, Jitter(0.20, 0.20));
        int    ill = (int)Math.Max(0, Jitter(500, 200));
        int    co2 = (int)Math.Max(400, Jitter(700, 400));   // 400〜1100 中心、ときどき警戒域

        _samples.OnNext(new Sample(
            Timestamp:          DateTimeOffset.Now,
            DrybulbTemperature: dbt,
            RelativeHumidity:   rh,
            GlobeTemperature:   glb,
            Velocity:           vel,
            Illuminance:        ill,
            Co2:                co2));
    }

    // ============================================================
    // get/set 系 (in-memory)
    // ============================================================
    public Task<Settings> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(_settings);

    public Task<Settings> SetSettingsAsync(SettingsPatch patch, CancellationToken ct = default)
    {
        _settings = new Settings(
            DrybulbTemperature: Patch(_settings.DrybulbTemperature, patch.DrybulbTemperature),
            RelativeHumidity:   Patch(_settings.RelativeHumidity,   patch.RelativeHumidity),
            GlobeTemperature:   Patch(_settings.GlobeTemperature,   patch.GlobeTemperature),
            Velocity:           Patch(_settings.Velocity,           patch.Velocity),
            Illuminance:        Patch(_settings.Illuminance,        patch.Illuminance),
            Co2:                Patch(_settings.Co2,                patch.Co2),
            StartTime:          patch.StartTime ?? _settings.StartTime);
        return Task.FromResult(_settings);
    }

    private static SensorSetting Patch(SensorSetting cur, SensorSettingPatch? p)
        => p == null ? cur : new SensorSetting(p.Enabled ?? cur.Enabled, p.Interval ?? cur.Interval);

    public Task<CorrectionFactors> GetCorrectionAsync(CancellationToken ct = default) => Task.FromResult(_corrections);

    public Task<CorrectionFactors> SetCorrectionAsync(CorrectionFactorsPatch patch, CancellationToken ct = default)
    {
        _corrections = new CorrectionFactors(
            DrybulbTemperature: PatchCc(_corrections.DrybulbTemperature, patch.DrybulbTemperature),
            RelativeHumidity:   PatchCc(_corrections.RelativeHumidity,   patch.RelativeHumidity),
            GlobeTemperature:   PatchCc(_corrections.GlobeTemperature,   patch.GlobeTemperature),
            Illuminance:        PatchCc(_corrections.Illuminance,        patch.Illuminance),
            Velocity:           PatchCc(_corrections.Velocity,           patch.Velocity));
        return Task.FromResult(_corrections);
    }

    private static CorrectionCoefficients PatchCc(CorrectionCoefficients cur, CorrectionCoefficientsPatch? p)
        => p == null ? cur : new CorrectionCoefficients(p.A ?? cur.A, p.B ?? cur.B);

    public Task<string> SetNameAsync(string name, CancellationToken ct = default)
    {
        _name = name;
        return Task.FromResult(name);
    }

    public Task<DateTimeOffset> SetTimeAsync(DateTimeOffset time, CancellationToken ct = default)
        => Task.FromResult(time);

    // v4: Alkaline 新品相当の電圧を返す (BatteryEstimator.DetectType の閾値 2850mV 超)
    // v3: LegacyV3Protocol と同様 unknown_command で throw (v3 firmware に get_battery 無し)
    public Task<BatteryInfo> GetBatteryAsync(CancellationToken ct = default)
    {
        if (_protocolVersion < 1)
            throw new MLProtocolException(MLProtocolErrorCodes.UnknownCommand, "get_battery is v4-only");
        return Task.FromResult(new BatteryInfo(VoltageMv: 3050, IsLow: false));
    }

    public Task StartLoggingAsync(LoggingConfig config, CancellationToken ct = default)
    {
        IsLogging = true;
        return Task.CompletedTask;
    }

    public Task StopLoggingAsync(CancellationToken ct = default)
    {
        IsLogging = false;
        return Task.CompletedTask;
    }

    public Task ClearDataAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task CalibrateCo2Async(Co2CalibrationMode mode, int targetPpm, CancellationToken ct = default)
    {
        // 短い fake 校正シーケンス (5 秒で完了)
        _ = Task.Run(async () =>
        {
            for (int remaining = 5; remaining > 0; remaining--)
            {
                _co2cal.OnNext(new Co2CalibrationProgress(
                    Timestamp:     DateTimeOffset.Now,
                    Remaining:     TimeSpan.FromSeconds(remaining),
                    State:         Co2CalibrationState.Measuring,
                    CorrectionPpm: 0,
                    CurrentPpm:    targetPpm + _rng.Next(-30, 30)));
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            _co2cal.OnNext(new Co2CalibrationProgress(
                Timestamp:     DateTimeOffset.Now,
                Remaining:     TimeSpan.Zero,
                State:         Co2CalibrationState.Pass,
                CorrectionPpm: (short)(targetPpm - 410),
                CurrentPpm:    targetPpm));
        }, ct);
        return Task.CompletedTask;
    }

    // Demo 用に少数のダミーレコード (60 件、1 分間隔) を返す
    private byte[]? _dummyDump;
    private const int DUMMY_RECORD_COUNT = 60;

    private byte[] BuildDummyDump()
    {
        if (_dummyDump != null) return _dummyDump;
        const int recSize = 22;
        var buf = new byte[DUMMY_RECORD_COUNT * recSize];
        var rng = new Random(123);
        long ts0 = DateTimeOffset.Now.AddMinutes(-DUMMY_RECORD_COUNT).ToUnixTimeSeconds();
        for (int i = 0; i < DUMMY_RECORD_COUNT; i++)
        {
            int o = i * recSize;
            buf[o] = 1; // generation
            uint ts = (uint)(ts0 + i * 60);
            buf[o + 1] = (byte)(ts & 0xFF);
            buf[o + 2] = (byte)((ts >> 8) & 0xFF);
            buf[o + 3] = (byte)((ts >> 16) & 0xFF);
            buf[o + 4] = (byte)((ts >> 24) & 0xFF);
            buf[o + 5] = 0x7F; // valid_flags: all 7 fields valid
            // illuminance: uint32 little endian, scaled lux*10
            uint ill = (uint)(rng.Next(300, 700) * 10);
            buf[o + 6] = (byte)(ill & 0xFF);
            buf[o + 7] = (byte)((ill >> 8) & 0xFF);
            buf[o + 8] = (byte)((ill >> 16) & 0xFF);
            buf[o + 9] = (byte)((ill >> 24) & 0xFF);
            // temp_dry int16, °C*100
            short dbt = (short)(2500 + rng.Next(-200, 200));
            buf[o + 10] = (byte)(dbt & 0xFF);
            buf[o + 11] = (byte)((dbt >> 8) & 0xFF);
            // temp_globe int16
            short glb = (short)(2600 + rng.Next(-200, 200));
            buf[o + 12] = (byte)(glb & 0xFF);
            buf[o + 13] = (byte)((glb >> 8) & 0xFF);
            // humidity uint16, %*100
            ushort rh = (ushort)(5500 + rng.Next(-1000, 1000));
            buf[o + 14] = (byte)(rh & 0xFF);
            buf[o + 15] = (byte)((rh >> 8) & 0xFF);
            // wind_speed uint16, m/s*10000
            ushort vel = (ushort)Math.Max(0, 2000 + rng.Next(-2000, 2000));
            buf[o + 16] = (byte)(vel & 0xFF);
            buf[o + 17] = (byte)((vel >> 8) & 0xFF);
            // voltage uint16
            ushort vlt = (ushort)(2500 + rng.Next(-100, 100));
            buf[o + 18] = (byte)(vlt & 0xFF);
            buf[o + 19] = (byte)((vlt >> 8) & 0xFF);
            // co2 uint16
            ushort co2 = (ushort)Math.Max(400, 700 + rng.Next(-200, 400));
            buf[o + 20] = (byte)(co2 & 0xFF);
            buf[o + 21] = (byte)((co2 >> 8) & 0xFF);
        }
        _dummyDump = buf;
        return buf;
    }

    public Task<DumpResult> GetCountAsync(CancellationToken ct = default)
        => Task.FromResult(new DumpResult(DUMMY_RECORD_COUNT, 22, "<BIBIhhHHHH>", ReadOnlyMemory<byte>.Empty));

    public Task<DumpResult> DumpAsync(IProgress<int>? progress = null, CancellationToken ct = default)
        => Task.FromResult(new DumpResult(DUMMY_RECORD_COUNT, 22, "<BIBIhhHHHH>", BuildDummyDump()));

    public void Dispose()
    {
        _sampleTimer?.Stop();
        _samples.OnCompleted(); _samples.Dispose();
        _ready.OnCompleted();   _ready.Dispose();
        _co2cal.OnCompleted();  _co2cal.Dispose();
    }
}
