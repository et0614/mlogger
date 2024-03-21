using Microsoft.Extensions.Logging;
using MLLib;

namespace MLS_Mobile;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class VelocityTuner : ContentPage
{
  private bool countDownStarted = false;

  private int countDownTime { get; set; } = 30;

  /// <summary>データを受信するMLoggerを取得する</summary>
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
      _mlLowAddress = value;
    }
  }

  public VelocityTuner()
	{
		InitializeComponent();

    BindingContext = this;

    Task.Run(async () =>
    {
      while (true)
      {
        if (countDownStarted)
        {
          countDownTime--;

          Application.Current.Dispatcher.Dispatch(() =>
          {
            cdownLabel.Text = countDownTime.ToString();
          });

          if (countDownTime <= 0)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              cdownLabel.TextColor = Colors.ForestGreen; //この記述方法、よろしくない。
            });            
            return;
          }
        }
        await Task.Delay(1000);
      }
    });
  }

  #region ロード・アンロードイベント
  protected override void OnAppearing()
  {
    base.OnAppearing();

    //スリープ禁止
    DeviceDisplay.Current.KeepScreenOn = true;

    //MLoggerイベント登録
    Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //スリープ解除
    DeviceDisplay.Current.KeepScreenOn = false;

    //MLoggerイベント解除
    Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  #endregion

  #region 通信処理

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    countDownStarted = true;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      velLabel.Text = Logger.VelocityVoltage.ToString("F3");
    });
  }

  #endregion

}