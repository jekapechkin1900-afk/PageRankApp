namespace PageRankApp.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell())
		{
			MinimumWidth = 720,
			MinimumHeight = 550,
			Width = 1280,
			Height = 800
		};

		return window;
	}
}