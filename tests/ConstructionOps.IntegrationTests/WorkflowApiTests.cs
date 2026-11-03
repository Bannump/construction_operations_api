using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
namespace ConstructionOps.IntegrationTests;

public sealed class WorkflowApiTests
{
    [Fact]
    public async Task CostsBudgetReportAndStatusWorkTogether()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await ApiScenarios.FinancialWorkflow(client);
    }
    [Theory]
    [InlineData("{\"name\":\" \",\"approvedBudget\":100,\"startDate\":\"2026-01-01\",\"endDate\":\"2026-12-31\"}")]
    [InlineData("{\"name\":\"A\",\"approvedBudget\":-1,\"startDate\":\"2026-01-01\",\"endDate\":\"2026-12-31\"}")]
    [InlineData("{\"name\":\"A\",\"approvedBudget\":1,\"startDate\":\"2026-12-31\",\"endDate\":\"2026-01-01\"}")]
    [InlineData("{\"name\":\"A\",\"approvedBudget\":1,\"startDate\":\"bad\",\"endDate\":\"2026-01-01\"}")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task InvalidRequestsReturnProblemDetails(string json)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/projects", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
    [Fact]
    public async Task MissingAndEmptyProjectIdsReturnMeaningfulErrors()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/projects/{Guid.Empty}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/projects?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/financials?status=Unknown")).StatusCode);
    }
}

internal static class ApiScenarios
{
    internal static async Task<Guid> CreateProject(HttpClient client, decimal budget = 1000)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Bridge", approvedBudget = budget, startDate = "2026-01-01", endDate = "2026-12-31" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    internal static async Task FinancialWorkflow(HttpClient client)
    {
        var id = await CreateProject(client);
        var empty = await client.GetFromJsonAsync<JsonElement>($"/api/projects/{id}/budget");
        Assert.Equal(0m, empty.GetProperty("totalSpent").GetDecimal());
        var labor = await client.PostAsJsonAsync($"/api/projects/{id}/labor", new { workerName = "Alex", hours = .1m, hourlyRate = .05m, workDate = "2026-02-01" });
        Assert.Equal(HttpStatusCode.Created, labor.StatusCode);
        Assert.Equal(.01m, (await labor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cost").GetDecimal());
        // Two half-cent entries must sum to two cents, not round only the final sum.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/projects/{id}/labor", new { workerName = "Alex", hours = .1m, hourlyRate = .05m, workDate = "2026-02-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/projects/{id}/expenses", new { description = "Steel", amount = 1200.12m, category = "Materials", incurredDate = "2026-02-01" })).StatusCode);
        var budget = await client.GetFromJsonAsync<JsonElement>($"/api/projects/{id}/budget");
        Assert.Equal(.02m, budget.GetProperty("laborCost").GetDecimal());
        Assert.Equal(1200.14m, budget.GetProperty("totalSpent").GetDecimal());
        Assert.Equal(-200.14m, budget.GetProperty("remainingBudget").GetDecimal());
        await CreateProject(client, 500);
        var report = await client.GetFromJsonAsync<JsonElement>("/api/reports/financials");
        Assert.Equal(2, report.GetProperty("projectCount").GetInt32());
        Assert.Equal(1500m, report.GetProperty("approvedBudget").GetDecimal());
        Assert.Equal(1200.14m, report.GetProperty("totalSpent").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/projects/{id}/status", new { status = "Completed" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/projects/{id}/status", new { status = "Active" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/projects/{id}/status", new { status = "Completed" })).StatusCode);
        var active = await client.GetFromJsonAsync<JsonElement>("/api/reports/financials?status=Active");
        Assert.Equal(0, active.GetProperty("projectCount").GetInt32());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/projects?status=Completed&pageSize=1");
        Assert.Equal(id, list[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/projects/{id}/expenses", new { description = "Steel", amount = -1, category = "Materials", incurredDate = "2026-02-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/projects/{id}/labor", new { workerName = "Alex", hours = 25, hourlyRate = 1, workDate = "2026-02-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/projects/{id}/status", new { status = "Bogus" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/projects/{id}/status", new { status = 1 })).StatusCode);
    }
}
