namespace MLS_Mobile;

using System.Text;
using System.Threading.Tasks;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;

public partial class CFSetting : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>�����������t���O</summary>
  private bool isInitialized = false;

  public MLogger Logger { get; set; }

  private bool isEdited = false;

  /// <summary>XBee��ݒ�E�擾����</summary>
  public ZigBeeBLEDevice MLXBee { get; set; }

  #endregion

  public CFSetting()
  {
    InitializeComponent();

    cV_dbt.Text = cV_glb.Text = cV_hmd.Text = cV_lux.Text = cV_vel.Text = MLSResource.CF_CorrectionValue;
    ms_dbt.Text = ms_glb.Text = ms_hmd.Text = ms_lux.Text = ms_vel.Text = MLSResource.CF_Measurement;

    lbl_dbt.Text = MLSResource.DrybulbTemperature;
    lbl_hmd.Text = MLSResource.RelativeHumidity;
    lbl_glb.Text = MLSResource.GlobeTemperature;
    lbl_vel.Text = MLSResource.Velocity;
    lbl_lux.Text = MLSResource.Illuminance;

    vel_voltage.Text = MLSResource.CF_VelocityVoltage;

    btnBack.Text = MLSResource.CF_Back;
    btnSet.Text = MLSResource.CF_Set;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //XBee�C�x���g�o�^      
    MLXBee.SerialDataReceived += MLXBee_SerialDataReceived;

    //MLogger�C�x���g�o�^
    Logger.CorrectionFactorsReceivedEvent += Logger_CorrectionFactorsReceivedEvent;

    //�␳�W���ǂݍ��ݏ���
    showIndicator(MLSResource.CF_Loading);
    Task.Run(async () =>
    {
      try
      {
        //�␳�W���ǂݍ��݃R�}���h
        MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rLCF\r"));

        await Task.Delay(5000);
        if (!isInitialized)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", MLSResource.CF_LoadingError, "OK");
            Navigation.PopAsync();
          });
        }
      }
      catch (Exception ex)
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", ex.Message, "OK");
          Navigation.PopAsync();
        });
      }
    });
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //XBee�C�x���g����
    MLXBee.SerialDataReceived -= MLXBee_SerialDataReceived;

    //MLogger�C�x���g����
    Logger.CorrectionFactorsReceivedEvent -= Logger_CorrectionFactorsReceivedEvent;
  }

  #region Entry���쎞�̏���

  private void dbt_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_dbt.TextColor = Colors.Red;
    isEdited = true;
  }

  private void hmd_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_hmd.TextColor = Colors.Red;
    isEdited = true;
  }

  private void glb_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_glb.TextColor = Colors.Red;
    isEdited = true;
  }

  private void vel_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_vel.TextColor = Colors.Red;
    isEdited = true;
  }

  private void lux_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_lux.TextColor = Colors.Red;
    isEdited = true;
  }

  #endregion

  #region �{�^���N���b�N�C�x���g�������̏���

  private async void Cancel_Clicked(object sender, EventArgs e)
  {
    if (isEdited)
    {
      if (await DisplayAlert("Alert", MLSResource.CF_Discard, "OK", "Cancel"))
        await Navigation.PopAsync();
    }
    else await Navigation.PopAsync();
  }

  private void Set_Clicked(object sender, EventArgs e)
  {
    if (!isEdited) return;

    //�l���K�����A�m�F����
    bool hasError = false;
    string errMsg = "";

    if (!double.TryParse(cA_dbt.Text, out double dbtA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.DrybulbTemperature) + Environment.NewLine;
    }
    if (!double.TryParse(cB_dbt.Text, out double dbtB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.DrybulbTemperature) + Environment.NewLine;
    }

    if (!double.TryParse(cA_hmd.Text, out double hmdA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.RelativeHumidity) + Environment.NewLine;
    }
    if (!double.TryParse(cB_hmd.Text, out double hmdB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.RelativeHumidity) + Environment.NewLine;
    }

    if (!double.TryParse(cA_glb.Text, out double glbA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.GlobeTemperature) + Environment.NewLine;
    }
    if (!double.TryParse(cB_glb.Text, out double glbB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.GlobeTemperature) + Environment.NewLine;
    }

    if (!double.TryParse(cA_vel.Text, out double velA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.RelativeAirVelocity) + Environment.NewLine;
    }
    if (!double.TryParse(cB_vel.Text, out double velB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.RelativeAirVelocity) + Environment.NewLine;
    }
    if (!double.TryParse(vel_0V.Text, out double velV))
    {
      hasError = true;
      errMsg += MLSResource.CF_InvalidVelocity1 + Environment.NewLine;
    }
    else if (velV <= 0)
    {
      hasError = true;
      errMsg += MLSResource.CF_InvalidVelocity2 + Environment.NewLine;
    }

    if (!double.TryParse(cA_lux.Text, out double luxA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.Illuminance) + Environment.NewLine;
    }
    if (!double.TryParse(cB_lux.Text, out double luxB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.Illuminance) + Environment.NewLine;
    }

    if (hasError) DisplayAlert("Alert", errMsg, "OK");
    else
    {
      //�␳�W���ݒ�R�}���h�𑗐M
      Task.Run(() =>
      {
        try
        {
          MLXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeCorrectionFactorsSettingCommand
          (dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, velV)));
        }
        catch (Exception ex)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
            Navigation.PopAsync();
          });

          /*Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
            Navigation.PopAsync();
          });*/
        }
      });
    }
  }

  private void resetLabelColor()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      lbl_dbt.TextColor = lbl_hmd.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Colors.Black;
    });

    /*Device.BeginInvokeOnMainThread(() =>
    {
      lbl_dbt.TextColor = lbl_hmd.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Colors.Black;
    });*/
    isEdited = false;
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

  private void Logger_CorrectionFactorsReceivedEvent(object sender, EventArgs e)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      cA_dbt.Text = Logger.DrybulbTemperature.CorrectionFactorA.ToString("F3");
      cB_dbt.Text = Logger.DrybulbTemperature.CorrectionFactorB.ToString("F2");

      cA_hmd.Text = Logger.RelativeHumdity.CorrectionFactorA.ToString("F3");
      cB_hmd.Text = Logger.RelativeHumdity.CorrectionFactorB.ToString("F2");

      cA_glb.Text = Logger.GlobeTemperature.CorrectionFactorA.ToString("F3");
      cB_glb.Text = Logger.GlobeTemperature.CorrectionFactorB.ToString("F2");

      cA_vel.Text = Logger.Velocity.CorrectionFactorA.ToString("F3");
      cB_vel.Text = Logger.Velocity.CorrectionFactorB.ToString("F3");
      vel_0V.Text = Logger.VelocityMinVoltage.ToString("F3");

      cA_lux.Text = Logger.Illuminance.CorrectionFactorA.ToString("F3");
      cB_lux.Text = Logger.Illuminance.CorrectionFactorB.ToString("F0");
    });

    resetLabelColor();

    isEdited = false;
    isInitialized = true;
    hideIndicator();
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

    /*Device.BeginInvokeOnMainThread(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });*/
  }

  /// <summary>�C���W�P�[�^���B��</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });

    /*Device.BeginInvokeOnMainThread(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });*/
  }

  #endregion

}