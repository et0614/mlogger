using CommunityToolkit.Maui.Views;

namespace MLS_Mobile;

public partial class TextInputPopup : Popup
{
	public TextInputPopup(string labelText, string entryValue, Keyboard key)
	{
		InitializeComponent();

    LabelText = labelText;
    EntryValue = entryValue;
    Key = key;

    BindingContext = this;
  }

  public string EntryValue { get; set; }

  public Keyboard Key { get; set; }

  public string LabelText { get; set; }


  private void btnOK_Clicked(object sender, EventArgs e)
  {
    Close(entName.Text);
  }

  private void btnCancel_Clicked(object sender, EventArgs e)
  {
    Close(null);
  }

}