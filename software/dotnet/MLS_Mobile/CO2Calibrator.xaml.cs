using MLLib;
using MLLib.Protocol;
using MLS_Mobile.Resources.i18n;
using static MLLib.MLogger;

namespace MLS_Mobile;


[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class CO2Calibrator : ContentPage
{

  #region インスタンス変数・プロパティ

  /// <summary>通信するMLoggerを取得する</summary>
  public MLogger Logger { get { return MLUtility.GetLogger(_mlLowAddress); } }

  /// <summary>v4 Co2CalibrationUpdates subscription (null on v3).</summary>
  private IDisposable _v4Sub;

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
      _mlLowAddress = value;
      _v4Sub?.Dispose();
      _v4Sub = null;
      if (MLUtility.Protocol != null)
      {
        _v4Sub = System.ObservableExtensions.Subscribe(MLUtility.Protocol.Co2CalibrationUpdates, OnV4Progress);
      }
    }
  }

  /// <summary>v4 progress handler - maps Co2CalibrationProgress to the existing UI labels.</summary>
  private void OnV4Progress(Co2CalibrationProgress p)
  {
    Application.Current?.Dispatcher.Dispatch(() =>
    {
      lblTime.Text = ((int)p.Remaining.TotalSeconds).ToString();
      lblPPM.Text  = p.CurrentPpm.ToString();

      if (p.State == Co2CalibrationState.Pass)
        lblRslt.Text = "The calibration was successfully completed.\r\nThe correction value was " + p.CorrectionPpm.ToString() + " ppm.";
      else if (p.State == Co2CalibrationState.Fail)
        lblRslt.Text = "The calibration process ended in failure.";
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
      _v4Sub?.Dispose();
      _v4Sub = null;
      Shell.Current.Navigated -= Current_Navigated; //イベント解除
    }
  }

}