using Microsoft.Extensions.Logging;
using GameOfCardsCsharp.Maui.Services;
using GameOfCardsCsharp.Tablic;

namespace GameOfCardsCsharp.Maui
{
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

            // Configure logging
#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
            builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif

            // Register services
            builder.Services.AddSingleton<LoggingService>();
            builder.Services.AddSingleton<GameLogger>();  // Add GameLogger
            builder.Services.AddTransient<TablicGameEngine>();
            builder.Services.AddTransient<TablicGamePage>();

            return builder.Build();
        }
    }
}
