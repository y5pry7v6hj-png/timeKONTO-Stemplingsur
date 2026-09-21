using Stemplingsur.Models;

namespace Stemplingsur.Services;

public interface IStemplingService
{
    Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetActiveEntriesAsync(CancellationToken cancellationToken = default);
    Task<TimeEntry> ClockInAsync(ClockInRequest request, CancellationToken cancellationToken = default);
    Task ClockOutAsync(ClockOutRequest request, CancellationToken cancellationToken = default);
}
