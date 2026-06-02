namespace MLLib.Protocol.Protocols;

/// <summary>
/// 受信バイト列を \n / \r\n 区切りで行に分解する単純なバッファ。
/// 通常モード時の <see cref="JsonRpcV4Protocol"/> 受信側で使用。
/// スレッドセーフではない (受信ハンドラ内で1スレッドから使う前提)。
/// </summary>
internal sealed class LineBuffer
{
    private readonly List<byte> _buf = new(capacity: 256);

    /// <summary>バッファをクリア (dump バイナリモード遷移時など)。</summary>
    public void Reset() => _buf.Clear();

    /// <summary>
    /// バイト列を末尾に追加し、完成した行を1つずつ <paramref name="lineHandler"/> に渡す。
    /// <paramref name="shouldStopAfterLine"/> を指定すると、各行処理後に true を返した
    /// 時点で残りバイトを処理せず、その時点までの消費 byte 数を返す (caller が mode を
    /// 切替えて続きを別経路で処理する用途、特に dump バイナリストリームへの遷移)。
    /// </summary>
    /// <returns>処理 (消費) した byte 数。指定無しの通常呼び出しでは data.Length。</returns>
    public int Append(ReadOnlySpan<byte> data, Action<string> lineHandler, Func<bool>? shouldStopAfterLine = null)
    {
        for (int i = 0; i < data.Length; i++)
        {
            var b = data[i];
            if (b == (byte)'\n' || b == (byte)'\r')
            {
                if (_buf.Count > 0)
                {
                    var line = System.Text.Encoding.UTF8.GetString(_buf.ToArray());
                    _buf.Clear();
                    lineHandler(line);
                    if (shouldStopAfterLine != null && shouldStopAfterLine())
                    {
                        return i + 1;
                    }
                }
            }
            else
            {
                _buf.Add(b);
            }
        }
        return data.Length;
    }
}
