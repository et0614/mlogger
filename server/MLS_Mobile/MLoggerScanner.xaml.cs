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

  private async void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    IDevice selectedXBee = (IDevice)e.CurrentSelection[0];
    mlList.SelectedItem = null;

    //MLDeviceに接続する
    showIndicator(MLSResource.SC_Connecting);

    //XBee接続
    string lowAddress;
    try
    {
      lowAddress = await Task.Run(string () =>
      { return MLUtility.OpenXbee(selectedXBee); });
    }
    catch (Exception ex)
    {
      await DisplayAlert("Alert", "Can't open XBee connection." + Environment.NewLine + ex.Message, "OK");
      hideIndicator();
      return;
    }

    //MLoggerの場合***
    if (MLUtility.ConnectedDevice == MLUtility.MLDevice.MLogger)
    {
      try
      {
        await Task.Run(async() =>
        {
          //バージョンを取得する
          for (int i = 0; i < 10; i++)
          {
            if (MLUtility.Logger.HasVersionReceived) return;
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand()));
            await Task.Delay(100);
          }
        });
      }
      catch (Exception ex)
      {
        await DisplayAlert("Alert", "Can't load MLogger version." + Environment.NewLine + ex.Message, "OK");
        hideIndicator();
        return;
      }
      //画面遷移
      if (MLUtility.Logger.HasVersionReceived)
        await Shell.Current.GoToAsync(nameof(DeviceSetting), new Dictionary<string, object> { { "mlLowAddress", lowAddress } });
      else
      {
        await DisplayAlert("Alert", "Can't load MLogger version.", "OK");
        await Task.Run(MLUtility.CloseXbee);
      }
    }

    //Transcieverの場合***
    else if (MLUtility.ConnectedDevice == MLUtility.MLDevice.MLTransciever)
    {
      //Bluetooth転送モード有効化
      try
      {
        await Task.Run(async () =>
        {
          MLUtility.Transceiver.HasRelayToBluetoothReceived = false;
          for (int i = 0; i < 10; i++)
          {
            if (MLUtility.Transceiver.HasRelayToBluetoothReceived) return;
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeRelayToBluetoothCommand()));
            await Task.Delay(100);
          }
        });
      }
      catch (Exception ex)
      {
        await DisplayAlert("Alert", "Can't enable Bluetooth relay." + Environment.NewLine + ex.Message, "OK");
        hideIndicator();
        return;
      }
      if (!MLUtility.Transceiver.HasRelayToBluetoothReceived)
      {
        await DisplayAlert("Alert", "Can't enable Bluetooth relay.", "OK");
        await Task.Run(MLUtility.CloseXbee);
        hideIndicator();
        return;
      }

      //現在時刻更新
      try
      {
        await Task.Run(async() =>
        {
          MLUtility.Transceiver.HasUpdateCurrentTimeReceived = false;
          for (int i = 0; i < 10; i++)
          {
            if (MLUtility.Transceiver.HasUpdateCurrentTimeReceived) return;
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeUpdateCurrentTimeCommand(DateTime.Now)));
            await Task.Delay(100);
          }
        });
      }
      catch (Exception ex)
      {
        await DisplayAlert("Alert", "Can't update current date and time." + Environment.NewLine + ex.Message, "OK");
        hideIndicator();
        return;
      }
      if (!MLUtility.Transceiver.HasUpdateCurrentTimeReceived)
      {
        await DisplayAlert("Alert", "Can't update current date and time.", "OK");
        await Task.Run(MLUtility.CloseXbee);
        hideIndicator();
        return;
      }

      //Bluetooth有効化+現在時刻更新が成功したら画面遷移
      await DisplayAlert("", "Current time has been updated.", "OK");
      await Shell.Current.GoToAsync(nameof(RelayedDataViewer));
    }

    hideIndicator();
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