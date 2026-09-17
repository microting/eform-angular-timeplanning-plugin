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
        // The format overload echoes its arguments, so a test can assert what a
        // message NAMES (here: how many locked days the import left alone), not
        // just which key it used.
        localizationService.GetString(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(x => x[0] + "|" + string.Join("|", (object[])x[1]));

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

        // A Removed row + an Active row for the same date/site -> the pre-fix
        // SingleOrDefaultAsync would throw on this pair. Seed the removed row
        // FIRST (create+delete) so the active row can then be created without
        // violating the unique (SdkSitId, Date, WorkflowState) index.
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

        var xlsx = BuildWorkbook(siteName, (dateStr, "8", "IMPORTED"));

        var result = await _service.Import(FormFile(xlsx));

        // Post-fix: no crash, and the ACTIVE row is the import target.
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Message, Is.EqualTo("Imported"),
            "an import that skipped nothing says only that it imported");

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

    /// <summary>
    /// A bulk import skips a locked day instead of failing the whole file.
    /// Without the skip the locked row is loaded, changed and saved, the
    /// interceptor refuses it, and Import returns Success false with the later
    /// rows never imported.
    ///
    /// The boundary is in the FUTURE only because Import never reaches a past
    /// day (it drops dates before now minus one day, which by time of day also
    /// drops yesterday), so no boundary that satisfies I2 is reachable. The
    /// lock itself does not check I2; this pins the skip as defense in depth.
    ///
    /// It also pins that the skip is REPORTED. A silent skip is what makes
    /// someone re-import a corrected timesheet over a reconciled month forever:
    /// they are told "Imported", they see the old numbers, and nothing in the
    /// result says which days did not move.
    /// </summary>
    [Test]
    public async Task Import_SkipsALockedDay_AndImportsTheRest()
    {
        const int microtingUid = 889;
        const string siteName = "ImportLockSite";
        var lockedDay = DateTime.Now.AddDays(5).Date;
        var openDay = lockedDay.AddDays(1);

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        await new SdkSite { Name = siteName, MicrotingUid = microtingUid }.Create(sdkDbContext);

        var boundary = new PlanRegistration
        {
            SdkSitId = microtingUid,
            Date = lockedDay,
            PlanText = "LOCKED-ORIG",
            Reconciled = true,
            ReconciledAt = new DateTime(2026, 1, 20, 9, 12, 0),
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await boundary.Create(TimePlanningPnDbContext);
        var boundaryBefore = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == boundary.Id);

        // The locked day comes first, so without the skip the import aborts
        // before it ever reaches the open day.
        var xlsx = BuildWorkbook(siteName,
            (lockedDay.ToString("dd.MM.yyyy"), "8", "IMPORTED-LOCKED"),
            (openDay.ToString("dd.MM.yyyy"), "6", "IMPORTED-OPEN"));

        var result = await _service.Import(FormFile(xlsx));

        Assert.That(result.Success, Is.True, result.Message);
        // The mock echoes "key|arg", so this asserts both the message and the
        // count it names -- one locked day, not "some".
        Assert.That(result.Message, Is.EqualTo("Imported ImportLockedDaysSkipped|1"),
            "the result must say how many days the lock left unchanged");

        var lockedAfter = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == boundary.Id);
        Assert.That(lockedAfter.PlanText, Is.EqualTo("LOCKED-ORIG"), "a locked day must not be imported into");
        Assert.That(lockedAfter.Version, Is.EqualTo(boundaryBefore.Version));
        Assert.That(lockedAfter.UpdatedAt, Is.EqualTo(boundaryBefore.UpdatedAt));

        var openRow = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == microtingUid && x.Date == openDay
                              && x.WorkflowState != Constants.WorkflowStates.Removed);
        Assert.That(openRow.PlanText, Is.EqualTo("IMPORTED-OPEN"), "the open day is still imported");
    }

    /// <summary>
    /// A sheet whose name DOES match a worker, but whose worker has no
    /// MicrotingUid, is skipped whole AND reported. Skipping it is the lock
    /// requirement -- an unreadable boundary makes IsLocked false for every row,
    /// so importing it would import the sheet with no lock check at all. But a
    /// silent skip here would be its own bug: the name matched, so the user
    /// believes that worker's sheet went in, and a plain success would leave
    /// them re-importing a file that never lands.
    ///
    /// Distinct from a sheet matching NO worker, which stays deliberately
    /// silent -- such a tab may not be about a worker at all.
    /// </summary>
    [Test]
    public async Task Import_ASheetWhoseWorkerHasNoMicrotingUid_IsSkippedAndReported()
    {
        const string siteName = "ImportNoUidSite";
        var importDate = DateTime.Now.AddDays(5).Date;

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        // Name matches the worksheet; MicrotingUid deliberately absent. Cleared
        // AFTER Create and then re-read, so the arrange cannot quietly test the
        // wrong thing if Create ever starts back-filling a uid of its own.
        var site = new SdkSite { Name = siteName, MicrotingUid = null };
        await site.Create(sdkDbContext);
        site.MicrotingUid = null;
        await sdkDbContext.SaveChangesAsync();
        Assert.That((await sdkDbContext.Sites.AsNoTracking().FirstAsync(x => x.Id == site.Id))
            .MicrotingUid, Is.Null, "arrange: the site must really have no MicrotingUid");

        var xlsx = BuildWorkbook(siteName,
            (importDate.ToString("dd.MM.yyyy"), "8", "SHOULD-NOT-LAND"));

        var result = await _service.Import(FormFile(xlsx));

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Message, Is.EqualTo("Imported ImportUnresolvableSheetsSkipped|1"),
            "the result must name the sheet it could not import");

        Assert.That(await TimePlanningPnDbContext.PlanRegistrations
                .AnyAsync(x => x.Date == importDate), Is.False,
            "a sheet that cannot be lock-checked must not be imported at all");
    }

    private static IFormFile FormFile(byte[] xlsx)
    {
        var file = Substitute.For<IFormFile>();
        file.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var target = ci.Arg<Stream>();
                target.Write(xlsx, 0, xlsx.Length);
                return Task.CompletedTask;
            });
        return file;
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

    private static byte[] BuildWorkbook(
        string sheetName, params (string Date, string Hours, string Text)[] rows)
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

            // Data rows (RowIndex 2..): A=date, B=planHours, C=planText.
            for (var i = 0; i < rows.Length; i++)
            {
                var r = i + 2;
                var data = new Row { RowIndex = (uint)r };
                data.Append(TextCell($"A{r}", rows[i].Date), NumberCell($"B{r}", rows[i].Hours),
                    TextCell($"C{r}", rows[i].Text));
                sheetData.Append(data);
            }

            wbPart.Workbook.Save();
        }
        return ms.ToArray();
    }
}
