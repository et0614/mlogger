using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Xamarin.Forms;

using System.Collections.ObjectModel;
using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  public partial class MainPage : ContentPage
  {

    #region インスタンス変数・プロパティ・定数宣言

    public const string DATA_FOLDER = "DATA";

    public const string CF_FOLDER = "CF";

    private readonly ObservableCollection<Label> cmds = new ObservableCollection<Label>();

    #endregion

    #region コンストラクタ

    public MainPage()
    {
      InitializeComponent();

      //国際化デバッグ用設定
      //MLSResource.Culture = new System.Globalization.CultureInfo("ja-JP"); //強制日本語表示
      //MLSResource.Culture = new System.Globalization.CultureInfo("en-US"); //強制英語表示

      //データフォルダを用意する
      Plugin.NetStandardStorage.CrossStorage.FileSystem.LocalStorage.CreateFolder
        (DATA_FOLDER, Plugin.NetStandardStorage.Abstractions.Types.CreationCollisionOption.OpenIfExists);
      
      //補正係数フォルダを用意する
      Plugin.NetStandardStorage.CrossStorage.FileSystem.LocalStorage.CreateFolder
        (CF_FOLDER, Plugin.NetStandardStorage.Abstractions.Types.CreationCollisionOption.OpenIfExists);

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
      lbl.TextColor = Color.Black;
      return lbl;
    }

    #endregion

    private void cmdList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
      if (cmdList.SelectedItem == null) return;

      int indx = cmds.IndexOf((Label)e.SelectedItem);
      cmdList.SelectedItem = null;

      switch (indx)
      {
        //MLoggerへ接続"
        case 0:
          MLoggerScanner mls = new MLoggerScanner();
          Navigation.PushAsync(mls);
          break;
        //収集データの操作
        case 1:
          LoggingDataList ldl = new LoggingDataList();
          Navigation.PushAsync(ldl);
          break;
        //熱的快適性計算機
        case 2:
          ThermalComfortCalculator tcc = new ThermalComfortCalculator();
          Navigation.PushAsync(tcc);
          break;
        //湿り空気計算機
        case 3:
          MoistAirCalculator mac = new MoistAirCalculator();
          Navigation.PushAsync(mac);
          break;
        //このソフトウェアについて
        case 4:
          AboutPage abt = new AboutPage();
          Navigation.PushAsync(abt);
          break;
        //DEBUG
        case 5:
          CFSetting cfs = new CFSetting();
          Navigation.PushAsync(cfs);
          break;
        default:
          break;
      }
    }

    #region インジケータの操作

    /// <summary>インジケータを表示する</summary>
    private void showIndicator(string message)
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        indicatorLabel.Text = message;
        grayback.IsVisible = indicator.IsVisible = true;
      });
    }

    /// <summary>インジケータを隠す</summary>
    private void hideIndicator()
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        grayback.IsVisible = indicator.IsVisible = false;
      });
    }

    #endregion

  }
}