using Mopups.Services;

namespace MLS_Mobile;

public partial class SettingPopup
{

  private string initialValue;

  public int PopID { get; private set; }

	public bool HasChanged { get { return initialValue != ChangedValue; } }

	public string ChangedValue { get; set; }

  public SettingPopup(int id, string message, string defaultValue, Keyboard keyboard)
  {
    InitializeComponent();

    PopID = id;
    lblMessage.Text = message;
    initialValue = ChangedValue = defaultValue;
    entName.Keyboard = keyboard;

    BindingContext = this;
  }

  private void btnOK_Clicked(object sender, EventArgs e)
  {
    MopupService.Instance.PopAsync();
  }

  private void btnCancel_Clicked(object sender, EventArgs e)
  {
    ChangedValue = initialValue;
    MopupService.Instance.PopAsync();
  }
  
}