namespace MLS_Mobile;

using System.Collections.ObjectModel;
using Popolo.HumanBody;
using MLS_Mobile.Resources.i18n;

using Microsoft.Maui.Devices.Sensors;

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

    if (Accelerometer.Default.IsSupported)
    {
      if (!Accelerometer.Default.IsMonitoring)
      {
        Accelerometer.Default.ShakeDetected += Accelerometer_ShakeDetected;
        Accelerometer.Default.Start(SensorSpeed.Game);
      }
    }
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    if (Accelerometer.Default.IsSupported)
    {
      if (Accelerometer.Default.IsMonitoring)
      {
        Accelerometer.Default.Stop();
        Accelerometer.Default.ShakeDetected -= Accelerometer_ShakeDetected;
      }
    }
  }

  #endregion

  #region コントロール操作時の処理

  private void Accelerometer_ShakeDetected(object sender, EventArgs e)
  {
    if (MLUtility.SDCardEnabled)
    {
      MLUtility.SDCardEnabled = false;
      DisplayAlert("", "Debug mode disabled", "Yes");
    }
    else
    {
      MLUtility.SDCardEnabled = true;
      DisplayAlert("", "Debug mode enabled", "Yes");
    }
  }

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

  #endregion

}