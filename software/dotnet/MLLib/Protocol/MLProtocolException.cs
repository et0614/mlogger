namespace MLLib.Protocol;

/// <summary>
/// 子機から error 応答 (<c>{"code","message"}</c>) が返ってきた場合の例外。
/// </summary>
public sealed class MLProtocolException : Exception
{
    /// <summary>エラーコード文字列 (<see cref="MLProtocolErrorCodes"/> 参照)。</summary>
    public string Code { get; }

    public MLProtocolException(string code, string message) : base(message)
    {
        Code = code;
    }

    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>
/// 標準エラーコード文字列。
/// firmware <c>protocol_dispatch.c</c> の send_error 呼び出しと同期。
/// </summary>
public static class MLProtocolErrorCodes
{
    public const string UnknownCommand       = "unknown_command";
    public const string InvalidParams        = "invalid_params";
    public const string OutOfRange           = "out_of_range";
    public const string UnsupportedTransport = "unsupported_transport";
    public const string Busy                 = "busy";
    public const string InternalError        = "internal_error";
}
