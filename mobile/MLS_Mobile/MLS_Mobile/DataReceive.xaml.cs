using System;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using MLS_Mobile.Services;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

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
  public partial class DataReceive : ContentPage
  {

    #region インスタンス変数・プロパティ

    /// <summary>初期化フラグ</summary>
    private bool initializing = true;

    /// <summary>計測開始フラグ</summary>
    private bool isStarted = false;

    /// <summary>計測終了フラグ</summary>
    private bool isEnded = false;

    private bool isEnding = false;

    /// <summary>Bluetooth通信デバイスを設定・取得する</summary>
    public IDevice MLDevice { get; set; }

    /// <summary>XBeeを設定・取得する</summary>
    public ZigBeeBLEDevice MLXBee { get; set; }

    /// <summary>ロガーを設定・取得する</summary>
    public MLogger Logger { get; set; }


    private readonly ObservableCollection<string> metItems = new ObservableCollection<string>();

    #endregion

    #region コンストラクタ

    public DataReceive()
    {
      InitializeComponent();

      title_tmp.Text = MLSResource.DrybulbTemperature;
      title_hmd.Text = MLSResource.RelativeHumidity;
      title_glb.Text = MLSResource.GlobeTemperature;
      title_vel.Text = MLSResource.Velocity;
      title_lux.Text = MLSResource.Illuminance;

      quitBtn.Text = MLSResource.DR_FinishMeasurement;


      cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
      metTitle.Text = MLSResource.MetabolicUnit + " [met]";

      //活動量リスト
      metItems.Add(ThermalComfortRes.Rs_Sleeping);
      metItems.Add(ThermalComfortRes.Rs_Reclining);
      metItems.Add(ThermalComfortRes.Rs_Seated);
      metItems.Add(ThermalComfortRes.Rs_Standing);
      metItems.Add(ThermalComfortRes.Walking_09);
      metItems.Add(ThermalComfortRes.Walking_12);
      metItems.Add(ThermalComfortRes.Walking_18);
      metItems.Add(ThermalComfortRes.Of_Seated);
      metItems.Add(ThermalComfortRes.Of_Typing);
      metItems.Add(ThermalComfortRes.Of_FilingSeated);
      metItems.Add(ThermalComfortRes.Of_FilingStanding);
      metItems.Add(ThermalComfortRes.Of_Walking);
      metItems.Add(ThermalComfortRes.Of_Lifting);
      metItems.Add(ThermalComfortRes.Driving_Automobile);
      metItems.Add(ThermalComfortRes.Driving_Aircraft1);
      metItems.Add(ThermalComfortRes.Driving_Aircraft2);
      metItems.Add(ThermalComfortRes.Driving_Aircraft3);
      metItems.Add(ThermalComfortRes.Driving_Heavy);
      metItems.Add(ThermalComfortRes.Cooling);
      metItems.Add(ThermalComfortRes.HouseCleaning);
      metItems.Add(ThermalComfortRes.HeavyLimbMovement);
      metItems.Add(ThermalComfortRes.Mw_Sawing);
      metItems.Add(ThermalComfortRes.Mw_Light);
      metItems.Add(ThermalComfortRes.Mw_Heavy);
      metItems.Add(ThermalComfortRes.Mw_HandlingBags);
      metItems.Add(ThermalComfortRes.Mw_ShovelWork);
      metItems.Add(ThermalComfortRes.Dancing);
      metItems.Add(ThermalComfortRes.Exercise);
      metItems.Add(ThermalComfortRes.Tennis);
      metItems.Add(ThermalComfortRes.Basketball);
      metItems.Add(ThermalComfortRes.Wrestling);
      metList.ItemsSource = metItems;
    }

    #endregion

    #region コントロール操作時の処理

    private async void QuitBtn_Clicked(object sender, EventArgs e)
    {
      if (await DisplayAlert(MLSResource.DR_FinishAlert, "", MLSResource.Yes, MLSResource.No))
      {
        _ = Task.Run(async () =>
          {
            try
            {
              MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rENL\r"));

              showIndicator(MLSResource.DR_StopLogging);
              isEnding = true;

              await Task.Delay(5000);
              if (!isEnded)
              {
                Device.BeginInvokeOnMainThread(() =>
                {
                  DisplayAlert("Alert", MLSResource.DR_FailStopping, "OK");
                  Navigation.PopToRootAsync();
                });
              }
            }
            catch { }
          });
      }
    }

    private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
      if (initializing) return;

      Logger.MetValue = metSlider.Value;
      Logger.CloValue = cloSlider.Value;
    }

    private void metList_SelectedIndexChanged(object sender, EventArgs e)
    {
      string sItem = (string)metList.SelectedItem;

      if (sItem == ThermalComfortRes.Rs_Sleeping) metSlider.Value = 0.7;
      else if (sItem == ThermalComfortRes.Rs_Reclining) metSlider.Value = 0.8;
      else if (sItem == ThermalComfortRes.Rs_Seated) metSlider.Value = 1.0;
      else if (sItem == ThermalComfortRes.Rs_Standing) metSlider.Value = 1.2;
      else if (sItem == ThermalComfortRes.Walking_09) metSlider.Value = 2.0;
      else if (sItem == ThermalComfortRes.Walking_12) metSlider.Value = 2.6;
      else if (sItem == ThermalComfortRes.Walking_18) metSlider.Value = 3.8;
      else if (sItem == ThermalComfortRes.Of_Seated) metSlider.Value = 1.0;
      else if (sItem == ThermalComfortRes.Of_Typing) metSlider.Value = 1.1;
      else if (sItem == ThermalComfortRes.Of_FilingSeated) metSlider.Value = 1.2;
      else if (sItem == ThermalComfortRes.Of_FilingStanding) metSlider.Value = 1.4;
      else if (sItem == ThermalComfortRes.Of_Walking) metSlider.Value = 1.7;
      else if (sItem == ThermalComfortRes.Of_Lifting) metSlider.Value = 2.1;
      else if (sItem == ThermalComfortRes.Driving_Automobile) metSlider.Value = 1.5;
      else if (sItem == ThermalComfortRes.Driving_Aircraft1) metSlider.Value = 1.2;
      else if (sItem == ThermalComfortRes.Driving_Aircraft2) metSlider.Value = 1.8;
      else if (sItem == ThermalComfortRes.Driving_Aircraft3) metSlider.Value = 2.4;
      else if (sItem == ThermalComfortRes.Driving_Heavy) metSlider.Value = 3.2;
      else if (sItem == ThermalComfortRes.Cooling) metSlider.Value = 1.8;
      else if (sItem == ThermalComfortRes.HouseCleaning) metSlider.Value = 2.7;
      else if (sItem == ThermalComfortRes.HeavyLimbMovement) metSlider.Value = 2.2;
      else if (sItem == ThermalComfortRes.Mw_Sawing) metSlider.Value = 1.8;
      else if (sItem == ThermalComfortRes.Mw_Light) metSlider.Value = 2.2;
      else if (sItem == ThermalComfortRes.Mw_Heavy) metSlider.Value = 4.0;
      else if (sItem == ThermalComfortRes.Mw_HandlingBags) metSlider.Value = 4.0;
      else if (sItem == ThermalComfortRes.Mw_ShovelWork) metSlider.Value = 4.4;
      else if (sItem == ThermalComfortRes.Dancing) metSlider.Value = 3.4;
      else if (sItem == ThermalComfortRes.Exercise) metSlider.Value = 3.5;
      else if (sItem == ThermalComfortRes.Tennis) metSlider.Value = 3.8;
      else if (sItem == ThermalComfortRes.Basketball) metSlider.Value = 6.3;
      else if (sItem == ThermalComfortRes.Wrestling) metSlider.Value = 7.9;
    }

    #endregion

    #region ロード・アンロードイベント

    protected override void OnAppearing()
    {
      this.Title = Logger.Name;

      //Clo値,代謝量初期化
      initializing = true;
      cloSlider.Value = Logger.CloValue;
      metSlider.Value = Logger.MetValue;
      initializing = false;

      //スリープ禁止
      DependencyService.Get<IDeviceService>().DisableSleep();

      //イベント登録      
      MLXBee.SerialDataReceived += MLXBee_SerialDataReceived;

      Task.Run(async () =>
      {
        try
        {
          //開始コマンドを送信//xbee通信無効,bluetooth通信有効,sdcard書き出し無効(ftf)
          MLXBee.SendSerialData
          (Encoding.ASCII.GetBytes("\rSTL" + MLogger.GetUnixTime(DateTime.Now) + "ftf\r"));

          showIndicator(MLSResource.DR_StartLogging);

          await Task.Delay(5000);
          if (!isStarted)
          {
            Device.BeginInvokeOnMainThread(() =>
            {
              DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
              Navigation.PopAsync();
            });
          }
        }
        catch (Exception ex)
        {
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
            Navigation.PopToRootAsync();
          });
        }
      });

    }

    protected override void OnDisappearing()
    {
      base.OnDisappearing();

      //スリープ解除
      DependencyService.Get<IDeviceService>().EnableSleep();

      //イベント解除
      MLXBee.SerialDataReceived -= MLXBee_SerialDataReceived;

      //Bluetooth接続を解除
      Task.Run(async () =>
      {
        if (MLXBee.IsOpen)
        {
          try
          {
            MLXBee.Close();
            IAdapter adapter = CrossBluetoothLE.Current.Adapter;
            await adapter.DisconnectDeviceAsync(MLDevice);
          }
          catch (Exception bex)
          {
            Device.BeginInvokeOnMainThread(() =>
            {
              DisplayAlert("Alert", bex.Message, "OK");
              Navigation.PopAsync(true);
            });
          }
        }
      });

    }

    #endregion

    #region 通信処理

    private void MLXBee_SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
      //受信データを追加
      Logger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      //コマンドがある限り処理を続ける
      string command;
      while ((command = Logger.GetCommand()) != null)
      {
        if (SolveCommand(command)) Logger.RemoveCommand(); //処理に成功した場合はコマンドを削除
        else break; //処理に失敗した場合には次回に再挑戦
      }
    }

    private bool SolveCommand(string command)
    {
      //計測開始
      if (command.StartsWith("STL"))
      {
        isStarted = true;
        hideIndicator();
      }

      //計測データ取得
      else if (command.StartsWith("DTT"))
      {
        isStarted = true;
        if (!isEnding) hideIndicator();

        Logger.SolveDTT
          (command, out DateTime now, out double tmp, out double hmd,
          out double glbV, out double glb, out double velV, out double vel, out double illm);

        Device.BeginInvokeOnMainThread(() =>
        {
          val_tmp.Text = Logger.LastTemperature.ToString("F2");
          val_hmd.Text = Logger.LastRelativeHumidity.ToString("F2");
          val_glb.Text = Logger.LastGlobeTemperature.ToString("F2");
          val_vel.Text = (100 * Logger.LastVelocity).ToString("F1");
          val_lux.Text = Logger.LastIlluminance.ToString("F2");
          val_mrt.Text = Logger.MeanRadiantTemperature.ToString("F2");
          val_pmv.Text = Logger.PMV.ToString("F2");
          val_ppd.Text = Logger.PPD.ToString("F1");
          val_set.Text = Logger.SETStar.ToString("F2");

          dtm_tmp.Text = Logger.LastMeasureTime_TH.ToString("yyyy/M/d HH:mm:ss");
          dtm_hmd.Text = Logger.LastMeasureTime_TH.ToString("yyyy/M/d HH:mm:ss");
          dtm_glb.Text = Logger.LastMeasureTime_Glb.ToString("yyyy/M/d HH:mm:ss");
          dtm_vel.Text = Logger.LastMeasureTime_Vel.ToString("yyyy/M/d HH:mm:ss");
          dtm_lux.Text = Logger.LastMeasureTime_Ill.ToString("yyyy/M/d HH:mm:ss");
        });

        //データを保存
        string line =
          now.ToString("yyyy/M/d,HH:mm:ss") + "," +
          tmp.ToString("F1") + "," +
          hmd.ToString("F1") + "," +
          glb.ToString("F2") + "," +
          vel.ToString("F1") + "," +
          illm.ToString("F2") + "," +
          glbV.ToString("F3") + "," +
          velV.ToString("F3") + "," +
          Logger.MetValue.ToString("F2") + "," +
          Logger.CloValue.ToString("F2") + Environment.NewLine;
        byte[] dat = Encoding.UTF8.GetBytes(line);

        IFolder localSt = CrossStorage.FileSystem.LocalStorage;
        IFolder folder = localSt.CreateFolder(MainPage.DATA_FOLDER, CreationCollisionOption.OpenIfExists);
        IFile file = folder.CreateFile(Logger.Name + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt", CreationCollisionOption.OpenIfExists);
        using (Stream strm = file.Open(FileAccess.ReadWrite))
        {
          strm.Seek(0, SeekOrigin.End);
          strm.Write(dat, 0, dat.Length);
        }
      }

      //計測終了
      else if (command.StartsWith("ENL"))
      {
        isEnded = true;
        hideIndicator();
        Device.BeginInvokeOnMainThread(() =>
        {
          Navigation.PopToRootAsync();
        });
      }

      return true;
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

  }
}