using MLLib;
using MLS_Mobile.Resources.i18n;
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
    MLoggerViewModelList.Add(mvm); //ここでエラー
  }

}