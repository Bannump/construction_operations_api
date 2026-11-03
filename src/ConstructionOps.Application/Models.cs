namespace ConstructionOps.Application;

public enum ProjectStatus { Planned, Active, Completed, Cancelled }
public enum ExpenseCategory { Materials, Equipment, Permits, Subcontractors, Other }

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public decimal ApprovedBudget { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public byte[] Version { get; set; } = [];
    public ICollection<LaborEntry> LaborEntries { get; set; } = new List<LaborEntry>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public sealed class LaborEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string WorkerName { get; set; } = "";
    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Cost => decimal.Round(Hours * HourlyRate, 2, MidpointRounding.AwayFromZero);
}

public sealed class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public ExpenseCategory Category { get; set; }
    public DateOnly IncurredDate { get; set; }
}
