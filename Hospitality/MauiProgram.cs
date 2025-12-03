using Microsoft.Extensions.Logging;
using Hospitality.Services;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Hospitality
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
                });

            builder.Services.AddMauiBlazorWebView();

            // Load configuration from appsettings.json
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Hospitality.appsettings.json");

            if (stream != null)
            {
                var config = new ConfigurationBuilder()
                    .AddJsonStream(stream)
                    .Build();

                builder.Configuration.AddConfiguration(config);
            }

            // Register HTTP client
            builder.Services.AddSingleton<HttpClient>();

            // Register application services
            builder.Services.AddSingleton<UserService>();
            builder.Services.AddSingleton<RoomService>();
            builder.Services.AddSingleton<BookingService>();
            builder.Services.AddSingleton<LoyaltyService>();
            builder.Services.AddSingleton<MessageService>();
            builder.Services.AddTransient<PayMongoService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

