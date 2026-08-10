namespace MLLib.Protocol;

/// <summary>
/// get_probe_info 応答のうち 1 プローブ分。
/// Connected=false のとき他フィールドは無効 (firmware は connected のみ返す)。
/// </summary>
/// <param name="Connected">I2C で INFO BLOCK が読めたか (= プローブ物理接続)</param>
/// <param name="DeviceId">FNV-1a 22bit の個体 ID (6 桁 hex 文字列)</param>
/// <param name="Name">プローブの装置ラベル (子機 EEPROM 永続、最大 15 文字)</param>
/// <param name="DataCount">有効計測値数 (温湿度プローブ=4、風速プローブ=2)</param>
public sealed record ProbePortInfo(
    bool Connected,
    string? DeviceId,
    string? Name,
    int DataCount);

/// <summary>
/// get_probe_info 応答。接続中のプローブの個体識別情報 (出荷時試験成績・校正成績の参照キー)。
/// </summary>
public sealed record ProbeInfo(
    ProbePortInfo ThProbe,
    ProbePortInfo VelocityProbe);
