namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;
using MLLib;
using System.Text;

public partial class MLoggerScanner : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  private bool bleChecked = false;

  /// <summary>XBeeを探索する時間[msec]</summary>
  private const int SCAN_TIME = 1000;

  /// <summary>探索できたXBeeのリスト</summary>
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

  protected override async void OnAppearing()
  {
    base.OnAppearing();

    //スキャン実行
    refView.Command.Execute(null);

    if (!bleChecked)
    {
      await checkBLEPermission();
      bleChecked = true;
    }
  }

  private void scanXBees()
  {
    //接続済みのXBeeがある場合には解除
    MLUtility.CloseXbee();

    //Bluetoothを用意
    IBluetoothLE bluetoothLE = CrossBluetoothLE.Current;

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
      if (dvName != null && dvName != "" && (dvName.StartsWith("MLogger_") || dvName.StartsWith("MLTransceiver")))
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
        if (newItem) MLXBees.Add(ev.Device);
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
    Task.Run(async () =>
    {
      try
      {
        string lowAddress = MLUtility.OpenXbee(selectedXBee);

        //MLoggerの場合：Openに成功したら設定ページへ移動
        if (MLUtility.ConnectedDevice == MLUtility.MLDevice.MLogger)
        {
          //バージョンを取得する
          MLUtility.Logger.HasVersionReceived = false;
          int tryNum = 0;
          while ((!MLUtility.Logger.HasVersionReceived) && tryNum < 4)
          {
            try
            {
              //Bluetooth転送コマンドを送信
              MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand()));
              await Task.Delay(200);
            }
            catch { }
            tryNum++;
          }

          //バージョンを取得できた場合
          if (MLUtility.Logger.HasVersionReceived)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              Shell.Current.GoToAsync(nameof(DeviceSetting),
                new Dictionary<string, object> { { "mlLowAddress", lowAddress } }
                );
            });
          }
          else
          {
            MLUtility.CloseXbee();
            Application.Current.Dispatcher.Dispatch(() =>
            { DisplayAlert("Alert", "Connection failed.", "OK"); });
          }
        }
        //Transcieverの場合
        else if (MLUtility.ConnectedDevice == MLUtility.MLDevice.MLTransciever)
        {
          //Bluetooth転送モードを有効にする
          MLUtility.Transceiver.HasRelayedToBluetoothReceived = false;
          int tryNum = 0;
          while ((!MLUtility.Transceiver.HasRelayedToBluetoothReceived) && tryNum < 4)
          {
            try
            {
              //Bluetooth転送コマンドを送信
              MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeRelayToBluetoothCommand()));
              await Task.Delay(200);
            }
            catch { }
            tryNum++;
          }

          //Bluetooth転送モードを有効にできた場合
          if (MLUtility.Transceiver.HasRelayedToBluetoothReceived)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              Shell.Current.GoToAsync(nameof(RelayedDataViewer));
            });
          }
          else
          {
            MLUtility.CloseXbee();
            Application.Current.Dispatcher.Dispatch(() =>
            { DisplayAlert("Alert", "Connection failed.", "OK"); });
          }
        }
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

  private async Task checkBLEPermission()
  {
#if ANDROID
    var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    if (status == PermissionStatus.Granted) return;

    if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
      await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_Bluetooth, "OK");

    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
#endif
  }

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