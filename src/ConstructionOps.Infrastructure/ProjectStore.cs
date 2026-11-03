using ConstructionOps.Application;
using Microsoft.EntityFrameworkCore;
namespace ConstructionOps.Infrastructure;

public sealed class ProjectStore(OperationsDbContext db) : IProjectStore
{
    public async Task AddAsync(Project project, CancellationToken ct) { db.Projects.Add(project); await db.SaveChangesAsync(ct); }
    public Task<Project?> FindAsync(Guid id, CancellationToken ct) => db.Projects.SingleOrDefaultAsync(p => p.Id == id, ct);
    public async Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, int page, int pageSize, CancellationToken ct) =>
        await db.Projects.AsNoTracking().Where(p => !status.HasValue || p.Status == status)
            .OrderBy(p => p.StartDate).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
