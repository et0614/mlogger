namespace MLS_Mobile;

using System.Collections.ObjectModel;

using System.Text;

using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;

using MLS_Mobile.Resources.i18n;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE;
using XBeeLibrary.Xamarin;

public partial class LoggingData : ContentPage
{

  #region インスタンス変数・プロパティ

  public string FileName { get; set; }

  public string MData { get; private set; }

  private bool isFormatted = false;

  #endregion

  #region コンストラクタ

  public LoggingData(string fileName)
	{
		InitializeComponent();

    FileName = fileName;
    lblFname.Text = "File: " + fileName;
    MData = 
      MLSResource.Date + "," + MLSResource.Time + "," +
      MLSResource.DrybulbTemperature + "," +
      MLSResource.RelativeHumidity + "," +
      MLSResource.GlobeTemperature + "," +
      MLSResource.Velocity + "," +
      MLSResource.Illuminance + "," +
      MLSResource.GlobeTemperatureVoltage + "," +
      MLSResource.VelocityVoltage + Environment.NewLine + 
      MLUtility.LoadDataFile(fileName);

    BindingContext = this;
  }

  #endregion

  #region publicメソッド

  private Grid makeGrid()
  {
    Grid myGrid = new Grid();
    myGrid.BackgroundColor = Colors.ForestGreen; //リソース名を参照すべき。

    ColumnDefinition colDef = new ColumnDefinition() { Width = new GridLength(80) };
    for (int i = 0; i < 8; i++) myGrid.AddColumnDefinition(colDef);

    string[] lines = MData.Split(Environment.NewLine);
    RowDefinition rDef = new RowDefinition() { Height = new GridLength(20) };
    for (int i = 0; i < lines.Length; i++)
    {
      if (lines[i] != "")
      {
        if (i == 0) myGrid.AddRowDefinition(new RowDefinition() { Height = new GridLength(40) });
        else myGrid.AddRowDefinition(rDef);

        string[] bf = lines[i].Split(',');
        for (int j = 1; j < 9; j++)
        {
          myGrid.Add(new Label
          {
            Text = bf[j],
            BackgroundColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Margin = new Thickness(1),
            LineBreakMode = LineBreakMode.WordWrap
          }, j - 1, i);
        }
      }
    }

    return myGrid;
  }

  #endregion

  #region コントロール操作時の処理

  private void copy_Clicked(object sender, EventArgs e)
  {
    Clipboard.Default.SetTextAsync(MData);
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

  private void format_Clicked(object sender, EventArgs e)
  {
    if (isFormatted) return;

    showIndicator(MLSResource.LD_Formatting);

    Task.Run(() =>
    {
      try
      {
        Grid grd = makeGrid();
        Application.Current.Dispatcher.Dispatch(() =>
        {
          btnFormat.IsEnabled = false;
          lbl_data.IsVisible = false;
          myStack.Children.Add(grd);
        });        
      }
      catch { }
      finally
      {
        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  #endregion

  #region インジケータの操作

  /// <summary>インジケータを表示する</summary>
  private void showIndicator(string message)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });
  }

  /// <summary>インジケータを隠す</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });
  }

  #endregion

}