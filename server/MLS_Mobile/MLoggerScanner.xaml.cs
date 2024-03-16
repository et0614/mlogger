namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;
using DigiIoT.Maui.Devices.XBee;
using MLLib;
using System.Text;

public partial class MLoggerScanner : ContentPage
{

  #region 列挙型定義

  /// <summary>接続先のデバイス</summary>
  private enum ConnectedDevice
  {
    /// <summary>接続なし</summary>
    None,
    /// <summary>MLogger</summary>
    MLogger,
    /// <summary>MLTransciever</summary>
    MLTransciever
  }

  #endregion

  #region インスタンス変数・プロパティ・定数宣言

  /// <summary>MLogger付属のXBeeのパスワード</summary>
  private const string ML_PASS = "ml_pass";

  /// <summary>接続先のデバイス</summary>
  private ConnectedDevice cnctDevice = ConnectedDevice.None;

  private bool bleChecked = false;

  /// <summary>XBeeを探索する時間[msec]</summary>
  private const int SCAN_TIME = 1000;

  private XBeeBLEDevice connectedXBee;

  private MLogger mLogger;

  private MLTransceiver mlTransceiver;

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
    endXBeeCommunication();

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
        startXBeeCommunication(selectedXBee);

        //MLoggerの場合：Openに成功したら設定ページへ移動
        if (cnctDevice == ConnectedDevice.MLogger)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            Shell.Current.GoToAsync(nameof(DeviceSetting),
                new Dictionary<string, object> { { "mLogger", mLogger }, { "xbee", connectedXBee } }
                );
          });
        }
        //Transcieverの場合
        else if (cnctDevice == ConnectedDevice.MLTransciever)
        {
          //Bluetooth転送モードを有効にする
          mlTransceiver.HasRelayedToBluetoothReceived = false;
          int tryNum = 0;
          while ((!mlTransceiver.HasRelayedToBluetoothReceived) && tryNum < 4 )
          {
            try
            {
              //Bluetooth転送コマンドを送信
              connectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeRelayToBluetoothCommand()));
              await Task.Delay(1000);
            }
            catch { }
            tryNum++;
          }

          //Bluetooth転送モードを有効にできた場合
          if (mlTransceiver.HasRelayedToBluetoothReceived)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              Shell.Current.GoToAsync(nameof(RelayedDataViewer),
                  new Dictionary<string, object> { { "mlTransceiver", mlTransceiver } }
                  );
            });
          }
          else
          {
            endXBeeCommunication();
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

  #region XBee通信関連の処理

  /// <summary>MLoggerのXBeeと接続する</summary>
  /// <param name="device"></param>
  private void startXBeeCommunication(IDevice device)
  {
    //通信中のXBeeがある場合は接続を閉じる
    endXBeeCommunication();

    if (DeviceInfo.Current.Platform == DevicePlatform.Android)
      connectedXBee = new XBeeBLEDevice(device.Id.ToString(), ML_PASS);
    else connectedXBee = new XBeeBLEDevice(device, ML_PASS);

    //XBeeをOpen
    connectedXBee.Connect();

    //接続先:MLogger
    if (device.Name.StartsWith("MLogger_"))
    {
      cnctDevice = ConnectedDevice.MLogger;
      mLogger = new MLogger(connectedXBee.GetAddressString());
      mLogger.LocalName = device.Name;
    }
    //接続先:MLTransceiver
    else if (device.Name.StartsWith("MLTransceiver"))
    {
      cnctDevice = ConnectedDevice.MLTransciever;
      mlTransceiver = new MLTransceiver(connectedXBee.GetAddressString());
    }

    //イベント登録      
    connectedXBee.SerialDataReceived += ConnectedXBee_SerialDataReceived;
  }

  private void ConnectedXBee_SerialDataReceived
    (object sender, XBeeLibrary.Core.Events.Relay.SerialDataReceivedEventArgs e)
  {
    if (cnctDevice == ConnectedDevice.MLogger)
    {
      mLogger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      //コマンド処理
      while (mLogger.HasCommand)
      {
        try
        {
          mLogger.SolveCommand();
        }
        catch { }
      }
    }
    else if (cnctDevice == ConnectedDevice.MLTransciever)
    {
      mlTransceiver.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      //コマンド処理
      while (mlTransceiver.HasCommand)
      {
        try
        {
          mlTransceiver.SolveCommand();
        }
        catch { }
      }
    }
  }

  /// <summary>MLDeviceのXbeeとの接続を解除する</summary>
  private void endXBeeCommunication()
  {
    //通信中のXBeeがある場合は接続を閉じる
    if (connectedXBee != null)
    {
      //イベントを解除する
      connectedXBee.SerialDataReceived -= ConnectedXBee_SerialDataReceived;

      //開いていれば別スレッドで閉じる
      if (connectedXBee.IsConnected)
      {
        XBeeBLEDevice clsBee = connectedXBee;
        Task.Run(() =>
        {
          try
          {
            clsBee.Disconnect();
          }
          catch { }
        });
      }
    }

    mLogger = null;
    mlTransceiver = null;
    cnctDevice = ConnectedDevice.None;
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