using Stemplingsur.ViewModels;

namespace Stemplingsur.Views;

public partial class ClockPage : ContentPage
{
    public ClockPage(ClockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnQuickTimeClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || BindingContext is not ClockViewModel viewModel)
            return;

        if (!TimeSpan.TryParseExact(button.Text, @"hh\:mm", null, out var selectedTime))
            return;

        viewModel.Time = selectedTime;
        TimeEntry.Text = selectedTime.ToString(@"hh\:mm");
    }
}
