namespace MLS_Mobile;

using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;

[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class DataReceive : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>Clo�l��ݒ�E�擾����</summary>
  public double CloValue
  { get; set; } = 1.2;

  /// <summary>Met�l��ݒ�E�擾����</summary>
  public double MetValue
  { get; set; } = 1.1;

  #endregion

  #region �R���X�g���N�^

  public DataReceive()
  {
    InitializeComponent();

    BindingContext = this;

    cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
    metTitle.Text = MLSResource.MetabolicUnit + " [met]";

    this.Title = MLUtility.Logger.LocalName;

    //Clo�l,��ӗʏ�����
    CloValue = MLUtility.Logger.CloValue;
    MetValue = MLUtility.Logger.MetValue;

    //MLogger�C�x���g�o�^
    MLUtility.Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  /// <summary>�f�X�g���N�^</summary>
  ~DataReceive()
  {
    //MLogger�C�x���g����
    MLUtility.Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  //���ߗʐݒ�{�^���N���b�N���̏���
  private void CloBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ClothingCoordinator));
  }

  private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
  {
    MLUtility.Logger.MetValue = metSlider.Value;
    MLUtility.Logger.CloValue = cloSlider.Value;
  }

  //�����ʐݒ�{�^���N���b�N���̏���
  private void ActBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ActivitySelector));
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g
  protected override void OnAppearing()
  {
    base.OnAppearing();

    //�X���[�v�֎~
    DeviceDisplay.Current.KeepScreenOn = true;

    //���ߗʂƑ�ӗʂ𔽉f
    cloSlider.Value = CloValue;
    metSlider.Value = MetValue;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�X���[�v����
    DeviceDisplay.Current.KeepScreenOn = false;
  }

  #endregion

  #region �ʐM����

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      val_tmp.Text = MLUtility.Logger.DrybulbTemperature.LastValue.ToString("F1");
      val_hmd.Text = MLUtility.Logger.RelativeHumdity.LastValue.ToString("F1");
      val_glb.Text = MLUtility.Logger.GlobeTemperature.LastValue.ToString("F1");
      val_vel.Text = MLUtility.Logger.Velocity.LastValue.ToString("F2");
      val_lux.Text = MLUtility.Logger.Illuminance.LastValue.ToString("F1");
      val_mrt.Text = MLUtility.Logger.MeanRadiantTemperature.ToString("F1");
      val_pmv.Text = MLUtility.Logger.PMV.ToString("F2");
      val_ppd.Text = MLUtility.Logger.PPD.ToString("F1");
      val_set.Text = MLUtility.Logger.SETStar.ToString("F1");

      dtm_tmp.Text = MLUtility.Logger.DrybulbTemperature.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_hmd.Text = MLUtility.Logger.RelativeHumdity.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_glb.Text = MLUtility.Logger.GlobeTemperature.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_vel.Text = MLUtility.Logger.Velocity.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_lux.Text = MLUtility.Logger.Illuminance.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
    });

    //�f�[�^��ۑ�
    string line =
      MLUtility.Logger.LastMeasured.ToString("yyyy/M/d,HH:mm:ss") + "," +
      MLUtility.Logger.DrybulbTemperature.LastValue.ToString("F1") + "," +
      MLUtility.Logger.RelativeHumdity.LastValue.ToString("F1") + "," +
      MLUtility.Logger.GlobeTemperature.LastValue.ToString("F2") + "," +
      MLUtility.Logger.Velocity.LastValue.ToString("F3") + "," +
      MLUtility.Logger.Illuminance.LastValue.ToString("F2") + "," +
      MLUtility.Logger.GlobeTemperatureVoltage.ToString("F3") + "," +
      MLUtility.Logger.VelocityVoltage.ToString("F3") + Environment.NewLine;

    string fileName = MLUtility.Logger.LocalName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
    MLUtility.AppendData(fileName, line);
  }

  #endregion

  #region �C���W�P�[�^�̑���

  /// <summary>�C���W�P�[�^��\������</summary>
  private void showIndicator(string message)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });
  }

  /// <summary>�C���W�P�[�^���B��</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });
  }

  #endregion

}