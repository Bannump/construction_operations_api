using ConstructionOps.Application;
using Microsoft.AspNetCore.Mvc;
namespace ConstructionOps.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}")]
public sealed class CostsController(ICostService service, ILogger<CostsController> logger) : ControllerBase
{
    [HttpPost("labor")]
    public async Task<ActionResult<LaborDto>> Labor(Guid projectId, AddLaborRequest request, CancellationToken ct)
    {
        var entry = await service.AddLaborAsync(projectId, request, ct);
        logger.LogInformation("Added labor {EntryId} to project {ProjectId}", entry.Id, projectId);
        return StatusCode(StatusCodes.Status201Created, entry);
    }
    [HttpPost("expenses")]
    public async Task<ActionResult<ExpenseDto>> Expense(Guid projectId, AddExpenseRequest request, CancellationToken ct)
    {
        var expense = await service.AddExpenseAsync(projectId, request, ct);
        logger.LogInformation("Added expense {ExpenseId} to project {ProjectId}", expense.Id, projectId);
        return StatusCode(StatusCodes.Status201Created, expense);
    }
    [HttpGet("budget")]
    public async Task<ActionResult<BudgetSummary>> Budget(Guid projectId, CancellationToken ct) => Ok(await service.BudgetAsync(projectId, ct));
}
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ICostService service) : ControllerBase
{
    [HttpGet("financials")]
    public async Task<ActionResult<FinancialSummary>> Financials(CancellationToken ct, ProjectStatus? status = null) => Ok(await service.FinancialsAsync(status, ct));
}
