using MLLib;
using System.Collections.ObjectModel;
using System.Text;

namespace MLS_Mobile;

public partial class RelayedDataViewer : ContentPage
{

  #region インスタンス変数・プロパティ

  public ObservableCollection<MLoggerViewModel> MLoggerViewModelList
  { get; private set; } = new ObservableCollection<MLoggerViewModel>();

  private List<ImmutableMLogger> mLoggers = new List<ImmutableMLogger>();

  #endregion

	public RelayedDataViewer()
	{
		InitializeComponent();

    this.BindingContext = this;

    //Transcieverの日時を更新
    Task.Run(async () =>
    {
      while (!MLUtility.Transceiver.HasUpdateCurrentTimeReceived)
      {
        try
        {
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeUpdateCurrentTimeCommand(DateTime.Now)));
          await Task.Delay(500);
        }
        catch { }
      }
    });
  }

  #region ロード・アンロードイベント
  protected override void OnAppearing()
  {
    base.OnAppearing();

    //スリープ禁止
    DeviceDisplay.Current.KeepScreenOn = true;

    //イベント登録
    if (MLUtility.Transceiver != null)
    {
      mLoggers.Clear();
      MLoggerViewModelList.Clear();
      MLUtility.Transceiver.NewMLoggerDetectedEvent += Transciever_NewMLoggerDetectedEvent;
      MLUtility.Transceiver.CommandRelayEvent += Transciever_CommandRelayEvent;
    }
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //スリープ解除
    DeviceDisplay.Current.KeepScreenOn = false;

    //イベント解除
    if (MLUtility.Transceiver != null)
    {
      MLUtility.Transceiver.NewMLoggerDetectedEvent -= Transciever_NewMLoggerDetectedEvent;
      MLUtility.Transceiver.CommandRelayEvent -= Transciever_CommandRelayEvent;
      mLoggers.Clear();
      MLoggerViewModelList.Clear();
    }
  }

  #endregion

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

    /*//名称を読み込む
    Task.Run(async () =>
    {
      while (!logger.HasLoggerNameReceived)
      {
        try
        {
          if (MLUtility.ConnectedXBee.IsConnected)
            MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(
              MLTransceiver.MakeRelayCommand(logger.LowAddress, MLogger.MakeLoadLoggerNameCommand())
              ));
          
          await Task.Delay(500);
        }
        catch { }
      }
    });*/
  }

  private void mlvList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    MLoggerViewModel selectedMLoggerViewModel = (MLoggerViewModel)e.CurrentSelection[0];
    mlvList.SelectedItem = null;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      Shell.Current.GoToAsync(nameof(DataReceive),
        new Dictionary<string, object> { { "mlLowAddress", selectedMLoggerViewModel.Logger.LowAddress } }
        );
    });
  }
}