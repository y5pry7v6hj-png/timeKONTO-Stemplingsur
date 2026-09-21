using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Stemplingsur;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);

#if MACCATALYST || WINDOWS
        window.TitleBar = new TitleBar
        {
            Title = "timeKONTO STEMPLINGSUR",
            BackgroundColor = Color.FromArgb("#123D31"),
            ForegroundColor = Colors.White,
            HeightRequest = 40
        };
#endif

        return window;
    }
}
