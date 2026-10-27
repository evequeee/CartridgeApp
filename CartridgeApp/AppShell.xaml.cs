using CartridgeApp.Views;

namespace CartridgeApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(GameFormPage), typeof(GameFormPage));
	}
}
