using System;
using System.ComponentModel;
using MLLib.Protocol;

namespace MLS_Mobile.Services;

/// <summary>
/// アプリ内で共有される「現在の計測値 / 接続状態」のスナップショット。
/// DataReceive (計測オーナー) が UpdateFromSample で書き込み、Thermal comfort や Moist air
/// など他 Tab の ViewModel が PropertyChanged を購読してライブ入力として利用する。
///
/// 設計方針:
///  - サービス自体は DI コンテナ singleton として 1 つだけ存在し、アプリの生存期間中保持
///  - Sample のフィールドが null (未計測) のときは既存値を保持する (古いままになる)
///  - 取得側は LastSampleAt と現在時刻の差で staleness を判断
///  - IsConnected が false に落ちたら値は信頼しないこと
/// </summary>
public interface ILiveMeasurementService : INotifyPropertyChanged
{
    /// <summary>直近に受け取った乾球温度 [°C]。未受信は null。</summary>
    double? DryBulbTemperature { get; }

    /// <summary>直近に受け取った相対湿度 [%]。未受信は null。</summary>
    double? RelativeHumidity { get; }

    /// <summary>直近に受け取ったグローブ温度 [°C]。未受信は null。</summary>
    double? GlobeTemperature { get; }

    /// <summary>直近に受け取った風速 [m/s]。未受信は null。</summary>
    double? Velocity { get; }

    /// <summary>直近に受け取った照度 [lx]。未受信は null。</summary>
    int? Illuminance { get; }

    /// <summary>直近に受け取った CO2 濃度 [ppm]。未受信は null。</summary>
    int? Co2 { get; }

    /// <summary>直近の Sample が到着した時刻。一度も受信していなければ null。</summary>
    DateTimeOffset? LastSampleAt { get; }

    /// <summary>子機との接続状態 (DataReceive ライフサイクルで管理)。</summary>
    bool IsConnected { get; }

    /// <summary>接続中デバイスの表示名 (hello.name 由来)。未接続なら null。</summary>
    string? DeviceName { get; }

    /// <summary>新しい Sample を反映 (null フィールドは前回値を保持)。</summary>
    void UpdateFromSample(Sample sample);

    /// <summary>接続状態とデバイス名を更新。切断時は connected=false / deviceName=null を渡す。</summary>
    void SetConnection(bool connected, string? deviceName);

    /// <summary>全プロパティを初期化 (切断後のクリア用)。</summary>
    void Clear();
}
