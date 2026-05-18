namespace MLLib.Protocol;

/// <summary>ロギング出力先 (複数同時 ON 可)。</summary>
public sealed record Transports(bool Zigbee, bool Ble, bool Flash, bool Usb);

/// <summary>ロギングモード。</summary>
public enum LoggingMode
{
    /// <summary>1回限り。電源 OFF で終了。</summary>
    Once,

    /// <summary>常設モード。電源再投入後も自動再開。Reset スイッチ 3秒長押しで解除。</summary>
    AutoRestart
}

/// <summary><see cref="IMLProtocol.StartLoggingAsync"/> のパラメータ。</summary>
public sealed record LoggingConfig(Transports Tx, LoggingMode Mode);

/// <summary>CO2 校正モード。</summary>
public enum Co2CalibrationMode
{
    /// <summary>強制校正 (30秒モード)。安定 CO2 環境で短時間校正。</summary>
    Forced,

    /// <summary>ファクトリ初期化 (12時間モード)。センサ完全リセット。</summary>
    Factory
}
