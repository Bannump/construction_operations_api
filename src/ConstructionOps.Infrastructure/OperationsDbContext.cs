using ConstructionOps.Application;
using Microsoft.EntityFrameworkCore;
namespace ConstructionOps.Infrastructure;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<LaborEntry> LaborEntries => Set<LaborEntry>();
    public DbSet<Expense> Expenses => Set<Expense>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        var p = b.Entity<Project>();
        p.HasKey(x => x.Id);
        p.Property(x => x.Name).HasMaxLength(200).IsRequired();
        p.Property(x => x.ApprovedBudget).HasPrecision(14, 2);
        p.Property(x => x.Version).IsRowVersion();
        p.HasIndex(x => new { x.Status, x.StartDate });
        p.HasMany(x => x.LaborEntries).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        p.HasMany(x => x.Expenses).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        p.ToTable("Projects", t =>
        {
            t.HasCheckConstraint("CK_Projects_Budget", "[ApprovedBudget] >= 0");
            t.HasCheckConstraint("CK_Projects_Dates", "[EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_Projects_Status", "[Status] BETWEEN 0 AND 3");
        });
        var l = b.Entity<LaborEntry>();
        l.HasKey(x => x.Id);
        l.Property(x => x.WorkerName).HasMaxLength(200).IsRequired();
        l.Property(x => x.Hours).HasPrecision(4, 2);
        l.Property(x => x.HourlyRate).HasPrecision(14, 2);
        l.Ignore(x => x.Cost);
        l.HasIndex(x => new { x.ProjectId, x.WorkDate });
        l.ToTable("LaborEntries", t =>
        {
            t.HasCheckConstraint("CK_Labor_Hours", "[Hours] > 0 AND [Hours] <= 24");
            t.HasCheckConstraint("CK_Labor_Rate", "[HourlyRate] >= 0");
        });
        var e = b.Entity<Expense>();
        e.HasKey(x => x.Id);
        e.Property(x => x.Description).HasMaxLength(500).IsRequired();
        e.Property(x => x.Amount).HasPrecision(14, 2);
        e.HasIndex(x => new { x.ProjectId, x.IncurredDate });
        e.HasIndex(x => x.Category);
        e.ToTable("Expenses", t =>
        {
            t.HasCheckConstraint("CK_Expenses_Amount", "[Amount] >= 0");
            t.HasCheckConstraint("CK_Expenses_Category", "[Category] BETWEEN 0 AND 4");
        });
    }
}
