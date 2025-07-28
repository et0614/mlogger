namespace MLS_Mobile;

using Microsoft.Extensions.Logging;
using MLLib;
using MLS_Mobile.Resources.i18n;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

public partial class MLoggerScanner : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

  private bool bleChecked = false;

  /// <summary>XBee��T�����鎞��[msec]</summary>
  private const int SCAN_TIME = 2000;

  /// <summary>�T���ł���XBee�̃��X�g</summary>
  public ObservableCollection<IDeviceViewModel> MLXBees { get; private set; } = new ObservableCollection<IDeviceViewModel>();

  #endregion

  #region �R���X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
  public MLoggerScanner()
  {
    InitializeComponent();

    BindingContext = this;

    //�C�x���g�o�^
    CrossBluetoothLE.Current.Adapter.DeviceDiscovered += (s, ev) =>
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
        if (newItem) MLXBees.Add(
          new IDeviceViewModel() { Device = ev.Device });
      }
    };

    //�X�L�������Ԍo�ߌ�
    CrossBluetoothLE.Current.Adapter.ScanTimeoutElapsed += (s, ev) =>
    {
      refView.IsRefreshing = false;
    };

    //���t���b�V���R�}���h��`
    ICommand refreshCommand = new Command(() =>
    {
      if (!CrossBluetoothLE.Current.Adapter.IsScanning)
        scanXBees();
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

    //BLE�L�������m�F�iAndroid�̂݁j
    await checkBLEPermission();
  }

  private void scanXBees()
  {
    //�ڑ��ς݂�XBee������ꍇ�ɂ͉���
    MLUtility.CloseXbee();

    //�A�_�v�^��p��
    IAdapter adapter = CrossBluetoothLE.Current.Adapter;

    //�X�L�����ݒ�
    adapter.ScanTimeout = SCAN_TIME;
    adapter.ScanMode = ScanMode.LowLatency;

    //�񓯊��X�L�����J�n
    MLXBees.Clear();
    adapter.StartScanningForDevicesAsync();
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  private async void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    IDevice selectedXBee = ((IDeviceViewModel)e.CurrentSelection[0]).Device;
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
      //�C�x���g�ҋ@�^�X�N���쐬
      var tcs = new TaskCompletionSource<bool>();

      //�C�x���g������������^�X�N������������n���h�����ꎞ�I�ɓo�^
      EventHandler handler = (s, e) => tcs.TrySetResult(true);
      MLUtility.Logger.VersionReceivedEvent += handler;

      try
      {
        //�R�}���h�𑗐M (�^�C���A�E�g���l�����Đ���J��Ԃ�)
        for (int i = 0; i < 5 && !tcs.Task.IsCompleted; i++)
        {
          try
          {
            await Task.Run(() => MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeGetVersionCommand())));
          }
          catch { }

          //�C�x���g�����邩�A�^�C���A�E�g(100ms)����܂ő҂�
          await Task.WhenAny(tcs.Task, Task.Delay(100));
        }

        //�^�X�N������Ɋ��������ꍇ�̂�
        if (tcs.Task.IsCompletedSuccessfully)
        {
          await Shell.Current.GoToAsync(nameof(DeviceSetting), new Dictionary<string, object> { { "mlLowAddress", lowAddress } });
        }
        else
        {
          await DisplayAlert("Alert", "Can't load MLogger version.", "OK");
          await Task.Run(MLUtility.CloseXbee);
        }
      }
      finally
      {
        //�n���h��������
        MLUtility.Logger.VersionReceivedEvent -= handler;
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
    if (!bleChecked)
    {

#if ANDROID
      //Bluetooth�Ɋւ�錠��
      var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
      if (status == PermissionStatus.Granted) return;
      if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
        await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_Bluetooth, "OK");

      status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

      //�t�߂̃f�o�C�X�iNearByDevice�j�Ɋւ�錠��
      status = await Permissions.CheckStatusAsync<PermissionNearByDevice>();
      if (status == PermissionStatus.Granted) return;
      if (Permissions.ShouldShowRationale<PermissionNearByDevice>())
        await Shell.Current.DisplayAlert("Needs permissions", MLSResource.SC_NearByDevice, "OK");

      status = await Permissions.RequestAsync<PermissionNearByDevice>();

#endif

      bleChecked = true;
    }
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

/// <summary>
/// �t�߂̃f�o�C�X�iNearByDevice�j�̌����ݒ��ʗp
/// </summary>
internal class PermissionNearByDevice : Permissions.BasePlatformPermission
{
#if ANDROID
  public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    new List<(string androidPermission, bool isRuntime)>
    {
        // Near By Device�͈ȉ��̌�����v������悤�ɂ���Ɛݒ��ʂ��o����
       (global::Android.Manifest.Permission.BluetoothScan,true),
        (global::Android.Manifest.Permission.BluetoothConnect,true),
        (global::Android.Manifest.Permission.BluetoothAdvertise,true)
    }.ToArray();
#endif
}
