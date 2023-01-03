namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using XBeeLibrary.Xamarin;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLLib;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;

public partial class MLoggerScanner : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  /// <summary>XBeeを探索する時間[msec]</summary>
  private const int SCAN_TIME = 1000;

  private const string ML_PASS = "ml_pass";

  private readonly ObservableCollection<xBee> xbees = new ObservableCollection<xBee>();

  ZigBeeBLEDevice MLXBee;

  #endregion

  #region コンストラクタ

  public MLoggerScanner()
  {
    InitializeComponent();

    Title = MLSResource.SC_Title;
    mlList.ItemsSource = xbees;

    //リフレッシュコマンド定義
    ICommand refreshCommand = new Command(() =>
    {
      scanXBees();
      refView.IsRefreshing = false;
    });
    refView.Command = refreshCommand;
  }

  #endregion

  #region ロード・アンロードイベント

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //スキャン実行
    refView.Command.Execute(null);
  }

  private void scanXBees()
  {
    //接続済みのXBeeがある場合には、別スレッドで接続を解除
    if (MLXBee != null && MLXBee.IsOpen)
    {
      ZigBeeBLEDevice cls = MLXBee;
      Task.Run(() =>
      {
        try
        {
          cls.Close();
        }
        catch { }
      });
    }

    //Bluetoothを用意
    IBluetoothLE bluetoothLE = CrossBluetoothLE.Current;
    if (bluetoothLE.State == BluetoothState.Off)
    {
      DisplayAlert("Alert", MLSResource.SC_Bluetooth, "OK");
      return;
    }

    //アダプタを用意
    IAdapter adapter = bluetoothLE.Adapter;

    //スキャン中でなければスキャン開始
    if (adapter.IsScanning) return;

    //スキャン設定
    adapter.ScanTimeout = SCAN_TIME;
    adapter.ScanMode = ScanMode.LowLatency;

    //BLEデバイスが見つかった時の処理
    adapter.DeviceDiscovered += (s, ev) =>
    {
      string dvName = ev.Device.Name;
      if (dvName != null && dvName != "" && dvName.StartsWith("MLogger_"))
      {
        bool newItem = true;
        for (int i = 0; i < xbees.Count; i++)
        {
          if (xbees[i].Name == dvName)
          {
            newItem = false;
            break;
          }
        }
        if (newItem)
          xbees.Add(new xBee(dvName, ev.Device.Id));
      }
    };

    //非同期スキャン開始
    xbees.Clear();
    adapter.StartScanningForDevicesAsync();
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //Bluetoothを用意
    /*IBluetoothLE bluetoothLe = CrossBluetoothLE.Current;
    if (bluetoothLe.State == BluetoothState.Off)
    {
      DisplayAlert("Alert", "Bluetoothを有効にしてください", "OK");
      return;
    }*/

    //アダプタを用意。スキャン中ならば停止
    /*IAdapter adapter = bluetoothLe.Adapter;
    if (adapter.IsScanning)
      adapter.StopScanningForDevicesAsync();*/
  }

  #endregion

  #region コントロール操作時の処理

  private void mlList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
  {
    if (e.SelectedItem == null) return;

    xBee selectedXBee = (xBee)e.SelectedItem;
    mlList.SelectedItem = null;

    if (xbees.Count == 1 && xbees[0].Name == MLSResource.SC_Empty)
      return;

    //インジケータを表示して制御不可にする
    showIndicator(MLSResource.SC_Connecting);

    Task.Run(async () =>
    {
      try
      {
        //BLE Deviceに接続
        IAdapter adapter = CrossBluetoothLE.Current.Adapter;
        IDevice mlDevice = await adapter.ConnectToKnownDeviceAsync(selectedXBee.Id);

        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
          MLXBee = new ZigBeeBLEDevice(selectedXBee.Id.ToString(), ML_PASS);
        else MLXBee = new ZigBeeBLEDevice(mlDevice, ML_PASS);

        //XBeeをOpen
        MLXBee.Open();

        //Openに成功したら設定ページへ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DeviceSetting dvset = new DeviceSetting();
          dvset.MLXBee = MLXBee;
          dvset.MLDevice = mlDevice;
          dvset.Logger = loadMLogger(MLXBee.GetAddressString(), selectedXBee.Name);
          mlList.SelectedItem = null; //選択解除

          dvset.InitializeMLogger();

          Navigation.PushAsync(dvset, true);
        });

      }
      catch (Exception bex)
      {
        //失敗した場合にはエラーメッセージを出す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", bex.Message, "OK");
        });
      }
      finally
      {
        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });

  }

  private void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    xBee selectedXBee = (xBee)e.CurrentSelection[0];
    mlList.SelectedItem = null;

    if (xbees.Count == 1 && xbees[0].Name == MLSResource.SC_Empty)
      return;

    //インジケータを表示して制御不可にする
    showIndicator(MLSResource.SC_Connecting);

    Task.Run(async () =>
    {
      try
      {
        //BLE Deviceに接続
        IAdapter adapter = CrossBluetoothLE.Current.Adapter;
        IDevice mlDevice = await adapter.ConnectToKnownDeviceAsync(selectedXBee.Id);

        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
          MLXBee = new ZigBeeBLEDevice(selectedXBee.Id.ToString(), ML_PASS);
        else MLXBee = new ZigBeeBLEDevice(mlDevice, ML_PASS);

        //XBeeをOpen
        MLXBee.Open();

        //Openに成功したら設定ページへ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DeviceSetting dvset = new DeviceSetting();
          dvset.MLXBee = MLXBee;
          dvset.MLDevice = mlDevice;
          dvset.Logger = loadMLogger(MLXBee.GetAddressString(), selectedXBee.Name);
          mlList.SelectedItem = null; //選択解除

          dvset.InitializeMLogger();

          Navigation.PushAsync(dvset, true);
        });

      }
      catch (Exception bex)
      {
        //失敗した場合にはエラーメッセージを出す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", bex.Message, "OK");
        });
      }
      finally
      {
        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  private MLogger loadMLogger(string address, string name)
  {
    MLogger logger = new MLogger(address);
    logger.LocalName = name;
    return logger;
  }

  #endregion

  #region インジケータの操作

  /// <summary>インジケータを表示する</summary>
  private void showIndicator(string message)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });
  }

  /// <summary>インジケータを隠す</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });
  }

  #endregion

  #region インナークラスの定義

  private class xBee
  {

    public string Name { get; private set; }

    public Guid Id { get; private set; }

    public xBee(string name, Guid id)
    {
      Name = name;
      Id = id;
    }

  }

  #endregion


}