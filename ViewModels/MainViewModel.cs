using System.Collections.ObjectModel;
using System.Windows.Input;
using Stemplingsur.Models;
using Stemplingsur.Services;
using Stemplingsur.Views;

namespace Stemplingsur.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    private readonly IStemplingService _service;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string _clockText = "";
    private string _dateText = "";
    private string _statusText = "Starter…";
    private bool _isBusy;

    public ObservableCollection<Employee> Employees { get; } = [];
    public ObservableCollection<TimeEntry> ActiveEntries { get; } = [];
    public string ClockText { get => _clockText; private set => SetProperty(ref _clockText, value); }
    public string DateText { get => _dateText; private set => SetProperty(ref _dateText, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public int ActiveCount => ActiveEntries.Count;

    public ICommand ClockInCommand { get; }
    public ICommand ClockOutCommand { get; }
    public ICommand RefreshCommand { get; }

    public MainViewModel(IStemplingService service)
    {
        _service = service;
        ClockInCommand = new Command<Employee>(async employee => await OpenClockInAsync(employee));
        ClockOutCommand = new Command<TimeEntry>(async entry => await OpenClockOutAsync(entry));
        RefreshCommand = new Command(async () => await LoadAsync());
        UpdateClock();
    }

    public void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText = now.ToString("HH:mm");
        DateText = now.ToString("dddd d. MMMM", new System.Globalization.CultureInfo("nb-NO"));
    }

    public Task LoadAsync() => RefreshFromServerAsync(showBusy: true);

    /// <summary>
    /// Henter status fra serveren. Brukes av automatisk polling på MainPage.
    /// Listene oppdateres bare når innholdet faktisk har endret seg.
    /// </summary>
    public async Task RefreshFromServerAsync(bool showBusy = false)
    {
        if (!await _refreshLock.WaitAsync(0))
            return;

        try
        {
            if (showBusy)
                IsBusy = true;

            // Klokken oppdateres én gang per refresh. Ingen ekstra layout-pass fra timeren.
            UpdateClock();

            var employeesTask = _service.GetEmployeesAsync();
            var activeTask = _service.GetActiveEntriesAsync();
            await Task.WhenAll(employeesTask, activeTask);

            var employees = await employeesTask;
            var active = await activeTask;
            var activeEmployeeIds = active.Select(a => a.EmployeeId).ToHashSet();
            var available = employees.Where(e => !activeEmployeeIds.Contains(e.Id)).ToList();

            // Oppdater bare elementene som faktisk er lagt til, fjernet, flyttet eller endret.
            // Dette unngår Clear()+refill og reduserer layoutarbeid på Android.
            SyncEmployees(Employees, available);
            var activeChanged = SyncActiveEntries(ActiveEntries, active);
            if (activeChanged)
                OnPropertyChanged(nameof(ActiveCount));

            if (StatusText != "● Tilkoblet")
                StatusText = "● Tilkoblet";
        }
        catch (Exception ex)
        {
            // Behold siste gyldige lister hvis nettet/API-et er midlertidig utilgjengelig.
            StatusText = $"● Feil · {ex.Message}";
        }
        finally
        {
            if (showBusy)
                IsBusy = false;
            _refreshLock.Release();
        }
    }

    private static void SyncEmployees(ObservableCollection<Employee> current, IReadOnlyList<Employee> incoming)
    {
        var incomingIds = incoming.Select(e => e.Id).ToHashSet();

        for (var i = current.Count - 1; i >= 0; i--)
        {
            if (!incomingIds.Contains(current[i].Id))
                current.RemoveAt(i);
        }

        for (var targetIndex = 0; targetIndex < incoming.Count; targetIndex++)
        {
            var item = incoming[targetIndex];
            var currentIndex = IndexOfEmployee(current, item.Id);

            if (currentIndex < 0)
            {
                current.Insert(targetIndex, item);
                continue;
            }

            if (currentIndex != targetIndex)
                current.Move(currentIndex, targetIndex);

            if (!EmployeeEqual(current[targetIndex], item))
                current[targetIndex] = item;
        }
    }

    private static bool SyncActiveEntries(ObservableCollection<TimeEntry> current, IReadOnlyList<TimeEntry> incoming)
    {
        var changed = false;
        var incomingIds = incoming.Select(e => e.Id).ToHashSet();

        for (var i = current.Count - 1; i >= 0; i--)
        {
            if (!incomingIds.Contains(current[i].Id))
            {
                current.RemoveAt(i);
                changed = true;
            }
        }

        for (var targetIndex = 0; targetIndex < incoming.Count; targetIndex++)
        {
            var item = incoming[targetIndex];
            var currentIndex = IndexOfEntry(current, item.Id);

            if (currentIndex < 0)
            {
                current.Insert(targetIndex, item);
                changed = true;
                continue;
            }

            if (currentIndex != targetIndex)
            {
                current.Move(currentIndex, targetIndex);
                changed = true;
            }

            if (!ActiveEntryEqual(current[targetIndex], item))
            {
                current[targetIndex] = item;
                changed = true;
            }
        }

        return changed;
    }

    private static int IndexOfEmployee(IReadOnlyList<Employee> items, int id)
    {
        for (var i = 0; i < items.Count; i++)
            if (items[i].Id == id) return i;
        return -1;
    }

    private static int IndexOfEntry(IReadOnlyList<TimeEntry> items, long id)
    {
        for (var i = 0; i < items.Count; i++)
            if (items[i].Id == id) return i;
        return -1;
    }

    private static bool EmployeeEqual(Employee a, Employee b) =>
        a.Id == b.Id &&
        a.FirstName == b.FirstName &&
        a.LastName == b.LastName;

    private static bool ActiveEntryEqual(TimeEntry a, TimeEntry b) =>
        a.Id == b.Id &&
        a.EmployeeId == b.EmployeeId &&
        a.EmployeeName == b.EmployeeName &&
        a.ClockIn == b.ClockIn &&
        a.ClockOut == b.ClockOut &&
        a.Comment == b.Comment;

    private async Task OpenClockInAsync(Employee? employee)
    {
        if (employee is null) return;
        try
        {
            await Shell.Current.GoToAsync(nameof(ClockPage), new Dictionary<string, object>
            {
                ["Employee"] = employee,
                ["Mode"] = "INN"
            });
        }
        catch (Exception ex)
        {
            StatusText = $"● Navigasjonsfeil · {ex.Message}";
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
                await page.DisplayAlertAsync("Kunne ikke åpne stempling", ex.Message, "OK");
        }
    }

    private async Task OpenClockOutAsync(TimeEntry? entry)
    {
        if (entry is null) return;
        try
        {
            await Shell.Current.GoToAsync(nameof(ClockPage), new Dictionary<string, object>
            {
                ["Entry"] = entry,
                ["Mode"] = "UT"
            });
        }
        catch (Exception ex)
        {
            StatusText = $"● Navigasjonsfeil · {ex.Message}";
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
                await page.DisplayAlertAsync("Kunne ikke åpne stempling", ex.Message, "OK");
        }
    }
}
