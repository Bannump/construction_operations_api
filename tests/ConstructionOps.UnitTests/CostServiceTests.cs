using ConstructionOps.Application;
namespace ConstructionOps.UnitTests;

public sealed class CostServiceTests
{
    private static readonly DateOnly Day = new(2026, 2, 1);
    private readonly Store store = new();
    [Fact]
    public async Task LaborIsValidatedPersistedAndRounded()
    {
        var result = await new CostService(store).AddLaborAsync(store.Project.Id, new(" Alex ", .1m, .05m, Day), default);
        Assert.Equal(.01m, result.Cost);
        Assert.Equal("Alex", result.WorkerName);
        Assert.Equal(result.Id, Assert.Single(store.Labor).Id);
    }
    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(24.01, 10)]
    [InlineData(1.001, 10)]
    [InlineData(1, -1)]
    [InlineData(1, 1.001)]
    public async Task InvalidLaborIsNotPersisted(decimal hours, decimal rate)
    {
        await Assert.ThrowsAsync<ValidationException>(() => new CostService(store).AddLaborAsync(store.Project.Id, new("Alex", hours, rate, Day), default));
        Assert.Empty(store.Labor);
    }
    [Fact]
    public async Task ExpenseIsPersistedWithCategory()
    {
        var result = await new CostService(store).AddExpenseAsync(store.Project.Id, new(" Steel ", 123.45m, ExpenseCategory.Materials, Day), default);
        Assert.Equal("Steel", result.Description);
        Assert.Equal(ExpenseCategory.Materials, result.Category);
        Assert.Equal(123.45m, Assert.Single(store.Expenses).Amount);
    }
    [Fact]
    public async Task InvalidExpenseCategoryAndAmountFail()
    {
        var service = new CostService(store);
        await Assert.ThrowsAsync<ValidationException>(() => service.AddExpenseAsync(store.Project.Id, new("Steel", 1m, (ExpenseCategory)99, Day), default));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddExpenseAsync(store.Project.Id, new("Steel", -1m, ExpenseCategory.Materials, Day), default));
        Assert.Empty(store.Expenses);
    }
    [Fact]
    public async Task MissingProjectsAndOutOfRangeDatesFail()
    {
        var service = new CostService(store);
        await Assert.ThrowsAsync<ValidationException>(() => service.AddLaborAsync(Guid.Empty, new("Alex", 1, 1, Day), default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddLaborAsync(Guid.NewGuid(), new("Alex", 1, 1, Day), default));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddLaborAsync(store.Project.Id, new("Alex", 1, 1, Day.AddYears(-1)), default));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddExpenseAsync(store.Project.Id, new("Steel", 1, ExpenseCategory.Materials, default), default));
    }
    private sealed class Store : ICostStore
    {
        public Project Project { get; } = new() { StartDate = Day, EndDate = Day.AddMonths(1) };
        public List<LaborEntry> Labor { get; } = [];
        public List<Expense> Expenses { get; } = [];
        public Task<Project?> FindProjectAsync(Guid id, CancellationToken ct) => Task.FromResult(id == Project.Id ? Project : null);
        public Task AddLaborAsync(LaborEntry entry, CancellationToken ct) { Labor.Add(entry); return Task.CompletedTask; }
        public Task AddExpenseAsync(Expense expense, CancellationToken ct) { Expenses.Add(expense); return Task.CompletedTask; }
        public Task<BudgetSummary?> BudgetAsync(Guid id, CancellationToken ct) => Task.FromResult<BudgetSummary?>(null);
        public Task<FinancialSummary> FinancialsAsync(ProjectStatus? status, CancellationToken ct) => Task.FromResult(new FinancialSummary(0, 0, 0, 0));
    }
}
