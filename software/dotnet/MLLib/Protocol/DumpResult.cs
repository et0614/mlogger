namespace MLLib.Protocol;

/// <summary>
/// <see cref="IMLProtocol.DumpAsync"/> の結果。
/// バイナリレコード列 (v3 互換フォーマット <c>BIBIhhHHHH</c> 18バイト/件) を保持。
/// </summary>
public sealed record DumpResult(
    int                   RecordCount,
    int                   RecordSize,
    string                Format,
    ReadOnlyMemory<byte>  Data);
