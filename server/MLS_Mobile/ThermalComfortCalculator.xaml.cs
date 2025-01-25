namespace MLS_Mobile;

using CommunityToolkit.Maui.Views;
using MLS_Mobile.Resources.i18n;
using Popolo.HumanBody;

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

  #endregion

  #region コンストラクタ

  public ThermalComfortCalculator()
  {
    InitializeComponent();

    BindingContext = this;
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

    double pmv = ThermalComfort.GetPMV(dbt, mrt, hmd, vel, clo, met, 0);
    double ppd = ThermalComfort.GetPPD(pmv);
    double setstar = TwoNodeModel.GetSETStarFromAmbientCondition(dbt, mrt, hmd, vel, clo, 58 * met, 0);

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
    if (await this.ShowPopupAsync(popup) != null)
      if (double.TryParse(popup.EntryValue, out double val))
        sld.Value = Math.Min(sld.Maximum, Math.Max(sld.Minimum, val));
  }

  #endregion

}