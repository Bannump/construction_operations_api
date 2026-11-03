using ConstructionOps.Api;
using System.Text.Json.Serialization;
using ConstructionOps.Application;
using ConstructionOps.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<OperationsDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Operations")
    ?? throw new InvalidOperationException("ConnectionStrings:Operations is required.")));
builder.Services.AddScoped<IProjectStore, ProjectStore>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ICostStore, CostStore>();
builder.Services.AddScoped<ICostService, CostService>();
var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await db.Database.MigrateAsync(timeout.Token);
    if (app.Configuration.GetValue<bool>("Database:Seed")) await DatabaseInitializer.SeedAsync(db, timeout.Token);
}
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (OperationsDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.Run();
public partial class Program { }
