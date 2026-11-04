using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using CartridgeApp.Data;
using CartridgeApp.Services;
using CartridgeApp.ViewModels;
using CartridgeApp.Views;

namespace CartridgeApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        string postgresHost = DeviceInfo.Platform == DevicePlatform.Android
            ? "10.0.2.2"
            : "localhost";
        string connectionString = $"Host={postgresHost};Port=5432;Database=cartridge;Username=cartridge;Password=cartridge";

        builder.Services.AddDbContext<CartridgeDbContext>(options =>
            options.UseNpgsql(connectionString));
        builder.Services.AddSingleton<IGameRepository, PostgresGameRepository>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<GameFormViewModel>();
        builder.Services.AddTransient<StatsViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<GameFormPage>();
        builder.Services.AddTransient<StatsPage>();

        return builder.Build();
    }
}