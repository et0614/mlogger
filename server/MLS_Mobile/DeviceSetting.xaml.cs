namespace MLS_Mobile;

using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using MLLib;
using MLS_Mobile.Resources.i18n;
using System;
using System.Text;
using System.Threading.Tasks;

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

  /// <summary>開発者モードか否か</summary>
  private static bool isDeveloperMode = false;

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

  #region コンストラクタ・デストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public DeviceSetting()
  {
    InitializeComponent();
  }

  /// <summary>シェイク時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void Accelerometer_ShakeDetected(object sender, EventArgs e)
  {
    //一般的ではないボタン群の表示・非表示切り替え
    isDeveloperMode = calvBtnA.IsVisible = calvBtnB.IsVisible = calCo2Btn.IsVisible = !calvBtnA.IsVisible;
  }

  #endregion

  #region ロード・アンロードイベント

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //シェイクイベント登録
    Accelerometer.ShakeDetected += Accelerometer_ShakeDetected;
    Accelerometer.Start(SensorSpeed.UI);

    //校正ボタンの表示・非表示
    calvBtnA.IsVisible = calvBtnB.IsVisible = calCo2Btn.IsVisible = isDeveloperMode;

    //基本は測定を停止させる
    isStopLogging = true;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //シェイクイベント解除
    Accelerometer.Stop();
    Accelerometer.ShakeDetected -= Accelerometer_ShakeDetected;

    if (Logger != null)
      Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  private async void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    //計測開始中でなければ停止させる
    if (isStopLogging)
    {
      //イベント待機タスクを作成
      var tcs = new TaskCompletionSource<bool>();

      //イベントが発生したらタスクを完了させるハンドラを一時的に登録
      EventHandler handler = (s, e) => tcs.TrySetResult(true);
      Logger.EndMeasuringMessageReceivedEvent += handler;

      try
      {
        //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
        var command = MLogger.MakeLoadMeasuringSettingCommand();
        for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
        {
          try
          {
            await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeEndLoggingCommand())));
          }
          catch { }

          //イベントが来るか、タイムアウト(500ms)するまで待つ
          await Task.WhenAny(tcs.Task, Task.Delay(500));
        }

        //タスクが正常に完了した場合のみUIを更新
        if (tcs.Task.IsCompletedSuccessfully)
        {
          MLUtility.WriteLog(Logger.XBeeName + "; End logging; ");
        }
      }
      finally
      {
        //ハンドラを解除
        Logger.EndMeasuringMessageReceivedEvent -= handler;
      }
    }
  }

  #endregion

  #region 初期化処理

  private async void initInfo()
  {
    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + Logger.LowAddress;
    spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
      Logger.Version_Major + "." + Logger.Version_Minor + "." + Logger.Version_Revision;

    //バージョンに応じた処理
    //Zigbee LEDの有効無効ボタンの有効化
    btn_zigled.IsEnabled = 3 <= Logger.Version_Minor;

    //常設設置モードボタンの有効化
    btn_pmntMode.IsEnabled =
      (3 <= Logger.Version_Minor) ||
      (2 == Logger.Version_Minor && 4 <= Logger.Version_Revision);

    //名称更新
    loadName();

    //測定設定更新
    loadMeasurementSetting();

    //Zigbee LED状態を更新
    loadZigbeeLEDStatus();

    //CO2濃度センサの有無を反映
    loadCO2SensorInfo();
  }

  #endregion

  #region MLogger情報更新処理

  /// <summary>測定設定を読み込む</summary>
  private async void loadMeasurementSetting()
  {
    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.MeasurementSettingReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      var command = MLogger.MakeLoadMeasuringSettingCommand();
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(command)));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみUIを更新
      if (tcs.Task.IsCompletedSuccessfully)
      {
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
          cbx_co2.IsToggled = Logger.CO2Level.Measure;
          ent_co2.Text = Logger.CO2Level.Interval.ToString();
          dpck_start.Date = Logger.StartMeasuringDateTime;
          tpck_start.Time = Logger.StartMeasuringDateTime.TimeOfDay;

          //編集要素の着色をもとに戻す
          resetTextColor();
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.MeasurementSettingReceivedEvent -= handler;
    }
  }

  /// <summary>名称を読み込む</summary>
  private async void loadName()
  {
    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.LoggerNameReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadLoggerNameCommand())));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみUIを更新
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.LoggerNameReceivedEvent -= handler;
    }
  }

  /// <summary>名称を設定する</summary>
  /// <param name="name">名称</param>
  private async void updateName(string name)
  {
    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.LoggerNameReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeChangeLoggerNameCommand(name))));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみUIを更新
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //ロギング
          MLUtility.WriteLog(Logger.XBeeName + "; Name changed; " + Logger.Name + "; ");

          spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.LoggerNameReceivedEvent -= handler;
    }
  }

  private async void loadCO2SensorInfo()
  {
    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.HasCO2LevelSensorReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeHasCO2LevelSensorCommand())));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみUIを更新
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          co2LevelGrid.IsVisible = Logger.HasCO2LevelSensor;
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.HasCO2LevelSensorReceivedEvent -= handler;
    }
  }

  /// <summary>測定設定を設定する</summary>
  private async void updateMeasurementSetting()
  {
    //入力エラーがあれば終了
    if (!isInputsCorrect(out int thSpan, out int glbSpan, out int velSpan, out int luxSpan, out int co2Span)) return;

    //設定コマンドを作成
    string sData = MLogger.MakeChangeMeasuringSettingCommand(
      dpck_start.Date.Add(tpck_start.Time),
      cbx_th.IsToggled, thSpan,
      cbx_glb.IsToggled, glbSpan,
      cbx_vel.IsToggled, velSpan,
      cbx_lux.IsToggled, luxSpan,
      false, 0, false, 0, false, 0, false,
      Logger.HasCO2LevelSensor && cbx_co2.IsToggled, co2Span); //CO2センサ

    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.MeasurementSettingReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(sData)));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみUIを更新
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //ロギング
          MLUtility.WriteLog(Logger.XBeeName + ": Measurement setting changed; " +
            (MLSResource.DrybulbTemperature + "=" + (Logger.DrybulbTemperature.Measure ? Logger.DrybulbTemperature.Interval + " sec; " : "false; ")) +
            (MLSResource.GlobeTemperature + "=" + (Logger.GlobeTemperature.Measure ? Logger.GlobeTemperature.Interval + " sec; " : "false; ")) +
            (MLSResource.Velocity + "=" + (Logger.Velocity.Measure ? Logger.Velocity.Interval + " sec; " : "false; ")) +
            (MLSResource.Illuminance + "=" + (Logger.Illuminance.Measure ? Logger.Illuminance.Interval + " sec; " : "false; ")) +
            (MLSResource.CO2level + "=" + (Logger.CO2Level.Measure ? Logger.CO2Level.Interval + " sec; " : "false; "))
            );

          //計測設定
          cbx_th.IsToggled = Logger.DrybulbTemperature.Measure;
          ent_th.Text = Logger.DrybulbTemperature.Interval.ToString();
          cbx_glb.IsToggled = Logger.GlobeTemperature.Measure;
          ent_glb.Text = Logger.GlobeTemperature.Interval.ToString();
          cbx_vel.IsToggled = Logger.Velocity.Measure;
          ent_vel.Text = Logger.Velocity.Interval.ToString();
          cbx_lux.IsToggled = Logger.Illuminance.Measure;
          ent_lux.Text = Logger.Illuminance.Interval.ToString();
          cbx_co2.IsToggled = Logger.CO2Level.Measure;
          ent_co2.Text = Logger.CO2Level.Interval.ToString();
          dpck_start.Date = Logger.StartMeasuringDateTime;
          tpck_start.Time = Logger.StartMeasuringDateTime.TimeOfDay;

          //編集要素の着色をもとに戻す
          resetTextColor();
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.MeasurementSettingReceivedEvent -= handler;
    }
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
        catch { }
        await Task.Delay(500);
      }
    });
  }

  private bool isInputsCorrect
  (out int thSpan, out int glbSpan, out int velSpan, out int luxSpan, out int co2Span)
  {
    bool hasError = false;
    string alert = "";
    if (!int.TryParse(ent_th.Text, out thSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.DrybulbTemperature + ")\r\n";
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
    if (!int.TryParse(ent_co2.Text, out co2Span))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.CO2level + ")\r\n";
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

  private async void CFButton_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.CorrectionFactorsReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand())));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみ
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(CFSetting),
            new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
            );
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.CorrectionFactorsReceivedEvent -= handler;

      //インジケータを隠す
      Application.Current.Dispatcher.Dispatch(hideIndicator);
    }
  }

  private async void VelocityCalibrationButton_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.VelocityCharateristicsReceivedEvent += handler;

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadVelocityCharateristicsCommand())));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみUIを更新
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //バージョンによって近似方法が異なるのでバージョン読み込み確認
        if (!MLUtility.Logger.VersionLoaded)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", "Version number has not loaded.", "OK");
          });
          return;
        }

        double[] minVandCoefs = new double[] { Logger.VelocityMinVoltage, Logger.VelocityCharacteristicsCoefA, Logger.VelocityCharacteristicsCoefB, Logger.VelocityCharacteristicsCoefC };

        //ここから電圧取得処理*******
        //イベント待機タスクを作成
        var tcs2 = new TaskCompletionSource<bool>();

        //イベントが発生したらタスクを完了させるハンドラを一時的に登録
        EventHandler handler2 = (s, e) => tcs2.TrySetResult(true);
        Logger.CalibratingVoltageReceivedEvent += handler2;

        try
        {
          //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
          for (int i = 0; i < 5 && !tcs2.Task.IsCompleted; i++)
          {
            try
            {
              await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeStartCalibratingVoltageCommand())));
            }
            catch { }

            //イベントが来るか、タイムアウト(500ms)するまで待つ
            await Task.WhenAny(tcs.Task, Task.Delay(500));
          }

          //タスクが正常に完了した場合のみUIを更新
          if (tcs.Task.IsCompletedSuccessfully)
          {
            //開始に成功したらページ移動
            Application.Current.Dispatcher.Dispatch(() =>
            {
              //新風速近似式
              if (3 < MLUtility.Logger.Version_Major || 3 < MLUtility.Logger.Version_Minor || 19 < MLUtility.Logger.Version_Revision)
                Shell.Current.GoToAsync(nameof(VelocityCalibrator2),
                  new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress }, { "minVandCoefs", minVandCoefs } }
                  );
              //旧風速近似式
              else
                Shell.Current.GoToAsync(nameof(VelocityCalibrator),
                  new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress }, { "minVandCoefs", minVandCoefs } }
                  );
            });
          }
        }
        finally
        {
          //ハンドラを解除
          Logger.CalibratingVoltageReceivedEvent -= handler2;
        }        
      }
      else
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.VelocityCharateristicsReceivedEvent -= handler;

      //インジケータを隠す
      Application.Current.Dispatcher.Dispatch(hideIndicator);
    }
  }

  private async void CO2CalibrationButton_Clicked(object sender, EventArgs e)
  {
    var popup = new TextInputPopup("Reference CO2 level [ppm].", "600", Keyboard.Numeric);
    var result = await this.ShowPopupAsync(popup);
    if (result != null)
    {
      if (!int.TryParse((string)result, out int refLevel))
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", "CO2 level is invalid", "OK");
        });
        return;
      }

      //イベント待機タスクを作成
      var tcs = new TaskCompletionSource<bool>();

      //イベントが発生したらタスクを完了させるハンドラを一時的に登録
      EventHandler handler = (s, e) => tcs.TrySetResult(true);
      Logger.CalibratingCO2LevelReceivedEvent += handler;

      //インジケータ表示
      showIndicator(MLSResource.CR_Connecting);

      try
      {
        //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
        for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
        {
          try
          {
            await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeCalibrateCO2LevelCommand(refLevel))));
          }
          catch { }

          //イベントが来るか、タイムアウト(500ms)するまで待つ
          await Task.WhenAny(tcs.Task, Task.Delay(500));
        }

        //タスクが正常に完了した場合のみUIを更新
        if (tcs.Task.IsCompletedSuccessfully)
        {
          //更新された情報を反映
          Application.Current.Dispatcher.Dispatch(() =>
          {
            Shell.Current.GoToAsync(nameof(CO2Calibrator),
              new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
              );
          });
        }
        else
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
          });
        }
      }
      finally
      {
        //ハンドラを解除
        Logger.CalibratingCO2LevelReceivedEvent -= handler;

        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(hideIndicator);
      }
    }
  }

  private async void SetNameButton_Clicked(object sender, EventArgs e)
  {
    var popup = new TextInputPopup(MLSResource.DS_SetName, Logger.Name, Keyboard.Text);
    var result = await this.ShowPopupAsync(popup);
    if (result != null) updateName(popup.EntryValue);
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

        Application.Current.Dispatcher.Dispatch(async () =>
        {
          var popup = new TextInputPopup(MLSResource.DS_ChangePANID, panID, Keyboard.Numeric);
          var result = await this.ShowPopupAsync(popup);
          if (result != null)
          {
            try
            {
              int panID = Convert.ToInt32(popup.EntryValue, 16);
              byte[] ar = BitConverter.GetBytes(panID);
              Array.Reverse(ar);

              await Task.Run(() =>
              {
                MLUtility.ConnectedXBee.SetParameter("ID", ar);
                MLUtility.ConnectedXBee.WriteChanges();
              });
            }
            catch { }
          }
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

  private async void startLogging(loggingMode lMode)
  {
    //計測停止フラグを解除
    isStopLogging = false;

    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.MeasuredValueReceivedEvent += handler;

    //インジケータ表示
    showIndicator(MLSResource.DR_StartLogging);

    try
    {
      //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
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
      for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
      {
        try
        {
          await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(cmd)));
        }
        catch { }

        //イベントが来るか、タイムアウト(500ms)するまで待つ
        await Task.WhenAny(tcs.Task, Task.Delay(500));
      }

      //タスクが正常に完了した場合のみ
      if (tcs.Task.IsCompletedSuccessfully)
      {
        //ログ
        if (lMode == loggingMode.bluetooth)
          MLUtility.WriteLog(Logger.XBeeName + "; Start logging by smart phone; ");
        else if (lMode == loggingMode.mfcard)
          MLUtility.WriteLog(Logger.XBeeName + "; Start logging to flash memory; ");
        else
          MLUtility.WriteLog(Logger.XBeeName + "; Start logging to PC; ");

        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //Bluetoothの場合にはスマートフォンでデータ表示
          if (lMode == loggingMode.bluetooth)
            Shell.Current.GoToAsync(nameof(DataReceive),
              new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
              );
          //フラッシュメモリまたはPCへの保存の場合にはスタートページへ戻る
          else Shell.Current.GoToAsync("..");
        });
      }
    }
    finally
    {
      //ハンドラを解除
      Logger.MeasuredValueReceivedEvent -= handler;

      //インジケータを隠す
      Application.Current.Dispatcher.Dispatch(hideIndicator);
    }
  }

  #endregion

  #region コントロール編集時の着色処理

  private void cbx_Toggled(object sender, ToggledEventArgs e)
  {
    if (sender.Equals(cbx_th)) lbl_th.TextColor = Colors.Red;
    else if (sender.Equals(cbx_glb)) lbl_glb.TextColor = Colors.Red;
    else if (sender.Equals(cbx_vel)) lbl_vel.TextColor = Colors.Red;
    else if (sender.Equals(cbx_lux)) lbl_lux.TextColor = Colors.Red;
    else if (sender.Equals(cbx_co2)) lbl_co2.TextColor = Colors.Red;
  }

  private void ent_TextChanged(object sender, TextChangedEventArgs e)
  {
    if (sender.Equals(ent_th)) lbl_th.TextColor = Colors.Red;
    else if (sender.Equals(ent_glb)) lbl_glb.TextColor = Colors.Red;
    else if (sender.Equals(ent_vel)) lbl_vel.TextColor = Colors.Red;
    else if (sender.Equals(ent_lux)) lbl_lux.TextColor = Colors.Red;
    else if (sender.Equals(ent_co2)) lbl_co2.TextColor = Colors.Red;
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
    if (tpck_start == null || Logger == null || tpck_start.Time == Logger.StartMeasuringDateTime.TimeOfDay) return;

    lbl_stdtime.TextColor = Colors.Red;
  }

  private void resetTextColor()
  {
    lbl_th.TextColor =
      lbl_glb.TextColor =
      lbl_vel.TextColor =
      lbl_lux.TextColor =
      lbl_stdtime.TextColor =
      lbl_co2.TextColor =
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

  #region ヘルプタップ時の処理

  private async void TapGestureRecognizer_Measure_Tapped(object sender, TappedEventArgs e)
  {
    var popup = new DescriptionPopup(DescriptionText.StartLogging);
    var result = await this.ShowPopupAsync(popup);
  }

  private async void TapGestureRecognizer_Setting_Tapped(object sender, TappedEventArgs e)
  {
    var popup = new DescriptionPopup(DescriptionText.MeasurementInterval);
    var result = await this.ShowPopupAsync(popup);
  }

  private async void TapGestureRecognizer_PCSetting_Tapped(object sender, TappedEventArgs e)
  {
    var popup = new DescriptionPopup(DescriptionText.PCSetting);
    var result = await this.ShowPopupAsync(popup);
  }

  #endregion

}