using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eForm.Infrastructure.Constants;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using TimePlanningWorkingHoursService = TimePlanning.Pn.Services.TimePlanningWorkingHoursService.TimePlanningWorkingHoursService;
using TimePlanning.Pn.Test.Helpers;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using RegistrationDeviceEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.RegistrationDevice;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression tests: when <c>PlanRegistration.NettoHoursOverrideActive</c> is
/// true, <c>NettoHoursOverride</c> is the day's effective netto — the base
/// FlexChain computes Flex from it, and the Excel totals honour it. Every
/// value the mobile app receives or writes must agree with that rule:
///  1. the period summary's TotalWorked* (CalculateHoursSummary),
///  2. the day view's NetWorkingHours (ReadWorkingHours → ReadBySiteAndDate),
///  3. the Flex/SumFlexEnd a five-minute punch persists (both UpdateWorkingHour
///     legs and TimePlanningPlanningService.UpdateByCurrentUserNam),
///  4. the dashboard's PercentageCompleted (UpdatePlanRegistrationsInPeriod).
/// </summary>
[TestFixture]
public class MobileOverrideConsistencyRegressionTests : TestBaseSetup
{
    private const int SiteUid = 8731;
    private static readonly string KioskToken = Guid.NewGuid().ToString("N");

    private IUserService _userService = null!;
    private TimePlanningWorkingHoursService _workingHoursService = null!;
    private TimePlanningWorkingHoursGrpcService _grpc = null!;
    private TimePlanningPlanningService _planningService = null!;
    private IPluginDbOptions<TimePlanningBaseSettings> _options = null!;

    private static readonly DateTime PreviousDay = new(2026, 3, 9);
    private static readonly DateTime OverrideDay = new(2026, 3, 10);
    private const string OverrideDayIso = "2026-03-10";

    /// <summary>A 08:00 → 14:00 punch (6.0 h) on OverrideDay, as the app sends it.</summary>
    private static WorkingHoursModel OverrideDayPunch() => new()
    {
        Start1Id = 97,
        Start1StartedAt = OverrideDayIso + "T08:00:00",
        Stop1Id = 169,
        Stop1StoppedAt = OverrideDayIso + "T14:00:00",
        Comment = ""
    };

    [SetUp]
    public async Task SetUpTest()
    {
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        await SeedSiteCallerAsync(_userService, SiteUid);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var core = await GetCore();
        var coreService = Substitute.For<IEFormCoreService>();
        coreService.GetCore().Returns(core);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // Five-minute site (UseOneMinuteIntervals OFF) that lets the worker edit
        // past days, so a punch on OverrideDay reaches the decimal chain.
        await new AssignedSiteEntity
        {
            SiteId = SiteUid,
            UseOneMinuteIntervals = false,
            AllowEditOfRegistrations = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        _workingHoursService = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            _userService,
            localizationService,
            SeededBaseDbContext!,
            _options,
            coreService);
        _grpc = new TimePlanningWorkingHoursGrpcService(_workingHoursService);

        var dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);
        _planningService = new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            _options,
            TimePlanningPnDbContext!,
            dbContextHelper,
            _userService,
            localizationService,
            SeededBaseDbContext!,
            coreService);
    }

    private async Task<PlanRegistrationEntity> SeedDay(
        DateTime date, double planHours, double nettoHours,
        bool overrideActive = false, double nettoHoursOverride = 0,
        double flex = 0, double sumFlexStart = 0, double sumFlexEnd = 0,
        int start1Id = 0, int stop1Id = 0)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = SiteUid,
            Date = date,
            PlanHours = planHours,
            NettoHours = nettoHours,
            NettoHoursOverrideActive = overrideActive,
            NettoHoursOverride = nettoHoursOverride,
            Flex = flex,
            SumFlexStart = sumFlexStart,
            SumFlexEnd = sumFlexEnd,
            Start1Id = start1Id,
            Stop1Id = stop1Id,
            RegisteredUnderOneMinuteIntervals = false,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    /// <summary>
    /// PreviousDay closes at +1.0h. OverrideDay: punched 08:00-14:00 (6.0h
    /// computed), plan 7.5h, override 9.0h active, so Flex = 9.0 - 7.5 = 1.5
    /// and SumFlexEnd = 1.0 + 1.5 = 2.5 — stored consistently.
    /// </summary>
    private async Task<PlanRegistrationEntity> SeedPunchScenario()
    {
        await SeedDay(PreviousDay, planHours: 7.5, nettoHours: 8.5,
            flex: 1.0, sumFlexStart: 0, sumFlexEnd: 1.0, start1Id: 97, stop1Id: 199);
        return await SeedDay(OverrideDay, planHours: 7.5, nettoHours: 6.0,
            overrideActive: true, nettoHoursOverride: 9.0,
            flex: 1.5, sumFlexStart: 1.0, sumFlexEnd: 2.5,
            start1Id: 97, stop1Id: 169);
    }

    private async Task AssertOverrideFlexPersisted(string leg)
    {
        var row = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == SiteUid && x.Date == OverrideDay);
        Assert.Multiple(() =>
        {
            Assert.That(row.NettoHoursOverrideActive, Is.True, $"{leg}: the punch must not clear the override");
            Assert.That(row.Flex, Is.EqualTo(1.5).Within(1e-6),
                $"{leg}: Flex must be NettoHoursOverride(9.0) - PlanHours(7.5) = 1.5, " +
                $"not computed NettoHours({row.NettoHours}) - PlanHours = {row.NettoHours - row.PlanHours}");
            Assert.That(row.SumFlexStart, Is.EqualTo(1.0).Within(1e-6), $"{leg}: SumFlexStart carries PreviousDay's 1.0");
            Assert.That(row.SumFlexEnd, Is.EqualTo(2.5).Within(1e-6),
                $"{leg}: SumFlexEnd must be 1.0 + override flex 1.5 = 2.5");
        });
    }

    // ---------------------------------------------------------------- 1

    [Test]
    public async Task CalculateHoursSummary_Grpc_TotalWorked_UsesActiveNettoHoursOverride()
    {
        // Override day: computed 6.0h, override 8.0h. Normal day: 7.0h.
        await SeedDay(new DateTime(2026, 2, 2), planHours: 7.5, nettoHours: 6.0,
            overrideActive: true, nettoHoursOverride: 8.0, flex: 0.5, sumFlexEnd: 0.5);
        await SeedDay(new DateTime(2026, 2, 3), planHours: 7.5, nettoHours: 7.0,
            flex: -0.5, sumFlexStart: 0.5, sumFlexEnd: 0);

        var response = await _grpc.CalculateHoursSummary(new CalculateHoursSummaryRequest
        {
            StartDate = "2026-02-02",
            EndDate = "2026-02-03"
        }, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True, response.Message);
        Assert.Multiple(() =>
        {
            Assert.That(response.Model.TotalWorkedHours + response.Model.TotalWorkedMinutes / 60.0,
                Is.EqualTo(15.0).Within(1e-6),
                "TotalWorked must be override 8.0 + 7.0 = 15.0h (the raw NettoHours sum is 13.0)");
            Assert.That(response.Model.TotalWorkedSeconds, Is.EqualTo(15 * 3600),
                "TotalWorkedSeconds must agree with the override-aware 15.0h");
        });
    }

    // ---------------------------------------------------------------- 2

    [Test]
    public async Task ReadWorkingHours_Grpc_NetWorkingHours_IsEffectiveNetto_WhenOverrideActive()
    {
        // Computed 6.0h, override 7.5h, plan 7.5h → stored Flex 0 (override-based).
        await SeedDay(OverrideDay, planHours: 7.5, nettoHours: 6.0,
            overrideActive: true, nettoHoursOverride: 7.5,
            flex: 0, sumFlexStart: 0, sumFlexEnd: 0, start1Id: 97, stop1Id: 169);

        var response = await _grpc.ReadWorkingHours(new ReadWorkingHoursRequest
        {
            Date = OverrideDayIso
        }, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True, response.Message);
        Assert.Multiple(() =>
        {
            Assert.That(response.Model.NetWorkingHours, Is.EqualTo(7.5).Within(1e-6),
                "NetWorkingHours sent to the app must be the active override 7.5, not the computed 6.0");
            Assert.That(response.Model.NetWorkingHours - response.Model.PlanHours,
                Is.EqualTo(response.Model.FlexHours).Within(1e-6),
                "The day view must satisfy netto - plan = flex");
        });
    }

    // ---------------------------------------------------------------- 3

    [Test]
    public async Task UpdateWorkingHours_Grpc_PersonalPunch_FiveMinuteSite_FlexUsesActiveOverride()
    {
        await SeedPunchScenario();

        var response = await _grpc.UpdateWorkingHours(new UpdateWorkingHoursRequest
        {
            Date = OverrideDayIso,
            Model = OverrideDayPunch()
        }, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True, response.Message);
        await AssertOverrideFlexPersisted("personal UpdateWorkingHour");
    }

    [Test]
    public async Task UpdateWorkingHours_Grpc_KioskPunch_FiveMinuteSite_FlexUsesActiveOverride()
    {
        await SeedPunchScenario();
        await new RegistrationDeviceEntity
        {
            Token = KioskToken,
            Name = "Kiosk Override Device",
            OtpCode = "54321",
            SoftwareVersion = "1.0.0",
            Manufacturer = "Test",
            Model = "Test",
            OsVersion = "1.0",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        var response = await _grpc.UpdateWorkingHours(new UpdateWorkingHoursRequest
        {
            Date = OverrideDayIso,
            SdkSiteId = SiteUid,
            Token = KioskToken,
            Model = OverrideDayPunch()
        }, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True, response.Message);
        await AssertOverrideFlexPersisted("kiosk UpdateWorkingHour");
    }

    [Test]
    public async Task UpdateByCurrentUserNam_FiveMinuteSite_FlexUsesActiveOverride()
    {
        var day = await SeedPunchScenario();

        var result = await _planningService.UpdateByCurrentUserNam(new TimePlanningPlanningPrDayModel
        {
            Id = day.Id,
            Date = OverrideDay,
            PlanHours = 7.5,
            Start1Id = 97,
            Stop1Id = 169,
            Pause1Id = 0,
            Start1StartedAt = new DateTime(2026, 3, 10, 8, 0, 0),
            Stop1StoppedAt = new DateTime(2026, 3, 10, 14, 0, 0),
            WorkerComment = ""
        });

        Assert.That(result.Success, Is.True, result.Message);
        await AssertOverrideFlexPersisted("UpdateByCurrentUserNam");
    }

    // ---------------------------------------------------------------- 4

    private async Task<TimePlanningPlanningModel> RunPeriod(DateTime from, DateTime to)
    {
        var assignedSite = await TimePlanningPnDbContext!.AssignedSites.SingleAsync(x => x.SiteId == SiteUid);
        var plannings = await TimePlanningPnDbContext.PlanRegistrations.AsNoTracking()
            .Where(x => x.SdkSitId == SiteUid && x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date)
            .Select(x => new PlanRegistrationEntity { Id = x.Id, Date = x.Date })
            .ToListAsync();
        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = SiteUid,
            SiteName = $"Site {SiteUid}",
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };
        return await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            plannings, siteModel, TimePlanningPnDbContext, assignedSite,
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            new SdkSite { Name = $"Site {SiteUid}", MicrotingUid = SiteUid },
            from, to, _options);
    }

    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_PercentageCompleted_IsWorkedOverPlanned()
    {
        // Days well before the payment cut-off, so no plan recompute. Plan 10h, worked 5h.
        var d1 = DateTime.Today.AddMonths(-3).Date;
        await SeedDay(d1, planHours: 5, nettoHours: 5, flex: 0);
        await SeedDay(d1.AddDays(1), planHours: 5, nettoHours: 0, flex: -5, sumFlexEnd: -5);

        var model = await RunPeriod(d1, d1.AddDays(1));

        Assert.That(model.PercentageCompleted, Is.EqualTo(50),
            "PercentageCompleted must be (worked 5h / planned 10h) * 100 = 50");
    }

    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_PercentageCompleted_CountsActiveOverride()
    {
        // Plan 10h; worked 2.5h plain + override 2.5h (computed 1.0h) = 5h.
        var d1 = DateTime.Today.AddMonths(-3).Date;
        await SeedDay(d1, planHours: 5, nettoHours: 2.5, flex: -2.5, sumFlexEnd: -2.5);
        await SeedDay(d1.AddDays(1), planHours: 5, nettoHours: 1.0,
            overrideActive: true, nettoHoursOverride: 2.5, flex: -2.5, sumFlexEnd: -5);

        var model = await RunPeriod(d1, d1.AddDays(1));

        Assert.That(model.PercentageCompleted, Is.EqualTo(50),
            "PercentageCompleted must be ((2.5 + override 2.5) / 10) * 100 = 50");
    }
}
