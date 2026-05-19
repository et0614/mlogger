#nullable enable
using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using MLLib.Protocol.Transport;
using XBeeLibrary.Core;

namespace MLServer.Transport;

/// <summary>
/// 1 つの XBee Zigbee coordinator 上で 1 つの remote node 宛/発の双方向通信を
/// 抱える <see cref="ISerialTransport"/>。Send は coordinator.SendData(remote, bytes)、
/// Receive は <see cref="XBeeZigbeeCoordinatorMux"/> の per-remote stream。
/// </summary>
public sealed class XBeeZigbeeTransport : ISerialTransport
{
    private readonly XBeeZigbeeCoordinatorMux _mux;
    private readonly RemoteXBeeDevice _remote;
    private readonly BehaviorSubject<bool> _conn = new(true);
    private bool _disposed;

    public XBeeZigbeeTransport(XBeeZigbeeCoordinatorMux mux, RemoteXBeeDevice remote)
    {
        _mux = mux;
        _remote = remote;
        Received = _mux.StreamFor(remote.GetAddressString());
    }

    public bool IsConnected => !_disposed && _mux.Coordinator.IsOpen;

    public IObservable<bool> ConnectionState => _conn;
    public IObservable<ReadOnlyMemory<byte>> Received { get; }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(XBeeZigbeeTransport));
        ct.ThrowIfCancellationRequested();
        // XBeeLibrary.Core SendData は同期 API。background thread で実行して await 可能に。
        return Task.Run(() => _mux.Coordinator.SendData(_remote, data.ToArray()), ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _conn.OnNext(false); _conn.OnCompleted(); _conn.Dispose(); } catch { }
    }
}
