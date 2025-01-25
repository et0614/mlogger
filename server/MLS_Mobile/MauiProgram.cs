using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MLS_Mobile;

public static class MauiProgram
{

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
    builder
      .UseMauiApp<App>()
			.UseMauiCommunityToolkit()
      .UseSkiaSharp()
      .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

        //font awesome
        fonts.AddFont("Brands-Regular-400.otf", "FAB");
        fonts.AddFont("Free-Regular-400.otf", "FAR");
        fonts.AddFont("Free-Solid-900.otf", "FAS");
      });

		//国際化
		builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);

    return builder.Build();
	}

}

