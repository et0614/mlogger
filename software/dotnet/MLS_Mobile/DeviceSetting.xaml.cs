namespace MLS_Mobile;

using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using MLLib;
using MLLib.Protocol;
using MLS_Mobile.Resources.i18n;
using System;
using System.Text;
using System.Threading;
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
      _mlLowAddress = value;
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

  #region 常設モード (PC ボタン長押し)

  /// <summary>長押し成立を検知したフラグ。直後の Clicked を 1 回スキップするために使う。</summary>
  private bool _pcLongPressTriggered = false;

  /// <summary>Pressed で開始するタイマー。Released で取消す。</summary>
  private CancellationTokenSource? _pcLongPressCts;

  /// <summary>長押し成立までの時間 [ms]。</summary>
  private const int PC_LONG_PRESS_MS = 2000;

  /// <summary>PC ボタン押下開始: 2 秒タイマーを起動 (Released または別ボタン押下で取消)。</summary>
  private async void CnctToPcButton_Pressed(object sender, EventArgs e)
  {
    _pcLongPressCts?.Cancel();
    _pcLongPressCts = new CancellationTokenSource();
    var ct = _pcLongPressCts.Token;

    try
    {
      await Task.Delay(PC_LONG_PRESS_MS, ct);
    }
    catch (TaskCanceledException) { return; }
    if (ct.IsCancellationRequested) return;

    // 長押し成立: 後続の Clicked を 1 回スキップさせるフラグを立てる
    _pcLongPressTriggered = true;

    try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); }
    catch { /* 端末が haptic 非対応でも気にしない */ }

    bool ok = await DisplayAlert(
      MLSResource.DS_PermanentConfirmTitle,
      MLSResource.DS_PermanentConfirmMessage,
      MLSResource.Yes,
      MLSResource.Cancel);

    if (ok) startLogging(loggingMode.permanent);
  }

  /// <summary>PC ボタン押上 (or タッチ離脱): 2 秒経過前なら長押しタイマーを取消。</summary>
  private void CnctToPcButton_Released(object sender, EventArgs e)
  {
    _pcLongPressCts?.Cancel();
  }

  #endregion

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

    //シェイクイベント登録
    Accelerometer.ShakeDetected += Accelerometer_ShakeDetected;
    Accelerometer.Start(SensorSpeed.UI);

    //校正ボタンの表示・非表示
    calvBtnA.IsVisible = calCo2Btn.IsVisible = initCo2Btn.IsVisible = isDeveloperMode;

    //基本は測定を停止させる
    isStopLogging = true;

    // best-effort stop_logging on entry (covers re-entry from DataReceive).
    if (MLUtility.Protocol != null && MLUtility.Protocol.IsLogging)
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

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //シェイクイベント解除
    Accelerometer.Stop();
    Accelerometer.ShakeDetected -= Accelerometer_ShakeDetected;
  }

  #endregion

  #region 初期化処理

  private async void initInfo()
  {
    await initInfoV4();
  }

  #endregion

  #region MLogger情報更新処理

  /// <summary>測定設定を読み込む</summary>
  private async void loadMeasurementSetting() => await loadMeasurementSettingV4();

  /// <summary>名称を読み込む</summary>
  /// <summary>名称を設定する</summary>
  /// <param name="name">名称</param>
  private async void updateName(string name) => await updateNameV4(name);

  /// <summary>測定設定を設定する</summary>
  private async void updateMeasurementSetting() => await updateMeasurementSettingV4();

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

  private async void CFButton_Clicked(object sender, EventArgs e) => await openCFSettingV4();

  private async void CO2CalibrationButton_Clicked(object sender, EventArgs e) => await calibrateCo2V4ForcedAsync();

  private async void CO2InitializeButton_Clicked(object sender, EventArgs e) => await calibrateCo2V4FactoryAsync();

  private async void SetNameButton_Clicked(object sender, EventArgs e)
  {
    // v4 では Logger.Name は更新されない (C# のデフォルト 'Unloaded' のまま) ので
    // hello でキャッシュした Protocol.Device.Name を popup の初期値に使う。
    string currentName = MLUtility.Protocol?.Device.Name ?? Logger.Name;
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

  private async void startLogging(loggingMode lMode) => await startLoggingV4(lMode);

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
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_StartLoggingFailed, ex);
    }
    finally
    {
      Application.Current.Dispatcher.Dispatch(hideIndicator);
    }
  }

  /// <summary>v4 path of initInfo - populates UI from cached DeviceInfo + GetSettingsAsync.</summary>
  private async Task initInfoV4()
  {
    if (_initInfoV4Done) return;
    _initInfoV4Done = true;
    var dev = MLUtility.Protocol.Device;
    Application.Current?.Dispatcher.Dispatch(() =>
    {
      spc_name.Text      = MLSResource.DS_SpecName     + ": " + dev.Name;
      spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
      spc_xbadds.Text    = MLSResource.DS_SpecXBAdd    + ": " + dev.HardwareId;
      spc_vers.Text      = MLSResource.DS_SpecVersion  + ": " + dev.FirmwareVersion;
      co2LevelGrid.IsVisible = dev.HasCo2Sensor;
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

    // After GetSettings succeeds the BLE link has settled enough that set_time
    // is reliably accepted by firmware. fire-and-forget; failure is logged only.
    _ = MLUtility.SyncDeviceTimeAsync();
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
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_SaveSettingsFailed, ex);
    }
  }

  /// <summary>v4 path of updateName - calls SetNameAsync and reflects the returned name.</summary>
  private async Task updateNameV4(string name)
  {
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
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_SetNameFailed, ex);
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
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_LoadSettingsFailed, ex);
    }
  }

  /// <summary>v4 path of CFButton_Clicked - pre-fetches correction factors then navigates.</summary>
  private async Task openCFSettingV4()
  {
    showIndicator(MLSResource.CF_Setting);   // 「補正係数を読み込み中...」
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
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_LoadCorrectionFailed, ex);
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
    int? refLevel = await PromptCo2LevelAsync(popup);
    if (refLevel is not int level) return;
    await calibrateCo2V4(Co2CalibrationMode.Forced, level, navigateToCalibrator: true);
  }

  /// <summary>v4 path of CO2InitializeButton_Clicked (factory reset).</summary>
  private async Task calibrateCo2V4FactoryAsync()
  {
    var popup = new TextInputPopup("Reference CO2 level [ppm].", "400", Keyboard.Numeric);
    int? refLevel = await PromptCo2LevelAsync(popup);
    if (refLevel is not int level) return;
    await calibrateCo2V4(Co2CalibrationMode.Factory, level, navigateToCalibrator: false);
  }

  /// <summary>
  /// CO2 reference level の数値入力 popup を出して値を返す。
  /// Cancel (Popup.Result == null) なら黙って null を返す。
  /// 数値として解釈できないときだけ alert を出す。
  /// </summary>
  private async Task<int?> PromptCo2LevelAsync(TextInputPopup popup)
  {
    var result = await this.ShowPopupAsync<string>(popup);
    if (result?.Result is not string typed || string.IsNullOrWhiteSpace(typed))
      return null;

    if (!int.TryParse(typed, out int refLevel))
    {
      await DisplayAlert(MLSResource.ERR_AlertTitle, MLSResource.DS_InvalidNumber, "OK");
      return null;
    }
    return refLevel;
  }

  private async Task calibrateCo2V4(Co2CalibrationMode mode, int refLevel, bool navigateToCalibrator)
  {
    showIndicator(MLSResource.DS_StartingCo2Calibration);
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
      await MLUtility.ShowErrorAsync(this, MLSResource.ERR_StartCo2CalibrationFailed, ex);
    }
    finally
    {
      Application.Current?.Dispatcher.Dispatch(hideIndicator);
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

  /// <summary>PCとの接続ボタンタップ時の処理 (長押し時は StartPermanentLoggingCommand 側で処理し、こちらは 1 回スキップ)</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void CnctToPcButton_Clicked(object sender, EventArgs e)
  {
    // 直前に長押しが成立していたら通常のクリック処理はスキップ
    if (_pcLongPressTriggered) { _pcLongPressTriggered = false; return; }
    startLogging(loggingMode.pc);
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


  #endregion

}