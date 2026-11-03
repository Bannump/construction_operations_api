namespace ConstructionOps.Application;

public static class Rules
{
    public const decimal MaxMoney = 999999999999.99m;
    public static void Text(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ValidationException($"{field} is required and must be at most {max} characters.");
    }
    public static void Money(decimal value, string field)
    {
        if (value < 0 || value > MaxMoney || decimal.Round(value, 2) != value)
            throw new ValidationException($"{field} must be between 0 and {MaxMoney} with at most two decimal places.");
    }
    public static void Dates(DateOnly start, DateOnly end)
    {
        if (start == default || end == default || end < start)
            throw new ValidationException("Dates are required and end date must be on or after start date.");
    }
    public static void Transition(ProjectStatus current, ProjectStatus next)
    {
        if (!Enum.IsDefined(next)) throw new ValidationException("Unknown project status.");
        if (!((current == ProjectStatus.Planned && next is ProjectStatus.Active or ProjectStatus.Cancelled)
            || (current == ProjectStatus.Active && next is ProjectStatus.Completed or ProjectStatus.Cancelled)))
            throw new ConflictException($"Cannot transition from {current} to {next}.");
    }
    public static void EntryDate(Project project, DateOnly date)
    {
        if (date == default || date < project.StartDate || date > project.EndDate)
            throw new ValidationException("Entry date must fall within the project date range.");
    }
}
