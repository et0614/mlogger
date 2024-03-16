namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;
using DigiIoT.Maui.Devices.XBee;
using MLLib;
using System.Text;

public partial class MLoggerScanner : ContentPage
{

  #region �񋓌^��`

  /// <summary>�ڑ���̃f�o�C�X</summary>
  private enum ConnectedDevice
  {
    /// <summary>�ڑ��Ȃ�</summary>
    None,
    /// <summary>MLogger</summary>
    MLogger,
    /// <summary>MLTransciever</summary>
    MLTransciever
  }

  #endregion

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

  /// <summary>MLogger�t����XBee�̃p�X���[�h</summary>
  private const string ML_PASS = "ml_pass";

  /// <summary>�ڑ���̃f�o�C�X</summary>
  private ConnectedDevice cnctDevice = ConnectedDevice.None;

  private bool bleChecked = false;

  /// <summary>XBee��T�����鎞��[msec]</summary>
  private const int SCAN_TIME = 1000;

  private XBeeBLEDevice connectedXBee;

  private MLogger mLogger;

  private MLTransceiver mlTransceiver;

  /// <summary>MLogger���ڂ�XBee�̃��X�g</summary>
  public ObservableCollection<IDevice> MLXBees { get; private set; } = new ObservableCollection<IDevice>();

  #endregion

  #region �R���X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
  public MLoggerScanner()
  {
    InitializeComponent();

    BindingContext = this;

    //���t���b�V���R�}���h��`
    ICommand refreshCommand = new Command(() =>
    {
      scanXBees();
      refView.IsRefreshing = false;
    });
    refView.Command = refreshCommand;
  }

  #endregion

  #region ���[�h�E�A�����[�h�C�x���g

  protected override async void OnAppearing()
  {
    base.OnAppearing();

    //�X�L�������s
    refView.Command.Execute(null);

    if (!bleChecked)
    {
      await checkBLEPermission();
      bleChecked = true;
    }
  }

  private void scanXBees()
  {
    //�ڑ��ς݂�XBee������ꍇ�ɂ͉���
    endXBeeCommunication();

    //Bluetooth��p��
    IBluetoothLE bluetoothLE = CrossBluetoothLE.Current;

    //�A�_�v�^��p��
    IAdapter adapter = bluetoothLE.Adapter;

    //�X�L�������łȂ���΃X�L�����J�n
    if (adapter.IsScanning) return;

    //�X�L�����ݒ�
    adapter.ScanTimeout = SCAN_TIME;
    adapter.ScanMode = ScanMode.LowLatency;

    //BLE�f�o�C�X�������������̏���
    adapter.DeviceDiscovered += (s, ev) =>
    {
      string dvName = ev.Device.Name;
      if (dvName != null && dvName != "" && (dvName.StartsWith("MLogger_") || dvName.StartsWith("MLTransceiver")))
      {
        bool newItem = true;
        for (int i = 0; i < MLXBees.Count; i++)
        {
          if (MLXBees[i].Name == dvName)
          {
            newItem = false;
            break;
          }
        }
        if (newItem) MLXBees.Add(ev.Device);
      }
    };

    //�񓯊��X�L�����J�n
    MLXBees.Clear();
    adapter.StartScanningForDevicesAsync();
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  private void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    IDevice selectedXBee = (IDevice)e.CurrentSelection[0];
    mlList.SelectedItem = null;

    //�C���W�P�[�^��\�����Đ���s�ɂ��Ă���ڑ�����
    showIndicator(MLSResource.SC_Connecting);
    Task.Run(async () =>
    {
      try
      {
        startXBeeCommunication(selectedXBee);

        //MLogger�̏ꍇ�FOpen�ɐ���������ݒ�y�[�W�ֈړ�
        if (cnctDevice == ConnectedDevice.MLogger)
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            Shell.Current.GoToAsync(nameof(DeviceSetting),
                new Dictionary<string, object> { { "mLogger", mLogger }, { "xbee", connectedXBee } }
                );
          });
        }
        //Transciever�̏ꍇ
        else if (cnctDevice == ConnectedDevice.MLTransciever)
        {
          //Bluetooth�]�����[�h��L���ɂ���
          mlTransceiver.HasRelayedToBluetoothReceived = false;
          int tryNum = 0;
          while ((!mlTransceiver.HasRelayedToBluetoothReceived) && tryNum < 4 )
          {
            try
            {
              //Bluetooth�]���R�}���h�𑗐M
              connectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeRelayToBluetoothCommand()));
              await Task.Delay(1000);
            }
            catch { }
            tryNum++;
          }

          //Bluetooth�]�����[�h��L���ɂł����ꍇ
          if (mlTransceiver.HasRelayedToBluetoothReceived)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              Shell.Current.GoToAsync(nameof(RelayedDataViewer),
                  new Dictionary<string, object> { { "mlTransceiver", mlTransceiver } }
                  );
            });
          }
          else
          {
            endXBeeCommunication();
            Application.Current.Dispatcher.Dispatch(() =>
            { DisplayAlert("Alert", "Connection failed.", "OK"); });
          }
        }
      }
      catch (Exception bex)
      {
        //���s�����ꍇ�ɂ̓G���[���b�Z�[�W���o��
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DisplayAlert("Alert", bex.Message, "OK");
        });
      }
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

  #region XBee�ʐM�֘A�̏���

  /// <summary>MLogger��XBee�Ɛڑ�����</summary>
  /// <param name="device"></param>
  private void startXBeeCommunication(IDevice device)
  {
    //�ʐM����XBee������ꍇ�͐ڑ������
    endXBeeCommunication();

    if (DeviceInfo.Current.Platform == DevicePlatform.Android)
      connectedXBee = new XBeeBLEDevice(device.Id.ToString(), ML_PASS);
    else connectedXBee = new XBeeBLEDevice(device, ML_PASS);

    //XBee��Open
    connectedXBee.Connect();

    //�ڑ���:MLogger
    if (device.Name.StartsWith("MLogger_"))
    {
      cnctDevice = ConnectedDevice.MLogger;
      mLogger = new MLogger(connectedXBee.GetAddressString());
      mLogger.LocalName = device.Name;
    }
    //�ڑ���:MLTransceiver
    else if (device.Name.StartsWith("MLTransceiver"))
    {
      cnctDevice = ConnectedDevice.MLTransciever;
      mlTransceiver = new MLTransceiver(connectedXBee.GetAddressString());
    }

    //�C�x���g�o�^      
    connectedXBee.SerialDataReceived += ConnectedXBee_SerialDataReceived;
  }

  private void ConnectedXBee_SerialDataReceived
    (object sender, XBeeLibrary.Core.Events.Relay.SerialDataReceivedEventArgs e)
  {
    if (cnctDevice == ConnectedDevice.MLogger)
    {
      mLogger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      //�R�}���h����
      while (mLogger.HasCommand)
      {
        try
        {
          mLogger.SolveCommand();
        }
        catch { }
      }
    }
    else if (cnctDevice == ConnectedDevice.MLTransciever)
    {
      mlTransceiver.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      //�R�}���h����
      while (mlTransceiver.HasCommand)
      {
        try
        {
          mlTransceiver.SolveCommand();
        }
        catch { }
      }
    }
  }

  /// <summary>MLDevice��Xbee�Ƃ̐ڑ�����������</summary>
  private void endXBeeCommunication()
  {
    //�ʐM����XBee������ꍇ�͐ڑ������
    if (connectedXBee != null)
    {
      //�C�x���g����������
      connectedXBee.SerialDataReceived -= ConnectedXBee_SerialDataReceived;

      //�J���Ă���ΕʃX���b�h�ŕ���
      if (connectedXBee.IsConnected)
      {
        XBeeBLEDevice clsBee = connectedXBee;
        Task.Run(() =>
        {
          try
          {
            clsBee.Disconnect();
          }
          catch { }
        });
      }
    }

    mLogger = null;
    mlTransceiver = null;
    cnctDevice = ConnectedDevice.None;
  }

  #endregion


  private async Task checkBLEPermission()
  {
#if ANDROID
    var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    if (status == PermissionStatus.Granted) return;

    if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
      await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_Bluetooth, "OK");

    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
#endif
  }

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