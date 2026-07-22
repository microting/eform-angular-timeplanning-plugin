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
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// End-to-end coverage for the new "Dagsoversigt" (Day overview) worksheet that
/// is added as the FIRST tab of both the single-worker and all-workers Excel
/// exports. These tests open the produced xlsx with OpenXml and assert the sheet
/// order, the 22-column header, the Excel Table definition, the cell styles and
/// the OADate cell values. The export's <c>ValidateExcel</c> swallows schema
/// errors, so opening/reading the file in a test is the only thing that catches
/// a malformed worksheet or table.
/// </summary>
[TestFixture]
public class DagsoversigtWorksheetExportTests : TestBaseSetup
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

        // Ensure a Danish Language exists in the SDK DB and bind it to the
        // user-language stub so Thread.CurrentUICulture is deterministically "da".
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

        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
            options,
            coreService);
    }

    // ------------------------------------------------------------------
    // 1. Pure helper: GetShiftTimeFraction
    // ------------------------------------------------------------------

    [Test]
    public void GetShiftTimeFraction_CoversGridStampAndEdgeCases()
    {
        // Legacy 5-minute grid: index 97 -> 08:00 -> 480 minutes / 1440.
        Assert.That(_service.GetShiftTimeFraction(97, null, false)!.Value,
            Is.EqualTo(480.0 / 1440.0).Within(1e-9));

        // Absent shift -> null.
        Assert.That(_service.GetShiftTimeFraction(null, null, false), Is.Null);

        // Index 0 is treated as "no shift" -> null.
        Assert.That(_service.GetShiftTimeFraction(0, null, false), Is.Null);

        // Index 289 -> 24:00 -> 1.0 (whole day).
        Assert.That(_service.GetShiftTimeFraction(289, null, false)!.Value,
            Is.EqualTo(1.0).Within(1e-9));

        // One-minute interval path: real stamp 08:04 -> (8*60+4)/1440.
        Assert.That(
            _service.GetShiftTimeFraction(97, new DateTime(2026, 5, 15, 8, 4, 0), true)!.Value,
            Is.EqualTo((8 * 60 + 4) / 1440.0).Within(1e-9));
    }

    // ------------------------------------------------------------------
    // 2. Single-worker: first sheet is Dagsoversigt with 22-column header.
    // ------------------------------------------------------------------

    [Test]
    public async Task SingleWorker_FirstSheetIsDagsoversigt_With22ColumnHeaderAndTable()
    {
        await SeedSiteAndPlanRegistration(
            siteUid: 9801,
            employeeNo: "1",
            date: new DateTime(2026, 5, 15),
            useOneMinuteIntervals: false,
            start1Id: 97, stop1Id: 121);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9801,
            DateFrom = new DateTime(2026, 5, 15),
            DateTo = new DateTime(2026, 5, 15),
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        result.Model!.Position = 0;
        using var doc = SpreadsheetDocument.Open(result.Model!, false);
        var workbookPart = doc.WorkbookPart!;
        var sheets = workbookPart.Workbook.Descendants<Sheet>().ToList();

        // First tab is the localized "Dagsoversigt".
        Assert.That(sheets[0].Name!.Value, Is.EqualTo("Dagsoversigt"));

        var firstPart = (WorksheetPart)workbookPart.GetPartById(sheets[0].Id!);
        var headerRow = firstPart.Worksheet.Descendants<Row>().First(r => r.RowIndex! == 1U);
        var headerCells = headerRow.Elements<Cell>().ToList();
        Assert.That(headerCells.Count, Is.EqualTo(22), "Dagsoversigt header must have 22 columns");

        Assert.That(CellText(headerCells[0], workbookPart), Is.EqualTo("Medarbejder nr."));
        Assert.That(CellText(headerCells[2], workbookPart), Is.EqualTo("Etiketter"));
        Assert.That(CellText(headerCells[21], workbookPart), Is.EqualTo("Timer netto"));

        // Exactly one Excel Table, named region A1:V{1+dataRows}.
        Assert.That(firstPart.TableDefinitionParts.Count(), Is.EqualTo(1));
        var dataRows = firstPart.Worksheet.Descendants<Row>().Count(r => r.RowIndex! > 1U);
        Assert.That(dataRows, Is.EqualTo(1), "Single seeded plan registration => one data row");
        Assert.That(firstPart.TableDefinitionParts.First().Table!.Reference!.Value,
            Is.EqualTo($"A1:V{1 + dataRows}"));
    }

    // ------------------------------------------------------------------
    // 3. Single-worker: cell styles & values for a data row.
    // ------------------------------------------------------------------

    [Test]
    public async Task SingleWorker_DataRow_HasCorrectStylesAndOaDateValues()
    {
        await SeedSiteAndPlanRegistration(
            siteUid: 9802,
            employeeNo: "1",
            date: new DateTime(2026, 5, 15),
            useOneMinuteIntervals: false,
            start1Id: 97, stop1Id: 121);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9802,
            DateFrom = new DateTime(2026, 5, 15),
            DateTo = new DateTime(2026, 5, 15),
        });

        Assert.That(result.Success, Is.True, result.Message);

        result.Model!.Position = 0;
        using var doc = SpreadsheetDocument.Open(result.Model!, false);
        var workbookPart = doc.WorkbookPart!;
        var sheets = workbookPart.Workbook.Descendants<Sheet>().ToList();
        var firstPart = (WorksheetPart)workbookPart.GetPartById(sheets[0].Id!);

        var dataRow = firstPart.Worksheet.Descendants<Row>().First(r => r.RowIndex! == 2U);

        // Date cell (col E): StyleIndex 5 (dd/mm/yyyy), numeric OADate.
        var dateCell = dataRow.Elements<Cell>().Single(c => c.CellReference == "E2");
        Assert.That(dateCell.StyleIndex!.Value, Is.EqualTo(5U));
        Assert.That(dateCell.DataType!.Value, Is.EqualTo(CellValues.Number));
        Assert.That(double.Parse(dateCell.CellValue!.Text, CultureInfo.InvariantCulture),
            Is.EqualTo(new DateTime(2026, 5, 15).ToOADate()).Within(1e-9));

        // Shift 1 start cell (col G): StyleIndex 3 (hh:mm), value = (97-1)*5/1440.
        var shift1StartCell = dataRow.Elements<Cell>().Single(c => c.CellReference == "G2");
        Assert.That(shift1StartCell.StyleIndex!.Value, Is.EqualTo(3U));
        Assert.That(double.Parse(shift1StartCell.CellValue!.Text, CultureInfo.InvariantCulture),
            Is.EqualTo((97 - 1) * 5 / 1440.0).Within(1e-9));

        // NettoHours cell (col V): StyleIndex 4 (0.00).
        var nettoCell = dataRow.Elements<Cell>().Single(c => c.CellReference == "V2");
        Assert.That(nettoCell.StyleIndex!.Value, Is.EqualTo(4U));
    }

    // ------------------------------------------------------------------
    // 4. All-workers: first sheet is Dagsoversigt; rows combined & sorted.
    // ------------------------------------------------------------------

    [Test]
    public async Task AllWorkers_FirstSheetIsDagsoversigt_RowsCombinedAndSorted()
    {
        // Two sites, employee numbers "1" and "2", each with a registration on
        // 2026-05-14 and 2026-05-15.
        await SeedSiteWithTwoDays(siteUid: 9901, employeeNo: "1",
            dateA: new DateTime(2026, 5, 14), dateB: new DateTime(2026, 5, 15));
        await SeedSiteWithTwoDays(siteUid: 9902, employeeNo: "2",
            dateA: new DateTime(2026, 5, 14), dateB: new DateTime(2026, 5, 15));

        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = new DateTime(2026, 5, 14),
                DateTo = new DateTime(2026, 5, 15),
            });

        Assert.That(result.Success, Is.True, result.Message);

        result.Model!.Position = 0;
        using var doc = SpreadsheetDocument.Open(result.Model!, false);
        var workbookPart = doc.WorkbookPart!;
        var sheets = workbookPart.Workbook.Descendants<Sheet>().ToList();

        // Sheet order: [Dagsoversigt, Total, Site 9901, Site 9902].
        Assert.That(sheets.Count, Is.EqualTo(4));
        Assert.That(sheets[0].Name!.Value, Is.EqualTo("Dagsoversigt"));
        Assert.That(sheets[1].Name!.Value, Is.EqualTo("Total"));
        Assert.That(sheets[2].Name!.Value, Is.EqualTo("Site 9901"));
        Assert.That(sheets[3].Name!.Value, Is.EqualTo("Site 9902"));

        var firstPart = (WorksheetPart)workbookPart.GetPartById(sheets[0].Id!);
        var rowsByIndex = firstPart.Worksheet.Descendants<Row>()
            .Where(r => r.RowIndex! > 1U)
            .OrderBy(r => r.RowIndex!.Value)
            .ToList();

        Assert.That(rowsByIndex.Count, Is.EqualTo(4),
            "Two sites x two days => four combined data rows");

        var date14 = new DateTime(2026, 5, 14).ToOADate();
        var date15 = new DateTime(2026, 5, 15).ToOADate();

        // Ordered by Date then EmployeeNo:
        // row2=(14,"1"), row3=(14,"2"), row4=(15,"1"), row5=(15,"2").
        AssertRowDateAndEmployee(rowsByIndex[0], workbookPart, date14, "1");
        AssertRowDateAndEmployee(rowsByIndex[1], workbookPart, date14, "2");
        AssertRowDateAndEmployee(rowsByIndex[2], workbookPart, date15, "1");
        AssertRowDateAndEmployee(rowsByIndex[3], workbookPart, date15, "2");
    }

    // ------------------------------------------------------------------
    // 5. Regression: a cross-midnight / out-of-range shift slot id (> 289)
    //    must not crash the export. Production bug: Stop1Id = 313 (= 02:00
    //    next day) made GetShiftTime index past the 288-entry plr.Options
    //    array and throw IndexOutOfRange. The all-workers path was the one
    //    that crashed in production, so both overloads are covered.
    // ------------------------------------------------------------------

    [Test]
    public async Task Export_WithCrossMidnightShiftSlotId_DoesNotThrow()
    {
        // Start1Id = 265 -> (265-1)*5 = 1320 min -> 22:00.
        // Stop1Id  = 313 -> (313-1)*5 = 1560 min -> 26:00 (= 02:00 next day),
        //            the > 289 case that used to overflow plr.Options and throw.
        // Pause1Id = 295 -> (295-1)*5 = 1470 min -> 24:30; Pause always goes
        //            through the crashing 2-arg GetShiftTime path (actualStamp
        //            is always null for pause), so it exercises the fix too.
        await SeedSiteAndPlanRegistration(
            siteUid: 9810,
            employeeNo: "1",
            date: new DateTime(2026, 5, 15),
            useOneMinuteIntervals: false,
            start1Id: 265, stop1Id: 313, pause1Id: 295);

        // --- Single-worker overload ---
        var singleResult = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursRequestModel
            {
                SiteId = 9810,
                DateFrom = new DateTime(2026, 5, 15),
                DateTo = new DateTime(2026, 5, 15),
            });

        Assert.That(singleResult.Success, Is.True, singleResult.Message);
        Assert.That(singleResult.Model, Is.Not.Null);
        Assert.That(singleResult.Model!.Length, Is.GreaterThan(0));

        // Confirm not just "no throw" but correct arithmetic output: the
        // Shift1 Stop cell for slot 313 renders "26:00" on the Dashboard sheet.
        var (_, shift1Stop) = ReadDashboardShift1Cells(singleResult.Model!);
        Assert.That(shift1Stop, Is.EqualTo("26:00"),
            "Out-of-range slot 313 must render arithmetically as 26:00, not throw");

        // Release the single-worker file handle before invoking the all-workers
        // overload. Both exports write to /tmp/results/{yyyyMMdd_HHmmss}_.xlsx and
        // return a still-open FileStream; calling them back-to-back inside the same
        // second would otherwise collide on the identical filename and fail with
        // an IOException unrelated to the slot-id regression under test.
        await singleResult.Model!.DisposeAsync();

        // --- All-workers overload (the path that crashed in production) ---
        var allResult = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = new DateTime(2026, 5, 15),
                DateTo = new DateTime(2026, 5, 15),
            });

        Assert.That(allResult.Success, Is.True, allResult.Message);
        Assert.That(allResult.Model, Is.Not.Null);
        Assert.That(allResult.Model!.Length, Is.GreaterThan(0));

        // The all-workers workbook has no "Dashboard" sheet; the positional
        // FillDataRow layout lives on the per-site sheet, named after the site
        // ("Site 9810"). Same 0-indexed columns: 8=Shift1Start, 9=Shift1Stop.
        var (_, allShift1Stop) = ReadDashboardShift1Cells(allResult.Model!, "Site 9810");
        Assert.That(allShift1Stop, Is.EqualTo("26:00"),
            "All-workers path (the one that crashed in production) must also render slot 313 as 26:00");

        await allResult.Model!.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // 6. All-workers: each per-site sheet exposes only the pay codes DECLARED
    //    in that site's own pay-rule-set; the Total sheet keeps the union.
    // ------------------------------------------------------------------

    [Test]
    public async Task AllWorkers_PerSiteSheets_UseEachWorkersOwnPayRuleSetCodes()
    {
        var date = new DateTime(2026, 5, 15);

        // Site A declares only "AAA"; Site B declares only "BBB"; Site C has no
        // pay-rule-set at all. Each site has a worked-time registration on `date`.
        await SeedSiteAndPlanRegistration(
            siteUid: 9701, employeeNo: "1", date: date,
            useOneMinuteIntervals: false, start1Id: 97, stop1Id: 121);
        await LinkPayRuleSetToSite(siteUid: 9701, name: "RuleSet A", payCode: "AAA");

        await SeedSiteAndPlanRegistration(
            siteUid: 9702, employeeNo: "2", date: date,
            useOneMinuteIntervals: false, start1Id: 97, stop1Id: 121);
        await LinkPayRuleSetToSite(siteUid: 9702, name: "RuleSet B", payCode: "BBB");

        await SeedSiteAndPlanRegistration(
            siteUid: 9703, employeeNo: "3", date: date,
            useOneMinuteIntervals: false, start1Id: 97, stop1Id: 121);
        // Site C: intentionally no LinkPayRuleSetToSite call → PayRuleSetId stays null.

        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = date,
                DateTo = date,
            });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var workbookPart = doc.WorkbookPart!;

            var siteAHeader = ReadHeaderRowText(workbookPart, "Site 9701");
            var siteBHeader = ReadHeaderRowText(workbookPart, "Site 9702");
            var siteCHeader = ReadHeaderRowText(workbookPart, "Site 9703");
            var totalHeader = ReadHeaderRowText(workbookPart, "Total");

            // Site A's sheet: only its own declared code.
            Assert.That(siteAHeader, Does.Contain("AAA"), "Site A sheet must declare its own code AAA");
            Assert.That(siteAHeader, Does.Not.Contain("BBB"), "Site A sheet must NOT carry Site B's code BBB");

            // Site B's sheet: only its own declared code.
            Assert.That(siteBHeader, Does.Contain("BBB"), "Site B sheet must declare its own code BBB");
            Assert.That(siteBHeader, Does.Not.Contain("AAA"), "Site B sheet must NOT carry Site A's code AAA");

            // Site C has no rule-set: neither code appears.
            Assert.That(siteCHeader, Does.Not.Contain("AAA"), "Site C (no rule-set) must NOT carry AAA");
            Assert.That(siteCHeader, Does.Not.Contain("BBB"), "Site C (no rule-set) must NOT carry BBB");

            // Total sheet keeps the union of all codes (unchanged behavior).
            Assert.That(totalHeader, Does.Contain("AAA"), "Total sheet must keep the union, including AAA");
            Assert.That(totalHeader, Does.Contain("BBB"), "Total sheet must keep the union, including BBB");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Opens the xlsx stream and returns the (Shift1Start, Shift1Stop) cell text
    /// for the first populated data row of the positional "Dashboard" sheet.
    /// Column layout from FillDataRow (0-indexed): 8=Shift1Start, 9=Shift1Stop.
    /// </summary>
    private static (string Start, string Stop) ReadDashboardShift1Cells(Stream xlsx, string sheetName = "Dashboard")
    {
        xlsx.Position = 0;
        using var doc = SpreadsheetDocument.Open(xlsx, false);
        var workbookPart = doc.WorkbookPart!;
        var dashboardSheet = workbookPart.Workbook.Descendants<Sheet>()
            .First(s => s.Name == sheetName);
        var dashboardPart = (WorksheetPart)workbookPart.GetPartById(dashboardSheet.Id!);
        var rows = dashboardPart.Worksheet.Descendants<Row>().ToList();
        foreach (var row in rows.Where(r => r.RowIndex == null || r.RowIndex! > 1U))
        {
            var cells = row.Elements<Cell>().ToList();
            if (cells.Count < 10) continue;
            var shift1Start = CellText(cells[8], workbookPart);
            var shift1Stop = CellText(cells[9], workbookPart);
            if (!string.IsNullOrEmpty(shift1Start) || !string.IsNullOrEmpty(shift1Stop))
            {
                return (shift1Start, shift1Stop);
            }
        }
        return ("", "");
    }


    private static void AssertRowDateAndEmployee(Row row, WorkbookPart wb, double expectedOaDate, string expectedEmployeeNo)
    {
        var employeeCell = row.Elements<Cell>().Single(c =>
            c.CellReference!.Value!.StartsWith("A"));
        var dateCell = row.Elements<Cell>().Single(c =>
            c.CellReference!.Value!.StartsWith("E"));

        Assert.That(CellText(employeeCell, wb), Is.EqualTo(expectedEmployeeNo));
        Assert.That(double.Parse(dateCell.CellValue!.Text, CultureInfo.InvariantCulture),
            Is.EqualTo(expectedOaDate).Within(1e-9));
    }

    private static string CellText(Cell c, WorkbookPart wb)
    {
        var sst = wb.SharedStringTablePart?.SharedStringTable;
        var raw = c.CellValue?.Text ?? c.InnerText ?? "";
        if (c.DataType?.Value == CellValues.SharedString && sst != null && int.TryParse(raw, out var idx))
        {
            return sst.ElementAt(idx).InnerText;
        }
        return raw;
    }

    /// <summary>
    /// Resolves the sheet named <paramref name="sheetName"/> from the workbook and
    /// returns the joined text of every header cell (row 1). Pay-code headers are
    /// plain string cells appended after the fixed columns, so a substring search
    /// of the joined text reliably tells whether a code column is present.
    /// </summary>
    private static string ReadHeaderRowText(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Descendants<Sheet>()
            .First(s => s.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var headerRow = part.Worksheet.Descendants<Row>().First(r => r.RowIndex! == 1U);
        return string.Join("|", headerRow.Elements<Cell>().Select(c => CellText(c, workbookPart)));
    }

    /// <summary>
    /// Creates a <see cref="PayRuleSet"/> declaring exactly one pay code (one
    /// <see cref="PayDayRule"/> → one <see cref="PayTierRule"/>) and links it to the
    /// already-seeded site's <see cref="AssignedSiteEntity"/> by setting
    /// <c>PayRuleSetId</c>. The declared code drives the per-site sheet's pay-code
    /// header columns regardless of worked time.
    /// </summary>
    private async Task LinkPayRuleSetToSite(int siteUid, string name, string payCode)
    {
        var payRuleSet = new PayRuleSet
        {
            Name = name,
            DayRules = new List<PayDayRule>
            {
                new PayDayRule
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new PayTierRule { UpToSeconds = null, PayCode = payCode, PayrollCode = "100", Order = 1 }
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>(),
            WorkflowState = Constants.WorkflowStates.Created,
        };
        await payRuleSet.Create(TimePlanningPnDbContext!);

        var assignedSite = await TimePlanningPnDbContext!.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstAsync(x => x.SiteId == siteUid);
        assignedSite.PayRuleSetId = payRuleSet.Id;
        await assignedSite.Update(TimePlanningPnDbContext!);

        // Give the in-range registration positive NettoHours so the WEEKDAY tier
        // emits a pay line for the declared code. The per-site COLUMN header comes
        // from the DECLARED codes (worked time irrelevant), but the Total sheet's
        // union is built from EMITTED pay codes, so it needs non-zero worked time.
        var registrations = await TimePlanningPnDbContext!.PlanRegistrations
            .Where(x => x.SdkSitId == siteUid
                        && x.WorkflowState != Constants.WorkflowStates.Removed
                        && x.Start1Id > 0)
            .ToListAsync();
        foreach (var registration in registrations)
        {
            registration.NettoHours = 2.0;
            await registration.Update(TimePlanningPnDbContext!);
        }
    }

    /// <summary>
    /// Seeds one SDK Site/Worker/SiteWorker + an AssignedSite + a prior-day and a
    /// requested-day PlanRegistration. Mirrors the helper in
    /// <c>WorkingHoursExcelExportE2ETests</c> but takes an explicit employee number.
    /// </summary>
    private async Task SeedSiteAndPlanRegistration(
        int siteUid, string employeeNo, DateTime date, bool useOneMinuteIntervals,
        int start1Id, int stop1Id, int pause1Id = 0)
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
            EmployeeNo = employeeNo,
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

        // Prior-day registration so Index() emits a "prePlanning" row at index 0
        // that the export drops via Skip(1).
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
            Pause1Id = pause1Id,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    /// <summary>
    /// Seeds a site with a prior-day registration (dropped via Skip(1)) plus two
    /// in-range registrations on <paramref name="dateA"/> and <paramref name="dateB"/>.
    /// </summary>
    private async Task SeedSiteWithTwoDays(
        int siteUid, string employeeNo, DateTime dateA, DateTime dateB)
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
            EmployeeNo = employeeNo,
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
            UseOneMinuteIntervals = false,
            Resigned = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        // Prior-day registration (dropped by Skip(1)).
        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = dateA.AddDays(-1),
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

        foreach (var date in new[] { dateA, dateB })
        {
            await new PlanRegistrationEntity
            {
                SdkSitId = siteUid,
                Date = date,
                Start1Id = 97,
                Stop1Id = 121,
                Pause1Id = 0,
                PlanText = "",
                CommentOffice = "",
                CommentOfficeAll = "",
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedByUserId = 1,
                UpdatedByUserId = 1,
            }.Create(TimePlanningPnDbContext!);
        }
    }
}
