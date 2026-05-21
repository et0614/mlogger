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
        // ObservableProperty の auto-generated setter を使わず field を直接書き換える。
        // これで個別 PropertyChanged を抑止し、最後に空文字 (全 property 変化) で 1 回だけ通知する。
        // (各 setter ごとに通知すると subscriber × プロパティ数の連鎖になり、UI に体感
        //  カクつきが出る原因になっていた。subscriber 側は _liveActive ガードと
        //  e.PropertyName での分岐で空文字も処理できるため互換性は保たれる。)
        if (s.DrybulbTemperature is double dbt) _dryBulbTemperature = dbt;
        if (s.RelativeHumidity   is double rh)  _relativeHumidity   = rh;
        if (s.GlobeTemperature   is double glb) _globeTemperature   = glb;
        if (s.Velocity           is double vel) _velocity           = vel;
        if (s.Illuminance        is int    ill) _illuminance        = ill;
        if (s.Co2                is int    co2) _co2                = co2;
        _lastSampleAt = s.Timestamp;

        OnPropertyChanged(string.Empty);
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
