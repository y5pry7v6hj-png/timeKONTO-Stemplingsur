namespace Stemplingsur.Models;

public sealed record ClockInRequest(int EmployeeId, DateTime When, string Comment);
public sealed record ClockOutRequest(long TimeEntryId, DateTime When, string Comment);
