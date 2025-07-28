namespace MLS_Mobile;

using Microsoft.Extensions.Logging;
using MLLib;
using MLS_Mobile.Resources.i18n;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

public partial class MLoggerScanner : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  private bool bleChecked = false;

  /// <summary>XBeeを探索する時間[msec]</summary>
  private const int SCAN_TIME = 2000;

  /// <summary>探索できたXBeeのリスト</summary>
  public ObservableCollection<IDeviceViewModel> MLXBees { get; private set; } = new ObservableCollection<IDeviceViewModel>();

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public MLoggerScanner()
  {
    InitializeComponent();

    BindingContext = this;

    //イベント登録
    CrossBluetoothLE.Current.Adapter.DeviceDiscovered += (s, ev) =>
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
        if (newItem) MLXBees.Add(
          new IDeviceViewModel() { Device = ev.Device });
      }
    };

    //スキャン時間経過後
    CrossBluetoothLE.Current.Adapter.ScanTimeoutElapsed += (s, ev) =>
    {
      refView.IsRefreshing = false;
    };

    //リフレッシュコマンド定義
    ICommand refreshCommand = new Command(() =>
    {
      if (!CrossBluetoothLE.Current.Adapter.IsScanning)
        scanXBees();
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

    //BLE有効化を確認（Androidのみ）
    await checkBLEPermission();
  }

  private void scanXBees()
  {
    //接続済みのXBeeがある場合には解除
    MLUtility.CloseXbee();

    //アダプタを用意
    IAdapter adapter = CrossBluetoothLE.Current.Adapter;

    //スキャン設定
    adapter.ScanTimeout = SCAN_TIME;
    adapter.ScanMode = ScanMode.LowLatency;

    //非同期スキャン開始
    MLXBees.Clear();
    adapter.StartScanningForDevicesAsync();
  }

  #endregion

  #region コントロール操作時の処理

  private async void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    IDevice selectedXBee = ((IDeviceViewModel)e.CurrentSelection[0]).Device;
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
      //イベント待機タスクを作成
      var tcs = new TaskCompletionSource<bool>();

      //イベントが発生したらタスクを完了させるハンドラを一時的に登録
      EventHandler handler = (s, e) => tcs.TrySetResult(true);
      MLUtility.Logger.VersionReceivedEvent += handler;

      try
      {
        //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
        for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
        {
          try
          {
            await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand())));
          }
          catch { }

          //イベントが来るか、タイムアウト(100ms)するまで待つ
          await Task.WhenAny(tcs.Task, Task.Delay(100));
        }

        //タスクが正常に完了した場合のみ
        if (tcs.Task.IsCompletedSuccessfully)
        {
          await Shell.Current.GoToAsync(nameof(DeviceSetting), new Dictionary<string, object> { { "mlLowAddress", lowAddress } });
        }
        else
        {
          await DisplayAlert("Alert", "Can't load MLogger version.", "OK");
          await Task.Run(MLUtility.CloseXbee);
        }
      }
      finally
      {
        //ハンドラを解除
        MLUtility.Logger.VersionReceivedEvent -= handler;
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
    if (!bleChecked)
    {

#if ANDROID
      //Bluetoothに関わる権限
      var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
      if (status == PermissionStatus.Granted) return;
      if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
        await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_Bluetooth, "OK");

      status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

      //付近のデバイス（NearByDevice）に関わる権限
      status = await Permissions.CheckStatusAsync<PermissionNearByDevice>();
      if (status == PermissionStatus.Granted) return;
      if (Permissions.ShouldShowRationale<PermissionNearByDevice>())
        await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_NearByDevice, "OK");

      status = await Permissions.RequestAsync<PermissionNearByDevice>();

#endif

      bleChecked = true;
    }
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

/// <summary>
/// 付近のデバイス（NearByDevice）の権限設定画面用
/// </summary>
internal class PermissionNearByDevice : Permissions.BasePlatformPermission
{
#if ANDROID
  public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    new List<(string androidPermission, bool isRuntime)>
    {
        // Near By Deviceは以下の権限を要求するようにすると設定画面を出せる
       (global::Android.Manifest.Permission.BluetoothScan,true),
        (global::Android.Manifest.Permission.BluetoothConnect,true),
        (global::Android.Manifest.Permission.BluetoothAdvertise,true)
    }.ToArray();
#endif
}
