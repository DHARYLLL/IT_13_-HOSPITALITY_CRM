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

            // Register connectivity and sync services (must be registered first)
            builder.Services.AddSingleton<ConnectivityService>();
            builder.Services.AddSingleton<SyncService>();
            
            // Register DualWriteService for online/offline dual-write support
            builder.Services.AddSingleton<DualWriteService>();

            // Register application services with dual-write support
            builder.Services.AddSingleton<UserService>(sp =>
                new UserService(
                    sp.GetRequiredService<DualWriteService>(),
                    sp.GetRequiredService<SyncService>()));
            builder.Services.AddSingleton<RoomService>(sp => 
        new RoomService(
              sp.GetRequiredService<SyncService>(),
     sp.GetRequiredService<DualWriteService>()));
            builder.Services.AddSingleton<BookingService>(sp => 
  new BookingService(
         sp.GetRequiredService<SyncService>(),
  sp.GetRequiredService<DualWriteService>()));
            builder.Services.AddSingleton<LoyaltyService>();
     builder.Services.AddSingleton<MessageService>(sp =>
      new MessageService(
    sp.GetRequiredService<DualWriteService>(),
              sp.GetRequiredService<SyncService>()));
    builder.Services.AddSingleton<PaymentService>(sp =>
             new PaymentService(
       sp.GetRequiredService<DualWriteService>(),
          sp.GetRequiredService<SyncService>()));
            builder.Services.AddSingleton<AnalyticsService>();
            builder.Services.AddTransient<PayMongoService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

