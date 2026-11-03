using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ConstructionOps.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<OperationsDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__Operations")
            ?? "Server=localhost,1433;Database=ConstructionOps;Integrated Security=true;TrustServerCertificate=true")
        .Options);
}
