// MLLib (JsonRpcV4Protocol + SerialPortTransport) を v4 ファームに対して動作確認する
// 簡易コンソールテスト。
//
// 使い方:
//   dotnet run --project MLProtocolTest                 # COMポート自動検出
//   dotnet run --project MLProtocolTest -- COM3         # 明示指定

using System.IO.Ports;
using System.Reactive.Linq;
using MLLib.Protocol;
using MLLib.Protocol.Protocols;
using MLLib.Protocol.Transport;

const int BaudRate = 115200;

string? portName = args.Length > 0 ? args[0] : await FindDeviceAsync();
if (portName is null)
{
    Console.WriteLine("Error: M-Logger device not found.");
    return 1;
}

Console.WriteLine($"\nConnecting to {portName}...");
using var transport = new SerialPortTransport(portName, BaudRate);
await Task.Delay(500);    // ポート初期化待ち

using var protocol = await JsonRpcV4Protocol.CreateAsync(transport,
    new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);

var d = protocol.Device;
Console.WriteLine($"\nDevice: {d.Device}");
Console.WriteLine($"  firmware_version : {d.FirmwareVersion}");
Console.WriteLine($"  protocol_version : {d.ProtocolVersion}");
Console.WriteLine($"  hardware_id      : {d.HardwareId}");
Console.WriteLine($"  name             : {d.Name}");
Console.WriteLine($"  logging          : {d.IsLogging}");

int passed = 0, failed = 0;
async Task Run(string label, Func<Task> action)
{
    Console.Write($"\n[Test] {label} ... ");
    try { await action(); Console.WriteLine("OK"); passed++; }
    catch (Exception e) { Console.WriteLine($"FAIL: {e.GetType().Name}: {e.Message}"); failed++; }
}

await Run("get_settings", async () =>
{
    var s = await protocol.GetSettingsAsync();
    Console.WriteLine();
    Console.WriteLine($"    DrybulbTemperature: {s.DrybulbTemperature}");
    Console.WriteLine($"    RelativeHumidity  : {s.RelativeHumidity}");
    Console.WriteLine($"    GlobeTemperature  : {s.GlobeTemperature}");
    Console.WriteLine($"    Velocity          : {s.Velocity}");
    Console.WriteLine($"    Illuminance       : {s.Illuminance}");
    Console.WriteLine($"    Co2               : {s.Co2}");
    Console.WriteLine($"    StartTime         : {s.StartTime}");
});

await Run("set_settings PATCH + restore (illuminance.interval=77)", async () =>
{
    var orig = await protocol.GetSettingsAsync();
    var patched = await protocol.SetSettingsAsync(new SettingsPatch
    {
        Illuminance = new SensorSettingPatch(Interval: 77)
    });
    if (patched.Illuminance.Interval != 77)
        throw new Exception($"interval not 77: {patched.Illuminance.Interval}");
    if (patched.Illuminance.Enabled != orig.Illuminance.Enabled)
        throw new Exception("PATCH leaked into 'enabled'");
    await protocol.SetSettingsAsync(new SettingsPatch { Illuminance = new SensorSettingPatch(orig.Illuminance.Enabled, orig.Illuminance.Interval) });
});

await Run("set_settings out_of_range (interval=999999)", async () =>
{
    try
    {
        await protocol.SetSettingsAsync(new SettingsPatch { Velocity = new SensorSettingPatch(Interval: 999999) });
        throw new Exception("expected MLProtocolException");
    }
    catch (MLProtocolException e) when (e.Code == MLProtocolErrorCodes.OutOfRange) { /* OK */ }
});

await Run("get_correction", async () =>
{
    var c = await protocol.GetCorrectionAsync();
    Console.WriteLine();
    Console.WriteLine($"    DrybulbTemperature: a={c.DrybulbTemperature.A:F3}, b={c.DrybulbTemperature.B:F3}");
    Console.WriteLine($"    RelativeHumidity  : a={c.RelativeHumidity.A:F3}, b={c.RelativeHumidity.B:F3}");
    Console.WriteLine($"    GlobeTemperature  : a={c.GlobeTemperature.A:F3}, b={c.GlobeTemperature.B:F3}");
    Console.WriteLine($"    Illuminance       : a={c.Illuminance.A:F3}, b={c.Illuminance.B:F3}");
    Console.WriteLine($"    Velocity          : a={c.Velocity.A:F3}, b={c.Velocity.B:F3}");
});

await Run("set_correction out_of_range (t_dry.a=99)", async () =>
{
    try
    {
        await protocol.SetCorrectionAsync(new CorrectionFactorsPatch
        {
            DrybulbTemperature = new CorrectionCoefficientsPatch(A: 99.0f)
        });
        throw new Exception("expected MLProtocolException");
    }
    catch (MLProtocolException e) when (e.Code == MLProtocolErrorCodes.OutOfRange) { /* OK */ }
});

await Run("set_name (round-trip)", async () =>
{
    var origName = d.Name;
    var n1 = await protocol.SetNameAsync("v4_test");
    if (n1 != "v4_test") throw new Exception($"got: {n1}");
    var n2 = await protocol.SetNameAsync(origName);
    if (n2 != origName) throw new Exception($"restore failed: {n2}");
});

await Run("set_time (now ±2s)", async () =>
{
    var now = DateTimeOffset.UtcNow;
    var returned = await protocol.SetTimeAsync(now);
    if (Math.Abs((returned - now).TotalSeconds) > 2)
        throw new Exception($"diff={(returned - now).TotalSeconds:F1}s");
});

await Run("dump (USB-CDC)", async () =>
{
    var r = await protocol.DumpAsync();
    Console.WriteLine($"\n    records={r.RecordCount}, size={r.RecordSize}, format={r.Format}, bytes={r.Data.Length}");
    if (r.Data.Length != r.RecordCount * r.RecordSize)
        throw new Exception($"size mismatch");
});

await Run("start_logging → smp stream → stop_logging", async () =>
{
    // 全センサ 1秒間隔 (co2 は無効)
    await protocol.SetSettingsAsync(new SettingsPatch
    {
        DrybulbTemperature = new SensorSettingPatch(true, 1),
        RelativeHumidity   = new SensorSettingPatch(true, 1),
        GlobeTemperature   = new SensorSettingPatch(true, 1),
        Velocity           = new SensorSettingPatch(true, 1),
        Illuminance        = new SensorSettingPatch(true, 1),
        Co2                = new SensorSettingPatch(false, 1),
    });

    int count = 0;
    using var sub = protocol.Samples.Subscribe(s =>
    {
        count++;
        if (count <= 3)
            Console.WriteLine($"\n    [{count}] ts={s.Timestamp:HH:mm:ss}, t={s.DrybulbTemperature}, h={s.RelativeHumidity}, g={s.GlobeTemperature}, vel={s.Velocity}, l={s.Illuminance}");
    });

    await protocol.StartLoggingAsync(new LoggingConfig(
        new Transports(Zigbee: false, Ble: false, Flash: false, Usb: true),
        LoggingMode.Once));
    await Task.Delay(4000);
    await protocol.StopLoggingAsync();
    if (count < 2) throw new Exception($"expected ≥2 smp events, got {count}");
    Console.Write($"    {count} samples received ");
});

Console.WriteLine($"\n========== {passed} passed, {failed} failed ==========");
return failed == 0 ? 0 : 1;


// =======================================================================
async Task<string?> FindDeviceAsync()
{
    Console.WriteLine("Scanning ports...");
    foreach (var name in SerialPort.GetPortNames())
    {
        Console.Write($"  Checking {name}... ");
        try
        {
            using var t = new SerialPortTransport(name, BaudRate);
            await Task.Delay(1200);
            using var p = await JsonRpcV4Protocol.CreateAsync(t,
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
            if (p.Device.Device == "M-Logger")
            {
                Console.WriteLine("Found!");
                return name;
            }
            Console.WriteLine("Not M-Logger.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"({e.GetType().Name})");
        }
    }
    return null;
}
