namespace Stemplingsur.Models;

public sealed class Employee
{
    public int Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}
