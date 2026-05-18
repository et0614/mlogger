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
    /// <summary>v4 hello probe のタイムアウト (この時間内に JSON 応答が返らなければ v3 と判定)。</summary>
    public static TimeSpan V4ProbeTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// プロトコルを自動判定して <see cref="IMLProtocol"/> を返す。
    ///
    /// 判定フロー:
    ///   1. v4 hello を投げて <see cref="V4ProbeTimeout"/> 以内に JSON 応答が返れば v4。
    ///   2. タイムアウト/非JSON応答ならば v3 VER で再試行して <see cref="LegacyV3Protocol"/>。
    ///   3. それでも応答が無ければ最終的に例外を投げる。
    ///
    /// 順序: v4 を先に試す。v4 firmware は v3 コマンドに応答しないので逆順だと誤判定になる。
    /// v3 firmware は JSON コマンドを単に無視する (3文字コマンドプレフィックスに一致しないため)
    /// ので v4 probe を投げても害は無い。
    /// </summary>
    public static async Task<IMLProtocol> DetectAsync(ISerialTransport transport, CancellationToken ct = default)
    {
        // --- 1. v4 hello probe ---
        Exception? v4Error = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V4ProbeTimeout);
            var v4 = await JsonRpcV4Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);

            // 念のため protocol_version を確認
            if (v4.Device.ProtocolVersion >= 1) return v4;

            // 想定外: JSON 応答だったが v4 ではない
            v4.Dispose();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 呼び出し元キャンセル: そのまま再 throw
            throw;
        }
        catch (Exception ex)
        {
            // タイムアウト / 非JSON / 通信エラー (BLE TX タイムアウト等) / その他
            // → v3 フォールバックを試す
            v4Error = ex;
        }

        // --- 2. v3 VER で再試行 ---
        try
        {
            return await LegacyV3Protocol.CreateAsync(transport, ct).ConfigureAwait(false);
        }
        catch (Exception v3Ex) when (v4Error is not null)
        {
            // v4/v3 両方失敗 → 両方の情報を含む AggregateException
            throw new AggregateException(
                "Failed to detect protocol (both v4 hello and v3 VER probes failed)",
                v4Error, v3Ex);
        }
    }
}
