namespace MLS_Mobile;

using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using MLLib;
using MLLib.Protocol;
using MLS_Mobile.Resources.i18n;
using System;
using System.Globalization;
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
    isDeveloperMode = calvBtnA.IsVisible = calCo2Btn.IsVisible = resetCo2Btn.IsVisible = initCo2Btn.IsVisible = !calvBtnA.IsVisible;
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
    calvBtnA.IsVisible = calCo2Btn.IsVisible = resetCo2Btn.IsVisible = initCo2Btn.IsVisible = isDeveloperMode;

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
      alert += MLSResource.DS_InvalidNumber + "(" + lbl_th.Text + ")\r\n";
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

  private async void CO2ResetButton_Clicked(object sender, EventArgs e) => await calibrateCo2V4ResetAsync();

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

      Application.Current.Dispatcher.Dispatch(async () =>
      {
        try
        {
          if (lMode == loggingMode.bluetooth)
            await Shell.Current.GoToAsync(nameof(DataReceive),
              new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } });
          else
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception navEx)
        {
          MLUtility.WriteLog($"[nav] GoToAsync FAIL: {navEx.GetType().Name}: {navEx.Message}");
        }
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
    var dev = MLUtility.Protocol.Device;
    bool isV4 = dev.ProtocolVersion >= 1;
    // 電池パネル / settings は時間と共に (firmware 側で別 client が変えるなど) 変わりうる
    // ため _initInfoV4Done に拘わらず毎回取得する。spec ラベルや時刻同期は初回のみで OK。
    Application.Current?.Dispatcher.Dispatch(() => applyProtocolModeToUI(isV4, dev.HasCo2Sensor));

    // 接続直後の RPC は順次発火 (battery → settings → set_time)。BLE link で 3 つ
    // 並行に投げると後発分が timeout する事象を回避するため。await で順番に流す。
    // さらに各 RPC の間に短い間隔を空けて XBee/firmware 側の処理完了を待つ
    // (battery 直後に settings を投げると settings 応答が来ない事象への対策)。
    if (isV4) { await refreshBatteryAsync();  await Task.Delay(300); }
    await refreshSettingsAsync();

    if (!_initInfoV4Done)
    {
      _initInfoV4Done = true;
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        spc_name.Text      = MLSResource.DS_SpecName     + ": " + dev.Name;
        spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
        spc_xbadds.Text    = MLSResource.DS_SpecXBAdd    + ": " + dev.HardwareId;
        spc_vers.Text      = MLSResource.DS_SpecVersion  + ": " + dev.FirmwareVersion;
      });

      // 初回のみ時刻同期。fire-and-forget だと直後にユーザーが start_logging を押したとき
      // BLE 競合で timeout する事象があるため、settings の後に短い間隔を空けて await で待つ。
      await Task.Delay(300);
      await MLUtility.SyncDeviceTimeAsync();
    }
  }

  /// <summary>get_settings を叩いて UI に反映 (毎回呼ばれる)。</summary>
  private async Task refreshSettingsAsync()
  {
    try
    {
      MLUtility.WriteLog("[settings] GetSettingsAsync start");
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var s = await MLUtility.Protocol.GetSettingsAsync(cts.Token);
      MLUtility.WriteLog("[settings] GetSettingsAsync OK");
      Application.Current?.Dispatcher.Dispatch(() =>
      {
        applySettingsToUI(s);
        resetTextColor();
      });
    }
    catch (Exception ex)
    {
      MLUtility.WriteLog($"[settings] GetSettingsAsync FAIL: {ex.GetType().Name}: {ex.Message}");
    }
  }

  /// <summary>get_battery を叩いて電池パネルを更新する (best-effort)。</summary>
  private async Task refreshBatteryAsync()
  {
    try
    {
      MLUtility.WriteLog("[battery] GetBatteryAsync start");
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      _battery = await MLUtility.Protocol.GetBatteryAsync(cts.Token);
      MLUtility.WriteLog($"[battery] GetBatteryAsync OK: {_battery.VoltageMv}mV low={_battery.IsLow}");
      Application.Current?.Dispatcher.Dispatch(updateBatteryPanel);
    }
    catch (Exception ex)
    {
      _battery = null;
      MLUtility.WriteLog($"[battery] GetBatteryAsync FAIL: {ex.GetType().Name}: {ex.Message}");
    }
  }

  /// <summary>直近の get_battery 応答 (null = 未取得 / 取得失敗)。</summary>
  private BatteryInfo? _battery;

  /// <summary>電池情報パネル (電圧/種別/連続計測可能時間) を現在の UI 入力と _battery から再描画。</summary>
  private void updateBatteryPanel()
  {
    if (_battery is null)
    {
      lbl_batVoltage.Text   = "-";
      lbl_batType.Text      = MLSResource.DS_BatteryTypeUnknown;
      lbl_batRuntime.Text   = "-";
      lbl_batWarning.IsVisible = false;
      return;
    }
    lbl_batVoltage.Text = (_battery.VoltageMv / 1000.0).ToString("F2") + " V";

    var type = BatteryEstimator.DetectType(_battery.VoltageMv);
    lbl_batType.Text = type switch
    {
      BatteryType.Alkaline => MLSResource.DS_BatteryTypeAlkaline,
      BatteryType.NiMH     => MLSResource.DS_BatteryTypeNiMH,
      _                    => MLSResource.DS_BatteryTypeUnknown,
    };

    // UI 入力値で消費電力を計算 (入力不正なら estimate を出さない)。電池パネルは
    // v4 接続時のみ表示されるので、ここでは th 行 (= General カテゴリ) を代表値とする。
    if (!int.TryParse(ent_th.Text,  out int thSpan)  ||
        !int.TryParse(ent_vel.Text, out int velSpan) ||
        !int.TryParse(ent_lux.Text, out int luxSpan) ||
        thSpan <= 0 || velSpan <= 0 || luxSpan <= 0)
    {
      lbl_batRuntime.Text = "-";
    }
    else
    {
      // BatteryEstimator は DrybulbTemperature を General 代表値として使うので、
      // th 行の値を th/humidity/t_glb/co2 に展開して Settings を組み立てる。
      var thSetting = new SensorSetting(cbx_th.IsToggled, (uint)thSpan);
      var s = new Settings(
        DrybulbTemperature: thSetting,
        RelativeHumidity:   thSetting,
        GlobeTemperature:   thSetting,
        Velocity:           new SensorSetting(cbx_vel.IsToggled, (uint)velSpan),
        Illuminance:        new SensorSetting(cbx_lux.IsToggled, (uint)luxSpan),
        Co2:                thSetting,
        StartTime:          DateTimeOffset.Now);
      double pMw = BatteryEstimator.EstimatePowerMw(s);
      var runtime = BatteryEstimator.EstimateContinuousRuntime(pMw, _battery.VoltageMv);
      lbl_batRuntime.Text = formatRuntime(runtime);
    }

    lbl_batWarning.IsVisible = _battery.IsLow;
    lbl_batWarning.Text      = MLSResource.DS_LowBatteryWarning;
  }

  private static string formatRuntime(TimeSpan t)
  {
    if (t.TotalHours <= 0) return "-";
    int totalDays = (int)t.TotalDays;
    if (totalDays >= 90) return MLSResource.DS_RuntimeLong;
    if (totalDays >= 1)
    {
      int hours = t.Hours;
      return string.Format(MLSResource.DS_RuntimeDays, totalDays, hours);
    }
    return string.Format(MLSResource.DS_RuntimeHours, Math.Max(1, (int)t.TotalHours));
  }

  /// <summary>v4 path of updateMeasurementSetting - builds SettingsPatch from UI and calls SetSettingsAsync.</summary>
  private async Task updateMeasurementSettingV4()
  {
    if (!isInputsCorrect(out int thSpan, out int glbSpan, out int velSpan, out int luxSpan, out int co2Span)) return;

    bool isV4 = MLUtility.Protocol.Device.ProtocolVersion >= 1;
    var thSetting  = new SensorSettingPatch(cbx_th.IsToggled, (uint)thSpan);
    SettingsPatch patch;
    if (isV4)
    {
      // v4: 一般行 (cbx_th/ent_th) のみが意味を持ち、JsonRpcV4Protocol が t_dry を代表値として
      // wire 上の general に packing する。t_dry/humidity/t_glb/co2 を同値で構成しておく。
      patch = new SettingsPatch
      {
        DrybulbTemperature = thSetting,
        RelativeHumidity   = thSetting,
        GlobeTemperature   = thSetting,
        Co2                = thSetting,
        Velocity           = new SensorSettingPatch(cbx_vel.IsToggled, (uint)velSpan),
        Illuminance        = new SensorSettingPatch(cbx_lux.IsToggled, (uint)luxSpan),
        StartTime          = new DateTimeOffset(dpck_start.Date.Add(tpck_start.Time)),
      };
    }
    else
    {
      // v3: 6 センサ個別 (t_dry と humidity はハード上で共有のため同一 patch)
      patch = new SettingsPatch
      {
        DrybulbTemperature = thSetting,
        RelativeHumidity   = thSetting,
        GlobeTemperature   = new SensorSettingPatch(cbx_glb.IsToggled, (uint)glbSpan),
        Velocity           = new SensorSettingPatch(cbx_vel.IsToggled, (uint)velSpan),
        Illuminance        = new SensorSettingPatch(cbx_lux.IsToggled, (uint)luxSpan),
        Co2                = new SensorSettingPatch(cbx_co2.IsToggled, (uint)co2Span),
        StartTime          = new DateTimeOffset(dpck_start.Date.Add(tpck_start.Time)),
      };
    }

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

  /// <summary>
  /// protocol version に応じて UI mode を切替。
  /// - v4 (ProtocolVersion >= 1): th 行を "General" ラベルにして 1 つで温湿度+グローブ+CO2 を制御、
  ///   グローブ行・CO2 行を非表示。電池パネルを表示。
  /// - v3: 旧 5 行 (温湿度 / グローブ温度 / 風速 / 照度 / CO2) を表示 (CO2 は HasCo2Sensor 次第)、
  ///   電池パネル非表示。v3 ユーザーの体験は従来と変わらないようにする。
  /// </summary>
  private void applyProtocolModeToUI(bool isV4, bool hasCo2)
  {
    if (isV4)
    {
      lbl_th.Text       = MLSResource.DS_GeneralMeasurement;
      row_glb.IsVisible = false;
      row_co2.IsVisible = false;
      batteryPanel.IsVisible = true;
      dumpPanel.IsVisible    = true;
    }
    else
    {
      lbl_th.Text       = MLSResource.DS_TemperatureAndHumidity;
      row_glb.IsVisible = true;
      row_co2.IsVisible = hasCo2;
      batteryPanel.IsVisible = false;
      // v3 firmware は MLS_Mobile からの dump 未対応 (GetCount/Dump が unknown_command)
      dumpPanel.IsVisible    = false;
    }
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
    var popup = new TextInputPopup(MLSResource.DS_PromptCo2RefLevel,"600", Keyboard.Numeric);
    int? refLevel = await PromptCo2LevelAsync(popup);
    if (refLevel is not int level) return;
    await calibrateCo2V4(Co2CalibrationMode.Forced, level, navigateToCalibrator: true);
  }

  /// <summary>v4 path of CO2InitializeButton_Clicked (full initialization = factory reset + 12h + FRC).</summary>
  private async Task calibrateCo2V4FactoryAsync()
  {
    var popup = new TextInputPopup(MLSResource.DS_PromptCo2RefLevel,"400", Keyboard.Numeric);
    int? refLevel = await PromptCo2LevelAsync(popup);
    if (refLevel is not int level) return;
    await calibrateCo2V4(Co2CalibrationMode.Factory, level, navigateToCalibrator: false);
  }

  /// <summary>v4 path of CO2ResetButton_Clicked (Sensirion factory_reset 単独、~90ms)。</summary>
  private async Task calibrateCo2V4ResetAsync()
  {
    bool proceed = await DisplayAlert(
      MLSResource.DS_ResetCO2ConfirmTitle,
      MLSResource.DS_ResetCO2ConfirmBody,
      MLSResource.Yes,
      MLSResource.Cancel);
    if (!proceed) return;

    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      // target_ppm は reset モードでは無視されるが、API 互換のため 0 を渡す
      await MLUtility.Protocol.CalibrateCo2Async(Co2CalibrationMode.Reset, 0, cts.Token);
      MLUtility.WriteLog("[co2] factory reset OK");
      await DisplayAlert(
        MLSResource.DS_ResetCO2ConfirmTitle,
        MLSResource.DS_ResetCO2Done,
        "OK");
    }
    catch (Exception ex)
    {
      await MLUtility.ShowErrorAsync(this, MLSResource.DS_ResetCO2Failed, ex);
    }
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

  // ============================================================
  // dump (内蔵フラッシュのデータをスマホに吸い出して CSV 保存)
  // ============================================================

  // BLE 実効スループット (KB/sec)。所要時間試算用。
  // 2026/06/02 実測: 10000 records (220 KB) を 90 秒 ≈ 2.4 KB/sec。
  // 安全側で 2.2 KB/sec を採用 (実機ばらつきと先頭/末尾オーバーヘッドを考慮、
  // 表示時間が実態より若干長めに出る)。
  private const double BLE_THROUGHPUT_BYTES_PER_SEC = 2200.0;

  private async void DumpButton_Clicked(object sender, EventArgs e) => await dumpV4Async();

  private async Task dumpV4Async()
  {
    // 1) get_count で件数を確認
    DumpResult header;
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      header = await MLUtility.Protocol.GetCountAsync(cts.Token);
    }
    catch (MLProtocolException ex) when (ex.Code == MLProtocolErrorCodes.Busy)
    {
      await DisplayAlert(MLSResource.ERR_AlertTitle, MLSResource.DS_DumpStopLogging, "OK");
      return;
    }
    catch (Exception ex)
    {
      await MLUtility.ShowErrorAsync(this, MLSResource.DS_DumpFailed, ex);
      return;
    }

    if (header.RecordCount <= 0)
    {
      await DisplayAlert(MLSResource.ERR_AlertTitle, MLSResource.DS_DumpNoData, "OK");
      return;
    }

    // 2) 件数と所要時間を提示してユーザー確認
    int totalBytes = header.RecordCount * header.RecordSize;
    int etaMinutes = Math.Max(1, (int)Math.Ceiling(totalBytes / BLE_THROUGHPUT_BYTES_PER_SEC / 60.0));
    bool proceed = await DisplayAlert(
      MLSResource.DS_DumpConfirmTitle,
      string.Format(MLSResource.DS_DumpConfirmBody, header.RecordCount, etaMinutes),
      MLSResource.Yes,
      MLSResource.Cancel);
    if (!proceed) return;

    // 3) dump 実行 (画面遷移ブロック: indicator を被せて全画面を覆う)
    string fileName = $"{Logger.XBeeName}_{DateTime.Now:yyyyMMdd_HHmmss}_M.txt";
    int savedCount = 0;
    // dump 中はスクリーンスリープ抑止 (スリープ → BLE 切断 → dump 失敗 を防ぐ)
    DeviceDisplay.Current.KeepScreenOn = true;
    try
    {
      // 進捗付き indicator
      var progress = new Progress<int>(bytesReceived =>
      {
        int recv = bytesReceived / header.RecordSize;
        Application.Current?.Dispatcher.Dispatch(() =>
        {
          indicatorLabel.Text = string.Format(MLSResource.DS_DumpInProgress, recv, header.RecordCount);
        });
      });
      showIndicator(string.Format(MLSResource.DS_DumpInProgress, 0, header.RecordCount));

      // 所要時間に応じた timeout (実効 ETA + 余裕 50%、最低 60sec)
      int timeoutSec = Math.Max(60, etaMinutes * 60 * 3 / 2);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
      var result = await MLUtility.Protocol.DumpAsync(progress, cts.Token);

      // 4) binary を decode → CSV にして保存
      savedCount = await Task.Run(() => saveDumpAsCsv(result, fileName));
    }
    catch (Exception ex)
    {
      Application.Current?.Dispatcher.Dispatch(hideIndicator);
      await MLUtility.ShowErrorAsync(this, MLSResource.DS_DumpFailed, ex);
      return;
    }
    finally
    {
      DeviceDisplay.Current.KeepScreenOn = false;
      Application.Current?.Dispatcher.Dispatch(hideIndicator);
    }

    // 5) 完了通知
    await DisplayAlert(
      MLSResource.DS_DumpConfirmTitle,
      string.Format(MLSResource.DS_DumpDone, savedCount, fileName),
      "OK");
    MLUtility.WriteLog($"[dump] {savedCount} records → {fileName}");
  }

  /// <summary>dump 結果 (binary) を CSV にして DataFiles に保存。返り値は書き込んだ件数。</summary>
  private static int saveDumpAsCsv(DumpResult result, string fileName)
  {
    int count = 0;
    var sb = new StringBuilder();
    foreach (var r in DumpDecoder.Decode(result.Data, result.RecordSize))
    {
      var t = r.Timestamp.LocalDateTime;
      sb.Clear();
      sb.Append(t.ToString("yyyy/M/d,HH:mm:ss")).Append(',');
      sb.Append(FmtNA(r.DrybulbTemperature, "F1")).Append(',');
      sb.Append(FmtNA(r.RelativeHumidity,   "F1")).Append(',');
      sb.Append(FmtNA(r.GlobeTemperature,   "F2")).Append(',');
      sb.Append(FmtNA(r.Velocity,           "F3")).Append(',');
      sb.Append(FmtNA(r.Illuminance,        "F2")).Append(',');
      // DataReceive と同形式: voltage と globe_voltage は n/a 列で揃える
      sb.Append("n/a,n/a,");
      sb.Append(r.Co2Ppm.HasValue ? r.Co2Ppm.Value.ToString(CultureInfo.InvariantCulture) : "n/a").Append(',');
      // memo 列 (dump では空)
      sb.Append(Environment.NewLine);
      MLUtility.AppendData(fileName, sb.ToString());
      count++;
    }
    return count;
  }

  // ============================================================
  // clear_data (機器内蔵フラッシュの記録データを消去)
  // ============================================================
  private async void ClearDataButton_Clicked(object sender, EventArgs e) => await clearDataV4Async();

  private async Task clearDataV4Async()
  {
    bool proceed = await DisplayAlert(
      MLSResource.DS_ClearDataConfirmTitle,
      MLSResource.DS_ClearDataConfirmBody,
      MLSResource.Yes,
      MLSResource.Cancel);
    if (!proceed) return;

    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      await MLUtility.Protocol.ClearDataAsync(cts.Token);
      MLUtility.WriteLog("[clear_data] OK");
      await DisplayAlert(
        MLSResource.DS_ClearDataConfirmTitle,
        MLSResource.DS_ClearDataDone,
        "OK");
    }
    catch (Exception ex)
    {
      await MLUtility.ShowErrorAsync(this, MLSResource.DS_ClearDataFailed, ex);
    }
  }

  private static string FmtNA(double? v, string fmt)
    => v.HasValue ? v.Value.ToString(fmt, CultureInfo.InvariantCulture) : "n/a";

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

  // 「未保存」状態を示す警告色 (Resources/Styles/Colors.xaml の Status_Warn)
  private static Color UnsavedColor => (Color)Application.Current.Resources["Status_Warn"];

  private void cbx_Toggled(object sender, ToggledEventArgs e)
  {
    if (sender.Equals(cbx_th)) lbl_th.TextColor = UnsavedColor;
    else if (sender.Equals(cbx_glb)) lbl_glb.TextColor = UnsavedColor;
    else if (sender.Equals(cbx_vel)) lbl_vel.TextColor = UnsavedColor;
    else if (sender.Equals(cbx_lux)) lbl_lux.TextColor = UnsavedColor;
    else if (sender.Equals(cbx_co2)) lbl_co2.TextColor = UnsavedColor;
    updateBatteryPanel();
  }

  private void ent_TextChanged(object sender, TextChangedEventArgs e)
  {
    if (sender.Equals(ent_th)) lbl_th.TextColor = UnsavedColor;
    else if (sender.Equals(ent_glb)) lbl_glb.TextColor = UnsavedColor;
    else if (sender.Equals(ent_vel)) lbl_vel.TextColor = UnsavedColor;
    else if (sender.Equals(ent_lux)) lbl_lux.TextColor = UnsavedColor;
    else if (sender.Equals(ent_co2)) lbl_co2.TextColor = UnsavedColor;
    updateBatteryPanel();
  }

  private void dpck_start_DateSelected(object sender, DateChangedEventArgs e)
  {
    //日付変更がなければ終了 (Logger 側は秒まで持つので .Date で比較)
    if (Logger == null || dpck_start.Date == Logger.StartMeasuringDateTime.Date) return;

    lbl_stdtime.TextColor = UnsavedColor;
  }

  private void tpck_start_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    //Time プロパティ以外の変更 (IsVisible, Layout 等) では走らせない
    if (e.PropertyName != nameof(TimePicker.Time)) return;
    //時刻変更がなければ終了
    if (tpck_start == null || Logger == null || tpck_start.Time == Logger.StartMeasuringDateTime.TimeOfDay) return;

    lbl_stdtime.TextColor = UnsavedColor;
  }

  private void resetTextColor()
  {
    lbl_th.TextColor =
      lbl_glb.TextColor =
      lbl_vel.TextColor =
      lbl_lux.TextColor =
      lbl_co2.TextColor =
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

  private async void TapGestureRecognizer_Other_Tapped(object sender, TappedEventArgs e)
  {
    var popup = new DescriptionPopup(DescriptionText.OtherSetting);
    var result = await this.ShowPopupAsync(popup);
  }

  private async void TapGestureRecognizer_Battery_Tapped(object sender, TappedEventArgs e)
  {
    var popup = new DescriptionPopup(DescriptionText.BatteryInfo);
    var result = await this.ShowPopupAsync(popup);
  }

  private async void TapGestureRecognizer_Data_Tapped(object sender, TappedEventArgs e)
  {
    var popup = new DescriptionPopup(DescriptionText.DataManagement);
    var result = await this.ShowPopupAsync(popup);
  }

  #endregion

}