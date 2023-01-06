namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;

public partial class MLoggerScanner : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

  /// <summary>XBee��T�����鎞��[msec]</summary>
  private const int SCAN_TIME = 1000;

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

  protected override void OnAppearing()
  {
    base.OnAppearing();

    //�X�L�������s
    refView.Command.Execute(null);
  }

  private void scanXBees()
  {
    //�ڑ��ς݂�XBee������ꍇ�ɂ͉���
    MLUtility.EndXBeeCommunication();

    //Bluetooth��p��
    IBluetoothLE bluetoothLE = CrossBluetoothLE.Current;
    if (bluetoothLE.State == BluetoothState.Off)
    {
      DisplayAlert("Alert", MLSResource.SC_Bluetooth, "OK");
      return;
    }

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
      if (dvName != null && dvName != "" && dvName.StartsWith("MLogger_"))
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
        if (newItem)
          MLXBees.Add(ev.Device);
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
    Task.Run(() =>
    {
      try
      {
        MLUtility.StartXBeeCommunication(selectedXBee);

        //Open�ɐ���������ݒ�y�[�W�ֈړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(DeviceSetting));
        });
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