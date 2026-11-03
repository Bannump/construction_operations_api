namespace ConstructionOps.Application;

public sealed class CostService(ICostStore store) : ICostService
{
    public async Task<LaborDto> AddLaborAsync(Guid projectId, AddLaborRequest r, CancellationToken ct)
    {
        Rules.Text(r.WorkerName, 200, "Worker name");
        if (r.Hours <= 0 || r.Hours > 24 || decimal.Round(r.Hours, 2) != r.Hours)
            throw new ValidationException("Hours must be greater than 0 and at most 24 with at most two decimal places.");
        Rules.Money(r.HourlyRate, "Hourly rate");
        Rules.EntryDate(await RequireProjectAsync(projectId, ct), r.WorkDate);
        var entry = new LaborEntry { ProjectId = projectId, WorkerName = r.WorkerName.Trim(), Hours = r.Hours, HourlyRate = r.HourlyRate, WorkDate = r.WorkDate };
        await store.AddLaborAsync(entry, ct);
        return new(entry.Id, entry.ProjectId, entry.WorkerName, entry.Hours, entry.HourlyRate, entry.WorkDate, entry.Cost);
    }
    public async Task<ExpenseDto> AddExpenseAsync(Guid projectId, AddExpenseRequest r, CancellationToken ct)
    {
        Rules.Text(r.Description, 500, "Description");
        Rules.Money(r.Amount, "Amount");
        if (!Enum.IsDefined(r.Category)) throw new ValidationException("Unknown expense category.");
        Rules.EntryDate(await RequireProjectAsync(projectId, ct), r.IncurredDate);
        var expense = new Expense { ProjectId = projectId, Description = r.Description.Trim(), Amount = r.Amount, Category = r.Category, IncurredDate = r.IncurredDate };
        await store.AddExpenseAsync(expense, ct);
        return new(expense.Id, expense.ProjectId, expense.Description, expense.Amount, expense.Category, expense.IncurredDate);
    }
    public async Task<BudgetSummary> BudgetAsync(Guid projectId, CancellationToken ct)
    {
        RequireId(projectId);
        return await store.BudgetAsync(projectId, ct) ?? throw new NotFoundException($"Project {projectId} was not found.");
    }
    public Task<FinancialSummary> FinancialsAsync(ProjectStatus? status, CancellationToken ct)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ValidationException("Unknown project status.");
        return store.FinancialsAsync(status, ct);
    }
    private async Task<Project> RequireProjectAsync(Guid id, CancellationToken ct)
    {
        RequireId(id);
        return await store.FindProjectAsync(id, ct) ?? throw new NotFoundException($"Project {id} was not found.");
    }
    private static void RequireId(Guid id)
    {
        if (id == Guid.Empty) throw new ValidationException("Project ID is required.");
    }
}
