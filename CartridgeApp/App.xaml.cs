using Microsoft.Extensions.DependencyInjection;

using System.Text;

namespace CartridgeApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			return new Window(new AppShell());
		}
		catch (Exception exception)
		{
			WriteStartupError(exception);

			var fallbackPage = new ContentPage
			{
				Title = "CartridgeApp",
				BackgroundColor = Color.FromArgb("#050A18"),
				Content = new VerticalStackLayout
				{
					Padding = 24,
					VerticalOptions = LayoutOptions.Center,
					Children =
					{
						new Label
						{
							Text = "CartridgeApp",
							FontSize = 24,
							TextColor = Colors.White,
							HorizontalTextAlignment = TextAlignment.Center
						},
						new Label
						{
							Text = "Startup error. Details were written to app cache.",
							TextColor = Color.FromArgb("#8FA6CC"),
							HorizontalTextAlignment = TextAlignment.Center
						}
					}
				}
			};

			return new Window(fallbackPage);
		}
	}

	private static void WriteStartupError(Exception exception)
	{
		try
		{
			string path = Path.Combine(FileSystem.CacheDirectory, "cartridge-startup-error.txt");
			File.WriteAllText(path, $"{DateTime.Now:O}{Environment.NewLine}{exception}");
		}
		catch
		{
		}
	}
}