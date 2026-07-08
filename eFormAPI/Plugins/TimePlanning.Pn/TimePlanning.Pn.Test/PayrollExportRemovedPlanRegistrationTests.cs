using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PayrollExportService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for the "removed shifts leak into payroll money" bug.
/// PlanRegistrationPayLines were joined to PlanRegistration but only the pay
/// line's own WorkflowState was filtered — a pay line whose underlying
/// PlanRegistration is soft-removed was still exported/previewed as paid hours.
/// The fix adds "PlanRegistration.WorkflowState != Removed" to both queries.
///
/// Each test is structured to FAIL pre-fix (the removed-PR pay line is counted)
/// and PASS post-fix (it is excluded).
/// </summary>
[TestFixture]
public class PayrollExportRemovedPlanRegistrationTests : TestBaseSetup
{
    private IPayrollExportService _service;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        _service = new PayrollExportService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<PayrollExportService>>(),
            _localizationService,
            _coreService);
    }

    private async Task<PlanRegistration> SeedPlanRegistrationWithPayLine(
        int sdkSitId, DateTime date, double hours, bool removed)
    {
        var pr = new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = date,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pr.Create(TimePlanningPnDbContext);

        var payLine = new PlanRegistrationPayLine
        {
            PlanRegistrationId = pr.Id,
            PayCode = "WORK",
            PayrollCode = "100",
            Hours = hours,
            HoursInSeconds = (int)(hours * 3600),
            CalculatedAt = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await payLine.Create(TimePlanningPnDbContext);

        // Soft-remove the PlanRegistration AFTER the (active) pay line exists,
        // reproducing the production shape: live pay line, dead parent shift.
        if (removed)
        {
            await pr.Delete(TimePlanningPnDbContext);
        }

        return pr;
    }

    [Test]
    public async Task PreviewPayroll_ExcludesPayLineWhosePlanRegistrationIsRemoved()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);

        // Active shift for worker 100 (7 h) — must be counted.
        await SeedPlanRegistrationWithPayLine(100, new DateTime(2026, 1, 10), 7.0, removed: false);
        // Removed shift for worker 200 (5 h) — must NOT be counted.
        await SeedPlanRegistrationWithPayLine(200, new DateTime(2026, 1, 11), 5.0, removed: true);

        var result = await _service.PreviewPayroll(periodStart, periodEnd);

        Assert.That(result.Success, Is.True, result.Message);
        // Pre-fix: WorkerCount == 2 and TotalHours == 12. Post-fix: only the active line.
        Assert.That(result.Model.WorkerCount, Is.EqualTo(1),
            "Only the active worker's pay line must be previewed");
        Assert.That(result.Model.TotalHours, Is.EqualTo(7.0m),
            "Removed PlanRegistration hours must not be summed into the preview");
    }

    [Test]
    public async Task ExportPayroll_TreatsRemovedPlanRegistrationPayLineAsNoData()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);

        // Configure a payroll system so the export gets past the settings gate.
        var settings = new PayrollIntegrationSettings
        {
            PayrollSystem = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await settings.Create(TimePlanningPnDbContext);

        // The ONLY pay line in range belongs to a removed PlanRegistration.
        await SeedPlanRegistrationWithPayLine(300, new DateTime(2026, 1, 12), 8.0, removed: true);

        var result = await _service.ExportPayroll(periodStart, periodEnd);

        // Post-fix: the removed line is filtered out, so there is no data to export.
        // Pre-fix: the line survives and the export proceeds past this guard.
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("NoPayrollDataForPeriod"),
            "A removed PlanRegistration must not be exported as payable hours");
    }
}
