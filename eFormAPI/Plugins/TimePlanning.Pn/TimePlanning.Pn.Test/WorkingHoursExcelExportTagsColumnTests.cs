using System;
using System.Collections.Generic;
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
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteTag = Microting.eForm.Infrastructure.Data.Entities.SiteTag;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkTag = Microting.eForm.Infrastructure.Data.Entities.Tag;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Coverage for the "Tags" (da: "Etiketter") column added to every sheet of the
/// working-hours Excel exports, placed immediately after the worker/site name
/// column. The cell value is the site's SDK tag names sorted alphabetically
/// (ordinal, case-insensitive) and joined with ", "; untagged sites get an
/// empty cell; removed <c>SiteTags</c> rows are excluded. Tests cover the
/// tag-map helper directly (internal, via InternalsVisibleTo) and the produced
/// workbooks at cell level via OpenXml — the Total sheet, a per-site tab, the
/// Dashboard sheet and the per-day "Dagsoversigt" sheet.
/// </summary>
[TestFixture]
public class WorkingHoursExcelExportTagsColumnTests : TestBaseSetup
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

        // Danish user language so Translations.X resolves deterministically to
        // the da values ("Etiketter", "Medarbejder") in the produced headers.
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
    // 1. Tag-map helper: sorted comma-join, removed rows excluded,
    //    untagged sites absent.
    // ------------------------------------------------------------------

    [Test]
    public async Task GetSiteTagNames_SortsCaseInsensitively_ExcludesRemoved_OmitsUntagged()
    {
        var core = await GetCore();
        var sdkDb = core.DbContextHelper.GetDbContext();

        var taggedSite = new SdkSite { Name = "Site 9601", MicrotingUid = 9601 };
        await taggedSite.Create(sdkDb);
        var untaggedSite = new SdkSite { Name = "Site 9602", MicrotingUid = 9602 };
        await untaggedSite.Create(sdkDb);

        // "alpha" + "Beta": ordinal-case-SENSITIVE order would be "Beta, alpha"
        // ('B' < 'a'); the required ordinal-case-INSENSITIVE order is "alpha, Beta".
        await TagSite(sdkDb, taggedSite, "Beta");
        await TagSite(sdkDb, taggedSite, "alpha");

        // A removed SiteTag row must be excluded even though the Tag itself lives on.
        var removedSiteTag = await TagSite(sdkDb, taggedSite, "Zulu");
        await removedSiteTag.Delete(sdkDb);

        var map = await TimePlanningWorkingHoursService.GetSiteTagNames(
            sdkDb, new[] { 9601, 9602 });

        Assert.That(map.ContainsKey(9601), Is.True);
        Assert.That(map[9601], Is.EqualTo("alpha, Beta"),
            "Tag names must be sorted ordinal-case-insensitively and joined with \", \"; removed SiteTags excluded");
        Assert.That(map.ContainsKey(9602), Is.False, "Untagged sites must be absent from the map");
    }

    // ------------------------------------------------------------------
    // 2. All-workers export: Total sheet + per-site tab carry the Tags
    //    column immediately after the worker/site name column.
    // ------------------------------------------------------------------

    [Test]
    public async Task AllWorkersExport_TotalAndPerSiteSheets_TagsColumnAfterNameColumn()
    {
        var date = new DateTime(2026, 7, 15);
        await SeedSiteAndPlanRegistration(siteUid: 9611, employeeNo: "1", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9612, employeeNo: "2", date: date);
        await TagSiteByUid(9611, "EL");
        await TagSiteByUid(9611, "Brand");
        // Site 9612 stays untagged.

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

            // --- Total sheet: header row (positional CreateCell layout:
            // 0=From, 1=To, 2=Employee no, 3=Worker, 4=Tags, 5=PlanHours...) ---
            var totalRows = SheetRows(workbookPart, "Total");
            var totalHeader = totalRows.First(r => r.RowIndex! == 1U).Elements<Cell>().ToList();
            Assert.That(CellText(totalHeader[3], workbookPart), Is.EqualTo("Medarbejder"));
            Assert.That(CellText(totalHeader[4], workbookPart), Is.EqualTo("Etiketter"),
                "Total sheet: Tags header must sit immediately after the Worker header");

            // --- Total sheet: one row per site; Tags value follows the site name ---
            var totalDataRows = totalRows.Where(r => r.RowIndex! > 1U)
                .Select(r => r.Elements<Cell>().ToList())
                .ToList();
            var taggedRow = totalDataRows.Single(c => CellText(c[3], workbookPart) == "Site 9611");
            var untaggedRow = totalDataRows.Single(c => CellText(c[3], workbookPart) == "Site 9612");
            Assert.That(CellText(taggedRow[4], workbookPart), Is.EqualTo("Brand, EL"),
                "Tagged site's Total row must carry the sorted, comma-joined tag names");
            Assert.That(CellText(untaggedRow[4], workbookPart), Is.EqualTo(string.Empty),
                "Untagged site's Total row must have an empty Tags cell");

            // --- Per-site tab (FillDataRow layout: 0=Employee no, 1=Worker, 2=Tags) ---
            var siteRows = SheetRows(workbookPart, "Site 9611");
            var siteHeader = siteRows.First(r => r.RowIndex! == 1U).Elements<Cell>().ToList();
            Assert.That(CellText(siteHeader[1], workbookPart), Is.EqualTo("Medarbejder"));
            Assert.That(CellText(siteHeader[2], workbookPart), Is.EqualTo("Etiketter"),
                "Per-site tab: Tags header must sit immediately after the Worker header");
            var siteDataCells = siteRows.First(r => r.RowIndex! == 2U).Elements<Cell>().ToList();
            Assert.That(CellText(siteDataCells[2], workbookPart), Is.EqualTo("Brand, EL"));

            var untaggedSiteRows = SheetRows(workbookPart, "Site 9612");
            var untaggedSiteData = untaggedSiteRows.First(r => r.RowIndex! == 2U).Elements<Cell>().ToList();
            Assert.That(CellText(untaggedSiteData[2], workbookPart), Is.EqualTo(string.Empty),
                "Untagged site's per-site tab rows must have an empty Tags cell");
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 3. Single-site export: Dashboard sheet + per-day "Dagsoversigt" sheet;
    //    the Tags value repeats on every row of the same site.
    // ------------------------------------------------------------------

    [Test]
    public async Task SingleSiteExport_DashboardAndDayOverview_TagsColumnAfterNameColumn()
    {
        var dateA = new DateTime(2026, 7, 16);
        var dateB = new DateTime(2026, 7, 17);
        await SeedSiteAndPlanRegistration(siteUid: 9621, employeeNo: "1", date: dateA, extraDate: dateB);
        await TagSiteByUid(9621, "EL");
        await TagSiteByUid(9621, "Brand");

        var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 9621,
            DateFrom = dateA,
            DateTo = dateB,
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            var workbookPart = doc.WorkbookPart!;

            // --- Dashboard sheet (FillDataRow layout: 0=Employee no, 1=Worker, 2=Tags) ---
            var dashboardRows = SheetRows(workbookPart, "Dashboard");
            var dashboardHeader = dashboardRows.First(r => r.RowIndex! == 1U).Elements<Cell>().ToList();
            Assert.That(CellText(dashboardHeader[1], workbookPart), Is.EqualTo("Medarbejder"));
            Assert.That(CellText(dashboardHeader[2], workbookPart), Is.EqualTo("Etiketter"),
                "Dashboard: Tags header must sit immediately after the Worker header");
            var dashboardData = dashboardRows.First(r => r.RowIndex! == 2U).Elements<Cell>().ToList();
            Assert.That(CellText(dashboardData[2], workbookPart), Is.EqualTo("Brand, EL"));

            // --- Dagsoversigt sheet (cell-referenced layout: A=Employee no,
            // B=Worker, C=Tags); the value repeats on every data row ---
            var dayRows = SheetRows(workbookPart, "Dagsoversigt");
            var dayHeader = dayRows.First(r => r.RowIndex! == 1U).Elements<Cell>().ToList();
            Assert.That(CellText(dayHeader[1], workbookPart), Is.EqualTo("Medarbejder"));
            Assert.That(CellText(dayHeader[2], workbookPart), Is.EqualTo("Etiketter"),
                "Dagsoversigt: Tags header must sit immediately after the Worker header");

            foreach (var rowIdx in new[] { 2U, 3U })
            {
                var row = dayRows.First(r => r.RowIndex! == rowIdx);
                var tagsCell = row.Elements<Cell>().Single(c => c.CellReference == $"C{rowIdx}");
                Assert.That(CellText(tagsCell, workbookPart), Is.EqualTo("Brand, EL"),
                    $"Dagsoversigt row {rowIdx}: Tags value must repeat on every row of the site");
            }
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Creates a Tag named <paramref name="tagName"/> and links it to
    /// <paramref name="site"/> via a SiteTag row; returns the SiteTag so the
    /// caller can soft-delete it.</summary>
    private static async Task<SdkSiteTag> TagSite(
        Microting.eForm.Infrastructure.MicrotingDbContext sdkDb, SdkSite site, string tagName)
    {
        var tag = new SdkTag { Name = tagName };
        await tag.Create(sdkDb);
        var siteTag = new SdkSiteTag { SiteId = site.Id, TagId = tag.Id };
        await siteTag.Create(sdkDb);
        return siteTag;
    }

    private async Task TagSiteByUid(int siteUid, string tagName)
    {
        var core = await GetCore();
        var sdkDb = core.DbContextHelper.GetDbContext();
        var site = await sdkDb.Sites.FirstAsync(x => x.MicrotingUid == siteUid);
        await TagSite(sdkDb, site, tagName);
    }

    /// <summary>Seeds SDK Site/Worker/SiteWorker + AssignedSite + a prior-day
    /// registration (dropped via the export's Skip(1)) + a registration on
    /// <paramref name="date"/> (and optionally <paramref name="extraDate"/>).
    /// Mirrors the seeding helpers of the other export test fixtures.</summary>
    private async Task SeedSiteAndPlanRegistration(
        int siteUid, string employeeNo, DateTime date, DateTime? extraDate = null)
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

        var dates = new List<DateTime> { date };
        if (extraDate.HasValue)
        {
            dates.Add(extraDate.Value);
        }

        // Prior-day registration so Index() emits a "prePlanning" row at index 0
        // that the export drops via Skip(1).
        foreach (var d in new[] { date.AddDays(-1) }.Concat(dates))
        {
            await new PlanRegistrationEntity
            {
                SdkSitId = siteUid,
                Date = d,
                Start1Id = d == date.AddDays(-1) ? 0 : 97,
                Stop1Id = d == date.AddDays(-1) ? 0 : 121,
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

    private static List<Row> SheetRows(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        return part.Worksheet.Descendants<Row>().ToList();
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
}
