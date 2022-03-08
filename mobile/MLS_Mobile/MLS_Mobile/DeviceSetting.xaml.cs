using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Services;
using MLServer;
using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class DeviceSetting : ContentPage
  {

    #region インスタンス変数プロパティ

    /// <summary>バージョン情報読み込み済みか</summary>
    private bool verstionLoaded = false;

    /// <summary>接続を保持するか否か</summary>
    private bool keepConnection = false;

    /// <summary>受信データバッファ</summary>
    //private string receivedData = "";

    /// <summary>Bluetooth通信デバイスを設定・取得する</summary>
    public IDevice MLDevice { get; set; }

    /// <summary>XBeeを設定・取得する</summary>
    public ZigBeeBLEDevice MLXBee { get; set; }

    /// <summary>ロガーを設定・取得する</summary>
    public MLogger Logger { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    public DeviceSetting()
    {
      InitializeComponent();

      Title = MLSResource.DS_MeasurementSettings;

      title1.Text = MLSResource.DS_TargetAndTimeInterval;
      title2.Text = MLSResource.DS_StartDTime;
      title3.Text = MLSResource.DS_Communicate;
      title4.Text = MLSResource.DS_Info;

      lbl_th.Text = MLSResource.DS_TemperatureAndHumidity;
      lbl_glb.Text = MLSResource.GlobeTemperature;
      lbl_vel.Text = MLSResource.Velocity;
      lbl_lux.Text = MLSResource.Illuminance;

      btnLoad.Text = MLSResource.DS_LoadSetting;
      btnSave.Text = MLSResource.DS_SaveSetting;
      btnStart.Text = MLSResource.DS_Start;
      btnCFactor.Text = MLSResource.DS_CFactor;

      spc_name.Text = MLSResource.DS_SpecName + ": -";
      spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": -";
      spc_mcadds.Text = MLSResource.DS_SpecMACAdd + ": -";
      spc_vers.Text = MLSResource.DS_SpecVersion + ": -";
    }

    #endregion

    #region ロード・アンロードイベント

    protected override void OnAppearing()
    {
      //スリープ禁止
      DependencyService.Get<IDeviceService>().DisableSleep();

      //接続切断フラグ
      keepConnection = false;

      //イベント登録      
      MLXBee.SerialDataReceived += MlXBee_SerialDataReceived;

      //バージョン更新
      loadVersion();

      Task.Run(() =>
      {
        try
        {
          //機器情報表示
          string xbAdd = MLXBee.GetAddressString();
          string mcAdd = MLXBee.GetBluetoothMacAddress();

          Device.BeginInvokeOnMainThread(() =>
          {
            spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
            spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + xbAdd;
            spc_mcadds.Text = MLSResource.DS_SpecMACAdd + ": " + mcAdd;
          });
        }
        catch (Exception ex)
        {
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
          });
        }
      });

      //機器情報更新
      updateSetting();
    }

    protected override void OnDisappearing()
    {
      base.OnDisappearing();

      //スリープ解除
      DependencyService.Get<IDeviceService>().EnableSleep();

      //イベント解除
      MLXBee.SerialDataReceived -= MlXBee_SerialDataReceived;

      //Bluetooth接続の切断
      if (!keepConnection)
      {
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

    }

    #endregion

    #region 通信処理

    private void MlXBee_SerialDataReceived
      (object sender, SerialDataReceivedEventArgs e)
    {
      Logger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      string command;
      while ((command = Logger.GetCommand()) != null)
      {
        if (solveCommand(command))
          Logger.RemoveCommand(); //処理に成功した場合はコマンドを削除
        else break; //処理に失敗した場合には次回に再挑戦
      }
    }

    private bool solveCommand(string command)
    {
      //バージョン情報
      if (command.StartsWith("VER"))
      {
        verstionLoaded = true;
        Device.BeginInvokeOnMainThread(() =>
        {
          spc_vers.Text = MLSResource.DS_SpecVersion + ": " + command.Remove(0, 4);
        });
      }

      //CMSかLMSであればListViewに計測設定を反映************************
      else if (command.StartsWith("CMS") || command.StartsWith("LMS"))
      {
        string[] buff = command.Substring(4, command.Length - 4).Split(',');
        DateTime stDtime = MLogger.GetDateTimeFromUTime(long.Parse(buff[8]));

        Device.BeginInvokeOnMainThread(() =>
        {
          //計測設定
          cbx_th.IsToggled = (buff[0] == "1");
          ent_th.Text = buff[1];
          cbx_glb.IsToggled = (buff[2] == "1");
          ent_glb.Text = buff[3];
          cbx_vel.IsToggled = (buff[4] == "1");
          ent_vel.Text = buff[5];
          cbx_lux.IsToggled = (buff[6] == "1");
          ent_lux.Text = buff[7];

          //計測開始日時
          stDate.Date = stDtime;
          stTime.Time = stDtime.TimeOfDay;

          //編集要素の着色をもとに戻す
          resetTextColor();
        });
      }

      return true;
    }

    #endregion

    #region コントロール操作時の処理

    private void StartButton_Clicked(object sender, EventArgs e)
    {
      DataReceive drcv = new DataReceive();
      drcv.MLDevice = this.MLDevice;
      drcv.MLXBee = this.MLXBee;
      drcv.Logger = this.Logger;

      //ロギング開始フラグOn
      keepConnection = true;

      //測定開始ページを表示
      Navigation.PushAsync(drcv, true);
    }

    private void SaveButton_Clicked(object sender, EventArgs e)
    {
      //入力エラーがあれば終了
      int thSpan, glbSpan, velSpan, luxSpan;
      if (!isInputsCorrect(out thSpan, out glbSpan, out velSpan, out luxSpan)) return;

      //設定コマンドを作成
      string sData = "CMS"
        + (cbx_th.IsToggled ? "t" : "f") + string.Format("{0,5}", thSpan)
        + (cbx_glb.IsToggled ? "t" : "f") + string.Format("{0,5}", glbSpan)
        + (cbx_vel.IsToggled ? "t" : "f") + string.Format("{0,5}", velSpan)
        + (cbx_lux.IsToggled ? "t" : "f") + string.Format("{0,5}", luxSpan)
        + string.Format("{0,10}", MLogger.GetUnixTime(stDate.Date + stTime.Time));

      Task.Run(() =>
      {
        try
        {
          //設定コマンドを送信
          MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\r" + sData + "\r"));
        }
        catch (Exception ex)
        {
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
          });
        }
      });

    }

    private bool isInputsCorrect
      (out int thSpan, out int glbSpan, out int velSpan, out int luxSpan)
    {
      bool hasError = false;
      string alert = "";
      if (!int.TryParse(ent_th.Text, out thSpan))
      {
        hasError = true;
        alert += "温湿度の測定間隔が整数ではありません\r\n";
      }
      if (!int.TryParse(ent_glb.Text, out glbSpan))
      {
        hasError = true;
        alert += "グローブ温度の測定間隔が整数ではありません\r\n";
      }
      if (!int.TryParse(ent_vel.Text, out velSpan))
      {
        hasError = true;
        alert += "微風速の測定間隔が整数ではありません\r\n";
      }
      if (!int.TryParse(ent_lux.Text, out luxSpan))
      {
        hasError = true;
        alert += "照度の測定間隔が整数ではありません\r\n";
      }

      if (hasError)
        DisplayAlert("Alert", alert, "OK");

      return !hasError;
    }

    private void LoadButton_Clicked(object sender, EventArgs e)
    {
      updateSetting();
    }

    private void updateSetting()
    {
      Task.Run(() =>
      {
        try
        {
          //設定内容取得コマンドを送信
          MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rLMS\r"));
        }
        catch (Exception ex)
        {
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
          });
        }
      });
    }

    private void loadVersion()
    {
      if (verstionLoaded) return;

      Task.Run(async() =>
      {
        while (!verstionLoaded)
        {
          try
          {
            //バージョン取得コマンドを送信
            MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rVER\r"));
            await Task.Delay(3000);
          }
          catch { }
        }        
      });
    }

    #endregion

    #region コントロール編集時の着色処理

    private TimeSpan tsp_org;
    private DateTime dt_org;

    private void cbx_Toggled(object sender, ToggledEventArgs e)
    {
      if (sender.Equals(cbx_th)) lbl_th.TextColor = Color.Red;
      else if (sender.Equals(cbx_glb)) lbl_glb.TextColor = Color.Red;
      else if (sender.Equals(cbx_vel)) lbl_vel.TextColor = Color.Red;
      else if (sender.Equals(cbx_lux)) lbl_lux.TextColor = Color.Red;
    }

    private void ent_TextChanged(object sender, TextChangedEventArgs e)
    {
      if (sender.Equals(ent_th)) lbl_th.TextColor = Color.Red;
      else if (sender.Equals(ent_glb)) lbl_glb.TextColor = Color.Red;
      else if (sender.Equals(ent_vel)) lbl_vel.TextColor = Color.Red;
      else if (sender.Equals(ent_lux)) lbl_lux.TextColor = Color.Red;
    }

    private void stDate_Focused(object sender, FocusEventArgs e)
    { dt_org = stDate.Date; }

    private void stDate_Unfocused(object sender, FocusEventArgs e)
    { if (dt_org != stDate.Date) title2.TextColor = Color.Red; }

    private void stTime_Focused(object sender, FocusEventArgs e)
    { tsp_org = stTime.Time; }

    private void stTime_Unfocused(object sender, FocusEventArgs e)
    { if (tsp_org != stTime.Time) title2.TextColor = Color.Red; }

    private void resetTextColor()
    {
      lbl_th.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = title2.TextColor = Color.Black;
    }

    #endregion

    private void CFButton_Clicked(object sender, EventArgs e)
    {
      keepConnection = true;

      CFSetting cfs = new CFSetting();
      cfs.MLXBee = MLXBee;
      cfs.Logger = this.Logger;
      Navigation.PushAsync(cfs);
    }

  }
}