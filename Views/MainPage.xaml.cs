using Stemplingsur.ViewModels;
using Stemplingsur.Models;

namespace Stemplingsur.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private IDispatcherTimer? _timer;

    public System.Windows.Input.ICommand ClockInCommand => _viewModel.ClockInCommand;
    public System.Windows.Input.ICommand ClockOutCommand => _viewModel.ClockOutCommand;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();

        if (_timer is null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(10);
            _timer.Tick += OnRefreshTimerTick;
        }

        _timer.Start();
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        await _viewModel.RefreshFromServerAsync();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        base.OnDisappearing();
    }
}

public partial class MainPage
{
    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var pin = await DisplayPromptAsync("Administrator", "Skriv administrator-PIN:",
            "ÅPNE", "AVBRYT", keyboard: Keyboard.Numeric);
        if (pin is null) return;
        if (pin != Services.ApiSettings.AdminPin)
        {
            await DisplayAlertAsync("Feil PIN", "Administrator-PIN er ikke riktig.", "OK");
            return;
        }
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}
