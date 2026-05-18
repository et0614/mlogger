using System.Reactive.Linq;
using System.Reactive.Subjects;
using DigiIoT.Maui.Devices.XBee;
using MLLib.Protocol.Transport;
using XBeeLibrary.Core.Events.Relay;

namespace MLS_Mobile.Transport;

/// <summary>
/// DigiIoT.Maui の <see cref="XBeeBLEDevice"/> を <see cref="ISerialTransport"/> でラップ。
/// MAUI アプリ (iOS/Android) で v4 ファームへの接続に使う。
///
/// 注意:
/// - 接続/切断は呼び出し側 (MLUtility.OpenXbee/CloseXbee) が責任を持つ。
///   本クラスのコンストラクタは「既に Connect 済み」の <see cref="XBeeBLEDevice"/> を受け取り、
///   <see cref="Dispose"/> では Disconnect しない (イベント購読解除のみ)。
/// - <see cref="XBeeBLEDevice.SerialDataReceived"/> は UI 以外のスレッドで発火し得る。
///   <see cref="Received"/> 購読側で必要なら ObserveOn で UI スレッドへディスパッチすること。
/// </summary>
public sealed class BleXBeeTransport : ISerialTransport
{
    private readonly XBeeBLEDevice _device;
    private readonly Subject<ReadOnlyMemory<byte>> _received = new();
    private readonly Subject<bool> _connectionState = new();
    private bool _disposed;

    public BleXBeeTransport(XBeeBLEDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _device.SerialDataReceived += OnSerialDataReceived;
        // 構築時点で既に接続済の前提
        _connectionState.OnNext(_device.IsConnected);
    }

    public bool IsConnected => _device.IsConnected;

    public IObservable<bool> ConnectionState => _connectionState.AsObservable();

    public IObservable<ReadOnlyMemory<byte>> Received => _received.AsObservable();

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_device.IsConnected)
            throw new InvalidOperationException("XBeeBLEDevice is not connected");

        // DigiIoT.Maui の SendSerialData は接続直後や BLE 状態によって
        // "Timeout writing in the TX Characteristic" を投げることがある。
        // 旧コード (MLoggerScanner) では 5回 × 100ms のリトライで吸収していたので、
        // transport 層でも同等のリトライを行う。
        const int MaxAttempts = 5;
        var payload = data.ToArray();
        Exception? lastError = null;
        for (int i = 0; i < MaxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                _device.SendSerialData(payload);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (i < MaxAttempts - 1)
                    await Task.Delay(150, ct).ConfigureAwait(false);
            }
        }
        throw lastError ?? new InvalidOperationException("SendSerialData failed after retries");
    }

    private void OnSerialDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        if (_disposed) return;
        if (e.Data is { Length: > 0 } d)
            _received.OnNext(d.AsMemory());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _device.SerialDataReceived -= OnSerialDataReceived;
        }
        catch { /* ignore */ }
        _received.OnCompleted();
        _received.Dispose();
        _connectionState.OnCompleted();
        _connectionState.Dispose();
    }
}
