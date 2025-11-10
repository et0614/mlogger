namespace MLS_Mobile;

using Microsoft.Maui.ApplicationModel.DataTransfer;
using MLS_Mobile.Resources.i18n;
using System.IO;
using System.Linq;
using System.Text;

[QueryProperty(nameof(FileName), "FileName")]
public partial class LoggingData : ContentPage
{

  #region インスタンス変数・プロパティ

  private bool isInitialized = false;

  public string FileName { get; set; }

  #endregion

  #region コンストラクタ

  public LoggingData()
	{
		InitializeComponent();

    BindingContext = this;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    if (!isInitialized)
    {
      isInitialized = true;

      string[] bf = FileName.Split('_');
      this.Title = "MLogger_" + bf[1] + ": "
        + bf[2].Substring(0, 4) + "/" + bf[2].Substring(4, 2) + "/" + bf[2].Substring(6, 2);
      
      //テーブル表示
      showIndicator(MLSResource.LD_Formatting);
      Task.Run(() =>
      {
        try
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            makeGrid();
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
  }

  #endregion

  #region publicメソッド

  /// <summary>ファイルからクリップボード用データを作る</summary>
  /// <param name="fileName">ファイル名称</param>
  /// <param name="maxLines">コピーする最大行数</param>
  /// <returns>クリップボード用データ</returns>
  public static string MakeClipData(string fileName, int maxLines)
  {
    string data = MLUtility.LoadDataFile(fileName, maxLines);
    // CO2があるデータと無いデータがあるので、1行目のカンマの数で判定する//当面の処理。いつか消したい。
    int commaCount = 0;
    using (var reader = new StringReader(data))
    {
      string firstLine = reader.ReadLine();
      if (firstLine != null) commaCount = firstLine.Count(c => c == ',');
    }
    bool hasCO2 = commaCount == 10;
    string co2Header = hasCO2 ? ("," + MLSResource.CO2level) : "";

    return MLSResource.Date + "," + 
      MLSResource.Time + "," +
      MLSResource.DrybulbTemperature + "," +
      MLSResource.RelativeHumidity + "," +
      MLSResource.GlobeTemperature + "," +
      MLSResource.Velocity + "," +
      MLSResource.Illuminance + "," +
      MLSResource.GlobeTemperatureVoltage + "," +
      MLSResource.VelocityVoltage + 
      co2Header + 
      ",note" + 
      Environment.NewLine +
      MLUtility.LoadDataFile(fileName, maxLines);
  }

  /// <summary>ファイルからクリップボード用データを作る</summary>
  /// <param name="fileName">ファイル名称</param>
  /// <returns>クリップボード用データ</returns>
  public static string MakeClipData(string fileName)
  {
    return MakeClipData(fileName, int.MaxValue);
  }

  private void makeGrid()
  {
    //全データを改行コードで分割//テーブル表示は500行まで（重たいので）
    string[] lines = MakeClipData(FileName, 500).Split(Environment.NewLine);

    //タイトル行
    string[] bf = lines[0].Split(',');
    int rowNum = bf.Length;
    int col = 0;
    for (int j = 1; j < rowNum - 1; j++) //日付（開始列）とnote（最終列）は表示しない
    {
      if (j != 7) //GlobeTemperatureVoltage列は表示しない
      {
        Label lbl = new Label
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
        };
        tableGrid.Add(lbl, col++, 0);
      }
    }

    //データ行
    StringBuilder[] sBuilds = new StringBuilder[rowNum - 3];
    for (int i = 1; i < lines.Length; i++)
    {
      if (lines[i] != "")
      {
        bf = lines[i].Split(',');
        col = 0;
        for (int j = 1; j < rowNum - 1; j++) //日付（開始列）とnote（最終列）は表示しない
        {
          if (j != 7) //GlobeTemperatureVoltage列は表示しない
          {
            if (i == 1) sBuilds[col] = new StringBuilder("");
            if (i == lines.Length - 1) sBuilds[col].Append(bf[j]);
            else sBuilds[col].AppendLine(bf[j]);
            col++;
          }
        }
      }
    }
    //データが少ない場合には空行を入れておく
    if (lines.Length < 40)
      for (int i = 0; i < 40 - lines.Length; i++)
        for (int j = 0; j < sBuilds.Length; j++)
          sBuilds[j].AppendLine();

    for (int i = 0; i < sBuilds.Length; i++)
    {
      Label lbl = new Label
      {
        Text = sBuilds[i].ToString(),
        BackgroundColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Margin = new Thickness(1)
      };
      tableGrid.Add(lbl, i, 1);
    }

    //データが500を超える場合には続きがあることを表示
    if (lines.Length - 1 == 500)
    {
      IView iv = new Label
      {
        Text = MLSResource.LD_Exceed,
        BackgroundColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Start,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Margin = new Thickness(1)
      };
      tableGrid.Add(iv, 0, 2);
      tableGrid.SetColumnSpan(iv, rowNum - 3);
    }
  }

  #endregion

  #region コントロール操作時の処理

  private void copy_Clicked(object sender, EventArgs e)
  {
    Clipboard.Default.SetTextAsync(MakeClipData(FileName));
  }

  private async void delete_Clicked(object sender, EventArgs e)
  {
    bool remove = await DisplayAlert("Alert", MLSResource.LD_DeleteAlert, "OK", "Cancel");
    if (remove)
    {
      MLUtility.DeleteDataFile(FileName);
      await Shell.Current.GoToAsync("..");
    }
  }

  private async void share_Clicked(object sender, EventArgs e)
  {
    // 一時ファイルに保存
    string cData = MakeClipData(FileName);
    string filePath = Path.Combine(FileSystem.CacheDirectory, FileName);
    File.WriteAllText(filePath, cData, Encoding.UTF8);

    //ファイルを共有
    await Share.Default.RequestAsync(new ShareFileRequest
    {
      Title = "M-Logger Data",
      File = new ShareFile(filePath)
    });

    /*  ShareTextRequest st = new ShareTextRequest
    {
      Text = cData,
      Title = "M-Logger Data"
    };

    try
    {
      //Androidでは1MBを境界にandroid.os.TransactionTooLargeExceptionというエラーが出る模様
      await Share.Default.RequestAsync(st);
    }
    catch
    {
      bool result = await DisplayAlert("Alert", MLSResource.LD_SaveFileAlert, MLSResource.OK, MLSResource.Cancel);
      if (result)
      {
        var data = Encoding.GetEncoding("UTF-8").GetBytes(cData);
        var stream = new MemoryStream(data);
        await FileSaver.Default.SaveAsync(FileName, stream);
      }
    }*/
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