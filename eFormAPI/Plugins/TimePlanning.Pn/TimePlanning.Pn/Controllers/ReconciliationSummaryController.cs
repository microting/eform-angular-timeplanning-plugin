#nullable enable
namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.ReconciliationSummaryService;

/// <summary>
/// Read-only Afstem statistics, polled by my-microting's customer-stats scan
/// with the service login. See ReconciliationSummaryModel for the wire shape.
/// </summary>
[Authorize(Roles = EformRole.Admin)]
[Route("api/time-planning-pn/reconciliation")]
public class ReconciliationSummaryController(IReconciliationSummaryService reconciliationSummaryService) : Controller
{
    [HttpGet("summary")]
    public Task<OperationDataResult<ReconciliationSummaryModel>> Summary()
        => reconciliationSummaryService.GetSummaryAsync();
}
