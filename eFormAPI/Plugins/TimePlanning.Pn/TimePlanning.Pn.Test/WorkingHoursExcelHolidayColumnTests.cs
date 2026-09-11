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
/// Cell-level coverage of the all-workers export's <c>Total</c> sheet columns
/// J ("Søn- og helligdagstimer") and the new K ("Helligdagstimer"), read back
/// out of the REAL produced .xlsx with OpenXml.
///
/// Column J counts Sundays plus every entry in danish_holidays_2025_2030.json
/// (Grundlovsdag noon-split, Juleaften included). Column K counts ONLY days
/// whose JSON category is <c>official_holiday</c> — no Sundays, no
/// Grundlovsdag, no Juleaften, and no noon split.
///
/// Both columns select each day's hours the same way column G
/// (<c>siteTotalNettoHours</c>) does: <c>NettoHoursOverrideActive ?
/// NettoHoursOverride : NettoHours</c>. Before that fix column J summed the
/// bare <c>NettoHours</c>, so an overridden Sunday made column I ("Normal
/// timer") and column J disagree with column G.
/// </summary>
[TestFixture]
public class WorkingHoursExcelHolidayColumnTests : TestBaseSetup
{
    // Danish resx values (Resources/Translations.da.resx) — the export switches
    // CurrentUICulture to "da", so these are the literal header strings.
    private const string DaFrom = "Fra";
    private const string DaTo = "Til";
    private const string DaEmployeeNo = "Medarbejder nr.";
    private const string DaWorker = "Medarbejder";
    private const string DaTags = "Etiketter";
    private const string DaPlanHours = "Plan timer";
    private const string DaNettoHours = "Timer netto";
    private const string DaSumFlexStart = "Flex sum";
    private const string DaNormalHours = "Normal timer";
    private const string DaHoursSunday = "Søn- og helligdagstimer";
    private const string DaHoursHoliday = "Helligdagstimer";
    private const string DaComments = "Fra medarbejder";
    private const string DaMessage = "Melding";
    private const string DaHoursSaturday = "Timer lørdag";

    // Total-sheet fixed column indices (0-based; index 10 == spreadsheet column K).
    private const int ColGNettoHours = 6;
    private const int ColINormalHours = 8;
    private const int ColJSundayAndHoliday = 9;
    private const int ColKHolidayHours = 10;

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
        // the da values ("Helligdagstimer") in the produced headers.
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
    // 1. Column ordering: the fixed prefix of the Total sheet, so a future
    //    insertion cannot silently shift "Helligdagstimer" off column K.
    // ------------------------------------------------------------------

    [Test]
    public async Task TotalSheet_FixedHeaderOrder_PutsHelligdagstimerInColumnK()
    {
        await SeedSite(siteUid: 9801, employeeNo: "1", priorDay: new DateTime(2026, 12, 18));
        await SeedDay(9801, new DateTime(2026, 12, 21), nettoHours: 8.0);

        var header = await ExportTotalHeader(new DateTime(2026, 12, 19), new DateTime(2026, 12, 26));

        var expectedFixedHeaders = new[]
        {
            DaFrom, DaTo, DaEmployeeNo, DaWorker, DaTags, DaPlanHours, DaNettoHours,
            DaSumFlexStart, DaNormalHours, DaHoursSunday, DaHoursHoliday, DaComments,
            DaMessage, DaHoursSaturday
        };

        Assert.That(header.Take(expectedFixedHeaders.Length), Is.EqualTo(expectedFixedHeaders),
            "The 14 fixed Total-sheet headers must keep exactly this order; the dynamic " +
            "pay-code and seed-message columns follow them");
        Assert.That(header[ColKHolidayHours], Is.EqualTo(DaHoursHoliday),
            "'Helligdagstimer' must sit at 0-based index 10, i.e. spreadsheet column K");
        Assert.That(header[ColJSundayAndHoliday], Is.EqualTo(DaHoursSunday),
            "...immediately after 'Søn- og helligdagstimer' in column J");
        Assert.That(header.Count(h => h == DaHoursHoliday), Is.EqualTo(1),
            "Exactly one Helligdagstimer column");
    }

    // ------------------------------------------------------------------
    // 2. Real workbook: column K's value for seeded data, and the K <= J
    //    subset relation exercised non-trivially — the window carries a
    //    Sunday, two statutory holidays, Grundlovsdag AND Juleaften, so
    //    column J counts strictly more than column K.
    // ------------------------------------------------------------------

    [Test]
    public async Task TotalSheet_ColumnK_SumsOnlyStatutoryHolidayHours_AndIsStrictlyLessThanColumnJInThisWindow()
    {
        await SeedSite(siteUid: 9802, employeeNo: "1", priorDay: new DateTime(2026, 6, 4));

        // Grundlovsdag (Friday), overenskomstfastsat_fridag: column J takes only
        // the hours after 12:00 (08:00-16:00 -> 4.0); column K excludes it entirely.
        await SeedDay(9802, new DateTime(2026, 6, 5), nettoHours: 8.0, start1Id: 97, stop1Id: 193);
        // Plain Sunday: column J counts it, column K does not.
        await SeedDay(9802, new DateTime(2026, 9, 13), nettoHours: 5.0);
        // Plain Monday: neither column counts it.
        await SeedDay(9802, new DateTime(2026, 9, 14), nettoHours: 7.0);
        // Juleaften (Thursday), overenskomstfastsat_fridag: column J counts it, K does not.
        await SeedDay(9802, new DateTime(2026, 12, 24), nettoHours: 4.0);
        // Juledag (Friday), official_holiday: BOTH columns count it.
        await SeedDay(9802, new DateTime(2026, 12, 25), nettoHours: 6.0);
        // 2. juledag (Saturday), official_holiday: BOTH columns count it.
        await SeedDay(9802, new DateTime(2026, 12, 26), nettoHours: 3.0);

        var row = await ExportTotalDataRow(new DateTime(2026, 6, 5), new DateTime(2026, 12, 27));

        var colG = Number(row[ColGNettoHours]);
        var colI = Number(row[ColINormalHours]);
        var colJ = Number(row[ColJSundayAndHoliday]);
        var colK = Number(row[ColKHolidayHours]);

        Assert.That(colG, Is.EqualTo(33.0).Within(1e-9),
            "Column G is every day's netto: 8 + 5 + 7 + 4 + 6 + 3");
        Assert.That(colJ, Is.EqualTo(22.0).Within(1e-9),
            "Column J: Grundlovsdag after noon (4) + Sunday (5) + Juleaften (4) + Juledag (6) + 2. juledag (3)");
        Assert.That(colK, Is.EqualTo(9.0).Within(1e-9),
            "Column K: ONLY the official_holiday days — Juledag (6) + 2. juledag (3). " +
            "No Sunday, no Grundlovsdag, no Juleaften, and no noon split");
        Assert.That(colI, Is.EqualTo(11.0).Within(1e-9),
            "Column I is G - J");

        // NOT a general invariant: K <= J holds for every date in the calendar
        // EXCEPT 2028-06-05, where Grundlovsdag collides with 2. pinsedag. There
        // column K takes the full netto while column J short-circuits to the
        // Grundlovsdag noon split, so K > J. See
        // PlanRegistrationHelperHolidayTests.IsStatutoryHoliday_Grundlovsdag2028_CollidesWithSecondWhitMonday_AndIsStatutory.
        // This window is in 2026, so the strict relation is expected here.
        Assert.That(colK, Is.LessThan(colJ),
            "This window carries a Sunday, Grundlovsdag and Juleaften, so column J counts " +
            "strictly more than column K — the numbers above are not agreeing by accident");
    }

    // ------------------------------------------------------------------
    // 3. The netto-override fix: an overridden Sunday and an overridden
    //    statutory holiday must contribute their OVERRIDE to columns J and K,
    //    exactly as they already do to column G. Before the fix both loops
    //    read the bare NettoHours, so column J reported 149 here.
    // ------------------------------------------------------------------

    [Test]
    public async Task TotalSheet_OverriddenSundayAndHoliday_UseTheOverrideInColumnsJAndK()
    {
        await SeedSite(siteUid: 9803, employeeNo: "1", priorDay: new DateTime(2026, 12, 18));

        // Sunday with the override ACTIVE: 1.5 counts, the raw 99 must not.
        await SeedDay(9803, new DateTime(2026, 12, 20), nettoHours: 99,
            nettoHoursOverride: 1.5, nettoHoursOverrideActive: true);
        // Plain Monday, no override.
        await SeedDay(9803, new DateTime(2026, 12, 21), nettoHours: 8.0);
        // Juledag with the override ACTIVE: 2.0 counts, the raw 50 must not.
        await SeedDay(9803, new DateTime(2026, 12, 25), nettoHours: 50,
            nettoHoursOverride: 2.0, nettoHoursOverrideActive: true);

        var row = await ExportTotalDataRow(new DateTime(2026, 12, 19), new DateTime(2026, 12, 26));

        var colG = Number(row[ColGNettoHours]);
        var colI = Number(row[ColINormalHours]);
        var colJ = Number(row[ColJSundayAndHoliday]);
        var colK = Number(row[ColKHolidayHours]);

        Assert.That(colG, Is.EqualTo(11.5).Within(1e-9),
            "Column G already honours the override: 1.5 + 8.0 + 2.0");
        Assert.That(colJ, Is.EqualTo(3.5).Within(1e-9),
            "Column J must sum the OVERRIDE on both overridden days (1.5 + 2.0), not the raw 99 + 50");
        Assert.That(colK, Is.EqualTo(2.0).Within(1e-9),
            "Column K must sum the override on the overridden statutory holiday (2.0), not the raw 50");
        Assert.That(colI, Is.EqualTo(8.0).Within(1e-9),
            "Column I is the plain Monday only — it must not go negative because column J over-counted");
        Assert.That(colI + colJ, Is.EqualTo(colG).Within(1e-9),
            "Normal hours + Sunday/holiday hours must add back up to the site's netto total");
    }

    // ------------------------------------------------------------------
    // 4. A site with no statutory-holiday hours still gets a numeric 0 in
    //    column K, so the column never shifts the ones after it.
    // ------------------------------------------------------------------

    [Test]
    public async Task TotalSheet_NoStatutoryHolidayInWindow_ColumnKIsZero_AndRowWidthMatchesHeader()
    {
        await SeedSite(siteUid: 9804, employeeNo: "1", priorDay: new DateTime(2026, 12, 18));
        await SeedDay(9804, new DateTime(2026, 12, 20), nettoHours: 5.0); // Sunday
        await SeedDay(9804, new DateTime(2026, 12, 21), nettoHours: 8.0); // Monday

        var rows = await ExportTotalRows(new DateTime(2026, 12, 19), new DateTime(2026, 12, 23));
        var header = rows.First();
        var row = rows[1];

        Assert.That(header[ColKHolidayHours], Is.EqualTo(DaHoursHoliday),
            "Index 10 must be the Helligdagstimer column on this sheet too");
        Assert.That(row.Count, Is.EqualTo(header.Count),
            "The Total-sheet data row must stay exactly as wide as the header");
        Assert.That(Number(row[ColJSundayAndHoliday]), Is.EqualTo(5.0).Within(1e-9));
        Assert.That(Number(row[ColKHolidayHours]), Is.EqualTo(0.0).Within(1e-9),
            "No official_holiday day in the window: column K is a numeric 0, not an empty cell");
        Assert.That(row[ColKHolidayHours], Is.Not.Empty,
            "Column K must always be written, otherwise every later column shifts left");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static double Number(string cell) =>
        double.Parse(cell, NumberStyles.Any, CultureInfo.InvariantCulture);

    private async Task<List<string>> ExportTotalHeader(DateTime from, DateTime to) =>
        (await ExportTotalRows(from, to)).First();

    private async Task<List<string>> ExportTotalDataRow(DateTime from, DateTime to) =>
        (await ExportTotalRows(from, to))[1];

    private async Task<List<List<string>>> ExportTotalRows(DateTime from, DateTime to)
    {
        var result = await _service.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = from, DateTo = to,
            });
        Assert.That(result.Success, Is.True, result.Message);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            return ReadSheetRows(doc.WorkbookPart!, "Total");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
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
    /// Seeds one SDK Site/Worker/SiteWorker + an AssignedSite + the MANDATORY
    /// prior-day PlanRegistration that Index() carries in as the synthesized
    /// "prePlanning" row and the export then drops via Skip(1) — without it the
    /// export would drop the first real data day instead.
    /// </summary>
    private async Task SeedSite(int siteUid, string employeeNo, DateTime priorDay)
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

        await new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2000 + siteUid,
        }.Create(sdkDb);

        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            Resigned = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await SeedDay(siteUid, priorDay, nettoHours: 0);
    }

    /// <summary>
    /// One PlanRegistration. Shift-1 slot ids are 1-based 5-minute slots
    /// (97 = 08:00, 145 = 12:00, 193 = 16:00); they matter only for the
    /// Grundlovsdag noon split, which reads the shift stamps.
    /// </summary>
    private async Task SeedDay(int siteUid, DateTime date, double nettoHours,
        int start1Id = 0, int stop1Id = 0,
        double nettoHoursOverride = 0, bool nettoHoursOverrideActive = false)
    {
        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date,
            Start1Id = start1Id,
            Stop1Id = stop1Id,
            Pause1Id = 0,
            NettoHours = nettoHours,
            NettoHoursOverride = nettoHoursOverride,
            NettoHoursOverrideActive = nettoHoursOverrideActive,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }
}
