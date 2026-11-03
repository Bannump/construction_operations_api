using ConstructionOps.Application;
using Microsoft.EntityFrameworkCore;
namespace ConstructionOps.Infrastructure;

public sealed class CostStore(OperationsDbContext db) : ICostStore
{
    public Task<Project?> FindProjectAsync(Guid id, CancellationToken ct) => db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
    public async Task AddLaborAsync(LaborEntry entry, CancellationToken ct) { db.LaborEntries.Add(entry); await db.SaveChangesAsync(ct); }
    public async Task AddExpenseAsync(Expense expense, CancellationToken ct) { db.Expenses.Add(expense); await db.SaveChangesAsync(ct); }
    // All inputs are nonnegative. Floor(x * 100 + .5) implements per-entry away-from-zero rounding in SQL and .NET.
    internal IQueryable<BudgetSummary> Budgets(ProjectStatus? status = null, Guid? projectId = null) =>
        db.Projects.AsNoTracking().Where(p => (!status.HasValue || p.Status == status) && (!projectId.HasValue || p.Id == projectId)).Select(p => new BudgetSummary(
            p.Id, p.Name, p.ApprovedBudget,
            p.LaborEntries.Sum(l => (decimal?)(Math.Floor(l.Hours * l.HourlyRate * 100m + 0.5m) / 100m)) ?? 0m,
            p.Expenses.Sum(e => (decimal?)e.Amount) ?? 0m));
    public Task<BudgetSummary?> BudgetAsync(Guid id, CancellationToken ct) => Budgets(projectId: id).SingleOrDefaultAsync(ct);
    public async Task<FinancialSummary> FinancialsAsync(ProjectStatus? status, CancellationToken ct)
    {
        // SQL produces one aggregate row per project; LINQ rolls up those rows without loading cost entities.
        var rows = await Budgets(status).ToListAsync(ct);
        return new(rows.Count, rows.Sum(p => p.ApprovedBudget), rows.Sum(p => p.LaborCost), rows.Sum(p => p.Expenses));
    }
}
