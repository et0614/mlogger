namespace MLS_Mobile;

using System.Text;

using Microsoft.Maui.ApplicationModel.DataTransfer;

using MLS_Mobile.Resources.i18n;

public partial class LoggingData : ContentPage
{

  #region インスタンス変数・プロパティ

  private bool isFormatted = false;

  private string fileName;

  public string DataTitle { get; private set; }

  public string ClipData { get; private set; }

  #endregion

  #region コンストラクタ

  public LoggingData(string fileName)
	{
		InitializeComponent();

    this.fileName = fileName;
    ClipData = 
      MLSResource.Date + "," + MLSResource.Time + "," +
      MLSResource.DrybulbTemperature + "," +
      MLSResource.RelativeHumidity + "," +
      MLSResource.GlobeTemperature + "," +
      MLSResource.Velocity + "," +
      MLSResource.Illuminance + "," +
      MLSResource.GlobeTemperatureVoltage + "," +
      MLSResource.VelocityVoltage + Environment.NewLine + 
      MLUtility.LoadDataFile(fileName);

    string[] bf = fileName.Split('_');
    DataTitle = "MLogger_" + bf[1] + ": " 
      + bf[2].Substring(0, 4) + "/" + bf[2].Substring(4, 2) + "/" + bf[2].Substring(6, 2);

    BindingContext = this;
  }

  #endregion

  #region publicメソッド

  private Grid makeGrid()
  {
    //表の形状を設定
    ColumnDefinition colDef = new ColumnDefinition() { Width = new GridLength(80) };
    Grid myGrid = new Grid()
    {
      BackgroundColor = Colors.ForestGreen,
      Padding = new Thickness(1),
      ColumnDefinitions = { colDef, colDef, colDef, colDef, colDef, colDef, colDef, colDef },
      RowDefinitions = {
        new RowDefinition() { Height = new GridLength(60) } ,
        new RowDefinition()
      }
    };

    //全データを改行コードで分割
    string[] lines = ClipData.Split(Environment.NewLine);

    //タイトル行
    string[] bf = lines[0].Split(',');
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
        Padding = new Thickness(2),
        LineBreakMode = LineBreakMode.CharacterWrap
      }, j - 1, 0);
    }

    //データ行
    StringBuilder[] sBuilds = new StringBuilder[8];
    for (int i = 1; i < lines.Length; i++)
    {
      if (lines[i] != "")
      {
        bf = lines[i].Split(',');
        for (int j = 1; j < 9; j++)
        {
          if (i == 1) sBuilds[j - 1] = new StringBuilder("");
          sBuilds[j - 1].Append(bf[j] + "\n");
        }
      }
    }
    for (int i = 0; i < 8; i++)
    {
      myGrid.Add(new Label
      {
        Text = sBuilds[i].ToString().TrimEnd('\n'),
        BackgroundColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Margin = new Thickness(1)
      }, i, 1);
    }

    return myGrid;
  }

  #endregion

  #region コントロール操作時の処理

  private void copy_Clicked(object sender, EventArgs e)
  {
    Clipboard.Default.SetTextAsync(ClipData);
  }

  private async void delete_Clicked(object sender, EventArgs e)
  {
    bool remove = await DisplayAlert("Alert", MLSResource.LD_DeleteAlert, "OK", "Cancel");
    if (remove)
    {
      MLUtility.DeleteDataFile(fileName);
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