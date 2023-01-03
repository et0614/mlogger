namespace MLS_Mobile;

using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using XBeeLibrary.Xamarin;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

using MLLib;

using MLS_Mobile.Resources.i18n;
using System.Windows.Input;

public partial class MLoggerScanner : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

  /// <summary>XBee��T�����鎞��[msec]</summary>
  private const int SCAN_TIME = 1000;

  private const string ML_PASS = "ml_pass";

  private readonly ObservableCollection<xBee> xbees = new ObservableCollection<xBee>();

  ZigBeeBLEDevice MLXBee;

  #endregion

  #region �R���X�g���N�^

  public MLoggerScanner()
  {
    InitializeComponent();

    Title = MLSResource.SC_Title;
    mlList.ItemsSource = xbees;

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
    //�ڑ��ς݂�XBee������ꍇ�ɂ́A�ʃX���b�h�Őڑ�������
    if (MLXBee != null && MLXBee.IsOpen)
    {
      ZigBeeBLEDevice cls = MLXBee;
      Task.Run(() =>
      {
        try
        {
          cls.Close();
        }
        catch { }
      });
    }

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
        for (int i = 0; i < xbees.Count; i++)
        {
          if (xbees[i].Name == dvName)
          {
            newItem = false;
            break;
          }
        }
        if (newItem)
          xbees.Add(new xBee(dvName, ev.Device.Id));
      }
    };

    //�񓯊��X�L�����J�n
    xbees.Clear();
    adapter.StartScanningForDevicesAsync();
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //Bluetooth��p��
    /*IBluetoothLE bluetoothLe = CrossBluetoothLE.Current;
    if (bluetoothLe.State == BluetoothState.Off)
    {
      DisplayAlert("Alert", "Bluetooth��L���ɂ��Ă�������", "OK");
      return;
    }*/

    //�A�_�v�^��p�ӁB�X�L�������Ȃ�Β�~
    /*IAdapter adapter = bluetoothLe.Adapter;
    if (adapter.IsScanning)
      adapter.StopScanningForDevicesAsync();*/
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  private void mlList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
  {
    if (e.SelectedItem == null) return;

    xBee selectedXBee = (xBee)e.SelectedItem;
    mlList.SelectedItem = null;

    if (xbees.Count == 1 && xbees[0].Name == MLSResource.SC_Empty)
      return;

    //�C���W�P�[�^��\�����Đ���s�ɂ���
    showIndicator(MLSResource.SC_Connecting);

    Task.Run(async () =>
    {
      try
      {
        //BLE Device�ɐڑ�
        IAdapter adapter = CrossBluetoothLE.Current.Adapter;
        IDevice mlDevice = await adapter.ConnectToKnownDeviceAsync(selectedXBee.Id);

        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
          MLXBee = new ZigBeeBLEDevice(selectedXBee.Id.ToString(), ML_PASS);
        else MLXBee = new ZigBeeBLEDevice(mlDevice, ML_PASS);

        //XBee��Open
        MLXBee.Open();

        //Open�ɐ���������ݒ�y�[�W�ֈړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DeviceSetting dvset = new DeviceSetting();
          dvset.MLXBee = MLXBee;
          dvset.MLDevice = mlDevice;
          dvset.Logger = loadMLogger(MLXBee.GetAddressString(), selectedXBee.Name);
          mlList.SelectedItem = null; //�I������

          dvset.InitializeMLogger();

          Navigation.PushAsync(dvset, true);
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

  private void mlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    xBee selectedXBee = (xBee)e.CurrentSelection[0];
    mlList.SelectedItem = null;

    if (xbees.Count == 1 && xbees[0].Name == MLSResource.SC_Empty)
      return;

    //�C���W�P�[�^��\�����Đ���s�ɂ���
    showIndicator(MLSResource.SC_Connecting);

    Task.Run(async () =>
    {
      try
      {
        //BLE Device�ɐڑ�
        IAdapter adapter = CrossBluetoothLE.Current.Adapter;
        IDevice mlDevice = await adapter.ConnectToKnownDeviceAsync(selectedXBee.Id);

        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
          MLXBee = new ZigBeeBLEDevice(selectedXBee.Id.ToString(), ML_PASS);
        else MLXBee = new ZigBeeBLEDevice(mlDevice, ML_PASS);

        //XBee��Open
        MLXBee.Open();

        //Open�ɐ���������ݒ�y�[�W�ֈړ�
        Application.Current.Dispatcher.Dispatch(() =>
        {
          DeviceSetting dvset = new DeviceSetting();
          dvset.MLXBee = MLXBee;
          dvset.MLDevice = mlDevice;
          dvset.Logger = loadMLogger(MLXBee.GetAddressString(), selectedXBee.Name);
          mlList.SelectedItem = null; //�I������

          dvset.InitializeMLogger();

          Navigation.PushAsync(dvset, true);
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

  private MLogger loadMLogger(string address, string name)
  {
    MLogger logger = new MLogger(address);
    logger.LocalName = name;
    return logger;
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

  #region �C���i�[�N���X�̒�`

  private class xBee
  {

    public string Name { get; private set; }

    public Guid Id { get; private set; }

    public xBee(string name, Guid id)
    {
      Name = name;
      Id = id;
    }

  }

  #endregion


}