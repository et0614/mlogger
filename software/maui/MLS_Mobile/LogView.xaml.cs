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
    string logText = MLUtility.ReadLog();
    if(logText == "") logText = "No log data available.";
    await Share.Default.RequestAsync(new ShareTextRequest
    {
      Text = logText,
      Title = "MLS_Mobile log data"
    });
  }
}