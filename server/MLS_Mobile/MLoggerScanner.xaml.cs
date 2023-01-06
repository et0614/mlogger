namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;

public partial class MLoggerScanner : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  /// <summary>XBeeを探索する時間[msec]</summary>
  private const int SCAN_TIME = 1000;

  /// <summary>MLogger搭載のXBeeのリスト</summary>
  public ObservableCollection<IDevice> MLXBees { get; private set; } = new ObservableCollection<IDevice>();

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public MLoggerScanner()
  {
    InitializeComponent();

    BindingContext = this;

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
    //接続済みのXBeeがある場合には解除
    MLUtility.EndXBeeCommunication();

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
        for (int i = 0; i < MLXBees.Count; i++)
        {
          if (MLXBees[i].Name == dvName)
          {
            newItem = false;
            break;
          }
        }
        if (newItem)
          MLXBees.Add(ev.Device);
      }
    };

    //非同期スキャン開始
    MLXBees.Clear();
    adapter.StartScanningForDevicesAsync();
  }

  #endregion

  #region コントロール操作時の処理

  private void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    IDevice selectedXBee = (IDevice)e.CurrentSelection[0];
    mlList.SelectedItem = null;

    //インジケータを表示して制御不可にしてから接続処理
    showIndicator(MLSResource.SC_Connecting);
    Task.Run(() =>
    {
      try
      {
        MLUtility.StartXBeeCommunication(selectedXBee);

        //Openに成功したら設定ページへ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(DeviceSetting));
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

}