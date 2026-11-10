using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
using TimePlanning.Pn.Services.ContentHandoverService;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// R4 on handover accept: moving a day's plan changes both workers' PlanHours
/// on that day, so both balances must be re-chained from that day through
/// each worker's last row. No AssignedSites are seeded (five-minute walk).
/// </summary>
[TestFixture]
public class HandoverAcceptFlexCarryForwardTests : TestBaseSetup
{
    private const int Sender = 7961;
    private const int Receiver = 7962;
    private IContentHandoverService _service = null!;
    private IUserService _userService = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var push = Substitute.For<IPushNotificationService>();
        var scopeProvider = Substitute.For<IServiceProvider>();
        scopeProvider.GetService(typeof(IPushNotificationService)).Returns(push);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopeProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        // Real core and BaseDbContext, so the service can resolve the seeded caller.
        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(Task.FromResult(core));
        SeededBaseDbContext ??= GetBaseDbContext();

        _service = new ContentHandoverService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ContentHandoverService>>(),
            TimePlanningPnDbContext!,
            _userService,
            localizationService,
            coreService,
            SeededBaseDbContext,
            scopeFactory);
    }

    private async Task<PlanRegistration> Seed(int sdkSitId, DateTime date, double planHours, double nettoHours,
        double sumFlexStart, double sumFlexEnd, string? planText = null)
    {
        var row = new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = date,
            PlanHours = planHours,
            PlanHoursInSeconds = (int)Math.Round(planHours * 3600),
            NettoHours = nettoHours,
            Flex = nettoHours - planHours,
            SumFlexStart = sumFlexStart,
            SumFlexEnd = sumFlexEnd,
            PlanText = planText,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    private async Task<PlanRegistration> Stored(int sdkSitId, DateTime date) =>
        await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == sdkSitId && x.Date == date);

    [Test]
    public async Task AcceptingAFullDayHandover_RechainsBothWorkersFromTheDayThroughTheirLastRows()
    {
        var date = new DateTime(2024, 1, 10);
        var later = date.AddDays(30);
        await Seed(Sender, date.AddDays(-1), 0, 0, 3, 3);
        var source = await Seed(Sender, date, planHours: 8, nettoHours: 8, sumFlexStart: 3, sumFlexEnd: 3,
            planText: "Important work");
        await Seed(Sender, later, 0, 0, 3, 3);
        await Seed(Receiver, date.AddDays(-1), 0, 0, 1, 1);
        var target = await Seed(Receiver, date, planHours: 0, nettoHours: 0, sumFlexStart: 1, sumFlexEnd: 1);
        await Seed(Receiver, later, 0, 0, 1, 1);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = Sender,
            ToSdkSitId = Receiver,
            Date = date,
            FromPlanRegistrationId = source.Id,
            ToPlanRegistrationId = target.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext!);

        await SeedSiteCallerAsync(_userService, Receiver); // the receiver is the logged-in caller

        var result = await _service.AcceptAsync(request.Id,
            new ContentHandoverDecisionModel { DecisionComment = "ok" });

        Assert.That(result.Success, Is.True, result.Message);
        var senderDay = await Stored(Sender, date);
        var senderLater = await Stored(Sender, later);
        var receiverDay = await Stored(Receiver, date);
        var receiverLater = await Stored(Receiver, later);
        Assert.Multiple(() =>
        {
            // Sender: plan 8 → 0 with 8 h worked → +8 on top of 3.
            Assert.That(senderDay.Flex, Is.EqualTo(8.0).Within(1e-9));
            Assert.That(senderDay.SumFlexEnd, Is.EqualTo(11.0).Within(1e-9));
            Assert.That(senderLater.SumFlexStart, Is.EqualTo(senderDay.SumFlexEnd).Within(1e-9));
            // Receiver: plan 0 → 8 with nothing worked → −8 on top of 1.
            Assert.That(receiverDay.Flex, Is.EqualTo(-8.0).Within(1e-9));
            Assert.That(receiverDay.SumFlexEnd, Is.EqualTo(-7.0).Within(1e-9));
            Assert.That(receiverLater.SumFlexStart, Is.EqualTo(receiverDay.SumFlexEnd).Within(1e-9));
        });
    }
}
