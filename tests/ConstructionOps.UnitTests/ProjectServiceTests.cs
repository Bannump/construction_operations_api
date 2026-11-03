using ConstructionOps.Application;
namespace ConstructionOps.UnitTests;

public sealed class ProjectServiceTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    [Fact]
    public async Task CreateAndRetrievePreservesBudgetAndTrimsName()
    {
        var service = new ProjectService(new MemoryStore());
        var p = await service.CreateAsync(new(" Site A ", 1000.12m, Start, Start.AddDays(30)), default);
        Assert.Equal("Site A", p.Name);
        Assert.Equal(ProjectStatus.Planned, p.Status);
        Assert.Equal(p, await service.GetAsync(p.Id, default));
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    public async Task InvalidBudgetIsRejected(decimal amount)
    {
        var service = new ProjectService(new MemoryStore());
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new("Site", amount, Start, Start), default));
    }
    [Fact]
    public async Task MissingProjectReturnsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new ProjectService(new MemoryStore()).GetAsync(Guid.NewGuid(), default));

    [Fact]
    public async Task StatusUpdatePersistsAndTerminalProjectCannotReopen()
    {
        var service = new ProjectService(new MemoryStore());
        var p = await service.CreateAsync(new("Site", 100, Start, Start), default);
        await service.UpdateStatusAsync(p.Id, new(ProjectStatus.Active), default);
        await service.UpdateStatusAsync(p.Id, new(ProjectStatus.Completed), default);
        Assert.Equal(ProjectStatus.Completed, (await service.GetAsync(p.Id, default)).Status);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateStatusAsync(p.Id, new(ProjectStatus.Active), default));
    }
    [Fact]
    public async Task InvalidPaginationAndEmptyIdAreRejected()
    {
        var service = new ProjectService(new MemoryStore());
        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(Guid.Empty, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync(null, 0, 20, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync((ProjectStatus)999, 1, 20, default));
    }
    private sealed class MemoryStore : IProjectStore
    {
        private readonly List<Project> projects = [];
        public Task AddAsync(Project p, CancellationToken ct) { projects.Add(p); return Task.CompletedTask; }
        public Task<Project?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(projects.SingleOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, int page, int size, CancellationToken ct) => Task.FromResult<IReadOnlyList<Project>>(projects);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
