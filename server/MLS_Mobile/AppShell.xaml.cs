using System.Text;

namespace MLS_Mobile;

public partial class AppShell : Shell
{

  public AppShell()
  {
    //国際化デバッグ用設定
    //Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja-JP"); //強制日本語表示
    //MLS_Mobile.Resources.i18n.MLSResource.Culture = MLS_Mobile.Resources.i18n.TCResource.Culture = new System.Globalization.CultureInfo("ja-JP"); //強制日本語表示
    //MLS_Mobile.Resources.i18n.MLSResource.Culture = MLS_Mobile.Resources.i18n.TCResource.Culture = new System.Globalization.CultureInfo("en-US"); //強制英語表示

    InitializeComponent();

    //データディレクトリを用意する
    MLUtility.InitDirAndFiles();

    //ルート登録
    Routing.RegisterRoute(nameof(ActivitySelector), typeof(ActivitySelector));
    Routing.RegisterRoute(nameof(ClothingCoordinator), typeof(ClothingCoordinator));
    Routing.RegisterRoute(nameof(LoggingData), typeof(LoggingData));
    Routing.RegisterRoute(nameof(DeviceSetting), typeof(DeviceSetting));
    Routing.RegisterRoute(nameof(CFSetting), typeof(CFSetting));
    Routing.RegisterRoute(nameof(DataReceive), typeof(DataReceive));
    Routing.RegisterRoute(nameof(Calibrator), typeof(Calibrator));
    Routing.RegisterRoute(nameof(RelayedDataViewer), typeof(RelayedDataViewer));

    makeDummyFile();
  }


  private void makeDummyFile()
  {
    /*MLUtility.DeleteDataFile("MLogger_999_19990101.txt");
    StringBuilder sBuilder = new StringBuilder("");
    for (int i = 0; i < 1000; i++)
      sBuilder.AppendLine("1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_999_19990101.txt", sBuilder.ToString());*/

    /*MLUtility.DeleteDataFile("MLogger_999_19990101.txt");
    MLUtility.DeleteDataFile("MLogger_999_19990102.txt");
    MLUtility.DeleteDataFile("MLogger_999_19990103.txt");
    MLUtility.DeleteDataFile("MLogger_999_19990110.txt");
    MLUtility.DeleteDataFile("MLogger_100_19990101.txt");
    MLUtility.DeleteDataFile("MLogger_100_20000111.txt");
    MLUtility.DeleteDataFile("MLogger_100_19850101.txt");
    MLUtility.DeleteDataFile("MLogger_100_19991201.txt");
    MLUtility.DeleteDataFile("MLogger_100_19990301.txt");

    
    MLUtility.AppendData("MLogger_999_19990101.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_999_19990102.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_999_19990103.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_999_19990110.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_100_19990101.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_100_20000111.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_100_19850101.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    MLUtility.AppendData("MLogger_100_19991201.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543");
    for(int i=0;i<50;i++)
      MLUtility.AppendData("MLogger_100_19990301.txt", "1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r1999/1/1,00:00:00,25.6,70.5,26.2,0.10,1530,0.895,1.543\r");
    */
  }

}
