using MLS_Mobile.Resources.i18n;

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
        bool confirm = await DisplayAlert(
            MLSResource.LV_ClearLog,
            MLSResource.LV_ClearConfirmMessage,
            MLSResource.Yes,
            MLSResource.Cancel);
        if (!confirm) return;
        MLUtility.ClearLog();
        logEditor.Text = "";
    }

    private async void share_Clicked(object sender, EventArgs e)
    {
        string logText = MLUtility.ReadLog();
        if (logText == "") logText = MLSResource.LV_NoDataAvailable;
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text  = logText,
            Title = MLSResource.LV_ShareTitle + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        });
    }
}
