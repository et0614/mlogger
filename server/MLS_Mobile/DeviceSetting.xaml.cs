namespace MLS_Mobile;

using System.Text;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

using Plugin.BLE.Abstractions.Contracts;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;

public partial class DeviceSetting : ContentPage
{

  #region �萔�錾

  private readonly DateTime ST_DTIME = new DateTime(1999, 1, 1, 0, 0, 0);

  #endregion

  #region �C���X�^���X�ϐ��v���p�e�B

  /// <summary>���M���O�J�n�t���O</summary>
  private bool isStarted = false;

  /// <summary>�o�[�W�������ǂݍ��ݍς݂�</summary>
  private bool verstionLoaded = false;

  /// <summary>�ݒ�ǂݍ��ݍς݂�</summary>
  private bool settingLoaded = false;

  /// <summary>Bluetooth�ʐM�f�o�C�X��ݒ�E�擾����</summary>
  public IDevice MLDevice { get; set; }

  /// <summary>XBee��ݒ�E�擾����</summary>
  public ZigBeeBLEDevice MLXBee { get; set; }

  /// <summary>���K�[��ݒ�E�擾����</summary>
  public MLogger Logger { get; set; }

  #endregion

  #region �R���X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
  public DeviceSetting()
  {
    InitializeComponent();

    title1.Text = MLSResource.DS_TargetAndTimeInterval;
    //title2.Text = MLSResource.DS_StartDTime;
    title3.Text = MLSResource.DS_Communicate;
    title4.Text = MLSResource.DS_Info;

    lbl_th.Text = MLSResource.DS_TemperatureAndHumidity;
    lbl_glb.Text = MLSResource.GlobeTemperature;
    lbl_vel.Text = MLSResource.Velocity;
    lbl_lux.Text = MLSResource.Illuminance;

    btnLoad.Text = MLSResource.DS_LoadSetting;
    btnSave.Text = MLSResource.DS_SaveSetting;
    btnStart.Text = MLSResource.DS_Start;
    btnCFactor.Text = MLSResource.DS_CFactor;
    btnSDLogging.Text = MLSResource.DS_SDLogging;

    spc_name.Text = MLSResource.DS_SpecName + ": -";
    spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": -";
    spc_mcadds.Text = MLSResource.DS_SpecMACAdd + ": -";
    spc_vers.Text = MLSResource.DS_SpecVersion + ": -";
  }

  public void InitializeMLogger()
  {
    //�o�[�W�����X�V
    loadVersion();

    Task.Run(() =>
    {
      try
      {
        //�@����\��
        string xbAdd = MLXBee.GetAddressString();
        string mcAdd = MLXBee.GetBluetoothMacAddress();

        Application.Current.Dispatcher.Dispatch(() =>
        {
          spc_name.Text = MLSResource.DS_SpecName + ": " + Logger.Name;
          spc_xbadds.Text = MLSResource.DS_SpecXBAdd + ": " + xbAdd;
          spc_mcadds.Text = MLSResource.DS_SpecMACAdd + ": " + mcAdd;
        });
      }
      catch (Exception ex)
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", ex.Message, "OK");
        });
      }
    });

    //�@����X�V
    updateSetting();
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //XBee�̃C�x���g�o�^      
    MLXBee.SerialDataReceived += MlXBee_SerialDataReceived;
    //MLogger�̃C�x���g�o�^
    Logger.VersionReceivedEvent += Logger_VersionReceivedEvent;
    Logger.MeasurementSettingReceivedEvent += Logger_MeasurementSettingReceivedEvent;
    Logger.StartMeasuringMessageReceivedEvent += Logger_StartMeasuringMessageReceivedEvent;

    //SD�J�[�h�����o���̉���ԍX�V
    btnSDLogging.IsVisible = MLUtility.SDCardEnabled;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    if (MLXBee != null)
      MLXBee.SerialDataReceived -= MlXBee_SerialDataReceived;

    //MLogger�̃C�x���g����
    Logger.VersionReceivedEvent -= Logger_VersionReceivedEvent;
    Logger.MeasurementSettingReceivedEvent -= Logger_MeasurementSettingReceivedEvent;
    Logger.StartMeasuringMessageReceivedEvent -= Logger_StartMeasuringMessageReceivedEvent;
  }

  #endregion

  #region �ʐM����

  private void MlXBee_SerialDataReceived
    (object sender, SerialDataReceivedEventArgs e)
  {
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
  }

  private void Logger_MeasurementSettingReceivedEvent(object sender, EventArgs e)
  {
    settingLoaded = true;

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

      //�ҏW�v�f�̒��F�����Ƃɖ߂�
      resetTextColor();
    });
  }

  private void Logger_VersionReceivedEvent(object sender, EventArgs e)
  {
    verstionLoaded = true;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      spc_vers.Text = MLSResource.DS_SpecVersion + ": " + Logger.Version_Major + "." + Logger.Version_Minor + "." + Logger.Version_Revision;
    });
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  private void StartButton_Clicked(object sender, EventArgs e)
  {
    DataReceive drcv = new DataReceive();
    drcv.MLDevice = this.MLDevice;
    drcv.MLXBee = this.MLXBee;
    drcv.Logger = this.Logger;
    drcv.StartLogging();

    //����J�n�y�[�W��\��
    Navigation.PushAsync(drcv, true);
  }

  private void SaveButton_Clicked(object sender, EventArgs e)
  {
    //���̓G���[������ΏI��
    int thSpan, glbSpan, velSpan, luxSpan;
    if (!isInputsCorrect(out thSpan, out glbSpan, out velSpan, out luxSpan)) return;

    //�ݒ�R�}���h���쐬
    string sData = "CMS"
      + (cbx_th.IsToggled ? "t" : "f") + string.Format("{0,5}", thSpan)
      + (cbx_glb.IsToggled ? "t" : "f") + string.Format("{0,5}", glbSpan)
      + (cbx_vel.IsToggled ? "t" : "f") + string.Format("{0,5}", velSpan)
      + (cbx_lux.IsToggled ? "t" : "f") + string.Format("{0,5}", luxSpan)
      + string.Format("{0,10}", MLogger.GetUnixTime(ST_DTIME));

    Task.Run(() =>
    {
      try
      {
        //�ݒ�R�}���h�𑗐M
        MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\r" + sData + "\r"));
      }
      catch (Exception ex)
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", ex.Message, "OK");
        });
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
      alert += "�����x�̑���Ԋu�������ł͂���܂���\r\n";
    }
    if (!int.TryParse(ent_glb.Text, out glbSpan))
    {
      hasError = true;
      alert += "�O���[�u���x�̑���Ԋu�������ł͂���܂���\r\n";
    }
    if (!int.TryParse(ent_vel.Text, out velSpan))
    {
      hasError = true;
      alert += "�������̑���Ԋu�������ł͂���܂���\r\n";
    }
    if (!int.TryParse(ent_lux.Text, out luxSpan))
    {
      hasError = true;
      alert += "�Ɠx�̑���Ԋu�������ł͂���܂���\r\n";
    }

    if (hasError)
      DisplayAlert("Alert", alert, "OK");

    return !hasError;
  }

  private void LoadButton_Clicked(object sender, EventArgs e)
  {
    updateSetting();
  }

  private void updateSetting()
  {
    settingLoaded = false;

    Task.Run(async () =>
    {
      while (!settingLoaded)
      {
        try
        {
          //�ݒ���e�擾�R�}���h�𑗐M
          MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rLMS\r"));
          await Task.Delay(1000);
        }
        catch { }
      }
    });
  }

  private void loadVersion()
  {
    if (verstionLoaded) return;

    Task.Run(async () =>
    {
      while (!verstionLoaded)
      {
        try
        {
          //�o�[�W�����擾�R�}���h�𑗐M
          MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rVER\r"));
          await Task.Delay(1000);
        }
        catch { }
      }
    });
  }

  private void SDButton_Clicked(object sender, EventArgs e)
  {
    isStarted = false;
    Task.Run(async () =>
    {
      int tryNum = 0;
      while (!isStarted)
      {
        //5�񎸔s������G���[�Ŗ߂�
        if (5 <= tryNum)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            DisplayAlert("Alert", MLSResource.DR_FailStarting, "OK");
            //Navigation.PopAsync();
          });
        }
        tryNum++;

        try
        {
          //�J�n�R�}���h�𑗐M//xbee�ʐM����,bluetooth�ʐM�L��,sdcard�����o������(ftf)
          MLXBee.SendSerialData
          (Encoding.ASCII.GetBytes("\rSTL" + MLogger.GetUnixTime(DateTime.Now) + "fft\r"));
          await Task.Delay(500);
        }
        catch { }
      }
      Application.Current.Dispatcher.Dispatch(async() =>
      {
        await DisplayAlert("Alert", MLSResource.DR_StartLogging, "OK");
        await Navigation.PopAsync();
      });
    });
  }

  private void CFButton_Clicked(object sender, EventArgs e)
  {
    CFSetting cfs = new CFSetting();
    cfs.MLXBee = MLXBee;
    cfs.Logger = this.Logger;
    Navigation.PushAsync(cfs);
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

  /*
  private void stDate_Focused(object sender, FocusEventArgs e)
  { dt_org = stDate.Date; }

  private void stDate_Unfocused(object sender, FocusEventArgs e)
  { if (dt_org != stDate.Date) title2.TextColor = Colors.Red; }

  private void stTime_Focused(object sender, FocusEventArgs e)
  { tsp_org = stTime.Time; }

  private void stTime_Unfocused(object sender, FocusEventArgs e)
  { if (tsp_org != stTime.Time) title2.TextColor = Colors.Red; }
  */

  private void resetTextColor()
  {
    //lbl_th.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = title2.TextColor = Colors.Black;
    lbl_th.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Colors.DarkGreen;
  }

  #endregion

}