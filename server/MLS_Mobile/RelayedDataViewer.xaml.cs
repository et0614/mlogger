using DigiIoT.Maui.Devices.XBee;
using MLLib;
using System.Collections.ObjectModel;
using System.Text;

namespace MLS_Mobile;

[QueryProperty(nameof(Transciever), "mlTransceiver")]
[QueryProperty(nameof(ConnectedXBee), "xbee")]
public partial class RelayedDataViewer : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  public ObservableCollection<MLoggerViewModel> MLoggerViewModelList
  { get; private set; } = new ObservableCollection<MLoggerViewModel>();

  private MLTransceiver _transciever;

  private List<ImmutableMLogger> mLoggers = new List<ImmutableMLogger>();

  /// <summary>�f�[�^����M����MLTransciever��ݒ�E�擾����</summary>
  public MLTransceiver Transciever
  {
    get
    {
      return _transciever;
    }
    set
    {
      if (_transciever != null)
      {
        _transciever.NewMLoggerDetectedEvent -= Transciever_NewMLoggerDetectedEvent;
        _transciever.CommandRelayEvent -= Transciever_CommandRelayEvent;
        mLoggers.Clear();
        MLoggerViewModelList.Clear();
      }

      _transciever = value;
      initInfo();
    }
  }

  /// <summary>�R�}���h���M�p��XBee��ݒ�E�擾����</summary>
  public XBeeBLEDevice ConnectedXBee { get; set; }

  #endregion

	public RelayedDataViewer()
	{
		InitializeComponent();

    this.BindingContext = this;
  }

  #region ���[�h�E�A�����[�h�C�x���g
  protected override void OnAppearing()
  {
    base.OnAppearing();

    //�X���[�v�֎~
    DeviceDisplay.Current.KeepScreenOn = true;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //�X���[�v����
    DeviceDisplay.Current.KeepScreenOn = false;
  }

  #endregion

  private void initInfo()
  {
    Transciever.NewMLoggerDetectedEvent += Transciever_NewMLoggerDetectedEvent;
    Transciever.CommandRelayEvent += Transciever_CommandRelayEvent;

    //�ڑ�����Transciever�̓������X�V
    Task.Run(async () =>
    {
      while (!Transciever.HasUpdateCurrentTimeReceived)
      {
        try
        {
          ConnectedXBee?.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeUpdateCurrentTimeCommand(DateTime.Now)));
          await Task.Delay(500);
        }
        catch { }
      }
    });
  }

  private void Transciever_CommandRelayEvent(object sender, EventArgs e)
  {
    tryToAddNewLogger(((MLTransceiverEventArgs)e).Logger);
  }

  private void Transciever_NewMLoggerDetectedEvent(object sender, EventArgs e)
  {
    tryToAddNewLogger(((MLTransceiverEventArgs)e).Logger);
  }

  private void tryToAddNewLogger(ImmutableMLogger logger)
  {
    if (mLoggers.Contains(logger)) return;

    mLoggers.Add(logger);
    MLoggerViewModel mvm = new() { Logger = logger };
    mvm.IsEnabled = false;
    MLoggerViewModelList.Add(mvm);

    //���̂�ǂݍ���
    Task.Run(async () =>
    {
      while (!logger.HasLoggerNameReceived)
      {
        try
        {
          ConnectedXBee?.SendSerialData(Encoding.ASCII.GetBytes(
            MLTransceiver.MakeRelayCommand(logger.LowAddress, MLogger.MakeLoadLoggerNameCommand())
            ));
          
          await Task.Delay(500);
        }
        catch { }
      }
    });
  }

  private void mlvList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    MLoggerViewModel selectedMLoggerViewModel = (MLoggerViewModel)e.CurrentSelection[0];
    mlvList.SelectedItem = null;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      Shell.Current.GoToAsync(nameof(DataReceive),
        new Dictionary<string, object> { { "mLogger", selectedMLoggerViewModel.Logger } }
        );
    });
  }
}