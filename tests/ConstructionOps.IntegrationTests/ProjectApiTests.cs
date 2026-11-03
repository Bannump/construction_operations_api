using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ConstructionOps.IntegrationTests;

public sealed class ProjectApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;
    public ProjectApiTests(ApiFactory factory) => client = factory.CreateClient();
    [Fact]
    public async Task CreateReturnsLocationAndRetrievableProject()
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Bridge", approvedBudget = 12000.25m, startDate = "2026-01-01", endDate = "2026-12-31" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bridge", body.GetProperty("name").GetString());
        Assert.Equal(12000.25m, body.GetProperty("approvedBudget").GetDecimal());
        Assert.Equal("Planned", body.GetProperty("status").GetString());
    }
}
