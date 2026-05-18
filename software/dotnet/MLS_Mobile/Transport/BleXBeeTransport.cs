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
        ct.ThrowIfCancellationRequested();

        // DigiIoT.Maui の SendSerialData は同期 API。
        // (1) Task.Run で threadpool に逃がして UI を凍結させない
        // (2) WaitAsync(ct) で ct がキャンセルされたら即座に OperationCanceledException
        //     を投げて呼び出し元に制御を返す。背景の SendSerialData は zombie として
        //     継続するが、UI 経路は確実に解放される
        var payload = data.ToArray();
        await Task.Run(() => _device.SendSerialData(payload)).WaitAsync(ct).ConfigureAwait(false);
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
