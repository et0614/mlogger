using MLLib;
using System.Collections.ObjectModel;

namespace MLS_Mobile;

[QueryProperty(nameof(Transciever), "mlTransceiver")]
public partial class RelayedDataViewer : ContentPage
{

  #region インスタンス変数・プロパティ

  public ObservableCollection<MLoggerViewModel> MLoggerViewModelList
  { get; private set; } = new ObservableCollection<MLoggerViewModel>();

  private MLTransceiver _transciever;

  private List<ImmutableMLogger> mLoggers = new List<ImmutableMLogger>();

  /// <summary>データを受信するMLTranscieverを設定・取得する</summary>
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


  #endregion

  /// <summary>MLogger搭載のXBeeのリスト</summary>
  public ObservableCollection<ImmutableMLogger> MLoggers { get; private set; } = 
    new ObservableCollection<ImmutableMLogger>();

	public RelayedDataViewer()
	{
		InitializeComponent();

    this.BindingContext = this;
  }

  private void initInfo()
  {
    Transciever.NewMLoggerDetectedEvent += Transciever_NewMLoggerDetectedEvent;
    Transciever.CommandRelayEvent += Transciever_CommandRelayEvent;
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
    MLoggerViewModel mvm = new MLoggerViewModel();
    mvm.Logger = logger;
    MLoggerViewModelList.Add(mvm);
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