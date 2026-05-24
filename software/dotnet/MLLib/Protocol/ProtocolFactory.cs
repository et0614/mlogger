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
    /// device ごとに前回検出した protocol kind を覚える hint cache。
    /// 同じ device に再接続する際、wrong-protocol の probe 待ち (v3 機なら最大 2 秒の v4 hello timeout) を skip できる。
    /// </summary>
    public enum ProtocolKind { Unknown, V3Ascii, V4JsonRpc }

    private static readonly Dictionary<string, ProtocolKind> _hintCache = new();
    private static readonly object _hintLock = new();

    /// <summary>device key (BLE address / XBee LowAddress 等) に紐付く前回検出結果を返す。未知なら Unknown。</summary>
    public static ProtocolKind GetCachedKind(string deviceKey)
    {
        if (string.IsNullOrEmpty(deviceKey)) return ProtocolKind.Unknown;
        lock (_hintLock)
            return _hintCache.TryGetValue(deviceKey, out var v) ? v : ProtocolKind.Unknown;
    }

    /// <summary>protocol kind を記録 (<see cref="ProtocolKind.Unknown"/> を渡すと削除)。</summary>
    public static void RecordKind(string deviceKey, ProtocolKind kind)
    {
        if (string.IsNullOrEmpty(deviceKey)) return;
        lock (_hintLock)
        {
            if (kind == ProtocolKind.Unknown) _hintCache.Remove(deviceKey);
            else _hintCache[deviceKey] = kind;
        }
    }

    /// <summary>cache 全消去 (テスト/デバッグ用)。</summary>
    public static void ClearKindCache()
    {
        lock (_hintLock) _hintCache.Clear();
    }

    /// <summary>
    /// プロトコルを自動判定して <see cref="IMLProtocol"/> を返す (cache hint なし版)。
    /// </summary>
    public static Task<IMLProtocol> DetectAsync(ISerialTransport transport, CancellationToken ct = default)
        => DetectAsync(transport, deviceKey: null, ct);

    /// <summary>
    /// プロトコルを自動判定して <see cref="IMLProtocol"/> を返す。
    ///
    /// 判定フロー:
    ///   - <paramref name="deviceKey"/> が hint cache にあれば、その kind を先に試す (hit 経路)。
    ///     成功すれば即返し、失敗 (firmware アップグレード等で stale) なら反対側に fall through。
    ///   - hint 未知時: v4 hello probe → v3 VER probe の順 (従来動作)。
    ///   - 両方失敗で <see cref="AggregateException"/>。
    ///
    /// 順序 (hint 無し時) が v4 先の理由:
    /// - v4 機が今後の主流で、v4 機の検出は ~50ms で完了させたい
    /// - v3 firmware は v4 hello JSON を未知コマンドとして無視するので副作用なし
    /// - v3 fallback パスの LegacyV3Protocol.SendAsync は先頭に \r を付けて送るので、
    ///   v3 firmware 側のコマンドバッファに v4 hello の残骸があっても \r で flush される
    /// - v3 機の VER probe には十分な timeout を与えられる (v4 機の UX を犠牲にしない)
    ///
    /// hint cache の効果 (出荷数の多い v3 ASCII 機での再接続を想定):
    /// - 初回接続: 従来通り (v4 hello 2s timeout + v3 fallback 数百ms = ~2.5s)
    /// - 2 回目以降: v3 hint → v3 を直接 → ~数百ms (v4 hello timeout の 2 秒分が消える)
    /// </summary>
    public static async Task<IMLProtocol> DetectAsync(
        ISerialTransport transport, string? deviceKey, CancellationToken ct = default)
    {
        var hint = deviceKey is not null ? GetCachedKind(deviceKey) : ProtocolKind.Unknown;

        // --- hint == V3Ascii: v3 を先に試す ---
        if (hint == ProtocolKind.V3Ascii)
        {
            try
            {
                using var v3cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                v3cts.CancelAfter(V3ProbeTimeout);
                var v3 = await LegacyV3Protocol.CreateAsync(transport, v3cts.Token).ConfigureAwait(false);
                return v3;  // cache hit → 既に v3 と記録済みなので update 不要
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                // cache stale (firmware が v4 にアップグレードされた等) → cache 無効化して v4 経路へ fall through
                if (deviceKey is not null) RecordKind(deviceKey, ProtocolKind.Unknown);
            }
        }

        // --- 1. v4 hello probe (主流 / hint==V4JsonRpc / hint==Unknown / v3 hint 失敗時) ---
        Exception? v4Error = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V4ProbeTimeout);
            var v4 = await JsonRpcV4Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);
            if (v4.Device.ProtocolVersion >= 1)
            {
                if (deviceKey is not null) RecordKind(deviceKey, ProtocolKind.V4JsonRpc);
                return v4;
            }
            v4.Dispose();
            throw new InvalidDataException("v4 hello succeeded but protocol_version < 1");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            v4Error = ex;
        }

        // --- 2. v3 VER probe (fallback) ---
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(V3ProbeTimeout);
            var v3 = await LegacyV3Protocol.CreateAsync(transport, cts.Token).ConfigureAwait(false);
            if (deviceKey is not null) RecordKind(deviceKey, ProtocolKind.V3Ascii);
            return v3;
        }
        catch (Exception v3Ex) when (v4Error is not null && !ct.IsCancellationRequested)
        {
            throw new AggregateException(
                "Failed to detect protocol (both v4 hello and v3 VER probes failed)",
                v4Error, v3Ex);
        }
    }
}
