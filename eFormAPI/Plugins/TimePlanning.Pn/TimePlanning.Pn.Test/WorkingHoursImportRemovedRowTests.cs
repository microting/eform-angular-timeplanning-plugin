using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for TimePlanningWorkingHoursService.Import matching a
/// soft-removed PlanRegistration. The lookup used SingleOrDefaultAsync with no
/// WorkflowState filter, so:
///   (1) a Removed row could be picked as the import target, and
///   (2) a Removed+Active pair for the same date/site threw
///       InvalidOperationException (SingleOrDefault on 2 rows), crashing Import.
/// The fix filters out Removed rows and switches to FirstOrDefaultAsync.
///
/// Structured to FAIL pre-fix (SingleOrDefault throws on the pair -> Success
/// false) and PASS post-fix (active row updated, removed row untouched).
/// </summary>
[TestFixture]
public class WorkingHoursImportRemovedRowTests : TestBaseSetup
{
    private ITimePlanningWorkingHoursService _service;
    private IEFormCoreService _coreService;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings());

        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext,
            Substitute.For<IUserService>(),
            localizationService,
            Substitute.For<BaseDbContext>(new DbContextOptions<BaseDbContext>()),
            options,
            _coreService);
    }

    [Test]
    public async Task Import_SkipsRemovedRow_AndDoesNotCrashOnRemovedActivePair()
    {
        const int microtingUid = 888;
        const string siteName = "ImportSite";
        // A date safely inside Import's accepted window (now-1 .. now+180).
        var importDate = DateTime.Now.AddDays(5).Date;
        var dateStr = importDate.ToString("dd.MM.yyyy");

        // SDK site whose Name matches the worksheet name.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = new SdkSite { Name = siteName, MicrotingUid = microtingUid };
        await site.Create(sdkDbContext);

        // Active row + a Removed row for the same date/site -> the pre-fix
        // SingleOrDefaultAsync would throw on this pair.
        var active = new PlanRegistration
        {
            SdkSitId = microtingUid,
            Date = importDate,
            PlanText = "ACTIVE-ORIG",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await active.Create(TimePlanningPnDbContext);
        var activeId = active.Id;

        var removed = new PlanRegistration
        {
            SdkSitId = microtingUid,
            Date = importDate,
            PlanText = "REMOVED-ORIG",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removed.Create(TimePlanningPnDbContext);
        await removed.Delete(TimePlanningPnDbContext); // WorkflowState -> Removed
        var removedId = removed.Id;

        var xlsx = BuildWorkbook(siteName, dateStr, "8", "IMPORTED");
        var file = Substitute.For<IFormFile>();
        file.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var target = ci.Arg<Stream>();
                target.Write(xlsx, 0, xlsx.Length);
                return Task.CompletedTask;
            });

        var result = await _service.Import(file);

        // Post-fix: no crash, and the ACTIVE row is the import target.
        Assert.That(result.Success, Is.True, result.Message);

        var reloadedActive = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == activeId);
        Assert.That(reloadedActive.PlanText, Is.EqualTo("IMPORTED"),
            "Import must update the active row");

        var reloadedRemoved = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == removedId);
        Assert.That(reloadedRemoved.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        Assert.That(reloadedRemoved.PlanText, Is.EqualTo("REMOVED-ORIG"),
            "Import must not write into the removed row");

        var activeCount = await TimePlanningPnDbContext.PlanRegistrations
            .CountAsync(x => x.SdkSitId == microtingUid && x.Date == importDate
                             && x.WorkflowState != Constants.WorkflowStates.Removed);
        Assert.That(activeCount, Is.EqualTo(1), "Import must not create a duplicate active row");
    }

    private static Cell TextCell(string reference, string value) => new Cell
    {
        CellReference = reference,
        DataType = CellValues.String,
        CellValue = new CellValue(value)
    };

    private static Cell NumberCell(string reference, string value) => new Cell
    {
        CellReference = reference,
        CellValue = new CellValue(value)
    };

    private static byte[] BuildWorkbook(string sheetName, string dateStr, string hours, string text)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var wsPart = wbPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            wsPart.Worksheet = new Worksheet(sheetData);

            var sheets = wbPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = wbPart.GetIdOfPart(wsPart),
                SheetId = 1,
                Name = sheetName
            });

            // Header row (RowIndex 1) is skipped by Import.
            var header = new Row { RowIndex = 1 };
            header.Append(TextCell("A1", "Date"), TextCell("B1", "Hours"), TextCell("C1", "Text"));
            sheetData.Append(header);

            // Data row (RowIndex 2): A=date, B=planHours, C=planText.
            var data = new Row { RowIndex = 2 };
            data.Append(TextCell("A2", dateStr), NumberCell("B2", hours), TextCell("C2", text));
            sheetData.Append(data);

            wbPart.Workbook.Save();
        }
        return ms.ToArray();
    }
}
