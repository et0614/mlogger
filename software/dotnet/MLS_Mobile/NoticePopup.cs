using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

namespace MLS_Mobile;

/// <summary>
/// 画面中央に短く出て自動で消えるお知らせ (iPhone の消音切替表示のような半透明の箱)。
/// </summary>
/// <remarks>
/// CommunityToolkit の Toast は画面下端 (タブのすぐ上) に出るため見落とされやすい。
/// 操作の結果を確実に伝えたい場面ではこちらを使う。背景は暗くせず、タップすればすぐ消える。
/// </remarks>
public sealed class NoticePopup : Popup
{
  /// <summary>表示しておく時間</summary>
  private static readonly TimeSpan SHOW_TIME = TimeSpan.FromSeconds(1.8);

  private bool _closed;

  private NoticePopup(string message)
  {
    Padding = 0;
    Margin = 0;
    BackgroundColor = Colors.Transparent;
    HorizontalOptions = LayoutOptions.Center;
    VerticalOptions = LayoutOptions.Center;

    var box = new Border
    {
      StrokeThickness = 0,
      StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
      Padding = new Thickness(24, 18),
      MaximumWidthRequest = 300,
      Content = new Label { Text = message, FontSize = 16, HorizontalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
    };
    // ライトでもダークでも読めるよう、どちらも濃い色の半透明にする
    box.SetAppThemeColor(BackgroundColorProperty, Color.FromRgba(0x1B, 0x5E, 0x20, 0xE6), Color.FromRgba(0x2E, 0x4A, 0x2E, 0xF0));

    var tap = new TapGestureRecognizer();
    tap.Tapped += async (_, __) => await closeOnceAsync();
    box.GestureRecognizers.Add(tap);

    Content = box;
    Closed += (_, __) => _closed = true;
  }

  private async Task closeOnceAsync()
  {
    if (_closed) return;
    _closed = true;
    try { await CloseAsync(); }
    catch (Exception ex) { MLUtility.WriteLog($"[notice] close failed: {ex.Message}"); }
  }

  /// <summary>今表示している画面の中央にお知らせを出し、少し経ったら消す。</summary>
  /// <param name="message">お知らせの文</param>
  public static async Task ShowAsync(string message)
  {
    Page? page = Shell.Current?.CurrentPage;
    if (page == null) return;

    var popup = new NoticePopup(message);
    var options = new PopupOptions
    {
      CanBeDismissedByTappingOutsideOfPopup = true,
      PageOverlayColor = Colors.Transparent,  // 背景は暗くしない
      Shape = null,
      Shadow = null,
    };
    try
    {
      Task shown = page.ShowPopupAsync(popup, options);
      await Task.WhenAny(shown, Task.Delay(SHOW_TIME));
      await popup.closeOnceAsync();
    }
    catch (Exception ex)
    {
      // お知らせが出せなくても本来の操作は済んでいるので、記録だけして続ける
      MLUtility.WriteLog($"[notice] show failed: {ex.Message}");
    }
  }
}
