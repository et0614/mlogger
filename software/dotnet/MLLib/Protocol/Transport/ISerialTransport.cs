namespace MLLib.Protocol.Transport;

/// <summary>
/// バイト列を双方向にやり取りする抽象。
/// BLE (XBee BLE)・Zigbee (XBee Zigbee)・USB-CDC 等の共通インタフェース。
/// <see cref="IMLProtocol"/> 実装は本インタフェース経由でフレームを送受信する。
/// </summary>
public interface ISerialTransport : IDisposable
{
    bool IsConnected { get; }

    /// <summary>接続状態の変化を通知 (true: 接続、false: 切断)。</summary>
    IObservable<bool> ConnectionState { get; }

    /// <summary>受信したバイト列を到着順に流す。1要素 = 1 トランスポート単位 (XBee API frame, USB packet, ...)。</summary>
    IObservable<ReadOnlyMemory<byte>> Received { get; }

    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
}
