namespace MLS_Mobile;

using System.Text;

using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;

using MLS_Mobile.Resources.i18n;

public partial class LoggingData : ContentPage
{

  public string FileName { get; set; }

  public LoggingData()
	{
		InitializeComponent();

    btnCopy.Text = MLSResource.LD_Copy;
    btnDelete.Text = MLSResource.LD_Delete;
  }

  public void LoadData(string fileName)
  {
    FileName = fileName;
    lblFname.Text = "File: " + fileName;

    string header = MLSResource.DateAndTime + "," +
      MLSResource.DrybulbTemperature + "," +
      MLSResource.RelativeHumidity + "," +
      MLSResource.GlobeTemperature + "," +
      MLSResource.Velocity + "," +
      MLSResource.Illuminance + "," +
      MLSResource.GlobeTemperatureVoltage + "," +
      MLSResource.VelocityVoltage + "," +
      MLSResource.MetabolicUnit + "," +
      MLSResource.ClothingUnit + Environment.NewLine;
    lbl_data.Text = header + MLUtility.LoadDataFile(fileName);
  }

  private void copy_Clicked(object sender, EventArgs e)
  {
    Clipboard.Default.SetTextAsync(lbl_data.Text);
  }

  private async void delete_Clicked(object sender, EventArgs e)
  {
    bool remove = await DisplayAlert("Alert", MLSResource.LD_DeleteAlert, "OK", "Cancel");
    if (remove)
    {
      MLUtility.DeleteDataFile(FileName);
      await Navigation.PopAsync();
    }
  }

}