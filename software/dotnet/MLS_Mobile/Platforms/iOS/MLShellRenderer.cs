using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;

namespace MLS_Mobile.Platforms.iOS;

/// <summary>
/// iOS ではタブを再タップすると UIKit がナビゲーションをルートまで戻してしまい、
/// Shell.OnNavigating では取り消せない (計測画面から確認なしにスキャナ一覧へ戻っていた)。
/// 計測画面 (DataReceive) を表示中の再タップだけを止め、AppShell の確認ダイアログに回す。
/// </summary>
public class MLShellRenderer : ShellRenderer
{
  protected override IShellItemRenderer CreateShellItemRenderer(ShellItem item)
    => new MLShellItemRenderer(this) { ShellItem = item };
}

public class MLShellItemRenderer : ShellItemRenderer
{
  public MLShellItemRenderer(IShellContext context) : base(context) { }

  public override void ViewDidLoad()
  {
    base.ViewDidLoad();

    // 基底は ViewDidLoad で ShouldSelectViewController を設定するので、その後で包む
    var baseShouldSelect = ShouldSelectViewController;
    ShouldSelectViewController = (tabController, viewController) =>
    {
      if (ReferenceEquals(viewController, SelectedViewController)
          && Shell.Current is AppShell shell
          && shell.CurrentPage is DataReceive)
      {
        shell.ConfirmFinishMeasurementAndPopToRoot();
        return false;   // UIKit によるルートへの巻き戻しを止める
      }
      return baseShouldSelect?.Invoke(tabController, viewController) ?? true;
    };
  }
}
