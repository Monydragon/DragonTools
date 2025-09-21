using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using DragonTools.Services; // add
using Plugin.LocalNotification;
using INotificationService = DragonTools.Services.INotificationService; // add

namespace DragonTools;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLocalNotification() // initialize local notifications plugin
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<ISpeechToTextService, SpeechToTextService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<TodoService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ServiceHelper.Init(app.Services);
        return app;
    }
}