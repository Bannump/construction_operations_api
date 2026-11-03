using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructionOps.Application;
using ConstructionOps.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ConstructionOps.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING")))
            Skip = "Set TEST_SQLSERVER_CONNECTION_STRING to a disposable SQL Server instance; tests create and delete unique databases.";
    }
}

[Trait("Category", "SqlServer")]
public sealed class SqlServerTests
{
    [SqlServerFact]
    public async Task MigratedDatabaseSupportsCompleteApiFinancialWorkflow()
    {
        await using var app = await SqlTestApp.CreateAsync();
        using var client = app.Factory.CreateClient();
        await ApiScenarios.FinancialWorkflow(client);
    }
    [SqlServerFact]
    public async Task ForeignKeysRejectOrphanedCosts()
    {
        await using var app = await SqlTestApp.CreateAsync();
        await using var db = app.Context();
        db.LaborEntries.Add(new LaborEntry { ProjectId = Guid.NewGuid(), WorkerName = "Alex", Hours = 1, HourlyRate = 1, WorkDate = new(2026, 1, 1) });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
        db.ChangeTracker.Clear();
        db.Expenses.Add(new Expense { ProjectId = Guid.NewGuid(), Description = "Steel", Amount = 1, IncurredDate = new(2026, 1, 1) });
        exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
    }
    [SqlServerFact]
    public async Task CheckConstraintsRejectInvalidValuesEvenOutsideApi()
    {
        await using var app = await SqlTestApp.CreateAsync();
        await using var db = app.Context();
        var p = new Project { Name = "Invalid", ApprovedBudget = -1, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) };
        db.Projects.Add(p);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
        db.ChangeTracker.Clear();
        p.ApprovedBudget = 100;
        db.Projects.Add(p);
        await db.SaveChangesAsync();
        db.LaborEntries.Add(new LaborEntry { ProjectId = p.Id, WorkerName = "Alex", Hours = 25, HourlyRate = 1, WorkDate = p.StartDate });
        exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
    }
    [SqlServerFact]
    public async Task RowVersionDetectsConcurrentStatusWrites()
    {
        await using var app = await SqlTestApp.CreateAsync();
        await using var first = app.Context();
        var p = new Project { Name = "Bridge", ApprovedBudget = 1, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) };
        first.Projects.Add(p);
        await first.SaveChangesAsync();
        await using var second = app.Context();
        var stale = await second.Projects.SingleAsync(x => x.Id == p.Id);
        p.Status = ProjectStatus.Active;
        await first.SaveChangesAsync();
        stale.Status = ProjectStatus.Cancelled;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
    [SqlServerFact]
    public async Task MigrationsSeedAndIndexesAreRepeatable()
    {
        await using var app = await SqlTestApp.CreateAsync();
        await using var db = app.Context();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await DatabaseInitializer.SeedAsync(db, default);
        await DatabaseInitializer.SeedAsync(db, default);
        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.Equal(1, await db.LaborEntries.CountAsync());
        Assert.Equal(1, await db.Expenses.CountAsync());
        var budget = await new CostStore(db).BudgetAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), default);
        Assert.NotNull(budget);
        Assert.Equal(4840m, budget.TotalSpent);
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.indexes WHERE name IN ('IX_Projects_Status_StartDate', 'IX_LaborEntries_ProjectId_WorkDate', 'IX_Expenses_ProjectId_IncurredDate', 'IX_Expenses_Category')").ToListAsync();
        Assert.Equal(4, indexes.Count);
        var scales = await db.Database.SqlQueryRaw<int>("SELECT CAST(scale AS int) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'Amount'").ToListAsync();
        Assert.Equal(2, Assert.Single(scales));
    }
    [SqlServerFact]
    public async Task EmptyFinancialReportReturnsZeros()
    {
        await using var app = await SqlTestApp.CreateAsync();
        using var client = app.Factory.CreateClient();
        var response = await client.GetAsync("/api/reports/financials");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, json.GetProperty("projectCount").GetInt32());
        Assert.Equal(0, json.GetProperty("remainingBudget").GetDecimal());
    }
}

internal sealed class SqlTestApp : IAsyncDisposable
{
    private readonly string connection;
    public WebApplicationFactory<Program> Factory { get; }
    private SqlTestApp(string connection)
    {
        this.connection = connection;
        Factory = new SqlApiFactory(connection);
    }
    internal OperationsDbContext Context() => new(new DbContextOptionsBuilder<OperationsDbContext>().UseSqlServer(connection).Options);
    internal static async Task<SqlTestApp> CreateAsync()
    {
        var source = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING")
            ?? throw new InvalidOperationException("SQL Server test connection is required.");
        var builder = new SqlConnectionStringBuilder(source) { InitialCatalog = "ConstructionOps_Test_" + Guid.NewGuid().ToString("N") };
        var app = new SqlTestApp(builder.ConnectionString);
        try
        {
            await using var db = app.Context();
            await db.Database.MigrateAsync();
            return app;
        }
        catch { await app.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
    }
    private sealed class SqlApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Operations", connection);
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Database:Seed", "false");
        }
    }
}
