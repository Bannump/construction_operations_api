using ConstructionOps.Application;
namespace ConstructionOps.UnitTests;

public sealed class BusinessRulesTests
{
    [Theory]
    [InlineData(ProjectStatus.Planned, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Planned, ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Cancelled)]
    public void AllowedTransitionsSucceed(ProjectStatus current, ProjectStatus next) => Rules.Transition(current, next);
    public static IEnumerable<object[]> InvalidTransitions() =>
        from current in Enum.GetValues<ProjectStatus>()
        from next in Enum.GetValues<ProjectStatus>()
        where !((current == ProjectStatus.Planned && next is ProjectStatus.Active or ProjectStatus.Cancelled)
            || (current == ProjectStatus.Active && next is ProjectStatus.Completed or ProjectStatus.Cancelled))
        select new object[] { current, next };
    [Theory, MemberData(nameof(InvalidTransitions))]
    public void InvalidTransitionsFail(ProjectStatus current, ProjectStatus next) => Assert.Throws<ConflictException>(() => Rules.Transition(current, next));
    [Fact]
    public void UnknownStatusFailsValidation() => Assert.Throws<ValidationException>(() => Rules.Transition(ProjectStatus.Active, (ProjectStatus)99));
    [Theory]
    [InlineData(0.1, 0.05, 0.01)]
    [InlineData(7.25, 31.99, 231.93)]
    [InlineData(8, 42.50, 340)]
    public void LaborCostRoundsEachEntryAwayFromZero(decimal hours, decimal rate, decimal expected) =>
        Assert.Equal(expected, new LaborEntry { Hours = hours, HourlyRate = rate }.Cost);
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(100, 70, 50, -20)]
    public void BudgetAllowsOverspend(decimal approved, decimal labor, decimal expense, decimal remaining)
    {
        var summary = new BudgetSummary(Guid.NewGuid(), "Project", approved, labor, expense);
        Assert.Equal(labor + expense, summary.TotalSpent);
        Assert.Equal(remaining, summary.RemainingBudget);
    }
    [Fact]
    public void InvalidDatesAndNamesAreRejected()
    {
        Assert.Throws<ValidationException>(() => Rules.Dates(default, new(2026, 1, 1)));
        Assert.Throws<ValidationException>(() => Rules.Dates(new(2026, 2, 1), new(2026, 1, 1)));
        Assert.Throws<ValidationException>(() => Rules.Text("  ", 200, "Name"));
        Assert.Throws<ValidationException>(() => Rules.Text(new string('a', 201), 200, "Name"));
    }
}
