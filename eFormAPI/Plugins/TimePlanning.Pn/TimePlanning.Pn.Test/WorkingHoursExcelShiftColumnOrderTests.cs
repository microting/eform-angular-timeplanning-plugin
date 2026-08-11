using System;
using System.Collections.Generic;
using System.Globalization;
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
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Locks the shift 3/4/5 column REORDER of the working-hours Excel exports:
/// the optional shift triples (start/stop/pause) now sit CONTIGUOUSLY after the
/// Shift 2 pause column and BEFORE NettoHours — in the header, in every data
/// row (FillDataRow) and in the totals row. Fixed layout (0-indexed):
/// 0 EmployeeNo, 1 Worker, 2 Tags, 3 WeekDay, 4 Date, 5 WeekNumber, 6 PlanText,
/// 7 PlanHours, 8-10 Shift1, 11-13 Shift2, [14-16 Shift3], [Shift4 triple],
/// [Shift5 triple], then NettoHours, Flex, SumFlexStart, PaidOutFlex, Message,
/// Comments, CommentOffice (then pay-code columns — none here, no rule-set is
/// linked, so header/data/totals cell counts must match EXACTLY).
/// The single-worker overload reads the site's own AssignedSite flags; the
/// all-workers overload computes GLOBAL flags (AssignedSites.Any) so one
/// flagged site widens every per-site sheet. Headers are asserted against the
/// Danish resx values (Resources/Translations.da.resx) because the export
/// switches CurrentUICulture to "da".
/// Also covers the Total sheet's per-seed-message columns: the new Id 13
/// "PregnancyLeave" message ("Graviditetsbetinget fravær") gets a column whose
/// value is the netto-hours sum of the rows carrying MessageId 13, honoring the
/// NettoHoursOverrideActive split.
/// </summary>
[TestFixture]
public class WorkingHoursExcelShiftColumnOrderTests : TestBaseSetup
{
    // Danish resx values (Resources/Translations.da.resx) — the export switches
    // CurrentUICulture to "da", so these are the literal header strings.
    private const string DaShift2Pause = "Skift 2: pause";
    private const string DaShift3Start = "Skift 3: start";
    private const string DaShift3Stop = "Skift 3: stop";
    private const string DaShift3Pause = "Skift 3: pause";
    private const string DaShift4Start = "Skift 4: start";
    private const string DaShift4Stop = "Skift 4: stop";
    private const string DaShift4Pause = "Skift 4: pause";
    private const string DaShift5Start = "Skift 5: start";
    private const string DaShift5Stop = "Skift 5: stop";
    private const string DaShift5Pause = "Skift 5: pause";
    private const string DaNettoHours = "Timer netto";
    private const string DaPregnancyLeave = "Graviditetsbetinget fravær";

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

        // Danish user language so Translations.X resolves deterministically to
        // the da values ("Skift 3: start", "Timer netto") in the headers.
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
    // 1. Flags all off: Shift2 pause is immediately followed by NettoHours.
    // ------------------------------------------------------------------

    [Test]
    public async Task Dashboard_NoExtraShifts_Shift2PauseIsFollowedByNettoHours()
    {
        var date = ExportDate();
        await SeedSiteAndPlanRegistration(siteUid: 9401, employeeNo: "1", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 2.0);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9401, DateFrom = date, DateTo = date,
        });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var rows = ReadSheetRows(doc.WorkbookPart!, "Dashboard");
            var header = rows.First();

            Assert.That(header[13], Is.EqualTo(DaShift2Pause),
                "Without extra shifts, header index 13 must be the Shift 2 pause column");
            Assert.That(header[14], Is.EqualTo(DaNettoHours),
                "Without extra shifts, NettoHours must come DIRECTLY after Shift 2 pause");
            Assert.That(header.Count, Is.EqualTo(21),
                "Fixed layout without extra shifts and without pay codes is exactly 21 columns");

            AssertWidthInvariant(rows, "Dashboard");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 2. Third shift only: Shift3 triple sits between Shift2 pause and NettoHours.
    // ------------------------------------------------------------------

    [Test]
    public async Task Dashboard_ThirdShiftEnabled_Shift3TripleSitsBetweenShift2AndNettoHours()
    {
        var date = ExportDate();
        await SeedSiteAndPlanRegistration(siteUid: 9402, employeeNo: "1", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 2.5,
            thirdShiftActive: true,
            start3Id: 97, stop3Id: 121, pause3Id: 7);

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9402, DateFrom = date, DateTo = date,
        });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var rows = ReadSheetRows(doc.WorkbookPart!, "Dashboard");
            var header = rows.First();

            Assert.That(header[13], Is.EqualTo(DaShift2Pause));
            Assert.That(header[14], Is.EqualTo(DaShift3Start),
                "Shift 3 start must come DIRECTLY after Shift 2 pause");
            Assert.That(header[15], Is.EqualTo(DaShift3Stop));
            Assert.That(header[16], Is.EqualTo(DaShift3Pause));
            Assert.That(header[17], Is.EqualTo(DaNettoHours),
                "NettoHours must come DIRECTLY after the Shift 3 triple");
            Assert.That(header.Count, Is.EqualTo(24));

            // The DATA row's shift-3 values must land under the shift-3 headers:
            // slot 97 -> 08:00, slot 121 -> 10:00, pause slot 7 -> 00:30.
            var dataRow = rows[1];
            Assert.That(dataRow[14], Is.EqualTo("08:00"),
                "Shift 3 start value must sit at the Shift 3 start header index");
            Assert.That(dataRow[15], Is.EqualTo("10:00"));
            Assert.That(dataRow[16], Is.EqualTo("00:30"));
            Assert.That(double.Parse(dataRow[17], CultureInfo.InvariantCulture),
                Is.EqualTo(2.5).Within(1e-9),
                "NettoHours value must still sit under the NettoHours header");

            AssertWidthInvariant(rows, "Dashboard");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 3. All five shifts: triples 3, 4, 5 are contiguous, then NettoHours.
    // ------------------------------------------------------------------

    [Test]
    public async Task Dashboard_AllFiveShifts_AllTriplesContiguous()
    {
        var date = ExportDate();
        await SeedSiteAndPlanRegistration(siteUid: 9403, employeeNo: "1", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 4.0,
            thirdShiftActive: true, fourthShiftActive: true, fifthShiftActive: true,
            start3Id: 97, stop3Id: 121,   // 08:00 - 10:00
            start4Id: 145, stop4Id: 169,  // 12:00 - 14:00
            start5Id: 193, stop5Id: 217); // 16:00 - 18:00

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9403, DateFrom = date, DateTo = date,
        });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var rows = ReadSheetRows(doc.WorkbookPart!, "Dashboard");
            var header = rows.First();

            var expected = new[]
            {
                DaShift3Start, DaShift3Stop, DaShift3Pause,
                DaShift4Start, DaShift4Stop, DaShift4Pause,
                DaShift5Start, DaShift5Stop, DaShift5Pause
            };
            Assert.That(header.Skip(14).Take(9), Is.EqualTo(expected),
                "Shift 3, 4 and 5 triples must be CONTIGUOUS at header indices 14..22");
            Assert.That(header[23], Is.EqualTo(DaNettoHours),
                "NettoHours must come DIRECTLY after the Shift 5 triple");
            Assert.That(header.Count, Is.EqualTo(30));

            // Every shift's data value lands at its own header's index.
            var dataRow = rows[1];
            Assert.That(dataRow[14], Is.EqualTo("08:00"), "Shift 3 start");
            Assert.That(dataRow[17], Is.EqualTo("12:00"), "Shift 4 start");
            Assert.That(dataRow[20], Is.EqualTo("16:00"), "Shift 5 start");
            Assert.That(double.Parse(dataRow[23], CultureInfo.InvariantCulture),
                Is.EqualTo(4.0).Within(1e-9), "NettoHours under the NettoHours header");

            AssertWidthInvariant(rows, "Dashboard");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 4. Asymmetric combo: ONLY the fifth shift enabled — the conditional
    //    blocks are independent, so the Shift5 triple takes indices 14..16.
    // ------------------------------------------------------------------

    [Test]
    public async Task Dashboard_FifthShiftOnlyWithoutThirdFourth()
    {
        var date = ExportDate();
        await SeedSiteAndPlanRegistration(siteUid: 9404, employeeNo: "1", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 2.0,
            fifthShiftActive: true,
            start5Id: 193, stop5Id: 217); // 16:00 - 18:00

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9404, DateFrom = date, DateTo = date,
        });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var rows = ReadSheetRows(doc.WorkbookPart!, "Dashboard");
            var header = rows.First();

            Assert.That(header[13], Is.EqualTo(DaShift2Pause));
            Assert.That(header[14], Is.EqualTo(DaShift5Start),
                "With only FifthShiftActive, the Shift 5 triple must sit right after Shift 2 pause");
            Assert.That(header[15], Is.EqualTo(DaShift5Stop));
            Assert.That(header[16], Is.EqualTo(DaShift5Pause));
            Assert.That(header[17], Is.EqualTo(DaNettoHours));
            Assert.That(header.Count, Is.EqualTo(24));
            Assert.That(header, Does.Not.Contain(DaShift3Start),
                "Shift 3 columns must NOT appear when only the fifth shift is enabled");
            Assert.That(header, Does.Not.Contain(DaShift4Start));

            var dataRow = rows[1];
            Assert.That(dataRow[14], Is.EqualTo("16:00"), "Shift 5 start value at its header index");

            AssertWidthInvariant(rows, "Dashboard");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 5. All-workers: the shift flags are GLOBAL (AssignedSites.Any), so one
    //    flagged site widens EVERY per-site sheet; the unflagged site's shift-3
    //    cells are empty but its NettoHours still aligns under its header.
    // ------------------------------------------------------------------

    [Test]
    public async Task AllWorkers_GlobalFlagWidensAllSiteSheets()
    {
        var date = ExportDate();
        // Site A: ThirdShiftActive + shift-3 data. Site B: no flags, no shift-3 data.
        await SeedSiteAndPlanRegistration(siteUid: 9405, employeeNo: "1", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 2.0,
            thirdShiftActive: true,
            start3Id: 97, stop3Id: 121);
        await SeedSiteAndPlanRegistration(siteUid: 9406, employeeNo: "2", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 3.0);

        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = date, DateTo = date,
            });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var workbookPart = doc.WorkbookPart!;

            foreach (var sheetName in new[] { "Site 9405", "Site 9406" })
            {
                var rows = ReadSheetRows(workbookPart, sheetName);
                var header = rows.First();
                Assert.That(header[13], Is.EqualTo(DaShift2Pause), sheetName);
                Assert.That(header[14], Is.EqualTo(DaShift3Start),
                    $"{sheetName}: one globally flagged site must widen ALL per-site sheets");
                Assert.That(header[15], Is.EqualTo(DaShift3Stop), sheetName);
                Assert.That(header[16], Is.EqualTo(DaShift3Pause), sheetName);
                Assert.That(header[17], Is.EqualTo(DaNettoHours), sheetName);
                AssertWidthInvariant(rows, sheetName);
            }

            // Site A's shift-3 data lands under the shift-3 headers.
            var siteARows = ReadSheetRows(workbookPart, "Site 9405");
            Assert.That(siteARows[1][14], Is.EqualTo("08:00"));

            // Site B has no shift-3 data: its cells are empty, NettoHours still aligned.
            var siteBRows = ReadSheetRows(workbookPart, "Site 9406");
            Assert.That(siteBRows[1][14], Is.EqualTo(string.Empty),
                "Site without shift-3 data must render empty shift-3 cells, not shifted columns");
            Assert.That(siteBRows[1][15], Is.EqualTo(string.Empty));
            Assert.That(siteBRows[1][16], Is.EqualTo(string.Empty));
            Assert.That(double.Parse(siteBRows[1][17], CultureInfo.InvariantCulture),
                Is.EqualTo(3.0).Within(1e-9),
                "Site B's NettoHours value must still sit under the NettoHours header");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 6. Total sheet: one column per seed message — the new Id 13
    //    "PregnancyLeave" column carries the netto-hours sum of the rows with
    //    MessageId 13, honoring the NettoHoursOverrideActive split
    //    (override-active rows contribute NettoHoursOverride, not NettoHours).
    // ------------------------------------------------------------------

    [Test]
    public async Task AllWorkers_TotalSheet_HasPregnancyLeaveColumn()
    {
        var date = ExportDate();
        // Day 1: MessageId 13, NettoHours 7.5 (no override).
        await SeedSiteAndPlanRegistration(siteUid: 9407, employeeNo: "1", date: date,
            start1Id: 97, stop1Id: 121, nettoHours: 7.5, messageId: 13);
        // Day 2: MessageId 13 with the override ACTIVE — the column must sum the
        // override value (1.5), not the raw NettoHours (99).
        await new PlanRegistrationEntity
        {
            SdkSitId = 9407,
            Date = date.AddDays(1),
            Start1Id = 97,
            Stop1Id = 121,
            Pause1Id = 0,
            NettoHours = 99,
            NettoHoursOverride = 1.5,
            NettoHoursOverrideActive = true,
            MessageId = 13,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = date, DateTo = date.AddDays(1),
            });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var rows = ReadSheetRows(doc.WorkbookPart!, "Total");
            var header = rows.First();

            var columnIndex = header.IndexOf(DaPregnancyLeave);
            Assert.That(columnIndex, Is.GreaterThanOrEqualTo(13),
                "The seed DaName 'Graviditetsbetinget fravær' must have a Total-sheet column, " +
                "positioned among the per-message columns after the 13 fixed (and any pay-code) columns");
            Assert.That(header.Count(h => h == DaPregnancyLeave), Is.EqualTo(1),
                "Exactly one PregnancyLeave column");

            // One data row per site; width must match the header so the message
            // columns stay aligned.
            var dataRow = rows[1];
            Assert.That(dataRow.Count, Is.EqualTo(header.Count),
                "Total-sheet data row must be exactly as wide as the header");
            Assert.That(double.Parse(dataRow[columnIndex], CultureInfo.InvariantCulture),
                Is.EqualTo(7.5 + 1.5).Within(1e-9),
                "PregnancyLeave column must sum NettoHours (7.5) + NettoHoursOverride (1.5) " +
                "of the MessageId 13 rows");
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
    /// A midnight date safely inside every window the code under test cares
    /// about: relative to now (never hardcoded). No forward horizon applies
    /// to the export's Index() path; 10 days out simply avoids the
    /// "today is locked" special case.
    /// </summary>
    private static DateTime ExportDate() => DateTime.Now.Date.AddDays(10);

    /// <summary>
    /// Permanent lock against column drift: on the positional sheets the
    /// header row, EVERY data row and the totals row must have exactly the
    /// same cell count (no pay-rule-set is linked in this fixture, so there
    /// are no trailing pay-code cells and the counts must match exactly).
    /// The totals row is the LAST row; its first cell carries the "Total"
    /// label (da resx PayRuleSetTotalRow).
    /// </summary>
    private static void AssertWidthInvariant(List<List<string>> rows, string sheetName)
    {
        var header = rows.First();
        Assert.That(rows.Count, Is.GreaterThanOrEqualTo(3),
            $"{sheetName}: expected header + at least one data row + totals row");
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.That(rows[i].Count, Is.EqualTo(header.Count),
                $"{sheetName}: row {i + 1} must be exactly as wide as the header " +
                "(header == data rows == totals row)");
        }
        Assert.That(rows.Last().First(), Is.EqualTo("Total"),
            $"{sheetName}: the last row must be the totals row (first cell 'Total')");
    }

    /// <summary>
    /// Returns every row of the named positional sheet as plain cell text,
    /// ordered by row index. Cells on these sheets carry no CellReference, so
    /// list index i lines up with header index i on every row.
    /// </summary>
    private static List<List<string>> ReadSheetRows(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        return part.Worksheet.Descendants<Row>()
            .OrderBy(r => r.RowIndex!.Value)
            .Select(r => r.Elements<Cell>().Select(c => CellText(c, workbookPart)).ToList())
            .ToList();
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
    /// Seeds one SDK Site/Worker/SiteWorker + an AssignedSite (with the shift
    /// 3/4/5 flags) + a prior-day PlanRegistration + the in-range registration.
    /// The prior-day row is MANDATORY: Index() carries it in as the synthesized
    /// "prePlanning" row that the export drops via Skip(1) — without it the
    /// export would drop the actual first data day instead. Mirrors the helper
    /// in DagsoversigtWorksheetExportTests plus the shift 3-5 slot ids
    /// (PlanRegistration.Start3Id/Stop3Id/Pause3Id ... Start5Id/Stop5Id/Pause5Id,
    /// 1-based 5-minute slots: 97 = 08:00, 121 = 10:00).
    /// </summary>
    private async Task SeedSiteAndPlanRegistration(
        int siteUid, string employeeNo, DateTime date,
        int start1Id, int stop1Id, double nettoHours,
        bool thirdShiftActive = false, bool fourthShiftActive = false, bool fifthShiftActive = false,
        int start3Id = 0, int stop3Id = 0, int pause3Id = 0,
        int start4Id = 0, int stop4Id = 0, int pause4Id = 0,
        int start5Id = 0, int stop5Id = 0, int pause5Id = 0,
        int? messageId = null)
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
            ThirdShiftActive = thirdShiftActive,
            FourthShiftActive = fourthShiftActive,
            FifthShiftActive = fifthShiftActive,
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
            Pause1Id = 0,
            Start3Id = start3Id,
            Stop3Id = stop3Id,
            Pause3Id = pause3Id,
            Start4Id = start4Id,
            Stop4Id = stop4Id,
            Pause4Id = pause4Id,
            Start5Id = start5Id,
            Stop5Id = stop5Id,
            Pause5Id = pause5Id,
            NettoHours = nettoHours,
            MessageId = messageId,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }
}
