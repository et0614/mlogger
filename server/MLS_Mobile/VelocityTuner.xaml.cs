using Microsoft.Extensions.Logging;
using MLLib;

namespace MLS_Mobile;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class VelocityTuner : ContentPage
{
  private bool countDownStarted = false;

  private int countDownTime { get; set; } = 30;

  /// <summary>�f�[�^����M����MLogger���擾����</summary>
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
    Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�X���[�v����
    DeviceDisplay.Current.KeepScreenOn = false;

    //MLogger�C�x���g����
    Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  #endregion

  #region �ʐM����

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