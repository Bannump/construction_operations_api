using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ConstructionOps.Application;

public sealed record CreateProjectRequest(
    [property: JsonRequired][Required, StringLength(200)] string Name,
    [property: JsonRequired] decimal ApprovedBudget, [property: JsonRequired] DateOnly StartDate, [property: JsonRequired] DateOnly EndDate);
public sealed record UpdateStatusRequest([property: JsonRequired] ProjectStatus Status);
public sealed record AddLaborRequest(
    [property: JsonRequired][Required, StringLength(200)] string WorkerName,
    [property: JsonRequired] decimal Hours, [property: JsonRequired] decimal HourlyRate, [property: JsonRequired] DateOnly WorkDate);
public sealed record AddExpenseRequest(
    [property: JsonRequired][Required, StringLength(500)] string Description,
    [property: JsonRequired] decimal Amount, [property: JsonRequired] ExpenseCategory Category, [property: JsonRequired] DateOnly IncurredDate);
public sealed record ProjectDto(Guid Id, string Name, ProjectStatus Status, decimal ApprovedBudget, DateOnly StartDate, DateOnly EndDate)
{
    public static ProjectDto From(Project p) => new(p.Id, p.Name, p.Status, p.ApprovedBudget, p.StartDate, p.EndDate);
}
public sealed record BudgetSummary(Guid ProjectId, string Name, decimal ApprovedBudget, decimal LaborCost, decimal Expenses)
{
    public decimal TotalSpent => LaborCost + Expenses;
    public decimal RemainingBudget => ApprovedBudget - TotalSpent;
}
public sealed record FinancialSummary(int ProjectCount, decimal ApprovedBudget, decimal LaborCost, decimal Expenses)
{
    public decimal TotalSpent => LaborCost + Expenses;
    public decimal RemainingBudget => ApprovedBudget - TotalSpent;
}
public sealed class ValidationException(string message) : Exception(message);
public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);

public interface IProjectStore
{
    Task AddAsync(Project project, CancellationToken ct);
    Task<Project?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, int page, int pageSize, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectStatus? status, int page, int pageSize, CancellationToken ct);
    Task<ProjectDto> UpdateStatusAsync(Guid id, UpdateStatusRequest request, CancellationToken ct);
}

public sealed record LaborDto(Guid Id, Guid ProjectId, string WorkerName, decimal Hours, decimal HourlyRate, DateOnly WorkDate, decimal Cost);
public sealed record ExpenseDto(Guid Id, Guid ProjectId, string Description, decimal Amount, ExpenseCategory Category, DateOnly IncurredDate);
public interface ICostStore
{
    Task<Project?> FindProjectAsync(Guid id, CancellationToken ct);
    Task AddLaborAsync(LaborEntry entry, CancellationToken ct);
    Task AddExpenseAsync(Expense expense, CancellationToken ct);
    Task<BudgetSummary?> BudgetAsync(Guid id, CancellationToken ct);
    Task<FinancialSummary> FinancialsAsync(ProjectStatus? status, CancellationToken ct);
}
public interface ICostService
{
    Task<LaborDto> AddLaborAsync(Guid projectId, AddLaborRequest request, CancellationToken ct);
    Task<ExpenseDto> AddExpenseAsync(Guid projectId, AddExpenseRequest request, CancellationToken ct);
    Task<BudgetSummary> BudgetAsync(Guid projectId, CancellationToken ct);
    Task<FinancialSummary> FinancialsAsync(ProjectStatus? status, CancellationToken ct);
}
