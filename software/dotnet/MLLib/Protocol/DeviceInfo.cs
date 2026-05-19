namespace MLLib.Protocol;

/// <summary>
/// 子機の識別情報。v4 hello 応答 / v3 VER 応答から構築される。
/// </summary>
public sealed record DeviceInfo(
    string Device,           // 固定値 "M-Logger"
    string FirmwareVersion,  // semver 文字列 (例 "4.0.0")
    int ProtocolVersion,     // v4 では 1、v3 互換時は 0 を入れる
    string HardwareId,       // AVR SIGROW.SERNUM0 を fnv1a_32 した 8文字hex (v3 では XBee アドレス代用)
    string Name,             // ユーザ設定可能、最大 20 文字
    bool IsLogging,
    bool HasCo2Sensor);      // CO2 センサ搭載有無 (v4 は常時 true、v3 は HCS で probe)
