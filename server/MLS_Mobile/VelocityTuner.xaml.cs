using XBeeLibrary.Core.IO;

namespace MLS_Mobile;

public partial class VelocityTuner : ContentPage
{
  private bool countDownStarted = false;

  private int countDownTime { get; set; } = 30;

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
    MLUtility.Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //スリープ解除
    DeviceDisplay.Current.KeepScreenOn = false;

    //MLoggerイベント解除
    MLUtility.Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  #endregion

  #region 通信処理

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    countDownStarted = true;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      velLabel.Text = MLUtility.Logger.VelocityVoltage.ToString("F3");
    });
  }

  #endregion

}