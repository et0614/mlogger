using Microsoft.Extensions.Logging;
using MLLib;

namespace MLS_Mobile;

[QueryProperty(nameof(Logger), "mLogger")]
public partial class VelocityTuner : ContentPage
{
  private bool countDownStarted = false;

  private int countDownTime { get; set; } = 30;

  //データを受信するMLogger
  private MLogger _mLogger;

  /// <summary>データを受信するMLoggerを設定・取得する</summary>
  public MLogger Logger
  {
    get
    {
      return _mLogger;
    }
    set
    {
      _mLogger = value;
      //initInfo();
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