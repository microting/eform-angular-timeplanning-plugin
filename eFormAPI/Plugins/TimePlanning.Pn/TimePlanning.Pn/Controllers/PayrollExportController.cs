using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Models.PayrollExport;
using TimePlanning.Pn.Services.PayrollExportService;

namespace TimePlanning.Pn.Controllers;

[Authorize(Roles = EformRole.Admin)]
[Route("api/time-planning-pn/payroll")]
public class PayrollExportController : Controller
{
    private readonly IPayrollExportService _payrollExportService;

    public PayrollExportController(IPayrollExportService payrollExportService)
    {
        _payrollExportService = payrollExportService;
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] PayrollExportRequestModel request)
    {
        if (request.PeriodEnd < request.PeriodStart || request.PeriodStart == default || request.PeriodEnd == default)
            return BadRequest(new OperationResult(false, "Invalid date range"));

        var result = await _payrollExportService.ExportPayroll(request.PeriodStart, request.PeriodEnd);
        if (!result.Success)
            return BadRequest(new OperationResult(false, result.ErrorMessage));
        return new FileContentResult(result.FileContent, "text/csv")
        {
            FileDownloadName = result.FileName
        };
    }

    [HttpGet("preview")]
    public async Task<OperationDataResult<PayrollExportPreviewModel>> Preview(
        [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        if (end < start || start == default || end == default)
            return new OperationDataResult<PayrollExportPreviewModel>(false, "Invalid date range");

        return await _payrollExportService.PreviewPayroll(start, end);
    }

    [HttpGet("settings")]
    public async Task<OperationDataResult<PayrollIntegrationSettingsModel>> GetSettings()
    {
        return await _payrollExportService.GetPayrollSettings();
    }

    [HttpPut("settings")]
    public async Task<OperationResult> UpdateSettings([FromBody] PayrollIntegrationSettingsModel model)
    {
        return await _payrollExportService.UpdatePayrollSettings(model);
    }
}

public class PayrollExportRequestModel
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
