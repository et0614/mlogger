using System.Text.Json;
using MLLib.Protocol.Protocols;
using MLLib.Protocol.Transport;

namespace MLLib.Protocol;

/// <summary>
/// 接続済み <see cref="ISerialTransport"/> に対してプロトコル種別 (v4 / v3) を自動判定し、
/// 対応する <see cref="IMLProtocol"/> 実装を返すファクトリ。
/// </summary>
public static class ProtocolFactory
{
    /// <summary>v4 hello probe のタイムアウト。主流ターゲット (v4 機) なので短く。</summary>
    public static TimeSpan V4ProbeTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>v3 VER probe のタイムアウト (この時間内に "VER:" 応答が返らなければ失敗)。
    /// v3 firmware は接続直後の BLE write 応答が数秒かかることがあるため長めに設定。</summary>
    public static TimeSpan V3ProbeTimeout { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// プロトコルを自動判定して <see cref="IMLProtocol"/> を返す。
    ///
    /// 判定フロー (v4 を先に試す):
    ///   1. v4 hello (~33バイト JSON) を投げて <see cref="V4ProbeTimeout"/> 以内に応答が返れば v4。
    ///   2. タイムアウト/通信エラーならば v3 VER (3バイト) で再試行。
    ///      v3 firmware は応答までに時間がかかることがあるので <see cref="V3ProbeTimeout"/> は長めに。
    ///   3. それでも応答が無ければ AggregateException。
    ///
    /// 順序が v4 先の理由:
    /// - v4 機が今後の主流で、v4 機の検出は ~50ms で完了させたい
    /// - v3 firmware は v4 hello JSON を未知コマンドとして無視するので副作用なし
    /// - v3 fallback パスの LegacyV3Protocol.SendAsync は先頭に \r を付けて送るので、
    ///   v3 firmware 側のコマンドバッファに v4 hello の残骸があっても \r で flush される
    /// - v3 機の VER probe には十分な timeout を与えられる (v4 機の UX を犠牲にしない)
    /// </summary>
    public static async Task<IMLProtocol> DetectAsync(ISerialTransport transport, CancellationToken ct = default)
    {
        // --- 1. v4 hello probe (主流) ---
        Exception? v4Error = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V4ProbeTimeout);
            var v4 = await JsonRpcV4Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);
            if (v4.Device.ProtocolVersion >= 1) return v4;
            v4.Dispose();
            throw new InvalidDataException("v4 hello succeeded but protocol_version < 1");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // タイムアウト / 通信エラー → v3 fallback を試す
            v4Error = ex;
        }

        // --- 2. v3 VER probe (fallback) ---
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V3ProbeTimeout);
            return await LegacyV3Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);
        }
        catch (Exception v3Ex) when (v4Error is not null && !ct.IsCancellationRequested)
        {
            throw new AggregateException(
                "Failed to detect protocol (both v4 hello and v3 VER probes failed)",
                v4Error, v3Ex);
        }
    }
}
