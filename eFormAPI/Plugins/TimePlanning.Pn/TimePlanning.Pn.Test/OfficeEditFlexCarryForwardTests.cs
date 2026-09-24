using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// R4: an office edit of day D carries the balance to the worker's LAST row,
/// including rows pre-created for future dates, and (R2) never recomputes a
/// later row's hours — it only re-chains Flex / SumFlexStart / SumFlexEnd.
///
/// The later one-minute row's device stamps (07:00-17:00, 10 h) deliberately
/// disagree with its stored hours (8 h): the old cascades re-derived them from
/// the stamps, which is how historic hours changed in the 2026-09 incident.
///
/// Ids: id n is (n - 1) * 5 minutes after midnight — 97 = 08:00, 193 = 16:00,
/// 211 = 17:30, 217 = 18:00.
/// </summary>
[TestFixture]
public class OfficeEditFlexCarryForwardTests : TestBaseSetup
{
    private TimePlanningPlanningService _planningService = null!;
    private TimePlanningWorkingHoursService _workingHoursService = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        coreService.GetCore().Returns(await GetCore());

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        var dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);

        _planningService = new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            options,
            TimePlanningPnDbContext!,
            dbContextHelper,
            userService,
            localizationService,
            null!,
            coreService);

        _workingHoursService = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
            options,
            coreService);
    }

    /// <summary>A five-minute site on the weekday-plan branch.</summary>
    private async Task SeedSite(int siteUid) =>
        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            UseGoogleSheetAsDefault = false,
            Resigned = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

    private async Task SeedFiveMinuteRow(int siteUid, DateTime date, int start1Id, int stop1Id,
        double planHours, double nettoHours, double sumFlexStart, double sumFlexEnd) =>
        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date,
            Start1Id = start1Id,
            Stop1Id = stop1Id,
            PlanHours = planHours,
            NettoHours = nettoHours,
            Flex = nettoHours - planHours,
            SumFlexStart = sumFlexStart,
            SumFlexEnd = sumFlexEnd,
            RegisteredUnderOneMinuteIntervals = false,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

    /// <summary>
    /// A one-minute row: ids and stored hours say 08:00-16:00 (8 h), the device
    /// stamps say 07:00-17:00 (10 h). Balance in step at <paramref name="sumFlexHours"/>.
    /// </summary>
    private async Task SeedOneMinuteRowWithDisagreeingStamps(int siteUid, DateTime date, double sumFlexHours) =>
        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date,
            Start1Id = 97,
            Stop1Id = 193,
            Start1StartedAt = date.AddHours(7),
            Stop1StoppedAt = date.AddHours(17),
            PlanHours = 8,
            PlanHoursInSeconds = 28800,
            NettoHours = 8,
            NettoHoursInSeconds = 28800,
            Flex = 0,
            FlexInSeconds = 0,
            SumFlexStart = sumFlexHours,
            SumFlexStartInSeconds = (int)Math.Round(sumFlexHours * 3600),
            SumFlexEnd = sumFlexHours,
            SumFlexEndInSeconds = (int)Math.Round(sumFlexHours * 3600),
            RegisteredUnderOneMinuteIntervals = true,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

    private async Task<PlanRegistrationEntity> Stored(int siteUid, DateTime date) =>
        await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == siteUid && x.Date == date);

    /// <summary>
    /// Seeds d0 (+1.5 h), d1 (0 h, balance 1.5), d2 (one-minute, stamps
    /// disagree, balance 1.5) and a row pre-created 30 days in the future
    /// (balance 1.5). The edit of d1 to 08:00-18:00 (10 h, plan 8 h) must end
    /// every later row at 3.5 h.
    /// </summary>
    private async Task SeedHistory(int siteUid, DateTime d0, DateTime d1, DateTime d2, DateTime future)
    {
        await SeedSite(siteUid);
        await SeedFiveMinuteRow(siteUid, d0, 97, 211, planHours: 8, nettoHours: 9.5, sumFlexStart: 0, sumFlexEnd: 1.5);
        await SeedFiveMinuteRow(siteUid, d1, 97, 193, planHours: 8, nettoHours: 8, sumFlexStart: 1.5, sumFlexEnd: 1.5);
        await SeedOneMinuteRowWithDisagreeingStamps(siteUid, d2, sumFlexHours: 1.5);
        await SeedFiveMinuteRow(siteUid, future, 0, 0, planHours: 0, nettoHours: 0, sumFlexStart: 1.5, sumFlexEnd: 1.5);
    }

    private async Task AssertCarriedThroughTheFutureRow(int siteUid, DateTime d1, DateTime d2, DateTime future)
    {
        var edited = await Stored(siteUid, d1);
        var later = await Stored(siteUid, d2);
        var last = await Stored(siteUid, future);
        Assert.Multiple(() =>
        {
            Assert.That(edited.NettoHours, Is.EqualTo(10.0).Within(1e-9), "the edited day's own hours");
            Assert.That(edited.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
            Assert.That(later.SumFlexStart, Is.EqualTo(edited.SumFlexEnd).Within(1e-9),
                "SumFlexStart(n+1) == SumFlexEnd(n)");
            Assert.That(later.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
            Assert.That(later.SumFlexEndInSeconds, Is.EqualTo(3.5 * 3600),
                "the one-minute row's seconds chain moves with it");
            Assert.That(later.NettoHoursInSeconds, Is.EqualTo(28800),
                "a later row's stored hours are never re-derived from its stamps (R2)");
            Assert.That(later.NettoHours, Is.EqualTo(8.0).Within(1e-9));
            Assert.That(last.SumFlexStart, Is.EqualTo(later.SumFlexEnd).Within(1e-9),
                "the row pre-created 30 days ahead is reached (R4)");
            Assert.That(last.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
        });
    }

    [Test]
    public async Task Update_OfficeEditOfAPastDay_CarriesBalanceThroughAFuturePreCreatedRow_WithoutRecomputingLaterHours()
    {
        const int siteUid = 7701;
        var d0 = DateTime.Now.Date.AddDays(-10);
        var d1 = DateTime.Now.Date.AddDays(-9);
        var d2 = DateTime.Now.Date.AddDays(-8);
        var future = DateTime.Now.Date.AddDays(30);
        await SeedHistory(siteUid, d0, d1, d2, future);
        var editedRow = await Stored(siteUid, d1);

        var result = await _planningService.Update(editedRow.Id, new TimePlanningPlanningPrDayModel
        {
            Id = editedRow.Id,
            Date = d1,
            Start1Id = 97,
            Stop1Id = 217,   // 08:00-18:00 = 10 h
            PlanHours = 8,
            CommentOffice = ""
        });

        Assert.That(result.Success, Is.True, result.Message);
        await AssertCarriedThroughTheFutureRow(siteUid, d1, d2, future);
    }

    [Test]
    public async Task GridSave_OfAPastDay_CarriesBalanceThroughAFuturePreCreatedRow_WithoutRecomputingLaterHours()
    {
        const int siteUid = 7702;
        // Older than one month, so UpdatePlanRegistration never rewrites PlanHours.
        var d0 = DateTime.Now.Date.AddDays(-62);
        var d1 = DateTime.Now.Date.AddDays(-61);
        var d2 = DateTime.Now.Date.AddDays(-60);
        var future = DateTime.Now.Date.AddDays(30);
        await SeedHistory(siteUid, d0, d1, d2, future);

        var result = await _workingHoursService.CreateUpdate(new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = siteUid,
            Plannings = new List<TimePlanningWorkingHoursModel>
            {
                new()
                {
                    Date = d1,
                    Shift1Start = 97,
                    Shift1Stop = 217,
                    Shift1Pause = 0,
                    PlanHours = 8,
                    NettoHours = 10,  // a five-minute grid row saves the hours the page posts
                    FlexHours = 2,
                    PaidOutFlex = "0",
                    Message = 0,
                    PlanText = "",
                    CommentOffice = "",
                    CommentOfficeAll = "",
                    CommentWorker = ""
                }
            }
        });

        Assert.That(result.Success, Is.True, result.Message);
        await AssertCarriedThroughTheFutureRow(siteUid, d1, d2, future);
    }
}
