using ConstructionOps.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace ConstructionOps.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string database = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Operations", "Server=unused;Database=unused");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<OperationsDbContext>();
            services.RemoveAll<DbContextOptions<OperationsDbContext>>();
            services.AddDbContext<OperationsDbContext>(o => o.UseInMemoryDatabase(database));
        });
    }
}
