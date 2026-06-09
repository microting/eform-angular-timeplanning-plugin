using System;
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

        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
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
    /// 2=WeekDay, 3=Date, 4=WeekNumber, 5=PlanText, 6=PlanHours, 7=Shift1Start,
    /// 8=Shift1Stop, 9=Shift1Pause. <c>CreateCell</c> doesn't set
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
            if (cells.Count < 9) continue;
            var shift1Start = CellText(cells[7]);
            var shift1Stop  = CellText(cells[8]);
            if (!string.IsNullOrEmpty(shift1Start) || !string.IsNullOrEmpty(shift1Stop))
            {
                return (shift1Start, shift1Stop);
            }
        }
        return ("", "");
    }
}
