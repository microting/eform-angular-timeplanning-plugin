using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;
using TimePlanning.Pn.Services.AbsenceRequestService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for AbsenceRequest approval writing absence flags into a
/// soft-removed PlanRegistration. ApplyAbsenceToPlanRegistration matched by
/// (SdkSitId, Date) with no WorkflowState filter, so an approval could stamp
/// OnVacation/Sick onto a Removed row instead of creating a fresh active one.
///
/// Structured to FAIL pre-fix (Removed row gets OnVacation) and PASS post-fix
/// (Removed row untouched; a new active row carries the absence).
/// </summary>
[TestFixture]
public class AbsenceRequestRemovedRowTests : TestBaseSetup
{
    private IAbsenceRequestService _service;

    [SetUp]
    public async Task SetUpTest()
    {
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(Task.FromResult(core));

        // Site 2 is the signed-in manager of the worker.
        await SeedManagerCallerAsync(userService, managerSiteUid: 2, workerSiteUid: 1);

        _service = new AbsenceRequestService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AbsenceRequestService>>(),
            TimePlanningPnDbContext,
            userService,
            localizationService,
            coreService,
            SeededBaseDbContext!,
            Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>());
    }

    [Test]
    public async Task ApproveAsync_SkipsRemovedPlanRegistration_AndCreatesFreshActiveRow()
    {
        const int workerSdkSitId = 1;
        var day1Date = new DateTime(2024, 3, 1);

        // A soft-removed PlanRegistration already exists for this worker/date.
        var removed = new PlanRegistration
        {
            SdkSitId = workerSdkSitId,
            Date = day1Date,
            OnVacation = false,
            Sick = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removed.Create(TimePlanningPnDbContext);
        await removed.Delete(TimePlanningPnDbContext); // WorkflowState -> Removed
        var removedId = removed.Id;

        // A pending vacation request covering that date.
        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = workerSdkSitId,
            DateFrom = day1Date,
            DateTo = day1Date,
            Status = AbsenceRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        var day = new AbsenceRequestDay
        {
            AbsenceRequestId = request.Id,
            Date = day1Date,
            MessageId = 2, // Vacation (seeded)
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await day.Create(TimePlanningPnDbContext);

        var result = await _service.ApproveAsync(request.Id,
            new AbsenceRequestDecisionModel { DecisionComment = "Approved" });
        Assert.That(result.Success, Is.True, result.Message);

        // The removed row must not have been used as the absence target.
        var reloadedRemoved = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == removedId);
        Assert.That(reloadedRemoved.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        Assert.That(reloadedRemoved.OnVacation, Is.False,
            "Approval must not stamp vacation onto a removed PlanRegistration");

        // A fresh active row must carry the vacation flag.
        var activeRows = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == workerSdkSitId && x.Date == day1Date
                        && x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        Assert.That(activeRows.Count, Is.EqualTo(1),
            "Approval must create a fresh active row rather than reuse the removed one");
        Assert.That(activeRows[0].OnVacation, Is.True);
    }
}
