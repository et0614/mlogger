namespace MLS_Mobile;

using System.Text;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using Mopups.Services;
using System;
using System.Xml.Linq;

public partial class DeviceSetting : ContentPage
{

  #region �萔�錾

  private readonly DateTime ST_DTIME = new DateTime(1999, 1, 1, 0, 0, 0);

  #endregion

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>���M���O���~�����邩�ۂ�</summary>
  private bool isStopLogging = true;

  #endregion

  #region �R���X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
  public DeviceSetting()
  {
    InitializeComponent();

    //�|�b�v�Ŗ߂��Ă����ꍇ
    MopupService.Instance.Popped += Instance_Popped;

    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + MLUtility.Logger.LocalName;
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + MLUtility.Logger.LowAddress;
    spc_vers.Text = MLSResource.DS_SpecVersion + ": -";

    //�o�[�W�����X�V
    loadVersion();

    //���̍X�V
    loadName();

    //����ݒ�X�V
    loadMeasurementSetting();

    //Zigbee LED��Ԃ��X�V
    loadZigbeeLEDStatus();
  }

  private void Instance_Popped(object sender, Mopups.Events.PopupNavigationEventArgs e)
  {
    if (!(e.Page is SettingNamePopup)) return;

    SettingNamePopup snPop = (SettingNamePopup)e.Page;

    //���̍X�V
    if (snPop.HasChanged)
      updateName(snPop.Name);
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //��{�͑�����~������
    isStopLogging = true;

    //SD�J�[�h�����o���̉���ԍX�V
    btnSDLogging.IsVisible = MLUtility.MMCardEnabled;

    MLUtility.Logger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    MLUtility.Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    //�v���J�n���łȂ���Β�~������
    if (isStopLogging)
    {
      MLUtility.Logger.HasEndMeasuringMessageReceived = false;

      Task.Run(async () =>
      {
        //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
        while (!MLUtility.Logger.HasEndMeasuringMessageReceived)
        {
          try
          {
            //��~�R�}���h�𑗐M
            MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeEndLoggingCommand()));
            await Task.Delay(500);
          }
          catch { }
        }
      });
    }
  }

  #endregion

  #region MLogger���X�V����

  /// <summary>����ݒ��ǂݍ���</summary>
  private void loadMeasurementSetting()
  {
    MLUtility.Logger.HasMeasurementSettingReceived = false;

    Task.Run(async () =>
    {
      //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
      while (!MLUtility.Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //�ݒ�ݒ�擾�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadMeasuringSettingCommand()));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //�v���ݒ�
        cbx_th.IsToggled = MLUtility.Logger.DrybulbTemperature.Measure;
        ent_th.Text = MLUtility.Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = MLUtility.Logger.GlobeTemperature.Measure;
        ent_glb.Text = MLUtility.Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = MLUtility.Logger.Velocity.Measure;
        ent_vel.Text = MLUtility.Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = MLUtility.Logger.Illuminance.Measure;
        ent_lux.Text = MLUtility.Logger.Illuminance.Interval.ToString();

        //�ҏW�v�f�̒��F�����Ƃɖ߂�
        resetTextColor();
      });
    });
  }

  /// <summary>�o�[�W��������ǂݍ���</summary>
  private void loadVersion()
  {
    MLUtility.Logger.HasVersionReceived = false;
    Task.Run(async () =>
    {
      //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
      while (!MLUtility.Logger.HasVersionReceived)
      {
        try
        {
          //�o�[�W�����擾�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand()));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
          MLUtility.Logger.Version_Major + "." +
          MLUtility.Logger.Version_Minor + "." +
          MLUtility.Logger.Version_Revision;
      });
    });
  }

  /// <summary>���̂�ǂݍ���</summary>
  private void loadName()
  {
    MLUtility.Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      while (!MLUtility.Logger.HasLoggerNameReceived)
      {
        try
        {
          //���̎擾�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadLoggerNameCommand()));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + MLUtility.Logger.Name;
      });
    });
  }

  /// <summary>���̂�ݒ肷��</summary>
  /// <param name="name">����</param>
  private void updateName(string name)
  {
    MLUtility.Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      while (!MLUtility.Logger.HasLoggerNameReceived)
      {
        try
        {
          //���̎擾�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeChangeLoggerNameCommand(name)));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + MLUtility.Logger.Name;
      });
    });
  }

  /// <summary>����ݒ��ݒ肷��</summary>
  private void updateMeasurementSetting()
  {
    //���̓G���[������ΏI��
    int thSpan, glbSpan, velSpan, luxSpan;
    if (!isInputsCorrect(out thSpan, out glbSpan, out velSpan, out luxSpan)) return;

    //�ݒ�R�}���h���쐬
    string sData = MLogger.MakeChangeMeasuringSettingCommand(
      ST_DTIME,
      cbx_th.IsToggled, thSpan,
      cbx_glb.IsToggled, glbSpan,
      cbx_vel.IsToggled, velSpan,
      cbx_lux.IsToggled, luxSpan,
      false, 0, false, 0, false, 0, false);


    MLUtility.Logger.HasMeasurementSettingReceived = false;
    Task.Run(async () =>
    {
      //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
      while (!MLUtility.Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //�ݒ�ݒ�擾�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(sData));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //�v���ݒ�
        cbx_th.IsToggled = MLUtility.Logger.DrybulbTemperature.Measure;
        ent_th.Text = MLUtility.Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = MLUtility.Logger.GlobeTemperature.Measure;
        ent_glb.Text = MLUtility.Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = MLUtility.Logger.Velocity.Measure;
        ent_vel.Text = MLUtility.Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = MLUtility.Logger.Illuminance.Measure;
        ent_lux.Text = MLUtility.Logger.Illuminance.Interval.ToString();

        //�ҏW�v�f�̒��F�����Ƃɖ߂�
        resetTextColor();
      });
    });
  }

  /// <summary>Zigbee�ʐMLED�\����Ԃ�ǂݍ���</summary>
  private void loadZigbeeLEDStatus()
  {
    Task.Run(async () =>
    {
      //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
      while (true)
      {
        try
        {
          byte[] rslt = MLUtility.LoggerSideXBee.GetParameter("D5");
          Application.Current.Dispatcher.Dispatch(() =>
          {
            btn_zigled.Text =
            (rslt[0] == 1) ? MLSResource.DS_DisableZigLED : MLSResource.DS_EnableZigLED;
          });
          return;
        }
        catch{ }
        await Task.Delay(500);
      }
    });
  }

  private bool isInputsCorrect
  (out int thSpan, out int glbSpan, out int velSpan, out int luxSpan)
  {
    bool hasError = false;
    string alert = "";
    if (!int.TryParse(ent_th.Text, out thSpan))
    {
      hasError = true;
      alert +=�@MLSResource.DS_InvalidNumber + "(" + MLSResource.DrybulbTemperature + ")\r\n";
    }
    if (!int.TryParse(ent_glb.Text, out glbSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.GlobeTemperature + ")\r\n";
    }
    if (!int.TryParse(ent_vel.Text, out velSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.Velocity + ")\r\n";
    }
    if (!int.TryParse(ent_lux.Text, out luxSpan))
    {
      hasError = true;
      alert += MLSResource.DS_InvalidNumber + "(" + MLSResource.Illuminance + ")\r\n";
    }

    if (hasError)
      DisplayAlert("Alert", alert, "OK");

    return !hasError;
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  private void StartButton_Clicked(object sender, EventArgs e)
  {
    startLogging(false);
  }

  private void SaveButton_Clicked(object sender, EventArgs e)
  {
    updateMeasurementSetting();
  }

  private void LoadButton_Clicked(object sender, EventArgs e)
  {
    loadMeasurementSetting();
  }

  private void CFButton_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(Calibrator));
  }

  private void SetNameButton_Clicked(object sender, EventArgs e)
  {
    MopupService.Instance.PushAsync(new SettingNamePopup(MLUtility.Logger.Name));
  }

  private void SDButton_Clicked(object sender, EventArgs e)
  {
    startLogging(true);
  }

  private void startLogging(bool writeToSDCard)
  {
    //�v����~�t���O������
    isStopLogging = false;

    MLUtility.Logger.HasStartMeasuringMessageReceived = false;

    //�C���W�P�[�^�\��
    showIndicator(MLSResource.DR_StartLogging);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        while (!MLUtility.Logger.HasStartMeasuringMessageReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
              return;
            });
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeStartMeasuringCommand(false, !writeToSDCard, writeToSDCard)));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          if(writeToSDCard) Shell.Current.GoToAsync("..");
          else Shell.Current.GoToAsync(nameof(DataReceive));
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

  #endregion

  #region �R���g���[���ҏW���̒��F����

  private void cbx_Toggled(object sender, ToggledEventArgs e)
  {
    if (sender.Equals(cbx_th)) lbl_th.TextColor = Colors.Red;
    else if (sender.Equals(cbx_glb)) lbl_glb.TextColor = Colors.Red;
    else if (sender.Equals(cbx_vel)) lbl_vel.TextColor = Colors.Red;
    else if (sender.Equals(cbx_lux)) lbl_lux.TextColor = Colors.Red;
  }

  private void ent_TextChanged(object sender, TextChangedEventArgs e)
  {
    if (sender.Equals(ent_th)) lbl_th.TextColor = Colors.Red;
    else if (sender.Equals(ent_glb)) lbl_glb.TextColor = Colors.Red;
    else if (sender.Equals(ent_vel)) lbl_vel.TextColor = Colors.Red;
    else if (sender.Equals(ent_lux)) lbl_lux.TextColor = Colors.Red;
  }

  private void resetTextColor()
  {
    lbl_th.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Colors.DarkGreen;
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

  #region Zigbee�ʐMLED�\���̗L�����E����������

  /// <summary>Zigbee�ʐMLED�\���̗L�����E��������ύX</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void LEDButton_Clicked(object sender, EventArgs e)
  {
    //�ݒ�ǂݍ��ݒ��͖���
    if (btn_zigled.Text == MLSResource.DS_LoadingZigLED) return;
    bool ledEnabled = (btn_zigled.Text == MLSResource.DS_DisableZigLED);

    Task.Run(async () =>
    {
      //��������܂�3��͌J��Ԃ�
      for (int i = 0; i < 3; i++)
      {
        try
        {
          MLUtility.LoggerSideXBee.SetParameter("D5", ledEnabled ? new byte[] { 4 } : new byte[] { 1 });
          Application.Current.Dispatcher.Dispatch(() =>
          {
            btn_zigled.Text = ledEnabled ? MLSResource.DS_EnableZigLED : MLSResource.DS_DisableZigLED;
          });
          return;
        }
        catch { }
        await Task.Delay(500);
      }
    });
  }

  #endregion

}