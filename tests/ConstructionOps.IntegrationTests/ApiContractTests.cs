using System.Net;
using System.Text.Json;
using ConstructionOps.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
namespace ConstructionOps.IntegrationTests;

public sealed class ApiContractTests
{
    [Fact]
    public async Task UnexpectedErrorsReturnGenericProblemDetailsWithTraceId()
    {
        using var factory = new ApiFactory();
        using var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IProjectStore, FailingStore>()));
        using var client = failing.CreateClient();
        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal diagnostic marker", text);
        using var document = JsonDocument.Parse(text);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }
    [Fact]
    public async Task DevelopmentOpenApiDocumentDescribesProjectAndReportRoutes()
    {
        using var factory = new ApiFactory();
        using var development = factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = development.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/api/projects", out _));
        Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/api/reports/financials", out _));
    }
    private sealed class FailingStore : IProjectStore
    {
        public Task AddAsync(Project p, CancellationToken ct) => throw new InvalidOperationException("Internal diagnostic marker");
        public Task<Project?> FindAsync(Guid id, CancellationToken ct) => throw new InvalidOperationException("Internal diagnostic marker");
        public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? s, int p, int size, CancellationToken ct) => throw new InvalidOperationException();
        public Task SaveAsync(CancellationToken ct) => throw new InvalidOperationException();
    }
}
