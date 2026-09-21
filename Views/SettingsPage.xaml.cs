using Stemplingsur.Services;

namespace Stemplingsur.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ApiStemplingService _api;
    private readonly CalendarService _calendar;

    public SettingsPage(ApiStemplingService api, CalendarService calendar)
    {
        InitializeComponent();
        _api = api;
        _calendar = calendar;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        BaseUrlEntry.Text = ApiSettings.BaseUrl;
        ApiKeyEntry.Text = await ApiSettings.GetApiKeyAsync();
        AdminPinEntry.Text = ApiSettings.AdminPin;
        CalendarUrlEntry.Text = ApiSettings.CalendarUrl;
        CalendarCutoffEntry.Text = ApiSettings.CalendarWorkdayCutoff;
        QuickTime1Entry.Text = ApiSettings.QuickTime1;
        QuickTime2Entry.Text = ApiSettings.QuickTime2;
        QuickTime3Entry.Text = ApiSettings.QuickTime3;
        QuickTime4Entry.Text = ApiSettings.QuickTime4;
        QuickOutTime1Entry.Text = ApiSettings.QuickOutTime1;
        QuickOutTime2Entry.Text = ApiSettings.QuickOutTime2;
        QuickOutTime3Entry.Text = ApiSettings.QuickOutTime3;
        QuickOutTime4Entry.Text = ApiSettings.QuickOutTime4;
        StatusLabel.Text = string.Empty;
    }

    private void OnToggleKeyClicked(object? sender, EventArgs e)
    {
        ApiKeyEntry.IsPassword = !ApiKeyEntry.IsPassword;
        if (sender is Button button)
            button.Text = ApiKeyEntry.IsPassword ? "VIS" : "SKJUL";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!Validate()) return;

        SetBusy(sender as Button, true, "LAGRER …");
        StatusLabel.Text = "Lagrer innstillinger …";
        StatusLabel.TextColor = Color.FromArgb("#6C7772");

        try
        {
            ApiSettings.BaseUrl = BaseUrlEntry.Text!;
            var secureStorageOk = await ApiSettings.SetApiKeyAsync(ApiKeyEntry.Text ?? string.Empty);
            ApiSettings.AdminPin = AdminPinEntry.Text!;
            ApiSettings.CalendarUrl = CalendarUrlEntry.Text ?? string.Empty;
            ApiSettings.CalendarWorkdayCutoff = CalendarCutoffEntry.Text ?? "06:00";
            ApiSettings.QuickTime1 = QuickTime1Entry.Text!;
            ApiSettings.QuickTime2 = QuickTime2Entry.Text!;
            ApiSettings.QuickTime3 = QuickTime3Entry.Text!;
            ApiSettings.QuickTime4 = QuickTime4Entry.Text!;
            ApiSettings.QuickOutTime1 = QuickOutTime1Entry.Text!;
            ApiSettings.QuickOutTime2 = QuickOutTime2Entry.Text!;
            ApiSettings.QuickOutTime3 = QuickOutTime3Entry.Text!;
            ApiSettings.QuickOutTime4 = QuickOutTime4Entry.Text!;

            StatusLabel.Text = secureStorageOk
                ? "● Innstillingene er lagret sikkert"
                : "● Innstillingene er lagret (lokal fallback for API-nøkkel)";
            StatusLabel.TextColor = Color.FromArgb("#123D31");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "● Kunne ikke lagre: " + ex.Message;
            StatusLabel.TextColor = Colors.DarkRed;
            await DisplayAlertAsync("Lagring feilet", ex.Message, "OK");
        }
        finally
        {
            SetBusy(sender as Button, false, "LAGRE");
        }
    }

    private async void OnTestClicked(object? sender, EventArgs e)
    {
        if (!Validate()) return;

        SetBusy(sender as Button, true, "TESTER …");
        StatusLabel.Text = "Tester tilkobling …";
        StatusLabel.TextColor = Color.FromArgb("#6C7772");

        try
        {
            // Bruk verdiene som står i feltene under testen.
            ApiSettings.BaseUrl = BaseUrlEntry.Text!;
            await ApiSettings.SetApiKeyAsync(ApiKeyEntry.Text ?? string.Empty);

            await _api.TestConnectionAsync();
            StatusLabel.Text = "● Tilkobling OK";
            StatusLabel.TextColor = Color.FromArgb("#123D31");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "● Tilkobling feilet: " + ex.Message;
            StatusLabel.TextColor = Colors.DarkRed;
            await DisplayAlertAsync("Tilkobling feilet", ex.Message, "OK");
        }
        finally
        {
            SetBusy(sender as Button, false, "TEST TILKOBLING");
        }
    }


    private async void OnTestCalendarClicked(object? sender, EventArgs e)
    {
        ApiSettings.CalendarUrl = CalendarUrlEntry.Text ?? string.Empty;
        ApiSettings.CalendarWorkdayCutoff = CalendarCutoffEntry.Text ?? "06:00";

        if (string.IsNullOrWhiteSpace(ApiSettings.CalendarUrl))
        {
            StatusLabel.Text = "WebCal/ICS-adressen er tom.";
            StatusLabel.TextColor = Colors.DarkRed;
            return;
        }

        SetBusy(sender as Button, true, "TESTER …");
        StatusLabel.Text = "Tester kalendere …";
        StatusLabel.TextColor = Color.FromArgb("#6C7772");

        try
        {
            await _calendar.TestAsync();
            var events = await _calendar.GetEventsForDateAsync(DateTime.Today);

            var calendarCount = CalendarService.GetConfiguredCalendarUrls().Count;
            StatusLabel.Text = events.Count == 0
                ? $"● {calendarCount} kalender(e) OK · ingen hendelser i arbeidsdøgnet"
                : $"● {calendarCount} kalender(e) OK · {events.Count} hendelse(r) i arbeidsdøgnet";
            StatusLabel.TextColor = Color.FromArgb("#123D31");

            if (events.Count > 0)
            {
                var preview = string.Join(
                    "\n",
                    events.Take(6).Select(e =>
                        $"{(e.IsAllDay ? "Hele dagen" : e.Start.ToString("HH:mm"))} · {e.Summary}"));

                await DisplayAlertAsync("Kalendere OK", preview, "OK");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "● Kalenderfeil: " + ex.Message;
            StatusLabel.TextColor = Colors.DarkRed;
            await DisplayAlertAsync("Kalenderfeil", ex.Message, "OK");
        }
        finally
        {
            SetBusy(sender as Button, false, "TEST KALENDERE");
        }
    }

    private bool Validate()
    {
        if (!Uri.TryCreate(BaseUrlEntry.Text?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            StatusLabel.Text = "Skriv inn en gyldig API-adresse.";
            StatusLabel.TextColor = Colors.DarkRed;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiKeyEntry.Text))
        {
            StatusLabel.Text = "API-nøkkel kan ikke være tom.";
            StatusLabel.TextColor = Colors.DarkRed;
            return false;
        }


        if (!TimeSpan.TryParseExact(CalendarCutoffEntry.Text?.Trim(), @"hh\:mm", null, out var calendarCutoff) ||
            calendarCutoff < TimeSpan.Zero || calendarCutoff >= TimeSpan.FromDays(1))
        {
            StatusLabel.Text = "Arbeidsdøgn-grensen må skrives som HH:mm, for eksempel 06:00.";
            StatusLabel.TextColor = Colors.DarkRed;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CalendarUrlEntry.Text))
        {
            try
            {
                ApiSettings.CalendarUrl = CalendarUrlEntry.Text;
                _ = CalendarService.GetConfiguredCalendarUrls();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Kalenderadresse: " + ex.Message;
                StatusLabel.TextColor = Colors.DarkRed;
                return false;
            }
        }

        var quickTimes = new[]
        {
            QuickTime1Entry.Text, QuickTime2Entry.Text, QuickTime3Entry.Text, QuickTime4Entry.Text,
            QuickOutTime1Entry.Text, QuickOutTime2Entry.Text, QuickOutTime3Entry.Text, QuickOutTime4Entry.Text
        };

        foreach (var quickTime in quickTimes)
        {
            // Tomt felt betyr at hurtigtidsknappen skal skjules.
            if (string.IsNullOrWhiteSpace(quickTime))
                continue;

            if (!TimeSpan.TryParseExact(quickTime.Trim(), @"hh\:mm", null, out var parsed) ||
                parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
            {
                StatusLabel.Text = "Hurtigtider må være tomme eller skrives som HH:mm, for eksempel 18:45.";
                StatusLabel.TextColor = Colors.DarkRed;
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(AdminPinEntry.Text))
        {
            StatusLabel.Text = "Administrator-PIN kan ikke være tom.";
            StatusLabel.TextColor = Colors.DarkRed;
            return false;
        }

        return true;
    }

    private static void SetBusy(Button? button, bool busy, string text)
    {
        if (button is null) return;
        button.IsEnabled = !busy;
        button.Text = text;
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
