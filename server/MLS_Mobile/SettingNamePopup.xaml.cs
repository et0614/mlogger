using Mopups.Services;

namespace MLS_Mobile;

public partial class SettingNamePopup
{

  private string initialName;

	public bool HasChanged { get { return initialName != Name; } }

	public string Name { get; set; }

	public SettingNamePopup(string name)
	{
		InitializeComponent();

    initialName = Name = name;

    BindingContext = this;
	}

  private void btnOK_Clicked(object sender, EventArgs e)
  {
    MopupService.Instance.PopAsync();
  }

  private void btnCancel_Clicked(object sender, EventArgs e)
  {
    Name = initialName;
    MopupService.Instance.PopAsync();
  }
  
}