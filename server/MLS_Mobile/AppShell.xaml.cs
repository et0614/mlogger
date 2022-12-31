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
  }
}
