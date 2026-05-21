using System.ComponentModel;
using MLLib;
using MLS_Mobile.Resources.i18n;
using MLS_Mobile.Services;

namespace MLS_Mobile;

public partial class AppShell : Shell
{
  private const string SCANNER_TAB_BASE = "ML Scanner";
  private readonly ILiveMeasurementService? _live;

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

    // 計測中バッジ: ILiveMeasurementService の接続状態を ML Scanner Tab のタイトルに反映
    // (どの Tab を見ていても「いま接続中である」事実が分かるようにするため)
    _live = IPlatformApplication.Current?.Services.GetService(typeof(ILiveMeasurementService))
            as ILiveMeasurementService;
    if (_live != null)
    {
      _live.PropertyChanged += OnLivePropertyChanged;
      UpdateScannerTabBadge();
    }
  }

  private void OnLivePropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(ILiveMeasurementService.IsConnected)
     && e.PropertyName != nameof(ILiveMeasurementService.DeviceName)) return;
    Dispatcher.Dispatch(UpdateScannerTabBadge);
  }

  private void UpdateScannerTabBadge()
  {
    if (scannerTab == null) return;
    if (_live?.IsConnected == true)
    {
      // 緑点 + デバイス名で「いま計測中」を明示。
      scannerTab.Title = _live.DeviceName is string n && !string.IsNullOrWhiteSpace(n)
                       ? $"● {n}"
                       : $"● {SCANNER_TAB_BASE}";
    }
    else
    {
      scannerTab.Title = SCANNER_TAB_BASE;
    }
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
