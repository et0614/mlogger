namespace MLS_Mobile;

using CommunityToolkit.Maui.Views;
using MLS_Mobile.Resources.i18n;
using Popolo.HumanBody;

[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class ThermalComfortCalculator : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

  /// <summary>Clo�l��ݒ�E�擾����</summary>
  public double CloValue
  { get; set; } = 1.2;

  /// <summary>Met�l��ݒ�E�擾����</summary>
  public double MetValue
  { get; set; } = 1.1;

  #endregion

  #region �R���X�g���N�^

  public ThermalComfortCalculator()
  {
    InitializeComponent();

    BindingContext = this;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //���ߗʂƑ�ӗʂ𔽉f
    cloSlider.Value = CloValue;
    metSlider.Value = MetValue;

    //�X�V
    updateIndices();

    //�V�F�C�N���mOff
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

  #region �R���g���[�����쎞�̏���

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

  //���ߗʐݒ�{�^���N���b�N���̏���
  private void CloBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ClothingCoordinator));
  }

  //�����ʐݒ�{�^���N���b�N���̏���
  private void ActBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ActivitySelector));
  }

  /// <summary>���x���^�b�v���̏���</summary>
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