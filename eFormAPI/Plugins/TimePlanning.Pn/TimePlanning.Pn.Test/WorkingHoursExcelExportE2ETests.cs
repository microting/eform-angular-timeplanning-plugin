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
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PayDayRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayDayRule;
using PayDayTypeRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayDayTypeRule;
using PayRuleSetEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayRuleSet;
using PayTierRuleEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PayTierRule;
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
        await base.Setup();

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

        var (shift1Start, shift1Stop) = ReadShift1Cells(result.Model!);
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

        var (shift1Start, shift1Stop) = ReadShift1Cells(result.Model!);
        Assert.That(shift1Start, Is.EqualTo("08:00"), "Legacy slot 97 → Options[96] = \"08:00\"");
        Assert.That(shift1Stop, Is.EqualTo("10:00"), "Legacy slot 121 → Options[120] = \"10:00\"");
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
    /// Opens the xlsx stream and returns the (Shift1Start, Shift1Stop) cell text
    /// for the first data row that has either populated. Column layout from
    /// <c>FillDataRow</c> (positional, 0-indexed): 0=EmployeeNo, 1=SiteName,
    /// 2=Tags, 3=WeekDay, 4=Date, 5=WeekNumber, 6=PlanText, 7=PlanHours,
    /// 8=Shift1Start, 9=Shift1Stop, 10=Shift1Pause. <c>CreateCell</c> doesn't set
    /// <c>CellReference</c>, so cells are positional within the row, not
    /// addressed by letter.
    /// </summary>
    private static (string Start, string Stop) ReadShift1Cells(Stream xlsx)
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
            if (cells.Count < 10) continue;
            var shift1Start = CellText(cells[8]);
            var shift1Stop  = CellText(cells[9]);
            if (!string.IsNullOrEmpty(shift1Start) || !string.IsNullOrEmpty(shift1Stop))
            {
                return (shift1Start, shift1Stop);
            }
        }
        return ("", "");
    }
}
