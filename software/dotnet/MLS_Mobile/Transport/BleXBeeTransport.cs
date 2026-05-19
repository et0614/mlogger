using System.Reactive.Linq;
using System.Reactive.Subjects;
using DigiIoT.Maui.Devices.XBee;
using MLLib.Protocol.Transport;

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
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// XBee BLE characteristic の単一 write 上限 (DigiIoT.Maui SendSerialData が
    /// "Data length cannot be greater than 255 bytes" を throw する) を回避するための
    /// 1 チャンクあたりの最大バイト数。マージンを取って 200B。
    /// </summary>
    private const int MaxBleChunkBytes = 200;

    /// <summary>
    /// 連続 SendAsync の最小間隔 [ms]。0 でスロットリング無効。
    /// (過去に 150/500ms を試したが効果未確定だったため一旦 0 に戻している)
    /// </summary>
    private const int MinSendIntervalMs = 0;

    /// <summary>
    /// multi-chunk TX における chunk 間の最小間隔 [ms]。0 で遅延なし。
    /// (過去に 50ms を試したが効果未確定だったため一旦 0 に戻している)
    /// </summary>
    private const int InterChunkDelayMs = 0;

    /// <summary>診断: chunk 毎の TX 結果 (size / time / error) を LogView に流すシンク。</summary>
    public static Action<string>? TxChunkSink { get; set; }

    public BleXBeeTransport(XBeeBLEDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        // 注: DigiIoT.Maui の SerialDataReceived は他 (MLUtility 静的コールバック等) からも
        // 購読されている。マルチ subscriber の挙動が信頼できないため、ここでは直接購読せず
        // 受信データは外部 (MLUtility) から FeedReceived() で流し込んでもらう。
        _connectionState.OnNext(_device.IsConnected);
    }

    /// <summary>
    /// 外部からの受信データ供給口。
    /// XBeeBLEDevice.SerialDataReceived の発火元 (MLUtility 静的コールバック等) から
    /// 受信バイト列を本メソッドに渡してもらう。
    /// </summary>
    /// <summary>
    /// FeedReceived 毎に生バイト情報を吐く診断シンク。
    /// MAUI 側から MLUtility.WriteLog にバインドして LogView で観察。
    /// </summary>
    public static Action<int, byte[]>? DiagnosticRxSink { get; set; }

    public void FeedReceived(byte[] data)
    {
        if (_disposed) return;
        if (data is { Length: > 0 })
        {
            DiagnosticRxSink?.Invoke(data.Length, data);
            _received.OnNext(data.AsMemory());
        }
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

        // _sendLock で SendAsync 呼び出し全体をシリアライズする。複数の RPC を並行発火
        // した場合 (例: OnAppearing で StopLoggingAsync と GetSettingsAsync を同時に呼ぶ)
        // 1 つの JSON line が他のバイト列に分断される (firmware が parse 失敗 → 応答返らず
        // → クライアント側 timeout) のを防ぐ。
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // DigiIoT.Maui の SendSerialData は同期 API かつ 255 バイト超を弾く。
            // 大きい JSON (例: set_settings ~280B) を ≤200B のチャンクに分割して
            // 順次送信し、firmware 側で UART/BLE バイトストリームとして連結させる。
            // SendSerialData 自体は (1) Task.Run で threadpool に逃がして UI 凍結回避、
            // (2) WaitAsync(ct) で ct キャンセル時に即制御を返す。
            var payload = data.ToArray();
            int chunkIdx = 0;
            int totalChunks = (payload.Length + MaxBleChunkBytes - 1) / MaxBleChunkBytes;
            for (int offset = 0; offset < payload.Length; offset += MaxBleChunkBytes)
            {
                ct.ThrowIfCancellationRequested();
                int len = Math.Min(MaxBleChunkBytes, payload.Length - offset);
                var chunk = new byte[len];
                Buffer.BlockCopy(payload, offset, chunk, 0, len);
                chunkIdx++;

                // 診断: chunk 毎の SendSerialData 所要時間を計測
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    await Task.Run(() => _device.SendSerialData(chunk)).WaitAsync(ct).ConfigureAwait(false);
                    sw.Stop();
                    TxChunkSink?.Invoke($"chunk {chunkIdx}/{totalChunks} {len}B OK {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    TxChunkSink?.Invoke($"chunk {chunkIdx}/{totalChunks} {len}B FAIL {sw.ElapsedMilliseconds}ms {ex.GetType().Name}: {ex.Message}");
                    throw;
                }

                // chunk 間にも小休止 (BLE GATT write キュー / XBee 内部処理に追従時間を確保)。
                // chunk1->chunk2 を背中合わせで投げると 2回目以降の multi-chunk TX が失われる
                // 実機現象 (Phase 4 P4 #2/#3 FAIL) への対策。
                if (offset + MaxBleChunkBytes < payload.Length)
                    await Task.Delay(InterChunkDelayMs, ct).ConfigureAwait(false);
            }

            // 次の SendAsync が即座に走らないように lock 内で待機。これにより BLE write キュー
            // (および firmware の Xbee_BlChars 完了待ち) が消化されてから次のコマンドが流れる。
            await Task.Delay(MinSendIntervalMs, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>診断: 誰が Dispose を呼んだか log するためのシンク</summary>
    public static Action<string>? DisposeTraceSink { get; set; }

    public void Dispose()
    {
        if (_disposed) return;
        DisposeTraceSink?.Invoke("BleXBeeTransport.Dispose stack: " + System.Environment.StackTrace.Replace("\r\n", " | ").Replace("\n", " | "));
        _disposed = true;
        _received.OnCompleted();
        _received.Dispose();
        _connectionState.OnCompleted();
        _connectionState.Dispose();
        _sendLock.Dispose();
    }
}
