using System.Globalization;
using Microsoft.Maui.Controls;
using Stemplingsur.ViewModels;

namespace Stemplingsur.Views;

public partial class ClockPage
{
    private bool _updatingDateTimeFields;

    private void OnDateEntryLoaded(object? sender, EventArgs e)
    {
        if (sender is not Entry entry || BindingContext is not ClockViewModel vm)
            return;

        _updatingDateTimeFields = true;
        entry.Text = vm.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        _updatingDateTimeFields = false;
    }

    private void OnTimeEntryLoaded(object? sender, EventArgs e)
    {
        if (sender is not Entry entry || BindingContext is not ClockViewModel vm)
            return;

        _updatingDateTimeFields = true;
        entry.Text = vm.Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        _updatingDateTimeFields = false;
    }

    private void OnDateEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingDateTimeFields || BindingContext is not ClockViewModel vm)
            return;

        if (DateTime.TryParseExact(
                e.NewTextValue,
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            vm.Date = parsedDate;
        }
    }

    private void OnTimeEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingDateTimeFields || BindingContext is not ClockViewModel vm)
            return;

        if (TimeSpan.TryParseExact(
                e.NewTextValue,
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out var parsedTime))
        {
            vm.Time = parsedTime;
        }
    }
}
