using MLLib;
using MLS_Mobile.Resources.i18n;
using static MLLib.MLogger;

namespace MLS_Mobile;


[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class CO2Calibrator : ContentPage
{

  #region インスタンス変数・プロパティ

  /// <summary>通信するMLoggerを取得する</summary>
  public MLogger Logger { get { return MLUtility.GetLogger(_mlLowAddress); } }

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

    //戻るボタンで遷移する場合の処理
    Shell.Current.Navigated += Current_Navigated;
  }

  private void Current_Navigated(object sender, ShellNavigatedEventArgs e)
  {
    if (e.Source == ShellNavigationSource.Pop)
    {
      Logger.CalibratingCO2LevelReceivedEvent -= Ml_CalibratingCO2LevelReceivedEvent;
      Shell.Current.Navigated -= Current_Navigated; //イベント解除
    }
  }

}