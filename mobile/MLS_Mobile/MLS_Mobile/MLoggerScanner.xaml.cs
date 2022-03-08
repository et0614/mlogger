using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using XBeeLibrary.Xamarin;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLServer;

using Plugin.NetStandardStorage;
using Plugin.NetStandardStorage.Abstractions.Types;
using Plugin.NetStandardStorage.Abstractions.Interfaces;

using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class MLoggerScanner : ContentPage
  {

    #region インスタンス変数・プロパティ・定数宣言

    private const string ML_PASS = "ml_pass";

    private readonly ObservableCollection<xbee> xbees = new ObservableCollection<xbee>();

    #endregion

    #region コンストラクタ

    public MLoggerScanner()
    {
      InitializeComponent();

      Title = MLSResource.SC_Title;
      mlList.ItemsSource = xbees;
    }

    #endregion

    #region ロード・アンロードイベント

    protected override void OnAppearing()
    {
      base.OnAppearing();

      //インジケータ表示
      showIndicator(MLSResource.SC_Scannning);

      //Bluetoothを用意
      IBluetoothLE bluetoothLe = CrossBluetoothLE.Current;
      if (bluetoothLe.State == BluetoothState.Off)
      {
        DisplayAlert("Alert", MLSResource.SC_Bluetooth, "OK");
        return;
      }

      //アダプタを用意
      IAdapter adapter = bluetoothLe.Adapter;

      //スキャン中でなければスキャン開始
      if (adapter.IsScanning) return;

      //スキャン設定
      adapter.ScanTimeout = 5000;
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
          {
            xbees.Add(new xbee(dvName, ev.Device.Id));
            hideIndicator();
          }
        }
      };

      //非同期スキャン開始
      xbees.Clear();
      adapter.StartScanningForDevicesAsync();

      //5秒待っても見つからなければ終了
      Task.Run(async() => 
      {
        await Task.Delay(5000);
        if (xbees.Count == 0)
        {
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", MLSResource.SC_Notfound, "OK");
            Navigation.PopAsync();
          });
        }
      });

    }

    protected override void OnDisappearing()
    {
      base.OnDisappearing();

      //Bluetoothを用意
      IBluetoothLE bluetoothLe = CrossBluetoothLE.Current;
      if (bluetoothLe.State == BluetoothState.Off)
      {
        DisplayAlert("Alert", "Bluetoothを有効にしてください", "OK");
        return;
      }

      //アダプタを用意。スキャン中ならば停止
      IAdapter adapter = bluetoothLe.Adapter;
      if (adapter.IsScanning)
        adapter.StopScanningForDevicesAsync();
    }

    #endregion

    #region インジケータの操作

    /// <summary>インジケータを表示する</summary>
    private void showIndicator(string message)
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        indicatorLabel.Text = message;
        grayback.IsVisible = indicator.IsVisible = true;
      });
    }

    /// <summary>インジケータを隠す</summary>
    private void hideIndicator()
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        grayback.IsVisible = indicator.IsVisible = false;
      });
    }

    #endregion

    #region インナークラスの定義

    private class xbee
    {

      public string Name { get; private set; }

      public Color TextColor { get; set; } = Color.Black;

      public Guid Id { get; private set; }

      public xbee(string name, Guid id)
      {
        Name = name;
        Id = id;
      }

    }

    #endregion

    #region コントロール操作時の処理

    private void mlList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
      if (e.SelectedItem == null) return;

      //インジケータを表示して制御不可にする
      showIndicator(MLSResource.SC_Connecting);

      Task.Run(async () =>
      {
        try
        {
          //BLE Deviceに接続
          IAdapter adapter = CrossBluetoothLE.Current.Adapter;
          IDevice mlDevice = await adapter.ConnectToKnownDeviceAsync(((xbee)e.SelectedItem).Id);

          ZigBeeBLEDevice mlXBee;
          switch (Device.RuntimePlatform)
          {
            //Androidの場合
            case Device.Android:
              mlXBee = new ZigBeeBLEDevice(((xbee)e.SelectedItem).Id.ToString(), ML_PASS);
              break;
            //その他のデバイス（iOS）
            default:
              mlXBee = new ZigBeeBLEDevice(mlDevice, ML_PASS);
              break;
          }

          //XBeeをOpen
          mlXBee.Open();

          //Openに成功したら設定ページへ移動
          Device.BeginInvokeOnMainThread(() =>
          {
            DeviceSetting dvset = new DeviceSetting();
            dvset.MLXBee = mlXBee;
            dvset.MLDevice = mlDevice;
            dvset.Logger = loadMLogger(mlXBee.GetAddressString(), ((xbee)e.SelectedItem).Name);
            mlList.SelectedItem = null; //選択解除
            Navigation.PushAsync(dvset, true);
          });
        }
        catch (Exception bex)
        {
          //失敗した場合にはエラーメッセージを出す
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", bex.Message, "OK");
          });
        }
        finally
        {
          //インジケータを隠す
          Device.BeginInvokeOnMainThread(() =>
          {
            hideIndicator();
          });
        }
      });

    }

    private MLogger loadMLogger(string address, string name)
    {
      MLogger logger = new MLogger(address);
      logger.Name = name;
      string cfName = name + ".txt";

      IFolder localSt = CrossStorage.FileSystem.LocalStorage;
      IFolder folder = localSt.CreateFolder(MainPage.CF_FOLDER, CreationCollisionOption.OpenIfExists);
      if (folder.CheckFileExists(cfName))
      {
        IFile file = folder.GetFile(cfName);
        Stream strm = file.Open(FileAccess.Read);
        byte[] buff = new byte[strm.Length];
        strm.Read(buff, 0, (int)strm.Length);
        string cfLine = Encoding.UTF8.GetString(buff);
        logger.InitCFactors(cfLine);
      }

      return logger;
    }

    #endregion

  }
}