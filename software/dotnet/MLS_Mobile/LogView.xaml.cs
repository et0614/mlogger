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
        logEditor.Text = MLUtility.ReadLog();
    }

    private async void clear_Clicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Clear log", "Delete all log entries?", "Yes", "Cancel");
        if (!confirm) return;
        MLUtility.ClearLog();
        logEditor.Text = "";
    }

    private async void share_Clicked(object sender, EventArgs e)
    {
        string logText = MLUtility.ReadLog();
        if (logText == "") logText = "No log data available.";
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = logText,
            Title = "M-Logger log " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        });
    }
}
