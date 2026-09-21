namespace Stemplingsur.Models;

public sealed class TimeEntry
{
    public long Id { get; init; }
    public int EmployeeId { get; init; }
    public string EmployeeName { get; init; } = "";
    public DateTime ClockIn { get; init; }
    public DateTime? ClockOut { get; set; }
    public string Comment { get; set; } = "";
    public string ClockInText => $"Inn {ClockIn:HH:mm}";
}
