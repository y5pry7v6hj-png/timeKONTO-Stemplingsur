namespace Stemplingsur.Models;

public sealed record CalendarEvent(
    string Summary,
    DateTime Start,
    DateTime? End,
    bool IsAllDay,
    string Location,
    string Description);
