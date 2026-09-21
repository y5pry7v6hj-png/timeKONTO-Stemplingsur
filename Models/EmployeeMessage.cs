namespace Stemplingsur.Models;

public sealed class EmployeeMessage
{
    public int Id { get; init; }
    public int EmployeeId { get; init; }
    public DateTime Start { get; init; }
    public DateTime? End { get; init; }
    public string Text { get; init; } = "";
    public bool IsRead { get; set; }
}
