using ConstructionOps.Application;
using Microsoft.EntityFrameworkCore;
namespace ConstructionOps.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task SeedAsync(OperationsDbContext db, CancellationToken ct)
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (await db.Projects.AnyAsync(p => p.Id == id, ct)) return;
        var project = new Project
        {
            Id = id,
            Name = "Riverside Bridge",
            Status = ProjectStatus.Active,
            ApprovedBudget = 250000m,
            StartDate = new(2026, 1, 1),
            EndDate = new(2026, 12, 31)
        };
        project.LaborEntries.Add(new LaborEntry
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ProjectId = id,
            WorkerName = "Alex Morgan",
            Hours = 8m,
            HourlyRate = 42.50m,
            WorkDate = new(2026, 1, 5)
        });
        project.Expenses.Add(new Expense
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ProjectId = id,
            Description = "Initial concrete delivery",
            Amount = 4500m,
            Category = ExpenseCategory.Materials,
            IncurredDate = new(2026, 1, 5)
        });
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
    }
}
