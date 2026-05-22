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

    // BLE 権限を確認してからスキャン開始。順序が逆だと Android 12+ で
    // SecurityException("Need android.permission.BLUETOOTH_SCAN") が発生する。
    await checkBLEPermission();

    refView.Command.Execute(null);
  }

  private async void scanXBees()
  {
    // Scanner に戻るたびに、以前の接続を完全に閉じてから再スキャンする。
    // 以前は IsConnected ガードでスキャンをスキップしていたが、それだと DeviceSetting から
    // pop して戻ったときにリストが更新されず、また Disconnect が fire-and-forget だったため
    // 次の Connect と race して、同じ子機への再接続が初回でエラーになる事象があった。
    await MLUtility.CloseXbeeAsync();

    IAdapter adapter = CrossBluetoothLE.Current.Adapter;
    adapter.ScanTimeout = SCAN_TIME;
    adapter.ScanMode = ScanMode.LowLatency;

    MLXBees.Clear();
    await adapter.StartScanningForDevicesAsync();
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
      lowAddress = await MLUtility.OpenXbeeAsync(selectedXBee);
    }
    catch (Exception ex)
    {
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_ConnectionFailed, ex);
      hideIndicator();
      return;
    }

    //MLoggerの場合***
    if (MLUtility.ConnectedDevice == MLDevice.MLogger)
    {
      //v4 hello probe → v3 VER フォールバックで IMLProtocol を自動判定。
      //v3 端末では ProtocolFactory が内部送信する VER の応答が既存の SerialDataReceived
      //静的コールバック経由でも Logger に流れるため、Logger.Version_* は副作用で更新される
      //(D6c で LegacyMLoggerAdapter 導入後はこの副作用依存を解消する予定)。
      try
      {
        await MLUtility.DetectProtocolAsync();
        await Shell.Current.GoToAsync(nameof(DeviceSetting), new Dictionary<string, object> { { "mlLowAddress", lowAddress } });
      }
      catch (Exception ex)
      {
        await MLUtility.ShowErrorAsync(this, MLSResource.ERR_ProtocolDetectionFailed, ex);
        await MLUtility.CloseXbeeAsync();
      }
    }

    hideIndicator();
  }

  #endregion

  private async Task checkBLEPermission()
  {
    if (bleChecked) return;

#if ANDROID
    // 位置情報権限 (Android 11 以前は BLE スキャンに必須、12+ では不要だが念のため確認しておく)
    var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    if (locStatus != PermissionStatus.Granted)
    {
      if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
        await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_Bluetooth, "OK");
      await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    }

    // 付近のデバイス権限 (= BLUETOOTH_SCAN / BLUETOOTH_CONNECT、Android 12+ で必須)。
    // ※ 位置情報が Granted でも、こちらが Denied のまま BLE スキャンを呼ぶと
    //    SecurityException("Need android.permission.BLUETOOTH_SCAN") になるため、
    //    早期 return せず必ず request まで通すこと。
    var nbStatus = await Permissions.CheckStatusAsync<PermissionNearByDevice>();
    if (nbStatus != PermissionStatus.Granted)
    {
      if (Permissions.ShouldShowRationale<PermissionNearByDevice>())
        await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_NearByDevice, "OK");
      await Permissions.RequestAsync<PermissionNearByDevice>();
    }
#endif

    bleChecked = true;
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
