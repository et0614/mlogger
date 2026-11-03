namespace MLS_Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// 配色は端末のライト/ダーク設定に従う (Styles.xaml と各画面の AppThemeBinding、
		// コードからは MLUtility.ThemeColor)。ダーク用の色は Resources/Styles/Colors.xaml の *_Dark。
	}

  protected override Window CreateWindow(IActivationState? activationState)
  {
    // 起動時に表示する root ページ
    return new Window(new AppShell());
  }
}
