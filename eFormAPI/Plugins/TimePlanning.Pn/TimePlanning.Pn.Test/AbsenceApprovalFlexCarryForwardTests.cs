using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
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
/// R4 on absence approval: approving creates rows for days that had none,
/// with an empty balance. The worker's balance must be carried through those
/// rows and on to the last row, including one pre-created for a future date.
/// No AssignedSite is seeded: the walk must tolerate its absence.
/// </summary>
[TestFixture]
public class AbsenceApprovalFlexCarryForwardTests : TestBaseSetup
{
    private const int SdkSitId = 7951;
    private IAbsenceRequestService _service = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(Task.FromResult(core));

        _service = new AbsenceRequestService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AbsenceRequestService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            coreService,
            Substitute.For<BaseDbContext>(new DbContextOptions<BaseDbContext>()),
            Substitute.For<TimePlanning.Pn.Services.PushNotificationService.IPushNotificationService>());
    }

    private async Task SeedRow(DateTime date, double planHours, double nettoHours, double sumFlexStart, double sumFlexEnd) =>
        await new PlanRegistration
        {
            SdkSitId = SdkSitId,
            Date = date,
            PlanHours = planHours,
            NettoHours = nettoHours,
            Flex = nettoHours - planHours,
            SumFlexStart = sumFlexStart,
            SumFlexEnd = sumFlexEnd,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

    private async Task<PlanRegistration> Stored(DateTime date) =>
        await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == SdkSitId && x.Date == date
                              && x.WorkflowState != Constants.WorkflowStates.Removed);

    [Test]
    public async Task ApprovingAbsenceOnDaysWithoutRows_ChainsTheNewRows_AndCarriesToAFutureRow()
    {
        var before = DateTime.Now.Date.AddDays(-12);
        var day1 = DateTime.Now.Date.AddDays(-10);
        var day2 = DateTime.Now.Date.AddDays(-9);
        var future = DateTime.Now.Date.AddDays(20);
        await SeedRow(before, planHours: 0, nettoHours: 0, sumFlexStart: 5, sumFlexEnd: 5);
        // Stale on purpose: plan 7.5, worked 8 → +0.5 on top of 5 is 5.5.
        await SeedRow(future, planHours: 7.5, nettoHours: 8, sumFlexStart: 99, sumFlexEnd: 99.5);

        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = SdkSitId,
            DateFrom = day1,
            DateTo = day2,
            Status = AbsenceRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext!);
        foreach (var date in new[] { day1, day2 })
        {
            await new AbsenceRequestDay
            {
                AbsenceRequestId = request.Id,
                Date = date,
                MessageId = 2, // Vacation (seeded)
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            }.Create(TimePlanningPnDbContext!);
        }

        var result = await _service.ApproveAsync(request.Id,
            new AbsenceRequestDecisionModel { ManagerSdkSitId = 2, DecisionComment = "ok" });

        Assert.That(result.Success, Is.True, result.Message);
        var r0 = await Stored(before);
        var r1 = await Stored(day1);
        var r2 = await Stored(day2);
        var last = await Stored(future);
        Assert.Multiple(() =>
        {
            Assert.That(r1.OnVacation, Is.True);
            Assert.That(r1.SumFlexStart, Is.EqualTo(r0.SumFlexEnd).Within(1e-9), "created row chained");
            Assert.That(r1.SumFlexEnd, Is.EqualTo(5.0).Within(1e-9));
            Assert.That(r2.SumFlexStart, Is.EqualTo(r1.SumFlexEnd).Within(1e-9));
            Assert.That(last.SumFlexStart, Is.EqualTo(r2.SumFlexEnd).Within(1e-9),
                "carried through the future pre-created row");
            Assert.That(last.SumFlexEnd, Is.EqualTo(5.5).Within(1e-9));
            Assert.That(last.NettoHours, Is.EqualTo(8.0).Within(1e-9), "hours untouched (R2)");
        });
    }
}
