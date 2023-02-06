using MLLib;
using MLS_Mobile.Resources.i18n;
using System.Text;

namespace MLS_Mobile;

public partial class Calibrator : ContentPage
{

  #region �萔�E�C���X�^���X�ϐ��E�v���p�e�B��`

  /// <summary>���������Z������[����]</summary>
  private readonly int[] VEL_CAL_TIMES = new int[] { 3, 5, 10, 30, 60, 180 };

  /// <summary>���x�����Z������[����]</summary>
	private readonly int[] TMP_CAL_TIMES = new int[] { 1, 3, 6, 12, 24 };

  /// <summary>�d���蓮�Z�������ۂ�</summary>
  private bool calibratingVoltage = false;

  /// <summary>�����d���̏����M���ۂ�</summary>
  private bool isFirstVoltageMessage = true;

  /// <summary>�����d���Z���p�J�E���g�_�E��</summary>
  private int countDownTime = 30;

  #endregion

  #region �R���X�g���N�^

  public Calibrator()
  {
    InitializeComponent();

    //���������Z�����ԃ��X�g�̍쐬
    List<string> velCalItems = new List<string>();
    for (int i = 0; i < VEL_CAL_TIMES.Length; i++)
      velCalItems.Add(VEL_CAL_TIMES[i] + " " + MLSResource.Minute);
    velPicker.ItemsSource = velCalItems;
    velPicker.SelectedIndex = 0;

    //���x�����Z�����ԃ��X�g�̍쐬
    List<string> tmpCalItems = new List<string>();
    for (int i = 0; i < TMP_CAL_TIMES.Length; i++)
      tmpCalItems.Add(TMP_CAL_TIMES[i] + " " + MLSResource.Hour);
    tmpPicker.ItemsSource = tmpCalItems;
    tmpPicker.SelectedIndex = 0;

    Task.Run(async () =>
    {
      while (true)
      {
        if (calibratingVoltage && 0 < countDownTime)
        {
          countDownTime--;
          Application.Current.Dispatcher.Dispatch(() =>
          {
            cdownLabel.Text = countDownTime.ToString();
            cdownLabel.TextColor = countDownTime == 0 ? Colors.ForestGreen : Colors.Red;
          });          
        }        
        await Task.Delay(1000);
      }
    });
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g

  protected override void OnAppearing()
  {
    base.OnAppearing();

    MLUtility.Logger.CalibratingVoltageReceivedEvent += Logger_CalibratingVoltageReceivedEvent;
    MLUtility.Logger.EndCalibratingVoltageMessageReceivedEvent += Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    MLUtility.Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
    MLUtility.Logger.EndCalibratingVoltageMessageReceivedEvent -= Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  private void Logger_CalibratingVoltageReceivedEvent(object sender, EventArgs e)
  {
    //�����M���ɃR���g���[����\�����ăJ�E���g�_�E�����n�߂�
    if (isFirstVoltageMessage)
    {
      calibratingVoltage = true;
      isFirstVoltageMessage = false;
      countDownTime = 30;

      Application.Current.Dispatcher.Dispatch(() =>
      {
        cdownLabel.TextColor = Colors.Red;
        velLabel.IsVisible = true;
        cdownLabel.IsVisible = true;
      });
    }

    //�d���\�����X�V
    Application.Current.Dispatcher.Dispatch(() =>
    {
      velLabel.Text = MLUtility.Logger.VelocityVoltage.ToString("F3");
    });
  }

  private void Logger_EndCalibratingVoltageMessageReceivedEvent(object sender, EventArgs e)
  {
    calibratingVoltage = false;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      velLabel.IsVisible = false;
      cdownLabel.IsVisible = false;
    });
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  /// <summary>�␳�W���ݒ�{�^���N���b�N���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void SetCF_Clicked(object sender, EventArgs e)
  {
    //�C���W�P�[�^�\��
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        MLUtility.Logger.HasCorrectionFactorsReceived = false;
        while (!MLUtility.Logger.HasCorrectionFactorsReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand()));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(CFSetting));
        });
      }
      catch { }
      finally
      {
        //�C���W�P�[�^���B��
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  /// <summary>���x�����Z���{�^���N���b�N���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void AutoCBT_Clicked(object sender, EventArgs e)
  {
    //�C���W�P�[�^�\��
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        MLUtility.Logger.HasTemperatureAutoCalibrationReceived = false;
        while (!MLUtility.Logger.HasTemperatureAutoCalibrationReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          int sec = TMP_CAL_TIMES[tmpPicker.SelectedIndex] * 3600;
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoTemperatureCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync("../..");
        });
      }
      catch { }
      finally
      {
        //�C���W�P�[�^���B��
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  /// <summary>���������Z���{�^���N���b�N���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void AutoCBV_Clicked(object sender, EventArgs e)
  {
    //�C���W�P�[�^�\��
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        MLUtility.Logger.HasVelocityAutoCalibrationReceived = false;
        while (!MLUtility.Logger.HasVelocityAutoCalibrationReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          int sec = VEL_CAL_TIMES[velPicker.SelectedIndex] * 60;
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoVelocityCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync("../..");
        });
      }
      catch { }
      finally
      {
        //�C���W�P�[�^���B��
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  /// <summary>�����d���蓮�Z���{�^���N���b�N���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void CBV_Clicked(object sender, EventArgs e)
  {
    //�C���W�P�[�^�\��
    showIndicator(MLSResource.CR_Connecting);

    isFirstVoltageMessage = true;

    Task.Run(async () =>
    {
      try
      {
        //�Z�����Ȃ�Β�~
        if (calibratingVoltage)
        {
          MLUtility.Logger.HasEndCalibratingVoltageMessageReceived = false;
          while (!MLUtility.Logger.HasEndCalibratingVoltageMessageReceived)
          {
            //5�񎸔s������G���[�\��
            int tryNum = 0;
            if (5 <= tryNum)
            {
              Application.Current.Dispatcher.Dispatch(() =>
              {
                DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
                return;
              });
            }
            tryNum++;

            //�J�n�R�}���h�𑗐M
            MLUtility.LoggerSideXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
        //��Z�����Ȃ�ΊJ�n
        else 
        {
          MLUtility.Logger.HasStartCalibratingVoltageMessageReceived = false;
          while (!MLUtility.Logger.HasStartCalibratingVoltageMessageReceived)
          {
            //5�񎸔s������G���[�\��
            int tryNum = 0;
            if (5 <= tryNum)
            {
              Application.Current.Dispatcher.Dispatch(() =>
              {
                DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
                return;
              });
            }
            tryNum++;

            //�J�n�R�}���h�𑗐M
            MLUtility.LoggerSideXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeStartCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
      }
      catch { }
      finally
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //�C���W�P�[�^���B��
          hideIndicator();

          //�R���g���[���̕\�����X�V
          voltageBtn.Text = calibratingVoltage ? MLSResource.CR_EndCalibration : MLSResource.CR_CalibrateVelocityVoltage;
          velPicker.IsEnabled = tmpPicker.IsEnabled = autoTmpBtn.IsEnabled = autoVelBtn.IsEnabled = corBtn.IsEnabled = !calibratingVoltage;
        });
      }
    });
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