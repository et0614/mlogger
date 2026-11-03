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

  // 確認済みで計測画面から抜けるとき、OnNavigating の確認を二重に出さないためのフラグ
  private bool _leaveConfirmed;

  /// <summary>
  /// 計測中 (DataReceive 表示中) に ML Scanner タブが再タップされたときの処理。
  /// iOS では UIKit が先にルートまで戻してしまい OnNavigating で取り消せないので、
  /// iOS のタブ部品 (Platforms/iOS/MLShellRenderer) が再タップを止めてここを呼ぶ。
  /// 計測を終了するか確認し、はいならルート (スキャナ一覧) まで戻る。
  /// </summary>
  public async void ConfirmFinishMeasurementAndPopToRoot()
  {
    if (CurrentPage is not DataReceive page) return;
    var result = await DisplayActionSheet(MLSResource.DR_FinishAlert, MLSResource.Cancel, MLSResource.Yes);
    if (result != MLSResource.Yes) return;

    page.DisposeViewModel();
    _leaveConfirmed = true;
    try { await page.Navigation.PopToRootAsync(); }
    finally { _leaveConfirmed = false; }
  }

  protected override async void OnNavigating(ShellNavigatingEventArgs args)
  {
    base.OnNavigating(args);
    if (_leaveConfirmed) return;

    //DataReceiveからpopするとき、計測を中止するか警告を発する
    if (args.Current != null && args.Current.Location.OriginalString.Contains(nameof(DataReceive)))
    {
      var currentpage = this.CurrentPage as DataReceive;
      // Pop (戻る) に加えて PopToRoot (計測中に ML Scanner タブをもう一度タップ) でも確認する。
      // PopToRoot を素通しすると、確認なしに計測が切れ、_vm も解放されない。
      if (currentpage is DataReceive
          && (args.Source == ShellNavigationSource.Pop || args.Source == ShellNavigationSource.PopToRoot))
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
