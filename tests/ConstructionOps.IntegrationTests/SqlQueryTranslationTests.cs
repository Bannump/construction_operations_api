using ConstructionOps.Application;
using ConstructionOps.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace ConstructionOps.IntegrationTests;

// Provider translation checks need no server. They complement, but do not replace, SQL Server execution tests.
public sealed class SqlQueryTranslationTests
{
    [Fact]
    public void BudgetQueryTranslatesFilteringAndRoundedAggregatesToSql()
    {
        using var db = new OperationsDbContext(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var sql = new CostStore(db).Budgets(ProjectStatus.Active, Guid.NewGuid()).ToQueryString();
        Assert.Contains("FLOOR", sql);
        Assert.Contains("SUM", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("[ProjectId]", sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
