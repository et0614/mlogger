namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;
using MLLib;
using System.Text;

public partial class MLoggerScanner : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

  private bool bleChecked = false;

  /// <summary>XBee��T�����鎞��[msec]</summary>
  private const int SCAN_TIME = 1000;

  /// <summary>�T���ł���XBee�̃��X�g</summary>
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
    MLUtility.CloseXbee();

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

  private async void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    IDevice selectedXBee = (IDevice)e.CurrentSelection[0];
    mlList.SelectedItem = null;

    //MLDevice�ɐڑ�����
    showIndicator(MLSResource.SC_Connecting);

    //XBee�ڑ�
    string lowAddress;
    try
    {
      lowAddress = await Task.Run(string () =>
      { return MLUtility.OpenXbee(selectedXBee); });
    }
    catch (Exception ex)
    {
      await DisplayAlert("Alert", "Can't open XBee connection." + Environment.NewLine + ex.Message, "OK");
      hideIndicator();
      return;
    }

    //MLogger�̏ꍇ***
    if (MLUtility.ConnectedDevice == MLUtility.MLDevice.MLogger)
    {
      try
      {
        await Task.Run(async() =>
        {
          //�o�[�W�������擾����
          for (int i = 0; i < 10; i++)
          {
            if (MLUtility.Logger.HasVersionReceived) return;
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand()));
            await Task.Delay(100);
          }
        });
      }
      catch (Exception ex)
      {
        await DisplayAlert("Alert", "Can't load MLogger version." + Environment.NewLine + ex.Message, "OK");
        hideIndicator();
        return;
      }
      //��ʑJ��
      if (MLUtility.Logger.HasVersionReceived)
        await Shell.Current.GoToAsync(nameof(DeviceSetting), new Dictionary<string, object> { { "mlLowAddress", lowAddress } });
      else
      {
        await DisplayAlert("Alert", "Can't load MLogger version.", "OK");
        await Task.Run(MLUtility.CloseXbee);
      }
    }

    //Transciever�̏ꍇ***
    else if (MLUtility.ConnectedDevice == MLUtility.MLDevice.MLTransciever)
    {
      //Bluetooth�]�����[�h�L����
      try
      {
        await Task.Run(async () =>
        {
          MLUtility.Transceiver.HasRelayToBluetoothReceived = false;
          for (int i = 0; i < 10; i++)
          {
            if (MLUtility.Transceiver.HasRelayToBluetoothReceived) return;
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeRelayToBluetoothCommand()));
            await Task.Delay(100);
          }
        });
      }
      catch (Exception ex)
      {
        await DisplayAlert("Alert", "Can't enable Bluetooth relay." + Environment.NewLine + ex.Message, "OK");
        hideIndicator();
        return;
      }
      if (!MLUtility.Transceiver.HasRelayToBluetoothReceived)
      {
        await DisplayAlert("Alert", "Can't enable Bluetooth relay.", "OK");
        await Task.Run(MLUtility.CloseXbee);
        hideIndicator();
        return;
      }

      //���ݎ����X�V
      try
      {
        await Task.Run(async() =>
        {
          MLUtility.Transceiver.HasUpdateCurrentTimeReceived = false;
          for (int i = 0; i < 10; i++)
          {
            if (MLUtility.Transceiver.HasUpdateCurrentTimeReceived) return;
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeUpdateCurrentTimeCommand(DateTime.Now)));
            await Task.Delay(100);
          }
        });
      }
      catch (Exception ex)
      {
        await DisplayAlert("Alert", "Can't update current date and time." + Environment.NewLine + ex.Message, "OK");
        hideIndicator();
        return;
      }
      if (!MLUtility.Transceiver.HasUpdateCurrentTimeReceived)
      {
        await DisplayAlert("Alert", "Can't update current date and time.", "OK");
        await Task.Run(MLUtility.CloseXbee);
        hideIndicator();
        return;
      }

      //Bluetooth�L����+���ݎ����X�V�������������ʑJ��
      await DisplayAlert("", "Current time has been updated.", "OK");
      await Shell.Current.GoToAsync(nameof(RelayedDataViewer));
    }

    hideIndicator();
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