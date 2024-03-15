using Microsoft.Extensions.Logging;
using MLLib;
using System.Collections.ObjectModel;

namespace MLS_Mobile;

[QueryProperty(nameof(Transciever), "mlTransceiver")]
public partial class RelayedDataViewer : ContentPage
{
  private MLTransceiver _transciever;

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
        _transciever.NewMLoggerDetectedEvent -= Transciever_NewMLoggerDetectedEvent;

      _transciever = value;
      initInfo();
    }
  }

  /// <summary>MLogger���ڂ�XBee�̃��X�g</summary>
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
  }

  private void Transciever_NewMLoggerDetectedEvent(object sender, EventArgs e)
  {
    MLoggers.Add(((NewMLoggerDetectedEventArgs)e).Logger);
  }
}