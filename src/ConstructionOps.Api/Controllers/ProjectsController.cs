using ConstructionOps.Application;
using Microsoft.AspNetCore.Mvc;
namespace ConstructionOps.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(IProjectService service, ILogger<ProjectsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await service.CreateAsync(request, ct);
        logger.LogInformation("Created project {ProjectId}", project.Id);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List(CancellationToken ct, ProjectStatus? status = null, int page = 1, int pageSize = 20) =>
        Ok(await service.ListAsync(status, page, pageSize, ct));
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ProjectDto>> Status(Guid id, UpdateStatusRequest request, CancellationToken ct) => Ok(await service.UpdateStatusAsync(id, request, ct));
}
