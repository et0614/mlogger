namespace MLS_Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

  protected override Window CreateWindow(IActivationState? activationState)
  {
    // 起動時に表示する root ページ
    return new Window(new AppShell());
  }
}
