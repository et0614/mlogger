#nullable enable
using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using XBeeLibrary.Core;
using XBeeLibrary.Core.Events;

namespace MLServer.Transport;

/// <summary>
/// 1 つの XBee Zigbee coordinator が複数の remote node から受信する際、フレームを
/// 送信元アドレスで demux し per-remote IObservable を提供する。新規 remote
/// アドレスを観測したら <see cref="NewRemoteDiscovered"/> イベントで通知。
///
/// 各 <see cref="XBeeZigbeeTransport"/> はこの mux から自分の remote の stream を
/// 取得して IMLProtocol に流す。
/// </summary>
public sealed class XBeeZigbeeCoordinatorMux : IDisposable
{
    private readonly ConcurrentDictionary<string, Subject<ReadOnlyMemory<byte>>> _streams = new();

    public event Action<RemoteXBeeDevice>? NewRemoteDiscovered;

    public ZigBeeDevice Coordinator { get; }

    public XBeeZigbeeCoordinatorMux(ZigBeeDevice coordinator)
    {
        Coordinator = coordinator;
        Coordinator.DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        var addr = e.DataReceived.Device.GetAddressString();
        bool isNew = !_streams.ContainsKey(addr);
        var subject = _streams.GetOrAdd(addr, _ => new Subject<ReadOnlyMemory<byte>>());
        if (isNew)
        {
            try { NewRemoteDiscovered?.Invoke(e.DataReceived.Device); }
            catch (Exception ex) { Console.WriteLine("NewRemoteDiscovered handler error: " + ex.Message); }
        }
        subject.OnNext(e.DataReceived.Data);
    }

    /// <summary>指定 remote アドレスの受信ストリームを得る (subject が無ければ作る)。</summary>
    public IObservable<ReadOnlyMemory<byte>> StreamFor(string remoteAddress)
        => _streams.GetOrAdd(remoteAddress, _ => new Subject<ReadOnlyMemory<byte>>()).AsObservable();

    /// <summary>coordinator が close/re-open された後に DataReceived を再バインド。</summary>
    public void RebindAfterReopen()
    {
        Coordinator.DataReceived -= OnDataReceived;
        Coordinator.DataReceived += OnDataReceived;
    }

    public void Dispose()
    {
        try { Coordinator.DataReceived -= OnDataReceived; } catch { }
        foreach (var s in _streams.Values)
        {
            try { s.OnCompleted(); s.Dispose(); } catch { }
        }
    }
}
