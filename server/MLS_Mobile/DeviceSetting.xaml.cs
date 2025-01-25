namespace MLS_Mobile;

using System;
using System.Text;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using CommunityToolkit.Maui.Views;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
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

  /// <summary>�J���҃��[�h���ۂ�</summary>
  private static bool isDeveloperMode = false;

  /// <summary>���M���O���~�����邩�ۂ�</summary>
  private bool isStopLogging = true;

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
      //�o�^�ς̏ꍇ�ɂ̓C�x���g������
      MLogger ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;

      initInfo();
    }
  }

  /// <summary>�f�[�^����M����MLogger��ݒ�E�擾����</summary>
  public MLogger Logger
  {
    get
    {
      return MLUtility.GetLogger(_mlLowAddress);
    }
  }

  #endregion

  #region �R���X�g���N�^�E�f�X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
  public DeviceSetting()
  {
    InitializeComponent();

    //�V�F�C�N�C�x���g�o�^
    Accelerometer.ShakeDetected += Accelerometer_ShakeDetected;
    Accelerometer.Start(SensorSpeed.UI);
  }

  /// <summary>�V�F�C�N���̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void Accelerometer_ShakeDetected(object sender, EventArgs e)
  {
    //��ʓI�ł͂Ȃ��{�^���Q�̕\���E��\���؂�ւ�
    isDeveloperMode = calvBtnA.IsVisible = calvBtnB.IsVisible = !calvBtnA.IsVisible;
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //�Z���{�^���̕\���E��\��
    calvBtnA.IsVisible = calvBtnB.IsVisible = isDeveloperMode;

    //��{�͑�����~������
    isStopLogging = true;
  }
  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�V�F�C�N�C�x���g����
    Accelerometer.Stop();
    Accelerometer.ShakeDetected -= Accelerometer_ShakeDetected;
  }

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    //�v���J�n���łȂ���Β�~������
    if (isStopLogging)
    {
      Logger.HasEndMeasuringMessageReceived = false;

      Task.Run(async () =>
      {
        //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
        while (!Logger.HasEndMeasuringMessageReceived)
        {
          try
          {
            //��~�R�}���h�𑗐M
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeEndLoggingCommand()));
            await Task.Delay(500);
          }
          catch { }
        }

        //���O
        MLUtility.WriteLog(Logger.XBeeName + "; End logging; ");
      });
    }
  }

  #endregion

  #region ����������

  private void initInfo()
  {
    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_localName.Text = MLSResource.DS_SpecLocalName + ": " + Logger.LocalName;
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + Logger.LowAddress;
    spc_vers.Text = MLSResource.DS_SpecVersion + ": " +
      Logger.Version_Major + "." + Logger.Version_Minor + "." + Logger.Version_Revision;

    //�o�[�W�����ɉ���������
    //Zigbee LED�̗L�������{�^���̗L����
    btn_zigled.IsEnabled = 3 <= Logger.Version_Minor;

    //��ݐݒu���[�h�{�^���̗L����
    btn_pmntMode.IsEnabled =
      (3 <= Logger.Version_Minor) ||
      (2 == Logger.Version_Minor && 4 <= Logger.Version_Revision);

    //���̍X�V
    loadName();

    //����ݒ�X�V
    loadMeasurementSetting();

    //Zigbee LED��Ԃ��X�V
    loadZigbeeLEDStatus();

    //CO2�Z�x�Z���T�̗L���𔽉f
    loadCO2SensorInfo();
  }

  #endregion

  #region MLogger���X�V����

  /// <summary>����ݒ��ǂݍ���</summary>
  private void loadMeasurementSetting()
  {
    Logger.HasMeasurementSettingReceived = false;

    Task.Run(async () =>
    {
      //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
      while (!Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //�ݒ�ݒ�擾�R�}���h�𑗐M
          MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadMeasuringSettingCommand()));
        }
        catch { }
        await Task.Delay(500);
        if (Logger == null) return; //�ڑ��������ɂ͏I��
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //�v���ݒ�
        cbx_th.IsToggled = Logger.DrybulbTemperature.Measure;
        ent_th.Text = Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = Logger.GlobeTemperature.Measure;
        ent_glb.Text = Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = Logger.Velocity.Measure;
        ent_vel.Text = Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = Logger.Illuminance.Measure;
        ent_lux.Text = Logger.Illuminance.Interval.ToString();
        dpck_start.Date = Logger.StartMeasuringDateTime;
        tpck_start.Time = Logger.StartMeasuringDateTime.TimeOfDay;

        //�ҏW�v�f�̒��F�����Ƃɖ߂�
        resetTextColor();
      });
    });
  }

  /// <summary>���̂�ǂݍ���</summary>
  private void loadName()
  {
    Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      while (!Logger.HasLoggerNameReceived)
      {
        try
        {
          //���̎擾�R�}���h�𑗐M
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadLoggerNameCommand()));
        }
        catch { }
        await Task.Delay(500);
        if (Logger == null) return; //�ڑ��������ɂ͏I��
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
      });
    });
  }

  private void loadCO2SensorInfo()
  {
    Logger.HasCO2LevelSensorReceived = false;
    Task.Run(async () =>
    {
      int tryNum = 0;
      while (!Logger.HasCO2LevelSensorReceived)
      {
        try
        {
          //CO2�Z�x�Z���T�̗L���擾�R�}���h�𑗐M
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeHasCO2LevelSensorCommand()));
        }
        catch { }
        await Task.Delay(500);
        if (Logger == null) return; //�ڑ��������ɂ͏I��
        tryNum++;
        if (5 < tryNum) return; //5��ł�����߂�i���@��ɂ͖{�@�\�͖������߁j
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        co2LevelGird.IsVisible = Logger.HasCO2LevelSensor;
      });
    });
  }

  /// <summary>���̂�ݒ肷��</summary>
  /// <param name="name">����</param>
  private void updateName(string name)
  {
    Logger.HasLoggerNameReceived = false;
    Task.Run(async () =>
    {
      if (Logger == null) return; //�ڑ��������ɂ͏I��
      while (!Logger.HasLoggerNameReceived)
      {
        try
        {
          //���̎擾�R�}���h�𑗐M
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeChangeLoggerNameCommand(name)));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //���M���O
        MLUtility.WriteLog(Logger.XBeeName + "; Name changed; " + Logger.Name + "; ");

        spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
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
      false, 0, false, 0, false, 0, false, false, 0);

    Logger.HasMeasurementSettingReceived = false;
    Task.Run(async () =>
    {
      //��񂪍X�V�����܂Ŗ��߂��J��Ԃ�
      while (!Logger.HasMeasurementSettingReceived)
      {
        try
        {
          //�ݒ�ݒ�擾�R�}���h�𑗐M
          MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(sData));
        }
        catch { }
        await Task.Delay(500);
      }

      //�X�V���ꂽ���𔽉f
      Application.Current.Dispatcher.Dispatch(() =>
      {
        //���M���O
        MLUtility.WriteLog(Logger.XBeeName + ": Measurement setting changed; " +
          (MLSResource.DrybulbTemperature + "=" + (Logger.DrybulbTemperature.Measure ? Logger.DrybulbTemperature.Interval + " sec; " : "false; ")) +
          (MLSResource.GlobeTemperature + "=" + (Logger.GlobeTemperature.Measure ? Logger.GlobeTemperature.Interval + " sec; " : "false; ")) +
          (MLSResource.Velocity + "=" + (Logger.Velocity.Measure ? Logger.Velocity.Interval + " sec; " : "false; ")) +
          (MLSResource.Illuminance + "=" + (Logger.Illuminance.Measure ? Logger.Illuminance.Interval + " sec; " : "false; "))
          );

        //�v���ݒ�
        cbx_th.IsToggled = Logger.DrybulbTemperature.Measure;
        ent_th.Text = Logger.DrybulbTemperature.Interval.ToString();
        cbx_glb.IsToggled = Logger.GlobeTemperature.Measure;
        ent_glb.Text = Logger.GlobeTemperature.Interval.ToString();
        cbx_vel.IsToggled = Logger.Velocity.Measure;
        ent_vel.Text = Logger.Velocity.Interval.ToString();
        cbx_lux.IsToggled = Logger.Illuminance.Measure;
        ent_lux.Text = Logger.Illuminance.Interval.ToString();

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
          byte[] rslt = MLUtility.ConnectedXBee.GetParameter("D5");
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
            });
            return;
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          MLUtility.ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand()));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(CFSetting),
            new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
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

  private void VelocityCalibrationButton_Clicked(object sender, EventArgs e)
  {
    //�C���W�P�[�^�\��
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        //�����␳�W���擾
        int tryNum = 0;
        Logger.HasVelocityCharacteristicsReceived = false;
        while (!Logger.HasVelocityCharacteristicsReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
            });
            return;
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          MLUtility.ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeLoadVelocityCharateristicsCommand()));

          await Task.Delay(500);
        }
        double[] minVandCoefs = new double[] { Logger.VelocityMinVoltage, Logger.VelocityCharacteristicsCoefA, Logger.VelocityCharacteristicsCoefB, Logger.VelocityCharacteristicsCoefC };

        //�����Z���J�n
        tryNum = 0;
        Logger.HasStartCalibratingVoltageMessageReceived = false;
        while (!Logger.HasStartCalibratingVoltageMessageReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
            });
            return;
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          MLUtility.ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeStartCalibratingVoltageCommand()));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(VelocityCalibrator),
            new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress }, { "minVandCoefs", minVandCoefs } }
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

  private async void SetNameButton_Clicked(object sender, EventArgs e)
  {
    var popup = new TextInputPopup(MLSResource.DS_SetName, Logger.Name, Keyboard.Text);
    var result = await this.ShowPopupAsync(popup);
    if (result != null) updateName(popup.EntryValue);
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
        byte[] id = MLUtility.ConnectedXBee.GetParameter("ID");
        string panID = BitConverter.ToString(id).Replace("-", "").TrimStart('0');

        Application.Current.Dispatcher.Dispatch(async () =>
        {
          var popup = new TextInputPopup(MLSResource.DS_ChangePANID, panID, Keyboard.Numeric);
          var result = await this.ShowPopupAsync(popup);
          if (result != null)
          {
            try
            {
              int panID = Convert.ToInt32(popup.EntryValue, 16);
              byte[] ar = BitConverter.GetBytes(panID);
              Array.Reverse(ar);

              await Task.Run(() =>
              {
                MLUtility.ConnectedXBee.SetParameter("ID", ar);
                MLUtility.ConnectedXBee.WriteChanges();
              });
            }
            catch { }
          }
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

    Logger.HasStartMeasuringMessageReceived = false;

    //�C���W�P�[�^�\��
    showIndicator(MLSResource.DR_StartLogging);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        while (!Logger.HasStartMeasuringMessageReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
            });
            return;
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
          MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(cmd));

          await Task.Delay(500);
        }

        //�J�n�ɐ���������y�[�W�ړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //Bluetooth�̏ꍇ�ɂ̓X�}�[�g�t�H���Ńf�[�^�\��
          if (lMode == loggingMode.bluetooth)
            Shell.Current.GoToAsync(nameof(DataReceive),
              new Dictionary<string, object> { { "mlLowAddress", MLoggerLowAddress } }
              );
          //�t���b�V���������܂���PC�ւ̕ۑ��̏ꍇ�ɂ̓X�^�[�g�y�[�W�֖߂�
          else Shell.Current.GoToAsync("..");
        });

        //���O
        if(lMode == loggingMode.bluetooth)
          MLUtility.WriteLog(Logger.XBeeName + "; Start logging by smart phone; ");
        else if(lMode == loggingMode.mfcard)
          MLUtility.WriteLog(Logger.XBeeName + "; Start logging to flash memory; ");
        else
          MLUtility.WriteLog(Logger.XBeeName + "; Start logging to PC; ");
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
    if (dpck_start.Date == Logger.StartMeasuringDateTime) return;

    lbl_stdtime.TextColor = Colors.Red;
  }

  private void tpck_start_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    //�����ύX���Ȃ���ΏI��
    if (tpck_start == null  || Logger == null || tpck_start.Time == Logger.StartMeasuringDateTime.TimeOfDay) return;

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
          MLUtility.ConnectedXBee.SetParameter("D5", ledEnabled ? new byte[] { 4 } : new byte[] { 1 });
          MLUtility.ConnectedXBee.WriteChanges(); //�ݒ�𔽉f
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