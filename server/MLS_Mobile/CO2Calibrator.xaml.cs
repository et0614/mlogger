using MLLib;
using MLS_Mobile.Resources.i18n;
using static MLLib.MLogger;

namespace MLS_Mobile;


[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class CO2Calibrator : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>�ʐM����MLogger���擾����</summary>
  public MLogger Logger { get { return MLUtility.GetLogger(_mlLowAddress); } }

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
        ml.CalibratingCO2LevelReceivedEvent -= Ml_CalibratingCO2LevelReceivedEvent;

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.CalibratingCO2LevelReceivedEvent += Ml_CalibratingCO2LevelReceivedEvent;
    }
  }

  private void Ml_CalibratingCO2LevelReceivedEvent(object sender, EventArgs e)
  {
    CalibratingCO2SensorLevelEventArgs ceArgs = e as CalibratingCO2SensorLevelEventArgs;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      lblTime.Text = ceArgs.RemainingTime.ToString("F0");

      if (ceArgs.RemainingTime == 0)
      {
        if (ceArgs.CalibrationSucceeded)
          lblRslt.Text = "The calibration was successfully completed.\r\nThe correction value was" + ceArgs.CorrectionCO2Level.ToString("F0") + " ppm.";
        else
          lblRslt.Text = "The calibration process ended in failure.";
      }
    });    
  }

  #endregion

  public CO2Calibrator()
	{
		InitializeComponent();

    //�߂�{�^���őJ�ڂ���ꍇ�̏���
    Shell.Current.Navigated += Current_Navigated;
  }

  private void Current_Navigated(object sender, ShellNavigatedEventArgs e)
  {
    if (e.Source == ShellNavigationSource.Pop)
    {
      Logger.CalibratingCO2LevelReceivedEvent -= Ml_CalibratingCO2LevelReceivedEvent;
      Shell.Current.Navigated -= Current_Navigated; //�C�x���g����
    }
  }

}