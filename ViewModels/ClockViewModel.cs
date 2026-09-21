using System.Windows.Input;
using Stemplingsur.Models;
using Stemplingsur.Services;

namespace Stemplingsur.ViewModels;

public sealed class ClockViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IStemplingService _service;
    private readonly CalendarService _calendar;
    private Employee? _employee;
    private TimeEntry? _entry;
    private string _mode = "INN";
    private string _comment = "";
    private DateTime _date = DateTime.Today;
    private TimeSpan _time = DateTime.Now.TimeOfDay;
    private string _error = "";
    private bool _isBusy;

    public string Name => _employee?.DisplayName ?? _entry?.EmployeeName ?? "";
    public string Title => _mode == "INN" ? "STEMPLE INN" : "STEMPLE UT";
    public bool IsClockIn => _mode == "INN";
    public bool IsClockOut => _mode == "UT";
    public string ClockInReference => _entry is null ? "" : $"Stemplet inn: {_entry.ClockIn:dd.MM.yyyy HH:mm}";
    public string CommentLabel => _mode == "INN" ? "Kommentar" : "Kommentar fra vakten";
    public string CommentHelp => _mode == "INN"
        ? "Valgfritt. Kommentaren følger denne vakten og vises igjen ved utstempling."
        : "Du kan endre eller legge til tekst før du stempler ut.";
    public string Comment { get => _comment; set => SetProperty(ref _comment, value); }
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
    public TimeSpan Time { get => _time; set => SetProperty(ref _time, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SetNowCommand { get; }

    public ClockViewModel(IStemplingService service, CalendarService calendar)
    {
        _service = service;
        _calendar = calendar;
        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        SetNowCommand = new Command(SetNow);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Mode", out var mode)) _mode = mode?.ToString() ?? "INN";
        if (query.TryGetValue("Employee", out var employee)) _employee = employee as Employee;
        if (query.TryGetValue("Entry", out var entry)) _entry = entry as TimeEntry;

        SetNow();

        // Hvis en ansatt har glemt å stemple ut og vi åpner utstempling
        // mer enn én kalenderdag etter innstemplingen, foreslå dagen etter
        // innstemplingen som utstemplingsdato. Klokkeslettet beholdes som "nå".
        // "SETT TIL NÅ" fungerer fortsatt normalt og kan velges manuelt.
        if (_mode == "UT" &&
            _entry is not null &&
            DateTime.Today > _entry.ClockIn.Date.AddDays(1))
        {
            Date = _entry.ClockIn.Date.AddDays(1);
        }

        Comment = _mode == "UT" ? (_entry?.Comment ?? "") : "";

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsClockIn));
        OnPropertyChanged(nameof(IsClockOut));
        OnPropertyChanged(nameof(ClockInReference));
        OnPropertyChanged(nameof(CommentLabel));
        OnPropertyChanged(nameof(CommentHelp));
    }

    private void SetNow()
    {
        var now = DateTime.Now;
        Date = now.Date;
        Time = new TimeSpan(now.Hour, now.Minute, 0);
    }

    private async Task SaveAsync()
    {
        if (IsBusy) return;
        Error = "";
        OnPropertyChanged(nameof(HasError));

        if (Comment.Length > 2000)
        {
            Error = "Kommentaren kan ikke være lengre enn 2000 tegn.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        try
        {
            IsBusy = true;
            var when = Date.Date + Time;
            var comment = Comment.Trim();

            // Samme hovedregel som VB6: ut-tid kan aldri være før inn-tid.
            if (_mode == "UT" && _entry is not null && when < _entry.ClockIn)
                throw new InvalidOperationException("Du kan ikke stemple ut på et tidspunkt som er tidligere enn du startet.");

            if (_mode == "UT" && _entry is not null && (when - _entry.ClockIn).TotalHours > 20)
                throw new InvalidOperationException("Du har ikke jobbet over 20 timer. Kontroller utstemplingsdato og klokkeslett.");

            var actionText = _mode == "INN" ? "stemple inn" : "stemple ut";
            var employeeName = Name;
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null)
                throw new InvalidOperationException("Kunne ikke åpne bekreftelsesdialogen.");

            var confirmed = await page.DisplayAlertAsync(
                _mode == "INN" ? "Bekreft innstempling" : "Bekreft utstempling",
                $"Vil du {actionText} {employeeName}\n{when:dd.MM.yyyy 'kl.' HH:mm}?",
                "BEKREFT",
                "AVBRYT");

            if (!confirmed)
                return;

            if (_mode == "INN" && _employee is not null)
            {
                await _service.ClockInAsync(new ClockInRequest(_employee.Id, when, comment));

                // Kalenderen er informativ og skal aldri kunne blokkere en gyldig innstempling.
                try
                {
                    var events = await _calendar.GetEventsForDateAsync(when.Date);
                    if (events.Count > 0)
                        await ShowCalendarEventsAsync(events, when.Date);
                }
                catch
                {
                    // Ignorer kalenderfeil her. Kalenderen kan testes separat under Innstillinger.
                }
            }
            else if (_entry is not null)
            {
                await _service.ClockOutAsync(new ClockOutRequest(_entry.Id, when, comment));
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task ShowCalendarEventsAsync(
        IReadOnlyList<CalendarEvent> events,
        DateTime date)
    {
        var lines = new List<string>();

        foreach (var item in events.Take(8))
        {
            var time = item.IsAllDay
                ? "Hele dagen"
                : item.Start.ToString("HH:mm");

            var line = $"{time} · {item.Summary}";

            if (!string.IsNullOrWhiteSpace(item.Location))
                line += $"\n{item.Location}";

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                var description = item.Description.Trim();
                if (description.Length > 220)
                    description = description[..220] + "…";
                line += $"\n{description}";
            }

            lines.Add(line);
        }

        if (events.Count > 8)
            lines.Add($"+ {events.Count - 8} flere hendelser");

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
        {
            await page.DisplayAlertAsync(
                $"Hendelser {date:dd.MM.yyyy}",
                string.Join("\n\n", lines),
                "OK");
        }
    }

}
