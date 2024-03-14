using DigiIoT.Maui.Devices.XBee;
using MLLib;
using MLS_Mobile.Resources.i18n;
using System.Text;

namespace MLS_Mobile;

[QueryProperty(nameof(Logger), "mLogger")]
[QueryProperty(nameof(ConnectedXBee), "xbee")]
public partial class Calibrator : ContentPage
{

  #region �萔�E�C���X�^���X�ϐ��E�v���p�e�B��`

  /// <summary>���ω����鎞��[sec]</summary>
  private const int AVE_TIME = 10;

  /// <summary>���������Z������[����]</summary>
  private readonly int[] VEL_CAL_TIMES = new int[] { 3, 5, 10, 30, 60, 180 };

  /// <summary>���x�����Z������[����]</summary>
	private readonly int[] TMP_CAL_TIMES = new int[] { 1, 3, 6, 12, 24 };

  /// <summary>�d���蓮�Z�������ۂ�</summary>
  private bool calibratingVoltage = false;

  /// <summary>�����d���̏����M���ۂ�</summary>
  private bool isFirstVoltageMessage = true;

  /// <summary>�����d���Z���p�J�E���g�_�E��</summary>
  private int countDownTime = 0;

  /// <summary>�����d�����X�g[V]</summary>
  private double[] velVols = new double[AVE_TIME];

  /// <summary>�R�}���h���M�p��XBee��ݒ�E�擾����</summary>
  public XBeeBLEDevice ConnectedXBee { get; set; }

  //�f�[�^����M����MLogger
  private MLogger _mLogger;

  /// <summary>�f�[�^����M����MLogger��ݒ�E�擾����</summary>
  public MLogger Logger
  {
    get
    {
      return _mLogger;
    }
    set
    {
      _mLogger = value;
    }
  }

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
        if (calibratingVoltage)
        {
          countDownTime++;
          Application.Current.Dispatcher.Dispatch(() =>
          {
            cdownLabel.Text = countDownTime.ToString();
            cdownLabel.TextColor = countDownTime < 30 ? Colors.Red : Colors.ForestGreen;
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

    Logger.CalibratingVoltageReceivedEvent += Logger_CalibratingVoltageReceivedEvent;
    Logger.EndCalibratingVoltageMessageReceivedEvent += Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�����d���Z�����Ȃ�ΏI���R�}���h�𑗐M
    if (calibratingVoltage)
      Task.Run(() =>
      {
        ConnectedXBee.SendSerialData
        (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));
      });

    Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
    Logger.EndCalibratingVoltageMessageReceivedEvent -= Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  private void Logger_CalibratingVoltageReceivedEvent(object sender, EventArgs e)
  {
    //�����M���ɃR���g���[����\�����ăJ�E���g�_�E�����n�߂�
    if (isFirstVoltageMessage)
    {
      calibratingVoltage = true;
      isFirstVoltageMessage = false;
      countDownTime = 0;

      //�������X�g������
      for(int i=0;i<AVE_TIME;i++)
        velVols[i] = Logger.VelocityVoltage;

      Application.Current.Dispatcher.Dispatch(() =>
      {
        cdownLabel.TextColor = Colors.Red;
        velVLabel.IsVisible = true;
        velLabel.IsVisible = true;
        aveVelVLabel.IsVisible = true;
        aveVelLabel.IsVisible = true;
        cdownLabel.IsVisible = true;
      });
    }

    //���ϕ����̌v�Z[
    double aveVol = 0;
    for (int i = 0; i < AVE_TIME - 1; i++)
    {
      aveVol += velVols[i];
      velVols[i] = velVols[i + 1];
    }
    aveVol += Logger.VelocityVoltage;
    velVols[AVE_TIME - 1] = Logger.VelocityVoltage;
    aveVol /= AVE_TIME;

    //�d���\�����X�V
    Application.Current.Dispatcher.Dispatch(() =>
    {
      double velV = Logger.VelocityVoltage;
      velLabel.Text = Logger.ConvertVelocityVoltage(velV).ToString("F2") + " m/s";
      velVLabel.Text = "(" + velV.ToString("F3") + " V)";
      aveVelLabel.Text = Logger.ConvertVelocityVoltage(aveVol).ToString("F2") + " m/s";
      aveVelVLabel.Text = "(" + aveVol.ToString("F3") + " V)";
    });
  }

  private void Logger_EndCalibratingVoltageMessageReceivedEvent(object sender, EventArgs e)
  {
    calibratingVoltage = false;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      velVLabel.IsVisible = false;
      velLabel.IsVisible = false;
      aveVelVLabel.IsVisible = false;
      aveVelLabel.IsVisible = false;
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
        Logger.HasCorrectionFactorsReceived = false;
        while (!Logger.HasCorrectionFactorsReceived)
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
          ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand()));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(CFSetting),
            new Dictionary<string, object> { { "mLogger", Logger }, { "xbee", ConnectedXBee } }
            );
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
        Logger.HasTemperatureAutoCalibrationReceived = false;
        while (!Logger.HasTemperatureAutoCalibrationReceived)
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
          ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoTemperatureCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(async () =>
        {
          await DisplayAlert("Alert", MLSResource.CR_StartCalibration, "OK");
          await Shell.Current.GoToAsync("../..");
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
        Logger.HasVelocityAutoCalibrationReceived = false;
        while (!Logger.HasVelocityAutoCalibrationReceived)
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
          ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoVelocityCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(async () =>
        {
          await DisplayAlert("Alert", MLSResource.CR_StartCalibration, "OK");
          await Shell.Current.GoToAsync("../..");
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
          Logger.HasEndCalibratingVoltageMessageReceived = false;
          while (!Logger.HasEndCalibratingVoltageMessageReceived)
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

            //�I���R�}���h�𑗐M
            ConnectedXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
        //��Z�����Ȃ�ΊJ�n
        else 
        {
          Logger.HasStartCalibratingVoltageMessageReceived = false;
          while (!Logger.HasStartCalibratingVoltageMessageReceived)
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
            ConnectedXBee.SendSerialData
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
          cdownLabel.Text = "0";
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