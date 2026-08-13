using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;
using MLS_Mobile.Services;

namespace MLS_Mobile;

public static class MauiProgram
{

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
    builder
      .UseMauiApp<App>()
      .UseMauiCommunityToolkit()
      .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

        //font awesome
        fonts.AddFont("Brands-Regular-400.otf", "FAB");
        fonts.AddFont("Free-Regular-400.otf", "FAR");
        fonts.AddFont("Free-Solid-900.otf", "FAS");
      });

#if IOS
    // iOS の SwipeItem は UIButton で、アイコンが template 画像として tint 色 (既定=青) で
    // 描画されるため FontImageSource の Color 指定が効かない。tint を白に固定する。
    Microsoft.Maui.Handlers.SwipeItemMenuItemHandler.Mapper.AppendToMapping("WhiteSwipeItemIcon", (handler, view) =>
    {
      handler.PlatformView.TintColor = UIKit.UIColor.White;
    });
#endif

    // i18n は MLSResource (resx 自動生成) が System.Resources.ResourceManager 経由で
    // 直接アクセスするので Microsoft.Extensions.Localization の AddLocalization は不要。

    builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);

    // 計測値の共有モデル。DataReceive が書き込み、他 Tab (Thermal comfort / Moist air) が
    // ライブ入力として読む。アプリ生存期間中 1 インスタンスのみ。
    builder.Services.AddSingleton<ILiveMeasurementService, LiveMeasurementService>();

    return builder.Build();
	}

}

