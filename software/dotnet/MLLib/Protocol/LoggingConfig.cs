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

/// <summary>CO2 校正モード。Sensirion STCC4 の各コマンドを組合せた 3 種類の操作。</summary>
public enum Co2CalibrationMode
{
    /// <summary>校正 — 30秒連続測定 + forced_recalibration。既知 CO2 濃度の下に置いて即時校正。</summary>
    Forced,

    /// <summary>完全初期化 — factory_reset → 12時間安定化 → forced_recalibration の compound 操作。
    /// Sensirion datasheet §1.1.4 "Initial Operation" を後付けで再現する。</summary>
    Factory,

    /// <summary>工場リセット — factory_reset 単独 (~90ms)。ASC/FRC 履歴を消去して bypass phase を
    /// 再開する。target_ppm は不要。</summary>
    Reset
}
