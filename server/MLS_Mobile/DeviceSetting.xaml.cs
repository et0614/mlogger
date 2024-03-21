namespace MLS_Mobile;

using System.Text;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using Mopups.Services;
using System;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class DeviceSetting : ContentPage
{

  #region 定数宣言

  private readonly DateTime ST_DTIME = new DateTime(1999, 1, 1, 0, 0, 0);

  #endregion

  #region 列挙型定義

  /// <summary>接続方式</summary>
  private enum loggingMode
  {
    /// <summary>Bluetoothで携帯などと接続</summary>
    bluetooth = 0,
    /// <summary>Microflash互換カードに記録</summary>
    mfcard = 1,
    /// <summary>ZigbeeでPCと接続</summary>
    pc = 2,
    /// <summary>Zigbeeで常設</summary>
    permanent = 3
  }

  #endregion

  #region インスタンス変数・プロパティ

  /// <summary>ロギングを停止させるか否か</summary>
  private bool isStopLogging = true;

  /// <summary>低位アドレス</summary>
  private string _mlLowAddress = "";

  /// <summary>低位アドレスを設定・取得する</summary>
  public string MLoggerLowAddress
  {
    get
    {
      return _mlLowAddress;
    }
    set
    {
      //登録済の場合にはイベントを解除
      MLogger ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;

      initInfo();
    }
  }

  /// <summary>データを受信するMLoggerを設定・取得する</summary>
  public MLogger Logger
  {
    get
    {
      return MLUtility.GetLogger(_mlLowAddress);
    }
  }

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public DeviceSetting()
  {
    InitializeComponent();

    //ポップで戻ってきた場合
    MopupService.Instance.Popped += Instance_Popped;
  }

  private void Instance_Popped(object sender, Mopups.Events.PopupNavigationEventArgs e)
  {
    if (!(e.Page is SettingPopup)) return;

    SettingPopup snPop = (SettingPopup)e.Page;

    //状態値を更新
    if (snPop.HasChanged)
    {
      if (snPop.PopID == 0)
        updateName(snPop.ChangedValue);
      else if (snPop.PopID == 1)
      {
        try
        {
          int panID = Convert.ToInt32(snPop.ChangedValue, 16);
          byte[] ar = BitConverter.GetBytes(panID);
          Array.Reverse(ar);
          Task.Run(() =>
          {
            MLUtility.ConnectedXBee.SetParameter("ID", ar);
            MLUtility.ConnectedXBee.WriteChanges();
          });
        }
        catch { }
      }
    }
  }

  #endregion

  #region ロード・アンロードイベント

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //基本は測定を停止させる
    isStopLogging = true;

    //Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    //計測開始中でなければ停止させる
    if (isStopLogging)
    {
      Logger.HasEndMeasuringMessageReceived = false;

      Task.Run(async () =>
      {
        //情報が更新されるまで命令を繰り返す
        while (!Logger.HasEndMeasuringMessageReceived)
        {
          try
          {
            //停止コマンドを送信
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeEndLoggingCommand()));
            await Task.Delay(500);
          }
          catch { }
        }
      });
    }
  }

  #endregion

  #region 初期化処理

  private void initInfo()
  {
    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + Logger.LowAddress;
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

  #endregion

  #region MLogger情報更新処理

  /// <summary>測定設定を読み込む</summary>
  private void loadMeasurementSetting()
  {
    Logger.HasMeasurementSettingReceived = false;

    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (!Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //設定設定取得コマンドを送信
          MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadMeasuringSettingCommand()));
        }
        catch { }
        await Task.Delay(500);
        if (Logger == null) return; //接続解除時には終了
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //計測設定
        cbx_th.IsToggled = Logger.DrybulbTemperature.Measure;
        ent_th.Text = Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = Logger.GlobeTemperature.Measure;
        ent_glb.Text = Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = Logger.Velocity.Measure;
        ent_vel.Text = Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = Logger.Illuminance.Measure;
        ent_lux.Text = Logger.Illuminance.Interval.ToString();
        dpck_start.Date = Logger.StartMeasuringDateTime;
        tpck_start.Time = Logger.StartMeasuringDateTime.TimeOfDay;

        //編集要素の着色をもとに戻す
        resetTextColor();
      });
    });
  }

  /// <summary>バージョン情報を読み込む</summary>
  private void loadVersion()
  {
    Logger.HasVersionReceived = false;
    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (!Logger.HasVersionReceived)
      {
        try
        {
          //バージョン取得コマンドを送信
          if(MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand()));
        }
        catch { }
        await Task.Delay(500);
        if (Logger == null) return; //接続解除時には終了
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
          Logger.Version_Major + "." +
          Logger.Version_Minor + "." +
          Logger.Version_Revision;

        //Zigbee LEDの有効無効ボタンの有効化
        btn_zigled.IsEnabled = 3 <= Logger.Version_Minor;

        //常設設置モードボタンの有効化
        btn_pmntMode.IsEnabled =
        (3 <= Logger.Version_Minor) ||
        (2 == Logger.Version_Minor && 4 <= Logger.Version_Revision);
      });
    });
  }

  /// <summary>名称を読み込む</summary>
  private void loadName()
  {
    Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      while (!Logger.HasLoggerNameReceived)
      {
        try
        {
          //名称取得コマンドを送信
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadLoggerNameCommand()));
        }
        catch { }
        await Task.Delay(500);
        if (Logger == null) return; //接続解除時には終了
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
      });
    });
  }

  /// <summary>名称を設定する</summary>
  /// <param name="name">名称</param>
  private void updateName(string name)
  {
    Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      if (Logger == null) return; //接続解除時には終了
      while (!Logger.HasLoggerNameReceived)
      {
        try
        {
          //名称取得コマンドを送信
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeChangeLoggerNameCommand(name)));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
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
      dpck_start.Date.Add(tpck_start.Time),
      cbx_th.IsToggled, thSpan,
      cbx_glb.IsToggled, glbSpan,
      cbx_vel.IsToggled, velSpan,
      cbx_lux.IsToggled, luxSpan,
      false, 0, false, 0, false, 0, false);


    Logger.HasMeasurementSettingReceived = false;
    Task.Run(async () =>
    {
      //情報が更新されるまで命令を繰り返す
      while (!Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //設定設定取得コマンドを送信
          MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(sData));
        }
        catch { }
        await Task.Delay(500);
      }

      //更新された情報を反映
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //計測設定
        cbx_th.IsToggled = Logger.DrybulbTemperature.Measure;
        ent_th.Text = Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = Logger.GlobeTemperature.Measure;
        ent_glb.Text = Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = Logger.Velocity.Measure;
        ent_vel.Text = Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = Logger.Illuminance.Measure;
        ent_lux.Text = Logger.Illuminance.Interval.ToString();

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
          byte[] rslt = MLUtility.ConnectedXBee.GetParameter("D5");
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
    startLogging(loggingMode.bluetooth);
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
    Shell.Current.GoToAsync(nameof(Calibrator),
      new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
      );
  }

  private void SetNameButton_Clicked(object sender, EventArgs e)
  {
    MopupService.Instance.PushAsync(new SettingPopup(
      0,
      MLSResource.DS_SetName, 
      Logger.Name,
      Keyboard.Text
      ));
  }

  private void SDButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.mfcard);
  }

  private void PANButton_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.DR_StartLogging);

    Task.Run(() =>
    {
      try
      {
        byte[] id = MLUtility.ConnectedXBee.GetParameter("ID");
        string panID = BitConverter.ToString(id).Replace("-", "").TrimStart('0');

        Application.Current.Dispatcher.Dispatch(() =>
        {
          MopupService.Instance.PushAsync(new SettingPopup(
            1,
            MLSResource.DS_ChangePANID,
            panID,
            Keyboard.Numeric
            ));
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

  private void startLogging(loggingMode lMode)
  {
    //計測停止フラグを解除
    isStopLogging = false;

    Logger.HasStartMeasuringMessageReceived = false;

    //インジケータ表示
    showIndicator(MLSResource.DR_StartLogging);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        while (!Logger.HasStartMeasuringMessageReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
            });
            return;
          }
          tryNum++;

          string cmd = "";
          switch (lMode)
          {
            case loggingMode.bluetooth:
              cmd = MLogger.MakeStartMeasuringCommand(false, true, false);
              break;
            case loggingMode.mfcard:
              cmd = MLogger.MakeStartMeasuringCommand(false, false, true);
              break;
            case loggingMode.pc:
              cmd = MLogger.MakeStartMeasuringCommand(true, false, false);
              break;
            case loggingMode.permanent:
              cmd = MLogger.MakeStartMeasuringCommand(true, false, false, true);
              break;
          }

          //開始コマンドを送信
          MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(cmd));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          if (lMode == loggingMode.bluetooth) 
          {
            Shell.Current.GoToAsync(nameof(DataReceive),
              new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
              );
          }
          else Shell.Current.GoToAsync("..");
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

  private void dpck_start_DateSelected(object sender, DateChangedEventArgs e)
  {
    //日付変更がなければ終了
    if (dpck_start.Date == Logger.StartMeasuringDateTime) return;

    lbl_stdtime.TextColor = Colors.Red;
  }

  private void tpck_start_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    //時刻変更がなければ終了
    if (tpck_start == null  || Logger == null || tpck_start.Time == Logger.StartMeasuringDateTime.TimeOfDay) return;

    lbl_stdtime.TextColor = Colors.Red;
  }

  private void resetTextColor()
  {
    lbl_th.TextColor = 
      lbl_glb.TextColor = 
      lbl_vel.TextColor = 
      lbl_lux.TextColor = 
      lbl_stdtime.TextColor = 
      Colors.DarkGreen;
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

  #region Zigbee通信関連の処理

  /// <summary>PCとの接続ボタンタップ時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void CnctToPcButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.pc);
  }

  /// <summary>常設モードボタンタップ時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void PermanentModeButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.permanent);
  }

  /// <summary>Zigbee通信LED表示の有効化・無効化を変更ボタンタップ時の処理</summary>
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
          MLUtility.ConnectedXBee.SetParameter("D5", ledEnabled ? new byte[] { 4 } : new byte[] { 1 });
          MLUtility.ConnectedXBee.WriteChanges(); //設定を反映
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