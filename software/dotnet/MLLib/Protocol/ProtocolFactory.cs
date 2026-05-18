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
    /// <summary>v3 VER probe のタイムアウト (この時間内に "VER:" 応答が返らなければ v4 へ)。</summary>
    public static TimeSpan V3ProbeTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>v4 hello probe のタイムアウト。</summary>
    public static TimeSpan V4ProbeTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// プロトコルを自動判定して <see cref="IMLProtocol"/> を返す。
    ///
    /// 判定フロー (v3 を先に試す):
    ///   1. v3 VER (3バイト) を投げて <see cref="V3ProbeTimeout"/> 以内に "VER:" 応答が返れば v3。
    ///   2. タイムアウト/通信エラーならば v4 hello (~33バイト) で再試行。
    ///   3. それでも応答が無ければ AggregateException。
    ///
    /// 順序が v3 先の理由:
    /// - v3 VER の方が短く (3バイト)、BLE MTU/iOS BLE スタック の問題を起こしにくい
    /// - v3 firmware は VER に応答するが v4 firmware は Phase E で v3 コマンドを全廃したので
    ///   v4 端末では VER は無視 (応答無し)
    /// - 結果として、v3 端末は ~50ms で判定、v4 端末は V3ProbeTimeout (2秒) + hello で判定
    /// </summary>
    public static async Task<IMLProtocol> DetectAsync(ISerialTransport transport, CancellationToken ct = default)
    {
        // --- 1. v3 VER probe ---
        Exception? v3Error = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V3ProbeTimeout);
            return await LegacyV3Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // タイムアウト / 通信エラー → v4 フォールバックを試す
            v3Error = ex;
        }

        // --- 2. v4 hello で再試行 ---
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V4ProbeTimeout);
            var v4 = await JsonRpcV4Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);
            if (v4.Device.ProtocolVersion >= 1) return v4;
            v4.Dispose();
            throw new InvalidDataException("v4 hello succeeded but protocol_version < 1");
        }
        catch (Exception v4Ex) when (v3Error is not null && !ct.IsCancellationRequested)
        {
            throw new AggregateException(
                "Failed to detect protocol (both v3 VER and v4 hello probes failed)",
                v3Error, v4Ex);
        }
    }
}
