using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MLS_Mobile
{
  /// <summary>IDeviceのViewModel</summary>
  public class IDeviceViewModel : INotifyPropertyChanged
  {

    #region INotifyPropertyChanged対応

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion

    #region インスタンス変数・プロパティ

    private IDevice _device;
    private string _name = "";
    private int _rssi = -100;
    private ImageSource _img = "signals_pwr0.png";

    public IDevice Device
    {
      get { return _device; }
      set
      {
        if (_device != value)
        {
          _device = value;

          this.Name = _device.Name;
          this.Rssi = _device.Rssi;
        }
      }
    }

    /// <summary>名称を取得する</summary>
    public string Name
    {
      get { return _name; }
      private set
      {
        if (_name != value)
        {
          _name = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>RSSIを設定・取得する</summary>
    public int Rssi
    {
      get { return _rssi; }
      private set
      {
        if (_rssi != value)
        {
          _rssi = value;
          OnPropertyChanged();
          //以下の6段階分割には特に根拠は無い
          if (_rssi < -95) Signal = "signals_pwr0.png";
          else if (_rssi < -85) Signal = "signals_pwr1.png";
          else if (_rssi < -75) Signal = "signals_pwr2.png";
          else if (_rssi < -65) Signal = "signals_pwr3.png";
          else if (_rssi < -50) Signal = "signals_pwr4.png";
          else Signal = "signals_pwr5.png";
        }
      }
    }

    public ImageSource Signal
    {
      get { return _img; }
      private set
      {
        if (_img != value)
        {
          _img = value;
          OnPropertyChanged();
        }
      }
    }

    #endregion

  }
}
