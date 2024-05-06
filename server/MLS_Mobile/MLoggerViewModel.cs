using MLLib;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MLS_Mobile
{
  /// <summary>MLoggerのViewModel</summary>
  public class MLoggerViewModel : INotifyPropertyChanged
  {

    #region INotifyPropertyChanged対応

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion

    #region 計測値関連のインスタンス変数・プロパティ

    private string _xbeeLowAddress = "00000000";

    private string _mloggerName = "MLogger_****";

    private string _xbeeName = "MLogger_new";

    //private string _drybulbTemperature, _relativeHumidity, _globeTemperature, _velocity, _illuminance, _mrt, _pmv, _ppd, _set;

    private string _drybulbTemperature = "";
    private string _relativeHumidity = "";
    private string _globeTemperature = "";
    private string _velocity = "";
    private string _illuminance = "";
    private string _mrt = "";
    private string _pmv = "";
    private string _ppd = "";
    private string _set = "";

    private DateTime _lastCom, _lastComDBT, _lastComHMD, _lastComGLB, _lastComVEL, _lastComILL;

    /// <summary>XBee Low Addressを設定・取得する</summary>
    public string XBeeLowAddress
    {
      get { return _xbeeLowAddress; }
      set
      {
        if (_xbeeLowAddress != value)
        {
          _xbeeLowAddress = value;
          OnPropertyChanged();
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
          OnPropertyChanged();
        }
      }
    }

    /// <summary>XBeeの名称を設定・取得する</summary>
    public string XBeeName
    {
      get { return _xbeeName; }
      set
      {
        if (_xbeeName != value)
        {
          _xbeeName = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>最終の通信日時を設定・取得する</summary>
    public DateTime LastCommunicated
    {
      get { return _lastCom; }
      set
      {
        if (_lastCom != value)
        {
          _lastCom = value;
          OnPropertyChanged();
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
          OnPropertyChanged();
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
          OnPropertyChanged();
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
          OnPropertyChanged();
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
          OnPropertyChanged();
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
          OnPropertyChanged();
        }
      }
    }

    /// <summary>乾球温度の最終更新日時を設定・取得する</summary>
    public DateTime LastCommunicated_DBT
    {
      get { return _lastComDBT; }
      set
      {
        if (_lastComDBT != value)
        {
          _lastComDBT = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>相対湿度の最終更新日時を設定・取得する</summary>
    public DateTime LastCommunicated_HMD
    {
      get { return _lastComHMD; }
      set
      {
        if (_lastComHMD != value)
        {
          _lastComHMD = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>グローブ温度の最終更新日時を設定・取得する</summary>
    public DateTime LastCommunicated_GLB
    {
      get { return _lastComDBT; }
      set
      {
        if (_lastComGLB != value)
        {
          _lastComGLB = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>風速の最終更新日時を設定・取得する</summary>
    public DateTime LastCommunicated_VEL
    {
      get { return _lastComVEL; }
      set
      {
        if (_lastComVEL != value)
        {
          _lastComVEL = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>照度の最終更新日時を設定・取得する</summary>
    public DateTime LastCommunicated_ILL
    {
      get { return _lastComILL; }
      set
      {
        if (_lastComILL != value)
        {
          _lastComILL = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>放射温度[C]を設定・取得する</summary>
    public string MeanRadiantTemperature
    {
      get { return _mrt; }
      set
      {
        if (_mrt != value)
        {
          _mrt = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>PMV[-]を設定・取得する</summary>
    public string PMV
    {
      get { return _pmv; }
      set
      {
        if (_pmv != value)
        {
          _pmv = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>PPD[-]を設定・取得する</summary>
    public string PPD
    {
      get { return _ppd; }
      set
      {
        if (_ppd != value)
        {
          _ppd = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>SET*[C]を設定・取得する</summary>
    public string SETStar
    {
      get { return _set; }
      set
      {
        if (_set != value)
        {
          _set = value;
          OnPropertyChanged();
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
          _mLogger.DataReceivedEvent -= _mLogger_DataReceivedEvent;
        }

        _mLogger = value;

        //イベント登録
        if (_mLogger != null)
        {
          _mLogger.MeasuredValueReceivedEvent += MLogger_MeasuredValueReceivedEvent;
          _mLogger.LoggerNameReceivedEvent += MLogger_LoggerNameReceivedEvent;
          _mLogger.DataReceivedEvent += _mLogger_DataReceivedEvent;
          XBeeLowAddress = _mLogger.LowAddress;
          XBeeName = _mLogger.XBeeName;
        }
      }
    }

    #endregion

    #region 描画に関わるインスタンス変数・プロパティ

    private bool _isEnabled = false;

    private Color _frameColor;// = Application.Current.Resources["Gray400"] as Color;

    /// <summary>有効か否かを設定・取得する</summary>
    public bool IsEnabled
    {
      get
      {
        return _isEnabled;
      }
      set
      {
        if (_isEnabled != value)
        {
          _isEnabled = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>枠の色を設定・取得する</summary>
    public Color FrameColor
    {
      get { return _frameColor; }
      set
      {
        if (_frameColor != value)
        {
          _frameColor = value;
          OnPropertyChanged();
        }
      }
    }

    #endregion

    #region イベント処理

    private void _mLogger_DataReceivedEvent(object sender, EventArgs e)
    {
      LastCommunicated = _mLogger.LastCommunicated;
    }

    private void MLogger_LoggerNameReceivedEvent(object sender, EventArgs e)
    {
      MLoggerName = _mLogger.Name;
    }

    private void MLogger_MeasuredValueReceivedEvent(object sender, EventArgs e)
    {
      //コントロール有効化
      IsEnabled = true;

      //計測値
      DrybulbTemperature = _mLogger.DrybulbTemperature.LastValue.ToString("F1");
      RelativeHumdity = _mLogger.RelativeHumdity.LastValue.ToString("F1");
      GlobeTemperature = _mLogger.GlobeTemperature.LastValue.ToString("F1");
      Velocity = _mLogger.Velocity.LastValue.ToString("F2");
      Illuminance = _mLogger.Illuminance.LastValue.ToString("F1");

      //計測日時
      LastCommunicated_DBT = _mLogger.DrybulbTemperature.LastMeasureTime;
      LastCommunicated_HMD = _mLogger.RelativeHumdity.LastMeasureTime;
      LastCommunicated_GLB = _mLogger.GlobeTemperature.LastMeasureTime;
      LastCommunicated_VEL = _mLogger.Velocity.LastMeasureTime;
      LastCommunicated_ILL = _mLogger.Illuminance.LastMeasureTime;

      //演算値
      MeanRadiantTemperature = _mLogger.MeanRadiantTemperature.ToString("F1");
      PMV = _mLogger.PMV.ToString("F2");
      PPD = _mLogger.PPD.ToString("F1");
      SETStar = _mLogger.SETStar.ToString("F1");
      
      //着色
      //FrameColor = Application.Current.Resources["Dark_G"] as Color;
    }

    #endregion

  }
}
