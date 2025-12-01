using Microsoft.Extensions.Logging;
using Hospitality.Services;

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

            // Register HTTP client using the available MAUI method
            builder.Services.AddSingleton<HttpClient>();
 
            // Register application services
            builder.Services.AddSingleton<UserService>();
            builder.Services.AddSingleton<RoomService>();
            builder.Services.AddSingleton<BookingService>();
            builder.Services.AddTransient<PayMongoService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

