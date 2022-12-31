namespace MLS_Mobile;

using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;
using MLS_Mobile.Resources.i18n;
using Plugin.BLE;

public partial class MainPage : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  public const string DATA_FOLDER = "DATA";

  private readonly ObservableCollection<Label> cmds = new ObservableCollection<Label>();

  #endregion

  public MainPage()
  {
    InitializeComponent();

    //国際化デバッグ用設定
    Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja-JP"); //強制日本語表示
    //MLS_Mobile.Resources.i18n.MLSResource.Culture = MLS_Mobile.Resources.i18n.TCResource.Culture = new System.Globalization.CultureInfo("ja-JP"); //強制日本語表示
    //MLS_Mobile.Resources.i18n.MLSResource.Culture = MLS_Mobile.Resources.i18n.TCResource.Culture = new System.Globalization.CultureInfo("en-US"); //強制英語表示

    //データフォルダを用意する
    Directory.CreateDirectory(FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_FOLDER);

    Title = "MLS Mobile";
    cmds.Add(makeLabel(MLSResource.ConnectMLogger)); //MLoggerへ接続
    cmds.Add(makeLabel(MLSResource.EditMeasuredData)); //収集データの操作
    cmds.Add(makeLabel(MLSResource.ThermalComfortCalculator)); //熱的快適性計算機
    cmds.Add(makeLabel(MLSResource.MoistAirCalculator)); //湿り空気計算機
    cmds.Add(makeLabel(MLSResource.AboutThisSoftware)); //このソフトウェアについて
    cmdList.ItemsSource = cmds;
  }

  private Label makeLabel(string text)
  {
    Label lbl = new Label();
    lbl.Text = text;
    lbl.TextColor = Colors.Black;
    return lbl;
  }

  private async void cmdList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
  {
    if (cmdList.SelectedItem == null) return;

    int indx = cmds.IndexOf((Label)e.SelectedItem);
    cmdList.SelectedItem = null;

    switch (indx)
    {
      //MLoggerへ接続"
      case 0:
        MLoggerScanner mls = new MLoggerScanner();
        await Navigation.PushAsync(mls);
        break;
      //収集データの操作
      case 1:
        LoggingDataList ldl = new LoggingDataList();
        await Navigation.PushAsync(ldl);
        break;
      //熱的快適性計算機
      case 2:
        ThermalComfortCalculator tcc = new ThermalComfortCalculator();
        await Navigation.PushAsync(tcc);
        break;
      //湿り空気計算機
      case 3:
        MoistAirCalculator mac = new MoistAirCalculator();
        await Navigation.PushAsync(mac);
        break;
      //このソフトウェアについて
      case 4:
        AboutPage abt = new AboutPage();
        await Navigation.PushAsync(abt);

        //await Shell.Current.GoToAsync("//AboutPage", true);
        break;
      //DEBUG
      case 5:
        //CFSetting cfs = new CFSetting();
        //Navigation.PushAsync(cfs);
        break;
      default:
        break;
    }
  }


}

