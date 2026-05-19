namespace MLS_Mobile;

using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using MLLib;
using MLLib.Protocol;
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
      MLUtility.WriteLog("[devset] MLoggerLowAddress setter fired addr=" + value);
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
    isDeveloperMode = calvBtnA.IsVisible = calCo2Btn.IsVisible = initCo2Btn.IsVisible = !calvBtnA.IsVisible;
  }

  #endregion

  #region ロード・アンロードイベント

  protected override void OnAppearing()
  {
    base.OnAppearing();
    MLUtility.WriteLog("[devset] OnAppearing fired");

    //シェイクイベント登録
    Accelerometer.ShakeDetected += Accelerometer_ShakeDetected;
    Accelerometer.Start(SensorSpeed.UI);

    //校正ボタンの表示・非表示
    calvBtnA.IsVisible = calCo2Btn.IsVisible = initCo2Btn.IsVisible = isDeveloperMode;

    //基本は測定を停止させる
    isStopLogging = true;

    // v4: protocol level stop_logging (best-effort, fire-and-forget).
    // v3 uses event-driven ENL via Logger_MeasuredValueReceivedEvent.
    // 初回 OnAppearing でのみ発火。iOS で popup dismiss が OnAppearing を
    // 再 fire するため、_initInfoV4Done と統合してガード。
    if (IsV4Protocol && MLUtility.Protocol.IsLogging)
    {
      _ = Task.Run(async () =>
      {
        try { await MLUtility.Protocol.StopLoggingAsync(); }
        catch { /* not currently logging is fine */ }
      });
    }
  }

  // initInfoV4 が既にこの page life で走ったか。iOS で popup dismiss が QueryProperty
  // を再 set して initInfo が多重発火する事象への対策。OnDisappearing でリセット。
  private bool _initInfoV4Done = false;

  /// <summary>v4 (JSON-RPC) protocol is detected and available.</summary>
  private static bool IsV4Protocol
    => MLUtility.Protocol != null && MLUtility.Protocol.Device.ProtocolVersion >= 1;

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    MLUtility.WriteLog("[devset] OnDisappearing fired");

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
    if (IsV4Protocol) { await initInfoV4(); return; }

    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + Logger.LowAddress;
    spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
      Logger.Version_Major + "." + Logger.Version_Minor + "." + Logger.Version_Revision;

    //バージョンに応じた処理
    //常設設置モードボタンの有効化
    btn_pmntMode.IsEnabled =
      (3 <= Logger.Version_Minor) ||
      (2 == Logger.Version_Minor && 4 <= Logger.Version_Revision);

    //名称更新
    loadName();

    //測定設定更新
    loadMeasurementSetting();

    //Zigbee LED状態を更新

    //CO2濃度センサの有無を反映
    loadCO2SensorInfo();
  }

  #endregion

  #region MLogger情報更新処理

  /// <summary>測定設定を読み込む</summary>
  private async void loadMeasurementSetting()
  {
    if (IsV4Protocol) { await loadMeasurementSettingV4(); return; }

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
    if (IsV4Protocol) { await updateNameV4(name); return; }

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
    if (IsV4Protocol) { await updateMeasurementSettingV4(); return; }

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
    if (IsV4Protocol) { await openCFSettingV4(); return; }

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

  private async void CO2CalibrationButton_Clicked(object sender, EventArgs e)
  {
    if (IsV4Protocol) { await calibrateCo2V4ForcedAsync(); return; }

    var popup = new TextInputPopup("Reference CO2 level [ppm].", "600", Keyboard.Numeric);
    var result = await this.ShowPopupAsync<string>(popup);
    if (result != null)
    {
      if (!int.TryParse(result.Result, out int refLevel))
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
          Application.Current?.Dispatcher.Dispatch(() =>
          {
            Shell.Current.GoToAsync(nameof(CO2Calibrator),
              new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
              );
          });
        }
        else
        {
          Application.Current?.Dispatcher.Dispatch(() =>
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
        Application.Current?.Dispatcher.Dispatch(hideIndicator);
      }
    }
  }

  private async void CO2InitializeButton_Clicked(object sender, EventArgs e)
  {
    if (IsV4Protocol) { await calibrateCo2V4FactoryAsync(); return; }

    var popup = new TextInputPopup("Reference CO2 level [ppm].", "400", Keyboard.Numeric);
    var result = await this.ShowPopupAsync<string>(popup);
    if (result != null)
    {
      if (!int.TryParse(result.Result, out int refLevel))
      {
        Application.Current?.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", "CO2 level is invalid", "OK");
        });
        return;
      }

      //イベント待機タスクを作成
      var tcs = new TaskCompletionSource<bool>();

      //イベントが発生したらタスクを完了させるハンドラを一時的に登録
      EventHandler handler = (s, e) => tcs.TrySetResult(true);
      Logger.InitializingCO2LevelReceivedEvent += handler;

      //インジケータ表示
      showIndicator(MLSResource.CR_Connecting);

      try
      {
        //コマンドを送信 (タイムアウトも考慮して数回繰り返す)
        for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
        {
          try
          {
            await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeInitializeCO2LevelCommand(refLevel))));
          }
          catch { }

          //イベントが来るか、タイムアウト(500ms)するまで待つ
          await Task.WhenAny(tcs.Task, Task.Delay(500));
        }

        //タスクが正常に完了した場合のみUIを更新
        if (tcs.Task.IsCompletedSuccessfully)
        {
          //成功したらスタートページに戻る（計測器は12hの連続初期化処理に入る）
          Application.Current?.Dispatcher.Dispatch(() =>
          {
            Shell.Current.GoToAsync("..");
          });
        }
        else
        {
          Application.Current?.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
          });
        }
      }
      finally
      {
        //ハンドラを解除
        Logger.InitializingCO2LevelReceivedEvent -= handler;

        //インジケータを隠す
        Application.Current?.Dispatcher.Dispatch(hideIndicator);
      }
    }
  }

  private async void SetNameButton_Clicked(object sender, EventArgs e)
  {
    // v4 では Logger.Name は更新されない (C# のデフォルト 'Unloaded' のまま) ので
    // hello でキャッシュした Protocol.Device.Name を popup の初期値に使う。
    string currentName = IsV4Protocol ? MLUtility.Protocol.Device.Name : Logger.Name;
    var popup = new TextInputPopup(MLSResource.DS_SetName, currentName, Keyboard.Text);
    // Popup<string>.CloseAsync(null) は IPopupResult を non-null で返してくる。
    // 真の Cancel 判定は Result が null かどうかで行う。
    // ただし iOS で OK 押下時にも Result が null になる事象を実機で確認したため、
    // popup.EntryValue (binding 経由で常に typed text を保持) をフォールバックに使う。
    // Cancel 時は EntryValue は popup 初期値のまま残るが、ユーザが何も typing
    // しなかった場合は initial と同じ値で update が走る (副作用 = 名前不変)。
    var result = await this.ShowPopupAsync<string>(popup);
    string typed = (result?.Result is string r && !string.IsNullOrEmpty(r))
                   ? r
                   : popup.EntryValue;
    // Cancel と OK を区別: result.Result が string なら確定 OK、null かつ initial と同じなら Cancel と推定。
    bool isCancel = (result?.Result == null) && (typed == currentName);
    if (!isCancel && !string.IsNullOrEmpty(typed)) updateName(typed);
  }

  private void SDButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.mfcard);
  }

  private async void startLogging(loggingMode lMode)
  {
    //計測停止フラグを解除
    isStopLogging = false;

    // v4 path: call IMLProtocol.StartLoggingAsync directly and skip the v3 STL flow.
    if (IsV4Protocol)
    {
      await startLoggingV4(lMode);
      return;
    }

    //イベント待機タスクを作成
    var tcs = new TaskCompletionSource<bool>();

    //イベントが発生したらタスクを完了させるハンドラを一時的に登録
    EventHandler handler = (s, e) => tcs.TrySetResult(true);
    Logger.MeasuredValueReceivedEvent += handler;
    Logger.StartMeasuringMessageReceivedEvent += handler;

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
      Logger.StartMeasuringMessageReceivedEvent -= handler;

      //インジケータを隠す
      Application.Current.Dispatcher.Dispatch(hideIndicator);
    }
  }

  /// <summary>v4 path of startLogging - calls IMLProtocol.StartLoggingAsync.</summary>
  private async Task startLoggingV4(loggingMode lMode)
  {
    var (transports, mode) = lMode switch
    {
      loggingMode.bluetooth => (new Transports(false, true, false, false), LoggingMode.Once),
      loggingMode.mfcard    => (new Transports(false, false, true, false), LoggingMode.Once),
      loggingMode.pc        => (new Transports(true, false, false, false), LoggingMode.Once),
      loggingMode.permanent => (new Transports(true, false, false, false), LoggingMode.AutoRestart),
      _ => (new Transports(false, true, false, false), LoggingMode.Once),
    };

    showIndicator(MLSResource.DR_StartLogging);
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await MLUtility.Protocol.StartLoggingAsync(new LoggingConfig(transports, mode), cts.Token);

      MLUtility.WriteLog(Logger.XBeeName + "; Start logging (v4); mode=" + lMode);

      Application.Current.Dispatcher.Dispatch(() =>
      {
        if (lMode == loggingMode.bluetooth)
          Shell.Current.GoToAsync(nameof(DataReceive),
            new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } });
        else
          Shell.Current.GoToAsync("..");
      });
    }
    catch (Exception ex)
    {
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        DisplayAlert("Alert", "Failed to start logging." + Environment.NewLine + ex.Message, "OK");
      });
    }
    finally
    {
      Application.Current.Dispatcher.Dispatch(hideIndicator);
    }
  }

  /// <summary>v4 path of initInfo - populates UI from cached DeviceInfo + GetSettingsAsync.</summary>
  private async Task initInfoV4()
  {
    if (_initInfoV4Done) { MLUtility.WriteLog("[devset] initInfoV4 SKIPPED guard"); return; }
    MLUtility.WriteLog("[devset] initInfoV4 RUN first-time");  // ガード: page lifecycle 中に 1 回だけ実行
    _initInfoV4Done = true;
    var dev = MLUtility.Protocol.Device;
    Application.Current?.Dispatcher.Dispatch(() =>
    {
      spc_name.Text      = MLSResource.DS_SpecName     + ": " + dev.Name;
      spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
      spc_xbadds.Text    = MLSResource.DS_SpecXBAdd    + ": " + dev.HardwareId;
      spc_vers.Text      = MLSResource.DS_SpecVersion  + ": " + dev.FirmwareVersion;
      btn_pmntMode.IsEnabled = true;        // v4 firmware always supports permanent mode
      co2LevelGrid.IsVisible = true;        // assume CO2 sensor present (toggle handles real state)
    });

    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var s = await MLUtility.Protocol.GetSettingsAsync(cts.Token);
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        applySettingsToUI(s);
        resetTextColor();
      });
    }
    catch
    {
      // best-effort; leave UI defaults
    }
  }

  /// <summary>v4 path of updateMeasurementSetting - builds SettingsPatch from UI and calls SetSettingsAsync.</summary>
  private async Task updateMeasurementSettingV4()
  {
    if (!isInputsCorrect(out int thSpan, out int glbSpan, out int velSpan, out int luxSpan, out int co2Span)) return;

    var thSetting  = new SensorSettingPatch(cbx_th.IsToggled,  (uint)thSpan);
    var patch = new SettingsPatch
    {
      DrybulbTemperature = thSetting,
      RelativeHumidity   = thSetting,                                              // RH shares with DBT in current UI
      GlobeTemperature   = new SensorSettingPatch(cbx_glb.IsToggled, (uint)glbSpan),
      Velocity           = new SensorSettingPatch(cbx_vel.IsToggled, (uint)velSpan),
      Illuminance        = new SensorSettingPatch(cbx_lux.IsToggled, (uint)luxSpan),
      Co2                = new SensorSettingPatch(cbx_co2.IsToggled, (uint)co2Span),
      StartTime          = new DateTimeOffset(dpck_start.Date.Add(tpck_start.Time)),
    };

    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var s = await MLUtility.Protocol.SetSettingsAsync(patch, cts.Token);

      MLUtility.WriteLog(Logger.XBeeName + ": Measurement setting changed (v4)");

      Application.Current?.Dispatcher.Dispatch(() =>
      {
        applySettingsToUI(s);
        resetTextColor();
      });
    }
    catch (Exception ex)
    {
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        DisplayAlert("Alert", "Failed to save settings." + Environment.NewLine + ex.Message, "OK");
      });
    }
  }

  /// <summary>v4 path of updateName - calls SetNameAsync and reflects the returned name.</summary>
  private async Task updateNameV4(string name)
  {
    MLUtility.WriteLog("v4 set_name START name='" + (name ?? "<null>") + "' len=" + (name?.Length ?? -1));
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      var newName = await MLUtility.Protocol.SetNameAsync(name, cts.Token);

      MLUtility.WriteLog("v4 set_name OK returned='" + newName + "'");

      Application.Current?.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + newName;
      });
    }
    catch (Exception ex)
    {
      MLUtility.WriteLog("v4 set_name FAIL " + ex.GetType().Name + ": " + ex.Message);
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        DisplayAlert("Alert", "Failed to set name." + Environment.NewLine + ex.Message, "OK");
      });
    }
  }

  /// <summary>Copy Settings (server response) into the UI controls.</summary>
  private void applySettingsToUI(Settings s)
  {
    cbx_th.IsToggled  = s.DrybulbTemperature.Enabled;
    ent_th.Text       = s.DrybulbTemperature.Interval.ToString();
    cbx_glb.IsToggled = s.GlobeTemperature.Enabled;
    ent_glb.Text      = s.GlobeTemperature.Interval.ToString();
    cbx_vel.IsToggled = s.Velocity.Enabled;
    ent_vel.Text      = s.Velocity.Interval.ToString();
    cbx_lux.IsToggled = s.Illuminance.Enabled;
    ent_lux.Text      = s.Illuminance.Interval.ToString();
    cbx_co2.IsToggled = s.Co2.Enabled;
    ent_co2.Text      = s.Co2.Interval.ToString();
    var local         = s.StartTime.LocalDateTime;
    dpck_start.Date   = local.Date;
    tpck_start.Time   = local.TimeOfDay;
  }

  /// <summary>v4 path of loadMeasurementSetting -- GetSettingsAsync + UI 反映。</summary>
  private async Task loadMeasurementSettingV4()
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var s = await MLUtility.Protocol.GetSettingsAsync(cts.Token);
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        applySettingsToUI(s);
        resetTextColor();
      });
    }
    catch (Exception ex)
    {
      await DisplayAlert("Alert", "Failed to load settings." + Environment.NewLine + ex.Message, "OK");
    }
  }

  /// <summary>v4 path of CFButton_Clicked - pre-fetches correction factors then navigates.</summary>
  private async Task openCFSettingV4()
  {
    showIndicator(MLSResource.CR_Connecting);
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await MLUtility.Protocol.GetCorrectionAsync(cts.Token);

      Application.Current?.Dispatcher.Dispatch(() =>
      {
        Shell.Current.GoToAsync(nameof(CFSetting),
          new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } });
      });
    }
    catch (Exception ex)
    {
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        DisplayAlert("Alert", "Failed to load correction." + Environment.NewLine + ex.Message, "OK");
      });
    }
    finally
    {
      Application.Current?.Dispatcher.Dispatch(hideIndicator);
    }
  }

  /// <summary>v4 path of CO2CalibrationButton_Clicked (forced calibration).</summary>
  private async Task calibrateCo2V4ForcedAsync()
  {
    var popup = new TextInputPopup("Reference CO2 level [ppm].", "600", Keyboard.Numeric);
    var result = await this.ShowPopupAsync<string>(popup);
    if (result == null) return;
    if (!int.TryParse(result.Result, out int refLevel))
    {
      Application.Current?.Dispatcher.Dispatch(() => { DisplayAlert("Alert", "CO2 level is invalid", "OK"); });
      return;
    }
    await calibrateCo2V4(Co2CalibrationMode.Forced, refLevel, navigateToCalibrator: true);
  }

  /// <summary>v4 path of CO2InitializeButton_Clicked (factory reset).</summary>
  private async Task calibrateCo2V4FactoryAsync()
  {
    var popup = new TextInputPopup("Reference CO2 level [ppm].", "400", Keyboard.Numeric);
    var result = await this.ShowPopupAsync<string>(popup);
    if (result == null) return;
    if (!int.TryParse(result.Result, out int refLevel))
    {
      Application.Current?.Dispatcher.Dispatch(() => { DisplayAlert("Alert", "CO2 level is invalid", "OK"); });
      return;
    }
    await calibrateCo2V4(Co2CalibrationMode.Factory, refLevel, navigateToCalibrator: false);
  }

  private async Task calibrateCo2V4(Co2CalibrationMode mode, int refLevel, bool navigateToCalibrator)
  {
    showIndicator(MLSResource.CR_Connecting);
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await MLUtility.Protocol.CalibrateCo2Async(mode, refLevel, cts.Token);

      Application.Current?.Dispatcher.Dispatch(() =>
      {
        if (navigateToCalibrator)
          Shell.Current.GoToAsync(nameof(CO2Calibrator),
            new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } });
        else
          Shell.Current.GoToAsync("..");
      });
    }
    catch (Exception ex)
    {
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        DisplayAlert("Alert", "Failed to start CO2 calibration." + Environment.NewLine + ex.Message, "OK");
      });
    }
    finally
    {
      Application.Current?.Dispatcher.Dispatch(hideIndicator);
    }
  }

  /// <summary>Manual BLE diag: echo burst (size sweep + repeats), awaited inline.</summary>
  private async void DiagBleButton_Clicked(object sender, EventArgs e)
  {
    var jp = MLUtility.Protocol as MLLib.Protocol.Protocols.JsonRpcV4Protocol;
    if (jp == null) { await DisplayAlert("Alert", "v4 protocol not active", "OK"); return; }

    diagBleBtn.IsEnabled = false;
    diagBleBtn.Text = "Running BLE diag...";
    try
    {
      MLUtility.WriteLog("=== BLE diag (manual) start ===");

      MLUtility.WriteLog("--- Phase 1: 5x size=80 (1-chunk) ---");
      for (int i = 1; i <= 5; i++)
      {
        try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
              int r = await jp.EchoAsync(80, cts.Token);
              MLUtility.WriteLog("P1 #" + i + " OK r=" + r); }
        catch (Exception ex) { MLUtility.WriteLog("P1 #" + i + " FAIL " + ex.GetType().Name + ": " + ex.Message); }
      }

      MLUtility.WriteLog("--- Phase 2: mixed [20, 80, 200, 300, 80, 20] ---");
      int[] sizes = new int[] { 20, 80, 200, 300, 80, 20 };
      for (int i = 0; i < sizes.Length; i++)
      {
        try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
              int r = await jp.EchoAsync(sizes[i], cts.Token);
              MLUtility.WriteLog("P2 #" + (i+1) + " sz=" + sizes[i] + " OK r=" + r); }
        catch (Exception ex) { MLUtility.WriteLog("P2 #" + (i+1) + " sz=" + sizes[i] + " FAIL " + ex.GetType().Name + ": " + ex.Message); }
      }

      MLUtility.WriteLog("--- Phase 3: 3x size=240 with 2000ms gap (mimics set_settings RX size) ---");
      for (int i = 1; i <= 3; i++)
      {
        try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
              int r = await jp.EchoAsync(240, 0, cts.Token);
              MLUtility.WriteLog("P3 #" + i + " sz=240 OK r=" + r); }
        catch (Exception ex) { MLUtility.WriteLog("P3 #" + i + " sz=240 FAIL " + ex.GetType().Name + ": " + ex.Message); }
        if (i < 3) await Task.Delay(2000);
      }

      MLUtility.WriteLog("--- Phase 4: 3x echo(size=20, pad=260) with 2000ms gap (mimics set_settings TX size) ---");
      for (int i = 1; i <= 3; i++)
      {
        try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
              int r = await jp.EchoAsync(20, 260, cts.Token);
              MLUtility.WriteLog("P4 #" + i + " sz=20 pad=260 OK r=" + r); }
        catch (Exception ex) { MLUtility.WriteLog("P4 #" + i + " sz=20 pad=260 FAIL " + ex.GetType().Name + ": " + ex.Message); }
        if (i < 3) await Task.Delay(2000);
      }

      MLUtility.WriteLog("=== BLE diag end ===");
      await DisplayAlert("BLE diag", "Done. Check LogView for details.", "OK");
    }
    finally
    {
      diagBleBtn.Text = "Run BLE Diag (echo burst)";
      diagBleBtn.IsEnabled = true;
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