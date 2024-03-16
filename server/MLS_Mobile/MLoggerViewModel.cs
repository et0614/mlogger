using MLLib;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MLS_Mobile
{
  public class MLoggerViewModel : INotifyPropertyChanged
  {

    #region INotifyPropertyChanged対応

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion

    #region インスタンス変数・プロパティ定義

    private string _xbeeLowAddress = "00000000";

    private string _mloggerName = "00000000";

    private string _lastConnected = "----/--/-- --:--:--";

    private string _drybulbTemperature = "-";

    private string _relativeHumidity = "-";

    private string _globeTemperature = "-";

    private string _velocity = "-";

    private string _illuminance = "-";

    /// <summary>XBee Low Addressを設定・取得する</summary>
    public string XBeeLowAddress
    {
      get { return _xbeeLowAddress; }
      set
      {
        if (_xbeeLowAddress != value)
        {
          _xbeeLowAddress = value;
          OnPropertyChanged(nameof(XBeeLowAddress));
        }
      }
    }

    /// <summary>MLoggerの名称を設定・取得する</summary>
    public string MLoggerName
    {
      get { return _mloggerName; }
      set
      {
        if (_mloggerName != value)
        {
          _mloggerName = value;
          OnPropertyChanged(nameof(MLoggerName));
        }
      }
    }

    /// <summary>最終の通信日時を設定・取得する</summary>
    public string LastConnected
    {
      get { return _lastConnected; }
      set
      {
        if (_lastConnected != value)
        {
          _lastConnected = value;
          OnPropertyChanged(nameof(LastConnected));
        }
      }
    }

    /// <summary>乾球温度[C]を設定・取得する</summary>
    public string DrybulbTemperature
    {
      get { return _drybulbTemperature; }
      set
      {
        if (_drybulbTemperature != value)
        {
          _drybulbTemperature = value;
          OnPropertyChanged(nameof(DrybulbTemperature));
        }
      }
    }

    /// <summary>相対湿度[%]を設定・取得する</summary>
    public string RelativeHumdity
    {
      get { return _relativeHumidity; }
      set
      {
        if (_relativeHumidity != value)
        {
          _relativeHumidity = value;
          OnPropertyChanged(nameof(RelativeHumdity));
        }
      }
    }

    /// <summary>グローブ温度[C]を設定・取得する</summary>
    public string GlobeTemperature
    {
      get { return _globeTemperature; }
      set
      {
        if (_globeTemperature != value)
        {
          _globeTemperature = value;
          OnPropertyChanged(nameof(GlobeTemperature));
        }
      }
    }

    /// <summary>風速[m/s]を設定・取得する</summary>
    public string Velocity
    {
      get { return _velocity; }
      set
      {
        if (_velocity != value)
        {
          _velocity = value;
          OnPropertyChanged(nameof(Velocity));
        }
      }
    }

    /// <summary>照度[lux]を設定・取得する</summary>
    public string Illuminance
    {
      get { return _illuminance; }
      set
      {
        if (_illuminance != value)
        {
          _illuminance = value;
          OnPropertyChanged(nameof(Illuminance));
        }
      }
    }

    private ImmutableMLogger _mLogger;

    public ImmutableMLogger Logger
    {
      get
      {
        return _mLogger;
      }
      set
      {
        //登録済みのイベントを解除
        if (_mLogger != null)
        {
          _mLogger.MeasuredValueReceivedEvent -= MLogger_MeasuredValueReceivedEvent;
          _mLogger.LoggerNameReceivedEvent -= MLogger_LoggerNameReceivedEvent;
        }

        _mLogger = value;
        _mLogger.MeasuredValueReceivedEvent += MLogger_MeasuredValueReceivedEvent;
        _mLogger.LoggerNameReceivedEvent += MLogger_LoggerNameReceivedEvent;

        XBeeLowAddress = _mLogger.LowAddress;
      }
    }

    #endregion

    private void MLogger_LoggerNameReceivedEvent(object sender, EventArgs e)
    {
      MLoggerName = _mLogger.Name;
      LastConnected = _mLogger.LastCommunicated.ToString("yyyy/MM/dd HH:mm:ss");
    }

    private void MLogger_MeasuredValueReceivedEvent(object sender, EventArgs e)
    {
      DrybulbTemperature = _mLogger.DrybulbTemperature.LastValue.ToString("F1");
      RelativeHumdity = _mLogger.RelativeHumdity.LastValue.ToString("F1");
      GlobeTemperature = _mLogger.GlobeTemperature.LastValue.ToString("F1");
      Velocity = _mLogger.Velocity.LastValue.ToString("F2");
      Illuminance = _mLogger.Illuminance.LastValue.ToString("F1");
      LastConnected = _mLogger.LastCommunicated.ToString("yyyy/MM/dd HH:mm:ss");
    }

  }
}
