using MLLib;
using MLS_Mobile.Resources.i18n;
using System.Collections.ObjectModel;
using System.Text;

namespace MLS_Mobile;

public partial class RelayedDataViewer : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  public ObservableCollection<MLoggerViewModel> MLoggerViewModelList
  { get; private set; } = new ObservableCollection<MLoggerViewModel>();

  private List<ImmutableMLogger> mLoggers = new List<ImmutableMLogger>();

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

    //�C�x���g�o�^
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

    //�X���[�v����
    DeviceDisplay.Current.KeepScreenOn = false;

    //�C�x���g����
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
    MLoggerViewModelList.Add(mvm); //�����ŃG���[
  }

}