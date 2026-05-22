using MLS_Mobile.Resources.i18n;

namespace MLS_Mobile;

public partial class AppShell : Shell
{
  public AppShell()
  {
    InitializeComponent();

    //必要に応じてデータディレクトリと設定ファイルを初期化する
    MLUtility.InitDirAndFiles();
    MLUtility.LoadMLNamesFile();

    //ルート登録 (MLTransceiver/RelayedDataViewer は v4 移行で削除)
    Routing.RegisterRoute(nameof(ActivitySelector), typeof(ActivitySelector));
    Routing.RegisterRoute(nameof(ClothingCoordinator), typeof(ClothingCoordinator));
    Routing.RegisterRoute(nameof(LoggingData), typeof(LoggingData));
    Routing.RegisterRoute(nameof(DeviceSetting), typeof(DeviceSetting));
    Routing.RegisterRoute(nameof(CFSetting), typeof(CFSetting));
    Routing.RegisterRoute(nameof(DataReceive), typeof(DataReceive));
    Routing.RegisterRoute(nameof(LogView), typeof(LogView));
    Routing.RegisterRoute(nameof(CO2Calibrator), typeof(CO2Calibrator));
  }

  protected override async void OnNavigating(ShellNavigatingEventArgs args)
  {
    base.OnNavigating(args);

    //DataReceiveからpopするとき、計測を中止するか警告を発する
    if (args.Current != null && args.Current.Location.OriginalString.Contains(nameof(DataReceive)))
    {
      var currentpage = this.CurrentPage as DataReceive;
      if (currentpage is DataReceive && args.Source == ShellNavigationSource.Pop)
      {
        ShellNavigatingDeferral token = args.GetDeferral();
        var result = await DisplayActionSheet(MLSResource.DR_FinishAlert, MLSResource.Cancel, MLSResource.Yes);
        if (result == MLSResource.Yes)
        {
          // Pop 確定時に _vm を即解放 (finalizer 任せだと ML Scanner Tab のバッジが残る)
          currentpage.DisposeViewModel();
          token.Complete();
        }
        else args.Cancel();
      }
    }
  }

}
