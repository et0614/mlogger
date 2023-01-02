namespace MLS_Mobile;

using MLLib;

using MLS_Mobile.Resources.i18n;

using System.Collections.ObjectModel;

using System.Text;

using Plugin.BLE.Abstractions.Contracts;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

using Microsoft.Maui.Controls;

public partial class DataReceive : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  private ClothingCoordinator cCoordinator;

  private ActivitySelector actSelector;

  /// <summary>�������t���O</summary>
  private bool initializing = true;

  /// <summary>�v���J�n�t���O</summary>
  private bool isStarted = false;

  /// <summary>�v���I���t���O</summary>
  private bool isEnded = false;

  /// <summary>�v���I���R�}���h���M���t���O</summary>
  private bool isEnding = false;

  /// <summary>Bluetooth�ʐM�f�o�C�X��ݒ�E�擾����</summary>
  public IDevice MLDevice { get; set; }

  /// <summary>XBee��ݒ�E�擾����</summary>
  public ZigBeeBLEDevice MLXBee { get; set; }

  /// <summary>���K�[��ݒ�E�擾����</summary>
  public MLogger Logger { get; set; }

  #endregion

  #region �R���X�g���N�^

  public DataReceive()
  {
    InitializeComponent();

    title_tmp.Text = MLSResource.DrybulbTemperature;
    title_hmd.Text = MLSResource.RelativeHumidity;
    title_glb.Text = MLSResource.GlobeTemperature;
    title_vel.Text = MLSResource.Velocity;
    title_lux.Text = MLSResource.Illuminance;

    quitBtn.Text = MLSResource.DR_FinishMeasurement;


    cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
    metTitle.Text = MLSResource.MetabolicUnit + " [met]";

    //������
    metSlider.Value = 1.2;
  }

  public void StartLogging()
  {
    this.Title = Logger.Name;

    //Clo�l,��ӗʏ�����
    initializing = true;
    cloSlider.Value = Logger.CloValue;
    metSlider.Value = Logger.MetValue;
    initializing = false;
    
    showIndicator(MLSResource.DR_StartLogging);
    Task.Run(async () =>
    {
      int tryNum = 0;
      isStarted = false;
      while (!isStarted)
      {
        //5�񎸔s������G���[�Ŗ߂�
        if (5 <= tryNum)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
            Navigation.PopAsync();
          });
        }
        tryNum++;

        try
        {
          //�J�n�R�}���h�𑗐M//xbee�ʐM����,bluetooth�ʐM�L��,sdcard�����o������(ftf)
          MLXBee.SendSerialData
          (Encoding.ASCII.GetBytes("\rSTL" + MLogger.GetUnixTime(DateTime.Now) + "ftf\r"));
          await Task.Delay(500);
        }
        catch { }
      }
    });
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  //���ߗʐݒ�{�^���N���b�N���̏���
  private void CloBtn_Clicked(object sender, EventArgs e)
  {
    if (cCoordinator == null)
      cCoordinator = new ClothingCoordinator();

    Navigation.PushAsync(cCoordinator, true);
  }

  private async void QuitBtn_Clicked(object sender, EventArgs e)
  {
    if (await DisplayAlert(MLSResource.DR_FinishAlert, "", MLSResource.Yes, MLSResource.No))
    {
      showIndicator(MLSResource.DR_StopLogging);
      isEnding = true;
      _ = Task.Run(async () =>
      {
        int tryNum = 0;

        while (!isEnded) 
        {
          //5�񎸔s������G���[�Ŗ߂�
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.DR_FailStopping, "OK");
              Navigation.PopAsync();
            });
          }
          tryNum++;

          try
          {
            MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rENL\r"));
            await Task.Delay(500);
          }
          catch { }
        }
      });
    }
  }

  private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
  {
    if (initializing) return;

    Logger.MetValue = metSlider.Value;
    Logger.CloValue = cloSlider.Value;
  }

  //�����ʐݒ�{�^���N���b�N���̏���
  private void ActBtn_Clicked(object sender, EventArgs e)
  {
    if (actSelector == null)
      actSelector = new ActivitySelector();

    Navigation.PushAsync(actSelector, true);
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g
  protected override void OnAppearing()
  {
    //�X���[�v�֎~
    DeviceDisplay.Current.KeepScreenOn = true;

    //XBee�C�x���g�o�^      
    MLXBee.SerialDataReceived += MLXBee_SerialDataReceived;

    //MLogger�C�x���g�o�^
    Logger.StartMeasuringMessageReceivedEvent += Logger_StartMeasuringMessageReceivedEvent;
    Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
    Logger.EndMeasuringMessageReceivedEvent += Logger_EndMeasuringMessageReceivedEvent;

    //���ߗʐݒ莞�͔��f
    if (cCoordinator != null && cCoordinator.ApplyChange)
    {
      cCoordinator.ApplyChange = false;
      cloSlider.Value = cCoordinator.CloValue;
    }

    //�����ʐݒ莞�͔��f
    if (actSelector != null && actSelector.ApplyChange)
    {
      actSelector.ApplyChange = false;
      metSlider.Value = actSelector.MetValue;
    }
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�X���[�v����
    DeviceDisplay.Current.KeepScreenOn = false;

    //XBee�C�x���g����
    MLXBee.SerialDataReceived -= MLXBee_SerialDataReceived;

    //MLogger�C�x���g����
    Logger.StartMeasuringMessageReceivedEvent -= Logger_StartMeasuringMessageReceivedEvent;
    Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
    Logger.EndMeasuringMessageReceivedEvent -= Logger_EndMeasuringMessageReceivedEvent;
  }

  #endregion

  #region �ʐM����

  private void MLXBee_SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
  {
    //��M�f�[�^��ǉ�
    Logger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

    //�R�}���h����
    while (Logger.HasCommand)
    {
      try
      {
        Logger.SolveCommand();
      }
      catch { }
    }
  }

  private void Logger_StartMeasuringMessageReceivedEvent(object sender, EventArgs e)
  {
    isStarted = true;
    hideIndicator();
  }

  private void Logger_EndMeasuringMessageReceivedEvent(object sender, EventArgs e)
  {
    isEnded = true;
    hideIndicator();

    Application.Current.Dispatcher.Dispatch(() =>
    {
      Navigation.PopAsync();
    });
  }

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    isStarted = true;
    if (!isEnding) hideIndicator();

    Application.Current.Dispatcher.Dispatch(() =>
    {
      val_tmp.Text = Logger.DrybulbTemperature.LastValue.ToString("F1");
      val_hmd.Text = Logger.RelativeHumdity.LastValue.ToString("F1");
      val_glb.Text = Logger.GlobeTemperature.LastValue.ToString("F1");
      val_vel.Text = Logger.Velocity.LastValue.ToString("F2");
      val_lux.Text = Logger.Illuminance.LastValue.ToString("F1");
      val_mrt.Text = Logger.MeanRadiantTemperature.ToString("F1");
      val_pmv.Text = Logger.PMV.ToString("F1");
      val_ppd.Text = Logger.PPD.ToString("F1");
      val_set.Text = Logger.SETStar.ToString("F1");

      dtm_tmp.Text = Logger.DrybulbTemperature.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_hmd.Text = Logger.RelativeHumdity.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_glb.Text = Logger.GlobeTemperature.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_vel.Text = Logger.Velocity.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_lux.Text = Logger.Illuminance.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
    });

    //�f�[�^��ۑ�
    string line =
      Logger.LastMeasured.ToString("yyyy/M/d,HH:mm:ss") + "," +
      Logger.DrybulbTemperature.LastValue.ToString("F1") + "," +
      Logger.RelativeHumdity.LastValue.ToString("F1") + "," +
      Logger.GlobeTemperature.LastValue.ToString("F2") + "," +
      Logger.Velocity.LastValue.ToString("F3") + "," +
      Logger.Illuminance.LastValue.ToString("F2") + "," +
      Logger.GlobeTemperatureVoltage.ToString("F3") + "," +
      Logger.VelocityVoltage.ToString("F3") + Environment.NewLine;

    string fileName = Logger.Name + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
    MLUtility.AppendData(fileName, line);
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