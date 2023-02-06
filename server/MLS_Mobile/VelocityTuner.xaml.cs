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
              cdownLabel.TextColor = Colors.ForestGreen; //���̋L�q���@�A��낵���Ȃ��B
            });            
            return;
          }
        }
        await Task.Delay(1000);
      }
    });
  }

  #region ���[�h�E�A�����[�h�C�x���g
  protected override void OnAppearing()
  {
    base.OnAppearing();

    //�X���[�v�֎~
    DeviceDisplay.Current.KeepScreenOn = true;

    //MLogger�C�x���g�o�^
    MLUtility.Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�X���[�v����
    DeviceDisplay.Current.KeepScreenOn = false;

    //MLogger�C�x���g����
    MLUtility.Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  #endregion

  #region �ʐM����

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