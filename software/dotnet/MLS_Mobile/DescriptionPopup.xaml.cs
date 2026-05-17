using CommunityToolkit.Maui.Views;

namespace MLS_Mobile;

public partial class DescriptionPopup : Popup
{
	public DescriptionPopup(string labelText)
	{
		InitializeComponent();

    LabelText = labelText;

    BindingContext = this;
  }

  public string LabelText { get; set; }

}