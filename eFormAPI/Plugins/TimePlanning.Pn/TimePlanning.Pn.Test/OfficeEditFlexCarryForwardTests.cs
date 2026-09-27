using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
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
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

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
/// The same carry-forward is pinned for the writers that SEED a row from its
/// predecessor: a day the grid save creates, the PlanTimer sheet pull
/// (created, updated and admin-skipped days) and the Excel import.
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
        var core = await GetCore();
        coreService.GetCore().Returns(core);

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

    /// <summary>
    /// Seeds d-2 (+1.5 h), d-1 (0 h, balance 1.5), NO row on d, then d+1 (0 h),
    /// d+2 (+1 h) and d+3 (0 h) chained from d-1's 1.5 h. Returns d+1..d+3.
    /// </summary>
    private async Task<DateTime[]> SeedHistoryWithTail(int siteUid, DateTime d, bool withRowOnD,
        bool useGoogleSheet = false)
    {
        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            UseGoogleSheetAsDefault = useGoogleSheet,
            Resigned = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);
        await SeedFiveMinuteRow(siteUid, d.AddDays(-2), 97, 211, planHours: 8, nettoHours: 9.5, sumFlexStart: 0, sumFlexEnd: 1.5);
        await SeedFiveMinuteRow(siteUid, d.AddDays(-1), 97, 193, planHours: 8, nettoHours: 8, sumFlexStart: 1.5, sumFlexEnd: 1.5);
        if (withRowOnD)
        {
            await SeedFiveMinuteRow(siteUid, d, 97, 193, planHours: 8, nettoHours: 8, sumFlexStart: 1.5, sumFlexEnd: 1.5);
        }
        await SeedFiveMinuteRow(siteUid, d.AddDays(1), 97, 193, planHours: 8, nettoHours: 8, sumFlexStart: 1.5, sumFlexEnd: 1.5);
        await SeedFiveMinuteRow(siteUid, d.AddDays(2), 97, 205, planHours: 8, nettoHours: 9, sumFlexStart: 1.5, sumFlexEnd: 2.5);
        await SeedFiveMinuteRow(siteUid, d.AddDays(3), 97, 193, planHours: 8, nettoHours: 8, sumFlexStart: 2.5, sumFlexEnd: 2.5);
        return new[] { d.AddDays(1), d.AddDays(2), d.AddDays(3) };
    }

    /// <summary>
    /// Day d ends at <paramref name="dSumFlexEnd"/>; every later row starts
    /// where its predecessor ended and ends at the expected balance.
    /// </summary>
    private async Task AssertTailCarriedFrom(int siteUid, DateTime d, double dSumFlexEnd, DateTime[] tail,
        double[] expectedTailSumFlexEnd)
    {
        var dayD = await Stored(siteUid, d);
        var tailRows = new List<PlanRegistrationEntity>();
        foreach (var date in tail)
        {
            tailRows.Add(await Stored(siteUid, date));
        }

        Assert.Multiple(() =>
        {
            Assert.That(dayD.SumFlexStart, Is.EqualTo(1.5).Within(1e-9), "d seeds from d-1");
            Assert.That(dayD.SumFlexEnd, Is.EqualTo(dSumFlexEnd).Within(1e-9), "d's own balance");
            var predecessorEnd = dayD.SumFlexEnd;
            for (var n = 0; n < tailRows.Count; n++)
            {
                Assert.That(tailRows[n].SumFlexStart, Is.EqualTo(predecessorEnd).Within(1e-9),
                    $"SumFlexStart(d+{n + 1}) == SumFlexEnd(d+{n})");
                Assert.That(tailRows[n].SumFlexEnd, Is.EqualTo(expectedTailSumFlexEnd[n]).Within(1e-9),
                    $"SumFlexEnd(d+{n + 1})");
                predecessorEnd = tailRows[n].SumFlexEnd;
            }
        });
    }

    private static TimePlanningWorkingHoursModel GridRow(DateTime date, int stop1Id, double nettoHours) => new()
    {
        Date = date,
        Shift1Start = 97,
        Shift1Stop = stop1Id,
        Shift1Pause = 0,
        PlanHours = 8,
        NettoHours = nettoHours,
        FlexHours = nettoHours - 8,
        PaidOutFlex = "0",
        Message = 0,
        PlanText = "",
        CommentOffice = "",
        CommentOfficeAll = "",
        CommentWorker = ""
    };

    /// <summary>
    /// CreatePlanning seeds a missing day from its predecessor; the save must
    /// then carry the new balance through every later row.
    /// </summary>
    [Test]
    public async Task GridSave_CreatingAMissingPastDay_CarriesBalanceToEveryLaterRow()
    {
        const int siteUid = 7703;
        // Older than one month, so UpdatePlanRegistration never rewrites PlanHours.
        var d = DateTime.Now.Date.AddDays(-61);
        var tail = await SeedHistoryWithTail(siteUid, d, withRowOnD: false);

        // The grid's first posted row is the carried-over predecessor (only
        // updated, never created), so d must come second to be created.
        var result = await _workingHoursService.CreateUpdate(new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = siteUid,
            Plannings = new List<TimePlanningWorkingHoursModel>
            {
                GridRow(d.AddDays(-1), 193, 8),
                GridRow(d, 217, 10)   // 08:00-18:00 = 10 h against 8 planned: +2 h
            }
        });

        Assert.That(result.Success, Is.True, result.Message);
        await AssertTailCarriedFrom(siteUid, d, 3.5, tail, new[] { 3.5, 4.5, 4.5 });
    }

    private const string SheetWorkerName = "Jane Doe";

    /// <summary>
    /// Seeds the SDK site the sheet's header names and returns a fresh SDK
    /// context (the shared one may still track sites from an earlier test).
    /// </summary>
    private async Task<MicrotingDbContext> SeedSheetWorkerSite(int siteUid)
    {
        var sdkDb = (await GetCore()).DbContextHelper.GetDbContext();
        await new SdkSite { Name = SheetWorkerName, MicrotingUid = siteUid }.Create(sdkDb);
        return sdkDb;
    }

    /// <summary>A PlanTimer sheet: header row, then one "hours, empty text" row per day.</summary>
    private static IList<IList<object>> PlanTimerSheet(params (DateTime Date, string Hours)[] days)
    {
        var sheet = new List<IList<object>>
        {
            new List<object> { "Dato", "Uge", "Ugedag", $"{SheetWorkerName} - timer", $"{SheetWorkerName} - tekst" }
        };
        sheet.AddRange(days.Select(day =>
            (IList<object>)new List<object> { day.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "", "", day.Hours, "" }));
        return sheet;
    }

    private Task ApplySheet(MicrotingDbContext sdkDb, IList<IList<object>> sheet) =>
        GoogleSheetHelper.ApplyPlanTimerSheet(sdkDb, TimePlanningPnDbContext!, sheet, Substitute.For<ILogger>());

    /// <summary>
    /// The sheet pull's create leg seeds a missing past day from its
    /// predecessor (8 planned, 0 worked: -8 h); later rows must follow.
    /// </summary>
    [Test]
    public async Task SheetPull_CreatingAMissingPastDay_CarriesBalanceToEveryLaterRow()
    {
        const int siteUid = 7704;
        var d = DateTime.Now.Date.AddDays(-20);
        await using var sdkDb = await SeedSheetWorkerSite(siteUid);
        var tail = await SeedHistoryWithTail(siteUid, d, withRowOnD: false, useGoogleSheet: true);

        await ApplySheet(sdkDb, PlanTimerSheet((d, "8")));

        await AssertTailCarriedFrom(siteUid, d, -6.5, tail, new[] { -6.5, -5.5, -5.5 });
    }

    /// <summary>
    /// The sheet pull's update leg lowers a past day's plan from 8 h to 6 h
    /// (8 h worked: +2 h); later rows must follow, and the written row's own
    /// balance uses the canonical sign (worked minus planned).
    /// </summary>
    [Test]
    public async Task SheetPull_UpdatingAPastDay_CarriesBalanceToEveryLaterRow()
    {
        const int siteUid = 7705;
        var d = DateTime.Now.Date.AddDays(-20);
        await using var sdkDb = await SeedSheetWorkerSite(siteUid);
        var tail = await SeedHistoryWithTail(siteUid, d, withRowOnD: true, useGoogleSheet: true);

        await ApplySheet(sdkDb, PlanTimerSheet((d, "6")));

        await AssertTailCarriedFrom(siteUid, d, 3.5, tail, new[] { 3.5, 4.5, 4.5 });
    }

    /// <summary>
    /// A day an admin changed is skipped by the sheet pull, so it is not a
    /// written day and nothing is walked from it: the (deliberately stale)
    /// later rows stay exactly as they were.
    /// </summary>
    [Test]
    public async Task SheetPull_AdminChangedDay_IsNotWrittenAndNotWalkedFrom()
    {
        const int siteUid = 7706;
        var d = DateTime.Now.Date.AddDays(-20);
        await using var sdkDb = await SeedSheetWorkerSite(siteUid);
        var tail = await SeedHistoryWithTail(siteUid, d, withRowOnD: true, useGoogleSheet: true);
        var dayD = await TimePlanningPnDbContext!.PlanRegistrations
            .SingleAsync(x => x.SdkSitId == siteUid && x.Date == d);
        dayD.PlanChangedByAdmin = true;
        dayD.SumFlexEnd = 5;   // breaks the chain after d on purpose
        await dayD.Update(TimePlanningPnDbContext);
        TimePlanningPnDbContext.ChangeTracker.Clear();

        await ApplySheet(sdkDb, PlanTimerSheet((d, "6")));

        var storedD = await Stored(siteUid, d);
        var tailSumFlexEnd = new List<double>();
        foreach (var date in tail)
        {
            tailSumFlexEnd.Add((await Stored(siteUid, date)).SumFlexEnd);
        }

        Assert.Multiple(() =>
        {
            Assert.That(storedD.PlanHours, Is.EqualTo(8.0).Within(1e-9), "the admin's day keeps its plan");
            Assert.That(storedD.SumFlexEnd, Is.EqualTo(5.0).Within(1e-9), "the skipped day is not re-chained");
            Assert.That(tailSumFlexEnd, Is.EqualTo(new[] { 1.5, 2.5, 2.5 }).Within(1e-9),
                "later rows untouched (as seeded): nothing was written, so nothing was walked");
        });
    }

    /// <summary>
    /// The Excel import only accepts dates from yesterday on, so d is in the
    /// future and d+1..d+3 stand for rows pre-created ahead of it. Any
    /// <paramref name="laterRows"/> follow d's row in the workbook.
    /// </summary>
    private async Task<(DateTime D, DateTime[] Tail, OperationResult Result)> ImportDayD(int siteUid,
        bool withRowOnD, string hours, params (string Date, string Hours, string Text)[] laterRows)
    {
        var d = DateTime.Now.Date.AddDays(5);
        var sheetName = $"Import worker {siteUid}";
        var sdkDb = (await GetCore()).DbContextHelper.GetDbContext();
        await using (sdkDb)
        {
            await new SdkSite { Name = sheetName, MicrotingUid = siteUid }.Create(sdkDb);
        }
        var tail = await SeedHistoryWithTail(siteUid, d, withRowOnD);

        var rows = new[] { (d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), hours, "") }
            .Concat(laterRows).ToArray();
        var xlsx = WorkingHoursImportRemovedRowTests.BuildWorkbook(sheetName, rows);
        var result = await _workingHoursService.Import(WorkingHoursImportRemovedRowTests.FormFile(xlsx));
        return (d, tail, result);
    }

    /// <summary>
    /// The import's create leg seeds a missing day from its predecessor
    /// (8 planned, 0 worked: -8 h); every later row must follow.
    /// </summary>
    [Test]
    public async Task Import_CreatingAMissingDay_CarriesBalanceToEveryLaterRow()
    {
        const int siteUid = 7707;
        var (d, tail, result) = await ImportDayD(siteUid, withRowOnD: false, hours: "8");

        Assert.That(result.Success, Is.True, result.Message);
        await AssertTailCarriedFrom(siteUid, d, -6.5, tail, new[] { -6.5, -5.5, -5.5 });
    }

    /// <summary>
    /// The import's update leg lowers a day's plan from 8 h to 6 h (8 h
    /// worked: +2 h); every later row must follow, and the written row's own
    /// balance uses the canonical sign (worked minus planned).
    /// </summary>
    [Test]
    public async Task Import_UpdatingADay_CarriesBalanceToEveryLaterRow()
    {
        const int siteUid = 7708;
        var (d, tail, result) = await ImportDayD(siteUid, withRowOnD: true, hours: "6");

        Assert.That(result.Success, Is.True, result.Message);
        await AssertTailCarriedFrom(siteUid, d, 3.5, tail, new[] { 3.5, 4.5, 4.5 });
    }

    /// <summary>
    /// The second data row's hours cell is not a number, so Import throws
    /// after d's row was already saved. The import still fails exactly as
    /// before, but the saved row must not be left as a chain break: the rows
    /// after it are carried.
    /// </summary>
    [Test]
    public async Task Import_ThrowingAfterARowWasSaved_StillCarriesThatRowsBalanceForward()
    {
        const int siteUid = 7709;
        const string badHours = "not-a-number";
        var expectedMessage = Assert.Throws<FormatException>(() => double.Parse(badHours,
            NumberStyles.AllowDecimalPoint, NumberFormatInfo.InvariantInfo))!.Message;

        var (d, tail, result) = await ImportDayD(siteUid, withRowOnD: false, hours: "8",
            (DateTime.Now.Date.AddDays(9).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), badHours, ""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False, "the bad cell still fails the import");
            Assert.That(result.Message, Is.EqualTo(expectedMessage), "the error is reported as before");
        });
        await AssertTailCarriedFrom(siteUid, d, -6.5, tail, new[] { -6.5, -5.5, -5.5 });
    }
}
