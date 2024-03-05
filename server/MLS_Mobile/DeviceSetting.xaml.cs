namespace MLS_Mobile;

using System.Text;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using Mopups.Services;
using System;

public partial class DeviceSetting : ContentPage
{

  #region �萔�錾

  private readonly DateTime ST_DTIME = new DateTime(1999, 1, 1, 0, 0, 0);

  #endregion

  #region �񋓌^��`

  /// <summary>�ڑ�����</summary>
  private enum loggingMode
  {
    /// <summary>Bluetooth�Ōg�тȂǂƐڑ�</summary>
    bluetooth = 0,
    /// <summary>Microflash�݊��J�[�h�ɋL�^</summary>
    mfcard = 1,
    /// <summary>Zigbee��PC�Ɛڑ�</summary>
    pc = 2, 
    /// <summary>Zigbee�ŏ��</summary>
    permanent = 3
  }

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
    if (!(e.Page is SettingPopup)) return;

    SettingPopup snPop = (SettingPopup)e.Page;

    //��Ԓl���X�V
    if (snPop.HasChanged)
    {
      if (snPop.PopID == 0)
        updateName(snPop.ChangedValue);
      else if (snPop.PopID == 1)
      {
        try
        {
          int panID = Convert.ToInt32(snPop.ChangedValue, 16);
          byte[] ar = BitConverter.GetBytes(panID);
          Array.Reverse(ar);
          Task.Run(() =>
          {
            MLUtility.LoggerSideXBee.SetParameter("ID", ar);
            MLUtility.LoggerSideXBee.WriteChanges();
          });
        }
        catch { }
      }
    }
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //��{�͑�����~������
    isStopLogging = true;

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
        if (MLUtility.Logger == null) return; //�ڑ��������ɂ͏I��
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
        dpck_start.Date = MLUtility.Logger.StartMeasuringDateTime;
        tpck_start.Time = MLUtility.Logger.StartMeasuringDateTime.TimeOfDay;

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
        if (MLUtility.Logger == null) return; //�ڑ��������ɂ͏I��
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
          MLUtility.Logger.Version_Major + "." +
          MLUtility.Logger.Version_Minor + "." +
          MLUtility.Logger.Version_Revision;

        //Zigbee LED�̗L�������{�^���̗L����
        btn_zigled.IsEnabled = 3 <= MLUtility.Logger.Version_Minor;

        //��ݐݒu���[�h�{�^���̗L����
        btn_pmntMode.IsEnabled =
        (3 <= MLUtility.Logger.Version_Minor) ||
        (2 == MLUtility.Logger.Version_Minor && 4 <= MLUtility.Logger.Version_Revision);
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
        if (MLUtility.Logger == null) return; //�ڑ��������ɂ͏I��
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
      if (MLUtility.Logger == null) return; //�ڑ��������ɂ͏I��
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
      dpck_start.Date.Add(tpck_start.Time),
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
    startLogging(loggingMode.bluetooth);
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
    MopupService.Instance.PushAsync(new SettingPopup(
      0,
      MLSResource.DS_SetName, 
      MLUtility.Logger.Name,
      Keyboard.Text
      ));
  }

  private void SDButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.mfcard);
  }

  private void PANButton_Clicked(object sender, EventArgs e)
  {
    //�C���W�P�[�^�\��
    showIndicator(MLSResource.DR_StartLogging);

    Task.Run(() =>
    {
      try
      {
        byte[] id = MLUtility.LoggerSideXBee.GetParameter("ID");
        string panID = BitConverter.ToString(id).Replace("-", "").TrimStart('0');

        Application.Current.Dispatcher.Dispatch(() =>
        {
          MopupService.Instance.PushAsync(new SettingPopup(
            1,
            MLSResource.DS_ChangePANID,
            panID,
            Keyboard.Numeric
            ));
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

  private void startLogging(loggingMode lMode)
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

          string cmd = "";
          switch (lMode)
          {
            case loggingMode.bluetooth:
              cmd = MLogger.MakeStartMeasuringCommand(false, true, false);
              break;
            case loggingMode.mfcard:
              cmd = MLogger.MakeStartMeasuringCommand(false, false, true);
              break;
            case loggingMode.pc:
              cmd = MLogger.MakeStartMeasuringCommand(true, false, false);
              break;
            case loggingMode.permanent:
              cmd = MLogger.MakeStartMeasuringCommand(true, false, false, true);
              break;
          }

          //�J�n�R�}���h�𑗐M
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(cmd));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          if(lMode == loggingMode.bluetooth) Shell.Current.GoToAsync(nameof(DataReceive));
          else Shell.Current.GoToAsync("..");
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

  private void dpck_start_DateSelected(object sender, DateChangedEventArgs e)
  {
    //���t�ύX���Ȃ���ΏI��
    if (dpck_start.Date == MLUtility.Logger.StartMeasuringDateTime) return;

    lbl_stdtime.TextColor = Colors.Red;
  }

  private void tpck_start_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    //�����ύX���Ȃ���ΏI��
    if (tpck_start == null  || MLUtility.Logger == null || tpck_start.Time == MLUtility.Logger.StartMeasuringDateTime.TimeOfDay) return;

    lbl_stdtime.TextColor = Colors.Red;
  }

  private void resetTextColor()
  {
    lbl_th.TextColor = 
      lbl_glb.TextColor = 
      lbl_vel.TextColor = 
      lbl_lux.TextColor = 
      lbl_stdtime.TextColor = 
      Colors.DarkGreen;
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

  #region Zigbee�ʐM�֘A�̏���

  /// <summary>PC�Ƃ̐ڑ��{�^���^�b�v���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void CnctToPcButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.pc);
  }

  /// <summary>��݃��[�h�{�^���^�b�v���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void PermanentModeButton_Clicked(object sender, EventArgs e)
  {
    startLogging(loggingMode.permanent);
  }

  /// <summary>Zigbee�ʐMLED�\���̗L�����E��������ύX�{�^���^�b�v���̏���</summary>
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
          MLUtility.LoggerSideXBee.WriteChanges(); //�ݒ�𔽉f
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