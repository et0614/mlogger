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
    /// </summary>
    public void Append(ReadOnlySpan<byte> data, Action<string> lineHandler)
    {
        foreach (var b in data)
        {
            if (b == (byte)'\n' || b == (byte)'\r')
            {
                if (_buf.Count > 0)
                {
                    var line = System.Text.Encoding.UTF8.GetString(_buf.ToArray());
                    _buf.Clear();
                    lineHandler(line);
                }
            }
            else
            {
                _buf.Add(b);
            }
        }
    }
}
