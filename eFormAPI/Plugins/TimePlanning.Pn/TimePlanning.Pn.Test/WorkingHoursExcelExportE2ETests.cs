using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using TimePlanning.Pn.Test.Helpers;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PayDayRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayDayRule;
using PayDayTypeRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayDayTypeRule;
using PayRuleSetEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayRuleSet;
using PayTierRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayTierRule;
using PayTimeBandRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayTimeBandRule;
using DayTypeEnum = Microting.TimePlanningBase.Infrastructure.Data.Entities.DayType;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// End-to-end regression lock for the Excel export. Seeds an SDK Site/Worker
/// pair + an <c>AssignedSite</c> with <c>UseOneMinuteIntervals=true</c> + a
/// <c>PlanRegistration</c> with non-5-min stamps (08:04→10:10), invokes
/// <c>GenerateExcelDashboard</c> single-worker route, opens the produced xlsx
/// with OpenXml, and asserts the Shift1 Start/Stop cells contain the exact
/// <c>HH:mm</c> text — no <c>:ss</c> suffix. This locks the 2026-05-15
/// HH:mm-vs-HH:mm:ss fix at the cell level. A 5-min-aligned counterpart
/// guards the legacy slot path.
/// </summary>
[TestFixture]
public class WorkingHoursExcelExportE2ETests : TestBaseSetup
{
    private TimePlanningWorkingHoursService _service = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(core);

        // Ensure a Language exists in the SDK DB and bind it to the user-language stub.
        var sdkDb = core.DbContextHelper.GetDbContext();
        var language = await sdkDb.Languages.FirstOrDefaultAsync(l => l.LanguageCode == "da");
        if (language == null)
        {
            language = new SdkLanguage { LanguageCode = "da", Name = "Danish" };
            await language.Create(sdkDb);
        }
        userService.GetCurrentUserLanguage().Returns(language);

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // The export scopes the requested site to the signed-in caller, so this
        // fixture needs a real one. Admin: scoping is a no-op.
        await SeedAdminCallerAsync(userService);

        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: SeededBaseDbContext!,
            options,
            coreService);
    }

    [Test]
    public async Task GenerateExcelDashboard_FlagOn_NonRoundMinutes_CellsShowHHmm()
    {
        await SeedSiteAndPlanRegistration(
            siteUid: 9701,
            date: new DateTime(2026, 5, 15),
            useOneMinuteIntervals: true,
            start1: new DateTime(2026, 5, 15, 8, 4, 0),
            stop1: new DateTime(2026, 5, 15, 10, 10, 0),
            start1Id: 97, stop1Id: 122);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9701,
            DateFrom = new DateTime(2026, 5, 15),
            DateTo = new DateTime(2026, 5, 15),
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        var (shift1Start, shift1Stop, _) = ReadShift1Cells(result.Model!);
        Assert.That(shift1Start, Is.EqualTo("08:04"),
            "Non-5-min Start1 stamp must render as HH:mm with no :ss suffix (regression lock for 2026-05-15 fix)");
        Assert.That(shift1Stop, Is.EqualTo("10:10"),
            "Non-5-min Stop1 stamp must render as HH:mm with no :ss suffix");
    }

    [Test]
    public async Task GenerateExcelDashboard_FlagOff_RoundMinutes_CellsShowHHmm()
    {
        // 5-min-aligned counterpart: legacy slot path must also render HH:mm.
        // With flag off, the cell text comes from PlanRegistration.Options[shift-1].
        await SeedSiteAndPlanRegistration(
            siteUid: 9702,
            date: new DateTime(2026, 5, 16),
            useOneMinuteIntervals: false,
            start1: null,
            stop1: null,
            start1Id: 97, stop1Id: 121);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9702,
            DateFrom = new DateTime(2026, 5, 16),
            DateTo = new DateTime(2026, 5, 16),
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        var (shift1Start, shift1Stop, _) = ReadShift1Cells(result.Model!);
        Assert.That(shift1Start, Is.EqualTo("08:00"), "Legacy slot 97 → Options[96] = \"08:00\"");
        Assert.That(shift1Stop, Is.EqualTo("10:00"), "Legacy slot 121 → Options[120] = \"10:00\"");
    }

    /// <summary>
    /// A pause override (<c>Pause1OverrideMinutes</c>, set when the office edits the
    /// total pause in the web dialog) is the authoritative shift pause: the planning
    /// grid and the netto hours honour it. The Excel "Shift 1 pause" cell must show
    /// the override too, not the sum of the worker's recorded pause stamps — here
    /// the worker forgot to end a pause, so the stamps sum to 3:27 while the
    /// override says 1:00.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_PauseOverrideSet_PauseCellShowsOverrideNotStampSum()
    {
        const int siteUid = 9708;
        var day = new DateTime(2026, 10, 22);
        await SeedSiteAndPlanRegistration(
            siteUid: siteUid,
            date: day,
            useOneMinuteIntervals: true,
            start1: day.AddHours(5).AddMinutes(55),
            stop1: day.AddHours(15).AddMinutes(1),
            start1Id: 72, stop1Id: 181);

        var row = await TimePlanningPnDbContext!.PlanRegistrations
            .SingleAsync(x => x.SdkSitId == siteUid && x.Date == day);
        row.RegisteredUnderOneMinuteIntervals = true;
        // What the override dialog writes alongside a 60-min override. Only keeps the
        // pause cell from rendering blank: with complete stamps present the legacy id
        // never contributes minutes, so the expected 01:00 can only come from the override.
        row.Pause1Id = 13;
        row.Pause1StartedAt = day.AddHours(9).AddMinutes(22);   // 09:22 → 12:15 = 173 min
        row.Pause1StoppedAt = day.AddHours(12).AddMinutes(15);
        row.Pause10StartedAt = day.AddHours(12).AddMinutes(15); // 12:15 → 12:49 = 34 min
        row.Pause10StoppedAt = day.AddHours(12).AddMinutes(49);
        row.Pause1OverrideMinutes = 60;
        await row.Update(TimePlanningPnDbContext!);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = siteUid,
            DateFrom = day,
            DateTo = day,
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        var (_, _, shift1Pause) = ReadShift1Cells(result.Model!);
        Assert.That(shift1Pause, Is.EqualTo("01:00"),
            "Pause1OverrideMinutes=60 must win over the recorded stamps (3:27) in the Excel pause cell");
    }

    /// <summary>
    /// Regression coverage for the read-time flex chain (<c>ApplyRunningFlexChain</c>
    /// in <c>TimePlanningWorkingHoursService</c>) failing to deduct a paid-out flex
    /// on <c>UseOneMinuteIntervals=true</c> sites. Production writers (e.g. the
    /// "set flex" flow, <c>TimePlanningFlexService.UpdatePlanning</c>) only ever
    /// populate the legacy double <c>PaiedOutFlex</c>; <c>PaiedOutFlexInSeconds</c>
    /// stays at its unpopulated default of 0 unless the caller sets it explicitly.
    /// Pre-fix, the chain subtracted <c>PaiedOutFlexInSeconds</c> with no fallback,
    /// so 2h flex with 30min paid out rendered as 2h (undeducted) instead of 1.5h.
    /// </summary>
    [Test]
    public async Task Index_FlagOn_PaiedOutFlexInSecondsZero_FallsBackToPaidOutFlexDouble()
    {
        await SeedFlexScenario(
            siteUid: 9703,
            date: new DateTime(2026, 5, 17),
            flexInSecondsOnTargetDay: 7200, // 2h flex
            paiedOutFlexOnTargetDay: 0.5, // 30 min paid out — legacy double only
            paiedOutFlexInSecondsOnTargetDay: 0);

        var result = await _service.Index(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9703,
            DateFrom = new DateTime(2026, 5, 16),
            DateTo = new DateTime(2026, 5, 17),
        });

        Assert.That(result.Success, Is.True, result.Message);
        var targetRow = result.Model!.Single(r => r.Date == new DateTime(2026, 5, 17));

        // 2h flex - 30min paid out = 1.5h (5400s). Pre-fix this read 2h (7200s)
        // because PaiedOutFlexInSeconds (always 0 from production writers) was
        // subtracted directly with no fallback to the populated double.
        Assert.That(targetRow.SumFlexEndInSeconds, Is.EqualTo(5400));
        Assert.That(targetRow.SumFlexEnd, Is.EqualTo(1.5).Within(0.001));
    }

    /// <summary>
    /// When <c>PaiedOutFlexInSeconds</c> IS populated (e.g. a future writer that
    /// keeps it in sync), that value must win over the double-derived fallback —
    /// mirrors <c>PlanRegistrationHelperTests.SumFlex_FlagOn_PaiedOutFlexInSecondsZero_FallsBackToPaiedOutFlex</c>'s
    /// counterpart case for the write-time chain.
    /// </summary>
    [Test]
    public async Task Index_FlagOn_PaiedOutFlexInSecondsSet_UsesStoredSecondsOverDouble()
    {
        await SeedFlexScenario(
            siteUid: 9704,
            date: new DateTime(2026, 5, 18),
            flexInSecondsOnTargetDay: 7200, // 2h flex
            paiedOutFlexOnTargetDay: 0.5, // legacy double says 30 min...
            paiedOutFlexInSecondsOnTargetDay: 900); // ...but the seconds column says 15 min; that must win.

        var result = await _service.Index(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9704,
            DateFrom = new DateTime(2026, 5, 17),
            DateTo = new DateTime(2026, 5, 18),
        });

        Assert.That(result.Success, Is.True, result.Message);
        var targetRow = result.Model!.Single(r => r.Date == new DateTime(2026, 5, 18));

        // 2h flex - 15min (the populated *InSeconds* column) = 1h45m (6300s).
        Assert.That(targetRow.SumFlexEndInSeconds, Is.EqualTo(6300));
        Assert.That(targetRow.SumFlexEnd, Is.EqualTo(1.75).Within(0.001));
    }

    [Test]
    public async Task GenerateExcelDashboard_FlagOn_PaiedOutFlexSet_SumFlexEndCellDeductsIt()
    {
        await SeedFlexScenario(
            siteUid: 9705,
            date: new DateTime(2026, 5, 19),
            flexInSecondsOnTargetDay: 7200, // 2h flex
            paiedOutFlexOnTargetDay: 0.5, // 30 min paid out — legacy double only
            paiedOutFlexInSecondsOnTargetDay: 0);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9705,
            DateFrom = new DateTime(2026, 5, 19),
            DateTo = new DateTime(2026, 5, 19),
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        var (sumFlexEnd, paidOutFlex) = ReadFlexCells(result.Model!);
        Assert.That(double.Parse(sumFlexEnd, CultureInfo.InvariantCulture), Is.EqualTo(1.5).Within(0.001),
            "SumFlexEnd column must reflect Flex minus PaiedOutFlex, not the raw Flex total " +
            "(regression lock for the read-time chain fallback fix)");
        Assert.That(double.Parse(paidOutFlex, CultureInfo.InvariantCulture), Is.EqualTo(0.5).Within(0.001));
    }

    // ------------------------------------------------------------------
    // #1739: hours and flex cells DISPLAY two decimals (0.00) but keep the
    // full-precision value (#1212). Counts, the week number and dates keep
    // their own formats.
    // ------------------------------------------------------------------

    private const uint TwoDecimalsStyleIndex = 4U;

    // Dashboard and per-site sheets (0-indexed): the only number cells that
    // are not hours are the date and the ISO week number.
    private const int DateColumn = 4;
    private const int WeekColumn = 5;

    private static List<Cell> HoursCells(List<Cell> row) => row
        .Where((c, i) => i != DateColumn && i != WeekColumn && c.DataType?.Value == CellValues.Number)
        .ToList();

    [Test]
    public async Task GenerateExcelDashboard_HoursCells_ShowTwoDecimals_AndKeepFullPrecision()
    {
        var (day1, day2) = await SeedTwoDecimalScenario(siteUid: 9706);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9706,
            DateFrom = day1,
            DateTo = day2,
        });

        Assert.That(result.Success, Is.True, result.Message);
        await using var xlsx = result.Model!;
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var workbookPart = doc.WorkbookPart!;
        AssertStyleIsTwoDecimals(workbookPart);
        var rows = SheetRows(workbookPart, "Dashboard").Skip(1).ToList(); // skip header

        // Column layout without extra shifts (0-indexed): DateColumn, WeekColumn,
        // 7=PlanHours, 14=NettoHours, 15=Flex, 16=SumFlexEnd,
        // 17=PaidOutFlex, 21=the pay code. The last row is the totals row.
        Assert.That(rows, Has.Count.EqualTo(3), "two days and the totals row");
        foreach (var cells in rows)
        {
            var hoursCells = HoursCells(cells);
            Assert.That(hoursCells, Has.Count.GreaterThanOrEqualTo(5));
            Assert.That(hoursCells.Select(c => c.StyleIndex?.Value),
                Has.All.EqualTo(TwoDecimalsStyleIndex),
                "every hours/flex/pay-line cell displays 0.00");
        }
        Assert.That(rows[0][DateColumn].StyleIndex?.Value, Is.EqualTo(2U), "the date keeps its date format");
        Assert.That(rows[0][WeekColumn].StyleIndex, Is.Null, "the week number stays an integer");

        // Full precision is stored (#1212): a 61-minute one-minute balance,
        // a 1 h 20 min netto override and the pay line it produces.
        Assert.That(CellNumber(rows[0][16]), Is.EqualTo(61 / 60.0).Within(1e-9));
        Assert.That(CellNumber(rows[1][14]), Is.EqualTo(80 / 60.0).Within(1e-9));
        var payLine = CellNumber(rows[1][21]);
        Assert.That(payLine, Is.EqualTo(80 / 60.0).Within(1e-3));
        Assert.That(payLine, Is.Not.EqualTo(Math.Round(payLine, 2)),
            "the stored pay-line value is not rounded to two decimals");
    }

    [Test]
    public async Task GenerateExcelDashboard_AllWorkers_TotalSheet_HoursShowTwoDecimals_CountsStayIntegers()
    {
        var (day1, day2) = await SeedTwoDecimalScenario(siteUid: 9707);

        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel { DateFrom = day1, DateTo = day2 });

        Assert.That(result.Success, Is.True, result.Message);
        await using var xlsx = result.Model!;
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var workbookPart = doc.WorkbookPart!;
        AssertStyleIsTwoDecimals(workbookPart);

        // Total sheet (0-indexed): 0/1=DateFrom/DateTo, 5=PlanHours,
        // 6=NettoHours, 7=SumFlexEnd, 8-10=hour splits, 11=comment count,
        // 12=message count, 13=Saturday hours, then pay codes and the
        // per-message hour sums.
        var cells = SheetRows(workbookPart, "Total")[1];
        Assert.That(cells[11].StyleIndex, Is.Null, "the comment count stays an integer");
        Assert.That(cells[12].StyleIndex, Is.Null, "the message count stays an integer");
        var hoursCells = cells
            .Where((c, i) => i > 1 && i != 11 && i != 12 && c.DataType?.Value == CellValues.Number)
            .ToList();
        Assert.That(hoursCells, Has.Count.GreaterThanOrEqualTo(7));
        Assert.That(hoursCells.Select(c => c.StyleIndex?.Value), Has.All.EqualTo(TwoDecimalsStyleIndex));
        Assert.That(CellNumber(cells[7]), Is.EqualTo(61 / 60.0).Within(1e-9),
            "the closing flex balance keeps full precision");

        // The per-site sheet's data rows use the same cells.
        var siteSheetName = workbookPart.Workbook.Descendants<Sheet>()
            .Select(x => x.Name!.Value!)
            .First(n => n != "Total" && n != "Dagsoversigt");
        var siteRows = SheetRows(workbookPart, siteSheetName).Skip(1).ToList();
        Assert.That(siteRows, Is.Not.Empty);
        foreach (var row in siteRows)
        {
            Assert.That(HoursCells(row).Select(c => c.StyleIndex?.Value),
                Has.All.EqualTo(TwoDecimalsStyleIndex));
        }
    }

    // ------------------------------------------------------------------
    // Pause-blind pay lines, holiday afternoon hours and pause cells.
    // The canonical pause rule (FlexChain.ComputeShiftPauseSeconds): a
    // Pause{N}OverrideMinutes wins when set, else the sum of the pause stamps,
    // else (Pause{N}Id - 1) * 5 min. An active NettoHoursOverride is the
    // effective netto. The Excel export must agree with both.
    // ------------------------------------------------------------------

    private static readonly DateTime PauseSaturday = new(2026, 5, 16); // not a holiday
    private static readonly DateTime PauseGrundlovsdag = new(2026, 6, 5); // Friday
    private static readonly DateTime PauseTuesday = new(2026, 5, 12);

    /// <summary>
    /// A Saturday 07:00 → 15:00 with a stamped 11:00 → 12:00 pause is 7 h of work.
    /// The day-type band path attributes shift segments by clock position; the
    /// pause hour is not worked and must not be paid, so the pay lines total the
    /// 7 h netto — not the 8 h gross span.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_SaturdayBandPayLines_PauseStamps_PayLinesTotalNetto()
    {
        const int siteUid = 9821;
        var day = PauseSaturday;
        await SeedPauseDay(siteUid, day, start: day.AddHours(7), stop: day.AddHours(15), nettoHours: 7.0,
            configure: row =>
            {
                row.Pause1Id = 13;
                row.Pause1StartedAt = day.AddHours(11);
                row.Pause1StoppedAt = day.AddHours(12);
            });
        await AssignPayRuleSet(siteUid, SaturdayAllDayBandRuleSet(siteUid));

        var cells = await ExportDashboardRowByHeader(siteUid, day);

        Assert.That(cells["SAT_WORK"], Is.EqualTo(7.0).Within(0.001),
            "Saturday 07:00-15:00 with a stamped 60-min pause must yield 7.00 h of pay lines (the netto); " +
            "8.00 means the pause hour was paid because the per-day model carries no pause stamps");
    }

    /// <summary>
    /// Same day, but the office set <c>Pause1OverrideMinutes = 60</c> while the stamps
    /// only cover 30 minutes. The override is the authoritative pause, so the pay
    /// lines must total 8 h − 60 min = 7 h (the netto), neither the gross 8 h nor the
    /// stamp-derived 7.5 h.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_SaturdayBandPayLines_PauseOverride_PayLinesSubtractOverride()
    {
        const int siteUid = 9822;
        var day = PauseSaturday;
        await SeedPauseDay(siteUid, day, start: day.AddHours(7), stop: day.AddHours(15), nettoHours: 7.0,
            configure: row =>
            {
                row.Pause1Id = 13;
                row.Pause1StartedAt = day.AddHours(11);
                row.Pause1StoppedAt = day.AddHours(11).AddMinutes(30);
                row.Pause1OverrideMinutes = 60;
            });
        await AssignPayRuleSet(siteUid, SaturdayAllDayBandRuleSet(siteUid));

        var cells = await ExportDashboardRowByHeader(siteUid, day);

        Assert.That(cells["SAT_WORK"], Is.EqualTo(7.0).Within(0.001),
            "Pause1OverrideMinutes=60 on a 07:00-15:00 Saturday must yield 7.00 h of pay lines; " +
            "8.00 = pause ignored, 7.50 = stamps used instead of the override");
    }

    /// <summary>
    /// The § 50 Staldarbejde preset (normal-time split path) on a Saturday 08:00 → 16:00
    /// with a stamped 11:30 → 12:30 pause straddling the 12:00 band boundary. Netto is
    /// 7 h, below the norm, so every worked second is attributed by clock position:
    /// 08:00 → 11:30 = 3.5 h SAT_NORMAL and 12:30 → 16:00 = 3.5 h SAT_ANIMAL_AFTERNOON
    /// (the same expectation PraktikantPayLineRoutingTests locks at unit level). Without
    /// the pause stamps the 7 h budget is consumed from 08:00 → 15:00 gross, giving 4 h /
    /// 3 h and charging the pause half-hour before noon as SAT_NORMAL.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_StaldarbejdeSaturday_PauseAcrossNoon_BandsUseWorkedTimeOnly()
    {
        const int siteUid = 9823;
        var day = PauseSaturday;
        await SeedPauseDay(siteUid, day, start: day.AddHours(8), stop: day.AddHours(16), nettoHours: 7.0,
            configure: row =>
            {
                row.Pause1Id = 13;
                row.Pause1StartedAt = day.AddHours(11).AddMinutes(30);
                row.Pause1StoppedAt = day.AddHours(12).AddMinutes(30);
            });
        var preset = PraktikantFixtures.Staldarbejde();
        preset.Id = 0;
        preset.WorkflowState = Constants.WorkflowStates.Created;
        await AssignPayRuleSet(siteUid, preset);

        var cells = await ExportDashboardRowByHeader(siteUid, day);

        Assert.That(cells["SAT_NORMAL"], Is.EqualTo(3.5).Within(0.001),
            "Worked time before 12:00 is 08:00-11:30 = 3.50 h SAT_NORMAL; 4.00 means the pause " +
            "(11:30-12:30) was not cut out of the shift segments");
        Assert.That(cells["SAT_ANIMAL_AFTERNOON"], Is.EqualTo(3.5).Within(0.001),
            "Worked time after 12:00 is 12:30-16:00 = 3.50 h SAT_ANIMAL_AFTERNOON; 3.00 means the " +
            "pause was charged to the normal-time budget and the afternoon was truncated at 15:00");
    }

    /// <summary>
    /// Grundlovsdag counts only the hours after 12:00 as Sunday/holiday hours (Total
    /// sheet column J). 08:00 → 16:00 with a stamped 13:00 → 14:00 pause has 4 h of
    /// span after noon but only 3 h worked, so column J must be 3 h and the normal
    /// hours column I must be 7 − 3 = 4 h.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_AllWorkers_Grundlovsdag_AfternoonHoursSubtractPause()
    {
        const int siteUid = 9824;
        var day = PauseGrundlovsdag;
        await SeedPauseDay(siteUid, day, start: day.AddHours(8), stop: day.AddHours(16), nettoHours: 7.0,
            configure: row =>
            {
                row.Pause1Id = 13;
                row.Pause1StartedAt = day.AddHours(13);
                row.Pause1StoppedAt = day.AddHours(14);
            });

        var totalRow = await ExportTotalRow(day);

        Assert.That(CellNumber(totalRow[9]), Is.EqualTo(3.0).Within(0.001),
            "Grundlovsdag afternoon hours: 12:00-16:00 minus the 13:00-14:00 pause = 3.00 h; " +
            "4.00 means CalculateHoursAfterNoon saw no pause intervals");
        Assert.That(CellNumber(totalRow[8]), Is.EqualTo(4.0).Within(0.001),
            "Normal hours = netto 7.00 - afternoon 3.00 = 4.00 h");
    }

    /// <summary>
    /// Total sheet column N (Saturday hours) must use the effective netto like its
    /// sibling totals (NettoHours, Sunday/holiday): a Saturday with NettoHours 6.0 and
    /// an active override of 8.0 is 8 h of Saturday work.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_AllWorkers_SaturdayTotal_HonoursNettoOverride()
    {
        const int siteUid = 9825;
        var day = PauseSaturday;
        await SeedPauseDay(siteUid, day, start: day.AddHours(7), stop: day.AddHours(13), nettoHours: 6.0,
            configure: row =>
            {
                row.NettoHoursOverride = 8.0;
                row.NettoHoursOverrideActive = true;
            });

        var totalRow = await ExportTotalRow(day);

        Assert.That(CellNumber(totalRow[6]), Is.EqualTo(8.0).Within(0.001),
            "sanity: the NettoHours total already honours the active override");
        Assert.That(CellNumber(totalRow[13]), Is.EqualTo(8.0).Within(0.001),
            "Saturday hours must be the effective netto 8.00 (active NettoHoursOverride), " +
            "not the raw NettoHours 6.00");
    }

    /// <summary>
    /// Shift 3's pause cell must follow the canonical pause rule like shifts 1–2:
    /// <c>Pause3OverrideMinutes = 30</c> wins over the legacy <c>Pause3Id = 13</c>
    /// (60 min). Dashboard sheet, Shift 3 pause column.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_Shift3PauseOverride_DashboardCellShowsOverride()
    {
        const int siteUid = 9826;
        var day = PauseTuesday;
        await SeedShift3PauseOverrideDay(siteUid, day);

        var xlsx = await ExportSiteDay(siteUid, day);

        // Dashboard with ThirdShiftActive (0-indexed): 14=Shift3Start, 15=Shift3Stop, 16=Shift3Pause.
        var row = FirstDataRowText(xlsx, "Dashboard");
        Assert.That(row[14], Is.EqualTo("14:00"), "sanity: shift 3 start");
        Assert.That(row[16], Is.EqualTo("00:30"),
            "Pause3OverrideMinutes=30 must win over Pause3Id=13 (60 min) in the Dashboard shift-3 pause cell");
    }

    /// <summary>Same scenario, Dagsoversigt (Day overview) sheet: 30 min = 30/1440 of a day.</summary>
    [Test]
    public async Task GenerateExcelDashboard_Shift3PauseOverride_DayOverviewCellShowsOverride()
    {
        const int siteUid = 9827;
        var day = PauseTuesday;
        await SeedShift3PauseOverrideDay(siteUid, day);

        var xlsx = await ExportSiteDay(siteUid, day);

        // Dagsoversigt (0-indexed): 12=Shift3Start, 13=Shift3Stop, 14=Shift3Pause (day fraction).
        var row = FirstDataRowText(xlsx, "Dagsoversigt");
        Assert.That(double.Parse(row[14], CultureInfo.InvariantCulture), Is.EqualTo(30 / 1440.0).Within(1e-9),
            "Pause3OverrideMinutes=30 must render as 30/1440 in the Day overview shift-3 pause cell, " +
            "not Pause3Id=13 → 60/1440");
    }

    /// <summary>
    /// An explicit <c>Pause1OverrideMinutes = 0</c> is a deliberate "no pause" decision by
    /// the office and is the authoritative pause. It must render as "00:00", not as a blank
    /// cell (blank means "no pause information at all"), even though Pause1Id is 0.
    /// </summary>
    [Test]
    public async Task GenerateExcelDashboard_Shift1PauseOverrideZero_PauseIdZero_CellShowsZeroNotBlank()
    {
        const int siteUid = 9828;
        var day = PauseTuesday;
        await SeedPauseDay(siteUid, day, start: day.AddHours(8), stop: day.AddHours(12), nettoHours: 4.0,
            configure: row =>
            {
                row.Pause1Id = 0;
                row.Pause1OverrideMinutes = 0;
            });

        var xlsx = await ExportSiteDay(siteUid, day);

        var (start, _, pause) = ReadShift1Cells(xlsx);
        Assert.That(start, Is.EqualTo("08:00"), "sanity: shift 1 start");
        Assert.That(pause, Is.EqualTo("00:00"),
            "Pause1OverrideMinutes=0 is an explicit zero pause and must render \"00:00\", not a blank cell");
    }

    private async Task SeedPauseDay(int siteUid, DateTime day, DateTime start, DateTime stop, double nettoHours,
        Action<PlanRegistrationEntity> configure)
    {
        await SeedSiteAndPlanRegistration(siteUid, day, useOneMinuteIntervals: true,
            start1: start, stop1: stop,
            start1Id: (int)start.TimeOfDay.TotalMinutes / 5 + 1,
            stop1Id: (int)stop.TimeOfDay.TotalMinutes / 5 + 1);

        var row = await TimePlanningPnDbContext!.PlanRegistrations
            .SingleAsync(x => x.SdkSitId == siteUid && x.Date == day);
        row.RegisteredUnderOneMinuteIntervals = true;
        row.NettoHours = nettoHours;
        row.NettoHoursInSeconds = (int)(nettoHours * 3600);
        configure(row);
        await row.Update(TimePlanningPnDbContext!);
    }

    private async Task SeedShift3PauseOverrideDay(int siteUid, DateTime day)
    {
        await SeedPauseDay(siteUid, day, start: day.AddHours(8), stop: day.AddHours(12), nettoHours: 6.5,
            configure: row =>
            {
                row.Start3Id = 169; // 14:00
                row.Stop3Id = 205;  // 17:00
                row.Start3StartedAt = day.AddHours(14);
                row.Stop3StoppedAt = day.AddHours(17);
                row.Pause3Id = 13;  // legacy 60 min
                row.Pause3OverrideMinutes = 30;
            });
        var assignedSite = await TimePlanningPnDbContext!.AssignedSites.FirstAsync(x => x.SiteId == siteUid);
        assignedSite.ThirdShiftActive = true;
        await assignedSite.Update(TimePlanningPnDbContext!);
    }

    private static PayRuleSetEntity SaturdayAllDayBandRuleSet(int siteUid) => new()
    {
        Name = $"Saturday band set {siteUid}",
        DayRules = new List<PayDayRuleEntity>(),
        DayTypeRules = new List<PayDayTypeRuleEntity>
        {
            new()
            {
                DayType = DayTypeEnum.Saturday,
                DefaultPayCode = "SAT_WORK",
                Priority = 1,
                TimeBandRules = new List<PayTimeBandRuleEntity>
                {
                    new() { StartSecondOfDay = 0, EndSecondOfDay = 86400, PayCode = "SAT_WORK", PayrollCode = "300", Priority = 1 },
                },
            },
        },
        WorkflowState = Constants.WorkflowStates.Created,
    };

    private async Task AssignPayRuleSet(int siteUid, PayRuleSetEntity payRuleSet)
    {
        await payRuleSet.Create(TimePlanningPnDbContext!);
        var assignedSite = await TimePlanningPnDbContext!.AssignedSites.FirstAsync(x => x.SiteId == siteUid);
        assignedSite.PayRuleSetId = payRuleSet.Id;
        await assignedSite.Update(TimePlanningPnDbContext!);
    }

    /// <summary>Exports one day for one site and returns the workbook stream.</summary>
    private async Task<Stream> ExportSiteDay(int siteUid, DateTime day)
    {
        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = siteUid, DateFrom = day, DateTo = day,
        });
        Assert.That(result.Success, Is.True, result.Message);
        return result.Model!;
    }

    /// <summary>
    /// Exports one day for one site and returns the Dashboard data row's numeric
    /// cells keyed by header text (pay-code columns are headed by the raw pay code).
    /// </summary>
    private async Task<Dictionary<string, double>> ExportDashboardRowByHeader(int siteUid, DateTime day)
    {
        await using var xlsx = await ExportSiteDay(siteUid, day);
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var rows = SheetRows(doc.WorkbookPart!, "Dashboard");
        var headers = rows[0].Select(c => c.CellValue?.Text ?? c.InnerText).ToList();
        var data = rows[1];
        var byHeader = new Dictionary<string, double>();
        for (var i = 0; i < headers.Count && i < data.Count; i++)
        {
            if (data[i].DataType?.Value == CellValues.Number && data[i].CellValue != null)
            {
                byHeader[headers[i]] = CellNumber(data[i]);
            }
        }
        return byHeader;
    }

    private async Task<List<Cell>> ExportTotalRow(DateTime day)
    {
        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel { DateFrom = day, DateTo = day });
        Assert.That(result.Success, Is.True, result.Message);

        await using var xlsx = result.Model!;
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        // Total sheet (0-indexed): 6=NettoHours, 8=normal hours, 9=Sunday+holiday
        // hours (Grundlovsdag: after 12:00 only), 13=Saturday hours.
        return SheetRows(doc.WorkbookPart!, "Total")[1];
    }

    private static List<string> FirstDataRowText(Stream xlsx, string sheetName)
    {
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var workbookPart = doc.WorkbookPart!;
        var sst = workbookPart.SharedStringTablePart?.SharedStringTable;
        return SheetRows(workbookPart, sheetName)[1]
            .Select(c =>
            {
                var raw = c.CellValue?.Text ?? c.InnerText ?? "";
                return c.DataType?.Value == CellValues.SharedString && sst != null && int.TryParse(raw, out var idx)
                    ? sst.ElementAt(idx).InnerText
                    : raw;
            })
            .ToList();
    }

    /// <summary>
    /// A one-minute site with a pay rule set paying every weekday second under
    /// one code, and two weekdays: 61 minutes worked and 61 minutes flex (so the
    /// seconds chain gives a SumFlexEnd of 61/60 h), then a 1 h 20 min netto
    /// override (whose pay line is 1 h 20 min). None of those has two exact
    /// decimals. The plain NettoHours column is already rounded to two decimals
    /// by Index, so it cannot carry the precision assertions.
    /// </summary>
    private async Task<(DateTime Day1, DateTime Day2)> SeedTwoDecimalScenario(int siteUid)
    {
        var day1 = new DateTime(2026, 5, 5); // Tuesday, not a holiday
        var day2 = day1.AddDays(1);
        await SeedSiteAndPlanRegistration(siteUid, day1, useOneMinuteIntervals: true,
            start1: day1.AddHours(8), stop1: day1.AddHours(9).AddMinutes(1),
            start1Id: 97, stop1Id: 110);

        var row1 = await TimePlanningPnDbContext!.PlanRegistrations
            .FirstAsync(x => x.SdkSitId == siteUid && x.Date == day1);
        row1.NettoHours = 61 / 60.0;
        row1.NettoHoursInSeconds = 61 * 60;
        row1.Flex = 61 / 60.0;
        row1.FlexInSeconds = 61 * 60;
        row1.RegisteredUnderOneMinuteIntervals = true;
        await row1.Update(TimePlanningPnDbContext!);

        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = day2,
            Start1Id = 97,
            Stop1Id = 113,
            Pause1Id = 0,
            Start1StartedAt = day2.AddHours(8),
            Stop1StoppedAt = day2.AddHours(9).AddMinutes(20),
            NettoHours = 7.5,
            NettoHoursOverride = 80 / 60.0,
            NettoHoursOverrideActive = true,
            RegisteredUnderOneMinuteIntervals = true,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var payRuleSet = new PayRuleSetEntity
        {
            Name = $"Rule set {siteUid}",
            DayRules = new List<PayDayRuleEntity>
            {
                new()
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRuleEntity>
                    {
                        new() { UpToSeconds = null, PayCode = "NORM", PayrollCode = "100", Order = 1 }
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRuleEntity>(),
            WorkflowState = Constants.WorkflowStates.Created,
        };
        await payRuleSet.Create(TimePlanningPnDbContext!);
        var assignedSite = await TimePlanningPnDbContext!.AssignedSites.FirstAsync(x => x.SiteId == siteUid);
        assignedSite.PayRuleSetId = payRuleSet.Id;
        await assignedSite.Update(TimePlanningPnDbContext!);

        return (day1, day2);
    }

    private static void AssertStyleIsTwoDecimals(WorkbookPart workbookPart)
    {
        var format = (CellFormat)workbookPart.WorkbookStylesPart!.Stylesheet.CellFormats!
            .ElementAt((int)TwoDecimalsStyleIndex);
        Assert.That(format.NumberFormatId?.Value, Is.EqualTo(2U), "built-in number format 2 is 0.00");
        Assert.That(format.ApplyNumberFormat?.Value, Is.True);
    }

    private static List<List<Cell>> SheetRows(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        return part.Worksheet.Descendants<Row>()
            .Select(r => r.Elements<Cell>().ToList())
            .ToList();
    }

    private static double CellNumber(Cell cell) =>
        double.Parse(cell.CellValue!.Text, CultureInfo.InvariantCulture);

    /// <summary>
    /// Seeds an SDK Site/Worker + UseOneMinuteIntervals=true AssignedSite + a
    /// neutral prior-day PlanRegistration (so the running SumFlexStart chain
    /// enters the target day at exactly 0) + a target-day PlanRegistration
    /// carrying the exact production shape of the PaiedOutFlex bug: FlexInSeconds
    /// set, the legacy double PaiedOutFlex set, and PaiedOutFlexInSeconds left at
    /// whatever the caller passes (0 = the value every production writer leaves it at).
    /// </summary>
    private async Task SeedFlexScenario(
        int siteUid, DateTime date,
        int flexInSecondsOnTargetDay, double paiedOutFlexOnTargetDay, int paiedOutFlexInSecondsOnTargetDay)
    {
        var core = await GetCore();
        var sdkDb = core.DbContextHelper.GetDbContext();

        var site = new SdkSite { Name = $"Site {siteUid}", MicrotingUid = siteUid };
        await site.Create(sdkDb);

        var worker = new SdkWorker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = $"test{siteUid}@example.com",
            MicrotingUid = 1000 + siteUid,
        };
        await worker.Create(sdkDb);

        var siteWorker = new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2000 + siteUid,
        };
        await siteWorker.Create(sdkDb);

        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date.AddDays(-1),
            Start1Id = 0,
            Stop1Id = 0,
            Pause1Id = 0,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date,
            Start1Id = 0,
            Stop1Id = 0,
            Pause1Id = 0,
            FlexInSeconds = flexInSecondsOnTargetDay,
            PaiedOutFlex = paiedOutFlexOnTargetDay,
            PaiedOutFlexInSeconds = paiedOutFlexInSecondsOnTargetDay,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    /// <summary>
    /// Reads the (SumFlexEnd, PaidOutFlex) cell text from the first data row that
    /// has either populated. Column layout from <c>FillDataRow</c> when
    /// Third/Fourth/FifthShiftActive are all off (0-indexed): ...14=NettoHours,
    /// 15=FlexHours, 16=SumFlexEnd, 17=PaidOutFlex(numeric), 18=Message, ...
    /// </summary>
    private static (string SumFlexEnd, string PaidOutFlex) ReadFlexCells(Stream xlsx)
    {
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var workbookPart = doc.WorkbookPart!;
        var dashboardSheet = workbookPart.Workbook.Descendants<Sheet>()
            .First(s => s.Name == "Dashboard");
        var dashboardPart = (WorksheetPart)workbookPart.GetPartById(dashboardSheet.Id!);
        var sheet = dashboardPart.Worksheet;
        var sst = workbookPart.SharedStringTablePart?.SharedStringTable;
        string CellText(Cell c)
        {
            var raw = c.CellValue?.Text ?? c.InnerText ?? "";
            if (c.DataType?.Value == CellValues.SharedString && sst != null && int.TryParse(raw, out var idx))
            {
                return sst.ElementAt(idx).InnerText;
            }
            return raw;
        }
        var rows = sheet.Descendants<Row>().ToList();
        foreach (var row in rows.Where(r => r.RowIndex == null || r.RowIndex! > 1U))
        {
            var cells = row.Elements<Cell>().ToList();
            if (cells.Count < 18) continue;
            var sumFlexEnd = CellText(cells[16]);
            var paidOutFlex = CellText(cells[17]);
            if (!string.IsNullOrEmpty(sumFlexEnd) || !string.IsNullOrEmpty(paidOutFlex))
            {
                return (sumFlexEnd, paidOutFlex);
            }
        }
        return ("", "");
    }

    private async Task SeedSiteAndPlanRegistration(
        int siteUid, DateTime date, bool useOneMinuteIntervals,
        DateTime? start1, DateTime? stop1,
        int start1Id, int stop1Id)
    {
        var core = await GetCore();
        var sdkDb = core.DbContextHelper.GetDbContext();

        var site = new SdkSite { Name = $"Site {siteUid}", MicrotingUid = siteUid };
        await site.Create(sdkDb);

        var worker = new SdkWorker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = $"test{siteUid}@example.com",
            MicrotingUid = 1000 + siteUid,
        };
        await worker.Create(sdkDb);

        var siteWorker = new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2000 + siteUid,
        };
        await siteWorker.Create(sdkDb);

        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = useOneMinuteIntervals,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        // Seed a prior-day PlanRegistration so Index() inserts a "prePlanning"
        // row at position 0; the export drops that row via Skip(1) and the
        // requested-range row survives at position 0 of the data sheet.
        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date.AddDays(-1),
            Start1Id = 0,
            Stop1Id = 0,
            Pause1Id = 0,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date,
            Start1Id = start1Id,
            Stop1Id = stop1Id,
            Pause1Id = 0,
            Start1StartedAt = start1,
            Stop1StoppedAt = stop1,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    /// <summary>
    /// Opens the xlsx stream and returns the (Shift1Start, Shift1Stop, Shift1Pause) cell text
    /// for the first data row that has either populated. Column layout from
    /// <c>FillDataRow</c> (positional, 0-indexed): 0=EmployeeNo, 1=SiteName,
    /// 2=Tags, 3=WeekDay, 4=Date, 5=WeekNumber, 6=PlanText, 7=PlanHours,
    /// 8=Shift1Start, 9=Shift1Stop, 10=Shift1Pause. <c>CreateCell</c> doesn't set
    /// <c>CellReference</c>, so cells are positional within the row, not
    /// addressed by letter.
    /// </summary>
    private static (string Start, string Stop, string Pause) ReadShift1Cells(Stream xlsx)
    {
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var workbookPart = doc.WorkbookPart!;
        // The first worksheet is now "Dagsoversigt" (Day overview); resolve the
        // Dashboard sheet explicitly by name so these assertions keep targeting it.
        var dashboardSheet = workbookPart.Workbook.Descendants<Sheet>()
            .First(s => s.Name == "Dashboard");
        var dashboardPart = (WorksheetPart)workbookPart.GetPartById(dashboardSheet.Id!);
        var sheet = dashboardPart.Worksheet;
        var sst = workbookPart.SharedStringTablePart?.SharedStringTable;
        string CellText(Cell c)
        {
            var raw = c.CellValue?.Text ?? c.InnerText ?? "";
            if (c.DataType?.Value == CellValues.SharedString && sst != null && int.TryParse(raw, out var idx))
            {
                return sst.ElementAt(idx).InnerText;
            }
            return raw;
        }
        var rows = sheet.Descendants<Row>().ToList();
        foreach (var row in rows.Where(r => r.RowIndex == null || r.RowIndex! > 1U))
        {
            var cells = row.Elements<Cell>().ToList();
            if (cells.Count < 11) continue;
            var shift1Start = CellText(cells[8]);
            var shift1Stop  = CellText(cells[9]);
            if (!string.IsNullOrEmpty(shift1Start) || !string.IsNullOrEmpty(shift1Stop))
            {
                return (shift1Start, shift1Stop, CellText(cells[10]));
            }
        }
        return ("", "", "");
    }
}
