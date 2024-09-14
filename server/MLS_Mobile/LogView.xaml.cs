namespace MLS_Mobile;

public partial class LogView : ContentPage
{
	public LogView()
	{
		InitializeComponent();
	}

  protected override void OnAppearing()
  {
    base.OnAppearing();

    logLabel.Text = MLUtility.ReadLog();
  }

  private async void share_Clicked(object sender, EventArgs e)
  {
    await Share.Default.RequestAsync(new ShareTextRequest
    {
      Text = MLUtility.ReadLog(),
      Title = "MLS_Mobile log data"
    });
  }
}