namespace MLS_Mobile;

using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using MLLib;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class DataReceive : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>��ʃA�h���X</summary>
  private string _mlLowAddress = "";

  /// <summary>��ʃA�h���X��ݒ�E�擾����</summary>
  public string MLoggerLowAddress
  {
    get
    {
      return _mlLowAddress;
    }
    set
    {
      //�o�^�ς̏ꍇ�ɂ̓C�x���g������
      MLogger ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
      {
        MLVModel.Logger = null;
        ml.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
      }

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);

      this.Title = ml.LocalName;

      //Clo�l,��ӗʏ�����
      CloValue = ml.CloValue;
      MetValue = ml.MetValue;

      //MLogger�C�x���g�o�^
      ml.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;

      //Bind
      MLVModel.Logger = ml;
    }
  }

  /// <summary>�f�[�^����M����MLogger��ݒ�E�擾����</summary>
  public MLogger Logger
  {
    get
    {
      return MLUtility.GetLogger(_mlLowAddress);
    }
  }

  public MLoggerViewModel MLVModel { get; set; } = new MLoggerViewModel();

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

    BindingContext = MLVModel;

    cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
    metTitle.Text = MLSResource.MetabolicUnit + " [met]";  
  }

  /// <summary>�f�R���X�g���N�^</summary>
  ~DataReceive()
  {
    if (Logger != null)
      Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
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
    if (Logger != null)
    {
      Logger.MetValue = metSlider.Value;
      Logger.CloValue = cloSlider.Value;
    }    
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
    //�f�[�^��ۑ�
    string line =
      Logger.LastMeasured.ToString("yyyy/M/d,HH:mm:ss") + "," +
      Logger.DrybulbTemperature.LastValue.ToString("F1") + "," +
      Logger.RelativeHumdity.LastValue.ToString("F1") + "," +
      Logger.GlobeTemperature.LastValue.ToString("F2") + "," +
      Logger.Velocity.LastValue.ToString("F3") + "," +
      Logger.Illuminance.LastValue.ToString("F2") + "," +
      Logger.GlobeTemperatureVoltage.ToString("F3") + "," +
      Logger.VelocityVoltage.ToString("F3") + Environment.NewLine;

    string fileName = Logger.LocalName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
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