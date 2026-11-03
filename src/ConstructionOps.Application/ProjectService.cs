namespace ConstructionOps.Application;

public sealed class ProjectService(IProjectStore store) : IProjectService
{
    public async Task<ProjectDto> CreateAsync(CreateProjectRequest r, CancellationToken ct)
    {
        Rules.Text(r.Name, 200, "Name");
        Rules.Money(r.ApprovedBudget, "Approved budget");
        Rules.Dates(r.StartDate, r.EndDate);
        var project = new Project { Name = r.Name.Trim(), ApprovedBudget = r.ApprovedBudget, StartDate = r.StartDate, EndDate = r.EndDate };
        await store.AddAsync(project, ct);
        return ProjectDto.From(project);
    }
    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct) => ProjectDto.From(await RequireAsync(id, ct));
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectStatus? status, int page, int pageSize, CancellationToken ct)
    {
        if ((status.HasValue && !Enum.IsDefined(status.Value)) || page < 1 || page > 1000000 || pageSize is < 1 or > 100)
            throw new ValidationException("Invalid status or pagination. Page must be 1..1000000 and pageSize 1..100.");
        return (await store.ListAsync(status, page, pageSize, ct)).Select(ProjectDto.From).ToArray();
    }
    public async Task<ProjectDto> UpdateStatusAsync(Guid id, UpdateStatusRequest r, CancellationToken ct)
    {
        var p = await RequireAsync(id, ct);
        Rules.Transition(p.Status, r.Status);
        p.Status = r.Status;
        await store.SaveAsync(ct);
        return ProjectDto.From(p);
    }
    private async Task<Project> RequireAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ValidationException("Project ID is required.");
        return await store.FindAsync(id, ct) ?? throw new NotFoundException($"Project {id} was not found.");
    }
}
