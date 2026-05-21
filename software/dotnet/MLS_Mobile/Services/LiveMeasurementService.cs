using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MLLib.Protocol;

namespace MLS_Mobile.Services;

/// <summary>
/// <see cref="ILiveMeasurementService"/> の標準実装。
/// MVVM Toolkit の ObservableObject ベースで各プロパティ変更を通知する。
/// スレッドモデル: Sample 受信は ViewModel 経由 (基本メインスレッド) のため特別なロックは取らない。
/// </summary>
public sealed partial class LiveMeasurementService : ObservableObject, ILiveMeasurementService
{
    [ObservableProperty] private double? _dryBulbTemperature;
    [ObservableProperty] private double? _relativeHumidity;
    [ObservableProperty] private double? _globeTemperature;
    [ObservableProperty] private double? _velocity;
    [ObservableProperty] private int?    _illuminance;
    [ObservableProperty] private int?    _co2;
    [ObservableProperty] private DateTimeOffset? _lastSampleAt;
    [ObservableProperty] private bool    _isConnected;
    [ObservableProperty] private string? _deviceName;

    public void UpdateFromSample(Sample s)
    {
        if (s.DrybulbTemperature is double dbt) DryBulbTemperature = dbt;
        if (s.RelativeHumidity   is double rh)  RelativeHumidity   = rh;
        if (s.GlobeTemperature   is double glb) GlobeTemperature   = glb;
        if (s.Velocity           is double vel) Velocity           = vel;
        if (s.Illuminance        is int    ill) Illuminance        = ill;
        if (s.Co2                is int    co2) Co2                = co2;
        LastSampleAt = s.Timestamp;
    }

    public void SetConnection(bool connected, string? deviceName)
    {
        IsConnected = connected;
        DeviceName  = deviceName;
        if (!connected)
        {
            // 切断時は staleness が確実に分かるよう LastSampleAt は残しつつ
            // IsConnected の変化を通知する (値そのものはクリアしない)。
        }
    }

    public void Clear()
    {
        DryBulbTemperature = null;
        RelativeHumidity   = null;
        GlobeTemperature   = null;
        Velocity           = null;
        Illuminance        = null;
        Co2                = null;
        LastSampleAt       = null;
        IsConnected        = false;
        DeviceName         = null;
    }
}
