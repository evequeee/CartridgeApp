namespace CartridgeApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		RegisterUnhandledExceptionHandlers();
	}

	protected override Window CreateWindow(IActivationState? activationState) => new Window(new AppShell());

	private static void RegisterUnhandledExceptionHandlers()
	{
#if WINDOWS
		Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, args) =>
		{
			WriteCrashLog("WinUI UnhandledException", args.Exception);
			args.Handled = true;
		};
#endif

		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			Exception exception = args.ExceptionObject as Exception ?? new Exception("Unknown AppDomain exception");
			WriteCrashLog("AppDomain UnhandledException", exception);
		};
	}

	private static void WriteCrashLog(string source, Exception exception)
	{
		try
		{
			string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
			string message = $"{DateTime.Now:O}{Environment.NewLine}{source}{Environment.NewLine}{exception}{Environment.NewLine}StackTrace:{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}";
			File.AppendAllText(path, message);
		}
		catch
		{
		}
	}
}