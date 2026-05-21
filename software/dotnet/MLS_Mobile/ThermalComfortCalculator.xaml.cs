namespace MLS_Mobile;

using System.ComponentModel;
using CommunityToolkit.Maui.Extensions;
using MLS_Mobile.Resources.i18n;
using MLS_Mobile.Services;
using MLS_Mobile.ViewModels;
using Popolo.Core.ThermalComfort;

[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class ThermalComfortCalculator : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  /// <summary>Clo値を設定・取得する</summary>
  public double CloValue
  { get; set; } = 1.2;

  /// <summary>Met値を設定・取得する</summary>
  public double MetValue
  { get; set; } = 1.1;

  /// <summary>共有計測値モデル (DataReceive 経由で値が流れてくる)</summary>
  private readonly ILiveMeasurementService? _live;

  #endregion

  #region コンストラクタ

  public ThermalComfortCalculator()
  {
    InitializeComponent();

    BindingContext = this;

    // ライブ計測連携: 接続状態をトグルの可用性に反映、IsConnected/DeviceName の変化と
    // 各計測値の変化を購読する。Page インスタンスは Shell が保持するため event handler
    // leak は実用上発生しない (Service は singleton、Page は 1 つ)。
    _live = IPlatformApplication.Current?.Services.GetService(typeof(ILiveMeasurementService))
            as ILiveMeasurementService;
    if (_live != null) _live.PropertyChanged += OnLivePropertyChanged;
    RefreshLiveAvailability();
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //着衣量と代謝量を反映
    cloSlider.Value = CloValue;
    metSlider.Value = MetValue;

    //更新
    updateIndices();

    //シェイク検知Off
    /*if (Accelerometer.Default.IsSupported)
    {
      if (!Accelerometer.Default.IsMonitoring)
      {
        Accelerometer.Default.ShakeDetected += Accelerometer_ShakeDetected;
        Accelerometer.Default.Start(SensorSpeed.Game);
      }
    }*/
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    /*if (Accelerometer.Default.IsSupported)
    {
      if (Accelerometer.Default.IsMonitoring)
      {
        Accelerometer.Default.Stop();
        Accelerometer.Default.ShakeDetected -= Accelerometer_ShakeDetected;
      }
    }*/
  }

  #endregion

  #region コントロール操作時の処理

  private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
  {
    updateIndices();
  }

  private void updateIndices()
  {
    double dbt = dbtSlider.Value;
    double hmd = hmdSlider.Value;
    double mrt = mrtSlider.Value;
    double vel = velSlider.Value;
    double clo = cloSlider.Value;
    double met = metSlider.Value;

    double pmv = FangerModel.GetPMV(dbt, mrt, hmd, vel, clo, met, 0);
    double ppd = FangerModel.GetPPD(pmv);
    double setstar = GaggeModel.GetSETStarFromAmbientCondition(dbt, mrt, hmd, vel, clo, 58 * met, 0);

    lblPMV.Text = pmv.ToString("F2");
    lblPPD.Text = ppd.ToString("F1");
    lblSET.Text = setstar.ToString("F2");
  }

  //着衣量設定ボタンクリック時の処理
  private void CloBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ClothingCoordinator));
  }

  //活動量設定ボタンクリック時の処理
  private void ActBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ActivitySelector));
  }

  /// <summary>ラベルタップ時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private async void Value_Tapped(object sender, TappedEventArgs e)
  {
    Label target = (Label)sender;
    Slider sld = null;
    TextInputPopup popup = null;
    if (target == dbtLabel)
    {
      sld = dbtSlider;
      popup = new TextInputPopup(MLSResource.DrybulbTemperature, sld.Value.ToString("F1"), Keyboard.Numeric);
    }
    else if (target == hmdLabel)
    {
      sld = hmdSlider;
      popup = new TextInputPopup(MLSResource.RelativeHumidity, sld.Value.ToString("F1"), Keyboard.Numeric);
    }
    else if (target == mrtLabel)
    {
      sld = mrtSlider;
      popup = new TextInputPopup(MLSResource.MeanRadiantTemperature, sld.Value.ToString("F1"), Keyboard.Numeric);
    }
    else if (target == velLabel)
    {
      sld = velSlider;
      popup = new TextInputPopup(MLSResource.Velocity, sld.Value.ToString("F2"), Keyboard.Numeric);
    }
    else if (target == cloLabel)
    {
      sld = cloSlider;
      popup = new TextInputPopup(MLSResource.ClothingUnit, sld.Value.ToString("F2"), Keyboard.Numeric);
    }
    else if (target == metLabel)
    {
      sld = metSlider;
      popup = new TextInputPopup(MLSResource.MetabolicUnit, sld.Value.ToString("F2"), Keyboard.Numeric);
    }

    if (popup == null) return;
    if (await this.ShowPopupAsync<string>(popup) != null)
      if (double.TryParse(popup.EntryValue, out double val))
        sld.Value = Math.Min(sld.Maximum, Math.Max(sld.Minimum, val));
  }

  #endregion

  #region ライブ計測連携 (Live モード)

  /// <summary>Live トグルが ON のとき、共有モデルから値を流し込んでいる状態か。</summary>
  private bool _liveActive = false;

  /// <summary>XAML の Switch.Toggled ハンドラ。</summary>
  private void LiveToggle_Toggled(object? sender, ToggledEventArgs e)
  {
    if (e.Value) EnableLiveMode();
    else         DisableLiveMode();
  }

  /// <summary>接続状態の変化に応じてトグルの有効/無効と表示文言を更新。</summary>
  private void RefreshLiveAvailability()
  {
    bool available = _live is { IsConnected: true };
    liveToggle.IsEnabled = available;
    liveStatusLabel.Text = available
        ? (_live!.DeviceName is string n ? $"Live ({n})" : "Live")
        : "Live (no device connected)";

    // 接続が切れた瞬間にライブモードが ON のままだと値が古いまま固定されるので強制 OFF
    if (!available && liveToggle.IsToggled) liveToggle.IsToggled = false;
  }

  private void EnableLiveMode()
  {
    if (_live == null) return;
    _liveActive = true;
    ApplyLiveValuesToSliders();
    // 派生値である MRT も含めてユーザー入力をロック (Live が流す)
    dbtSlider.IsEnabled = false;
    hmdSlider.IsEnabled = false;
    velSlider.IsEnabled = false;
    mrtSlider.IsEnabled = false;
  }

  private void DisableLiveMode()
  {
    _liveActive = false;
    dbtSlider.IsEnabled = true;
    hmdSlider.IsEnabled = true;
    velSlider.IsEnabled = true;
    mrtSlider.IsEnabled = true;
  }

  private void OnLivePropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    // IsConnected / DeviceName の変化はトグル状態に反映
    if (e.PropertyName == nameof(ILiveMeasurementService.IsConnected)
     || e.PropertyName == nameof(ILiveMeasurementService.DeviceName))
    {
      Dispatcher.Dispatch(RefreshLiveAvailability);
      return;
    }
    // 値変化はライブモード ON のときだけスライダに反映
    if (_liveActive) Dispatcher.Dispatch(ApplyLiveValuesToSliders);
  }

  private void ApplyLiveValuesToSliders()
  {
    if (_live == null || !_liveActive) return;

    if (_live.DryBulbTemperature is double dbt)
      dbtSlider.Value = Math.Clamp(dbt, dbtSlider.Minimum, dbtSlider.Maximum);
    if (_live.RelativeHumidity is double rh)
      hmdSlider.Value = Math.Clamp(rh, hmdSlider.Minimum, hmdSlider.Maximum);
    if (_live.Velocity is double vel)
      velSlider.Value = Math.Clamp(vel, velSlider.Minimum, velSlider.Maximum);

    // 平均放射温度 = Globe / DryBulb / Velocity から派生
    // (DataReceiveViewModel.GetMRT と同じピンポン球補正付きロジック)
    if (_live.GlobeTemperature is double glb
        && _live.DryBulbTemperature is double dbtForMrt
        && _live.Velocity is double velForMrt)
    {
      double mrt = DataReceiveViewModel.GetMRT(dbtForMrt, glb, velForMrt);
      mrtSlider.Value = Math.Clamp(mrt, mrtSlider.Minimum, mrtSlider.Maximum);
    }
  }

  #endregion

}