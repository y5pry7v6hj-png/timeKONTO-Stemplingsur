using Stemplingsur.Views;

namespace Stemplingsur;

public partial class AppShell : Shell
{
    public AppShell(MainPage mainPage)
    {
        InitializeComponent();
        Items.Add(new ShellContent { Route = "main", Content = mainPage });
        Routing.RegisterRoute(nameof(ClockPage), typeof(ClockPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
