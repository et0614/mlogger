namespace MLS_Mobile;

using System.Text;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using Mopups.Services;
using System;
using System.Xml.Linq;

public partial class DeviceSetting : ContentPage
{

  #region 定数宣言

  private readonly DateTime ST_DTIME = new DateTime(1999, 1, 1, 0, 0, 0);

  #endregion

  #region インスタンス変数・プロパティ

  /// <summary>ロギングを停止させるか否か</summary>
  private bool isStopLogging = true;

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public DeviceSetting()
  {
    InitializeComponent();

    //ポップで戻ってきた場合
    MopupService.Instance.Popped += Instance_Popped;

    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + MLUtility.Logger.LocalName;
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + MLUtility.Logger.LowAddress;
    spc_vers.Text = MLSResource.DS_SpecVersion + ": -";

    //バージョン更新
    loadVersion();

    //名称更新
    loadName();

    //測定設定更新
    loadMeasurementSetting();

    //Zigbee LED状態を更新
    loadZigbeeLEDStatus();
  }

  private void Instance_Popped(object sender, Mopups.Events.PopupNavigationEventArgs e)
  {
    if (!(e.Page is SettingNamePopup)) return;

    SettingNamePopup snPop = (SettingNamePopup)e.Page;

    //名称更新
    if (snPop.HasChanged)
      updateName(snPop.Name);
  }

  #endregion

  #region ロード・アンロードイベント

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //基本は測定を停止させる
    isStopLogging = true;

    //SDカード書き出しの可視状態更新
    btnSDLogging.IsVisible = MLUtility.MMCardEnabled;

    MLUtility.Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    MLUtility.Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    //計測開始中でなければ停止させる
    if (isStopLogging)
    {
      MLUtility.Logger.HasEndMeasuringMessageReceived = false;

      Task.Run(async () =>
      {
        //情報が更新されるまで命令を繰り返す
        while (!MLUtility.Logger.HasEndMeasuringMessageReceived)
        {
          try
          {
            //停止コマンドを送信
            MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeEndLoggingCommand()));
            await Task.Delay(500);
          }
          catch { }
        }
      });
    }
  }

  #endregion

  #region MLogger情報更新処理

  /// <summary>測定設定を読み込む</summary>
  private void loadMeasurementSetting()
  {
    MLUtility.Logger.HasMeasurementSettingReceived = false;

    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (!MLUtility.Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //設定設定取得コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadMeasuringSettingCommand()));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //計測設定
        cbx_th.IsToggled = MLUtility.Logger.DrybulbTemperature.Measure;
        ent_th.Text = MLUtility.Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = MLUtility.Logger.GlobeTemperature.Measure;
        ent_glb.Text = MLUtility.Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = MLUtility.Logger.Velocity.Measure;
        ent_vel.Text = MLUtility.Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = MLUtility.Logger.Illuminance.Measure;
        ent_lux.Text = MLUtility.Logger.Illuminance.Interval.ToString();

        //編集要素の着色をもとに戻す
        resetTextColor();
      });
    });
  }

  /// <summary>バージョン情報を読み込む</summary>
  private void loadVersion()
  {
    MLUtility.Logger.HasVersionReceived = false;
    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (!MLUtility.Logger.HasVersionReceived)
      {
        try
        {
          //バージョン取得コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand()));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
          MLUtility.Logger.Version_Major + "." +
          MLUtility.Logger.Version_Minor + "." +
          MLUtility.Logger.Version_Revision;
      });
    });
  }

  /// <summary>名称を読み込む</summary>
  private void loadName()
  {
    MLUtility.Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      while (!MLUtility.Logger.HasLoggerNameReceived)
      {
        try
        {
          //名称取得コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadLoggerNameCommand()));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + MLUtility.Logger.Name;
      });
    });
  }

  /// <summary>名称を設定する</summary>
  /// <param name="name">名称</param>
  private void updateName(string name)
  {
    MLUtility.Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      while (!MLUtility.Logger.HasLoggerNameReceived)
      {
        try
        {
          //名称取得コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeChangeLoggerNameCommand(name)));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + MLUtility.Logger.Name;
      });
    });
  }

  /// <summary>測定設定を設定する</summary>
  private void updateMeasurementSetting()
  {
    //入力エラーがあれば終了
    int thSpan, glbSpan, velSpan, luxSpan;
    if (!isInputsCorrect(out thSpan, out glbSpan, out velSpan, out luxSpan)) return;

    //設定コマンドを作成
    string sData = MLogger.MakeChangeMeasuringSettingCommand(
      ST_DTIME,
      cbx_th.IsToggled, thSpan,
      cbx_glb.IsToggled, glbSpan,
      cbx_vel.IsToggled, velSpan,
      cbx_lux.IsToggled, luxSpan,
      false, 0, false, 0, false, 0, false);


    MLUtility.Logger.HasMeasurementSettingReceived = false;
    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (!MLUtility.Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //設定設定取得コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(sData));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //計測設定
        cbx_th.IsToggled = MLUtility.Logger.DrybulbTemperature.Measure;
        ent_th.Text = MLUtility.Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = MLUtility.Logger.GlobeTemperature.Measure;
        ent_glb.Text = MLUtility.Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = MLUtility.Logger.Velocity.Measure;
        ent_vel.Text = MLUtility.Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = MLUtility.Logger.Illuminance.Measure;
        ent_lux.Text = MLUtility.Logger.Illuminance.Interval.ToString();

        //編集要素の着色をもとに戻す
        resetTextColor();
      });
    });
  }

  /// <summary>Zigbee通信LED表示状態を読み込む</summary>
  private void loadZigbeeLEDStatus()
  {
    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (true)
      {
        try
        {
          byte[] rslt = MLUtility.LoggerSideXBee.GetParameter("D5");
          Application.Current.Dispatcher.Dispatch(() =>
          {
            btn_zigled.Text =
            (rslt[0] == 1) ? MLSResource.DS_DisableZigLED : MLSResource.DS_EnableZigLED;
          });
          return;
        }
        catch{ }
        await Task.Delay(500);
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
      alert +=　MLSResource.DS_InvalidNumber + "(" + MLSResource.DrybulbTemperature + ")\r\n";
    }
    if (!int.TryParse(ent_glb.Text, out glbSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.GlobeTemperature + ")\r\n";
    }
    if (!int.TryParse(ent_vel.Text, out velSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.Velocity + ")\r\n";
    }
    if (!int.TryParse(ent_lux.Text, out luxSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.Illuminance + ")\r\n";
    }

    if (hasError)
      DisplayAlert("Alert", alert, "OK");

    return !hasError;
  }

  #endregion

  #region コントロール操作時の処理

  private void StartButton_Clicked(object sender, EventArgs e)
  {
    startLogging(false);
  }

  private void SaveButton_Clicked(object sender, EventArgs e)
  {
    updateMeasurementSetting();
  }

  private void LoadButton_Clicked(object sender, EventArgs e)
  {
    loadMeasurementSetting();
  }

  private void CFButton_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(Calibrator));
  }

  private void SetNameButton_Clicked(object sender, EventArgs e)
  {
    MopupService.Instance.PushAsync(new SettingNamePopup(MLUtility.Logger.Name));
  }

  private void SDButton_Clicked(object sender, EventArgs e)
  {
    startLogging(true);
  }

  private void startLogging(bool writeToSDCard)
  {
    //計測停止フラグを解除
    isStopLogging = false;

    MLUtility.Logger.HasStartMeasuringMessageReceived = false;

    //インジケータ表示
    showIndicator(MLSResource.DR_StartLogging);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        while (!MLUtility.Logger.HasStartMeasuringMessageReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
              return;
            });
          }
          tryNum++;

          //開始コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeStartMeasuringCommand(false, !writeToSDCard, writeToSDCard)));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          if(writeToSDCard) Shell.Current.GoToAsync("..");
          else Shell.Current.GoToAsync(nameof(DataReceive));
        });
      }
      catch { }
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

  #region コントロール編集時の着色処理

  private void cbx_Toggled(object sender, ToggledEventArgs e)
  {
    if (sender.Equals(cbx_th)) lbl_th.TextColor = Colors.Red;
    else if (sender.Equals(cbx_glb)) lbl_glb.TextColor = Colors.Red;
    else if (sender.Equals(cbx_vel)) lbl_vel.TextColor = Colors.Red;
    else if (sender.Equals(cbx_lux)) lbl_lux.TextColor = Colors.Red;
  }

  private void ent_TextChanged(object sender, TextChangedEventArgs e)
  {
    if (sender.Equals(ent_th)) lbl_th.TextColor = Colors.Red;
    else if (sender.Equals(ent_glb)) lbl_glb.TextColor = Colors.Red;
    else if (sender.Equals(ent_vel)) lbl_vel.TextColor = Colors.Red;
    else if (sender.Equals(ent_lux)) lbl_lux.TextColor = Colors.Red;
  }

  private void resetTextColor()
  {
    lbl_th.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Colors.DarkGreen;
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

  #region Zigbee通信LED表示の有効化・無効化処理

  /// <summary>Zigbee通信LED表示の有効化・無効化を変更</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void LEDButton_Clicked(object sender, EventArgs e)
  {
    //設定読み込み中は無視
    if (btn_zigled.Text == MLSResource.DS_LoadingZigLED) return;
    bool ledEnabled = (btn_zigled.Text == MLSResource.DS_DisableZigLED);

    Task.Run(async () =>
    {
      //成功するまで3回は繰り返す
      for (int i = 0; i < 3; i++)
      {
        try
        {
          MLUtility.LoggerSideXBee.SetParameter("D5", ledEnabled ? new byte[] { 4 } : new byte[] { 1 });
          Application.Current.Dispatcher.Dispatch(() =>
          {
            btn_zigled.Text = ledEnabled ? MLSResource.DS_EnableZigLED : MLSResource.DS_DisableZigLED;
          });
          return;
        }
        catch { }
        await Task.Delay(500);
      }
    });
  }

  #endregion

}