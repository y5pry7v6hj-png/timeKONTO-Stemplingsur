using Stemplingsur.Services;
using Stemplingsur.ViewModels;
using Stemplingsur.Views;

namespace Stemplingsur;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
        builder.Services.AddSingleton<ApiStemplingService>();
        builder.Services.AddSingleton<CalendarService>();
        builder.Services.AddSingleton<IStemplingService>(sp => sp.GetRequiredService<ApiStemplingService>());

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<ClockViewModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<ClockPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
