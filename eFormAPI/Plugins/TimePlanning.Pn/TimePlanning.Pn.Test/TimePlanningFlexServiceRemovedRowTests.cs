using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Flex.Update;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningFlexService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for TimePlanningFlexService.UpdateCreate writing flex
/// values (SumFlexEnd / PaiedOutFlex / CommentOffice) into a soft-removed
/// PlanRegistration. The match query had no WorkflowState filter, so a Removed
/// row for the same date/site was resurrected as the flex target.
///
/// Structured to FAIL pre-fix (the Removed row is updated in place) and PASS
/// post-fix (the Removed row is skipped and a fresh active row is created).
/// </summary>
[TestFixture]
public class TimePlanningFlexServiceRemovedRowTests : TestBaseSetup
{
    private ITimePlanningFlexService _service;

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

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings());

        _service = new TimePlanningFlexService(
            Substitute.For<ILogger<TimePlanningFlexService>>(),
            TimePlanningPnDbContext,
            userService,
            localizationService,
            coreService,
            options);
    }

    [Test]
    public async Task UpdateCreate_DoesNotWriteIntoRemovedRow_CreatesFreshActiveRow()
    {
        const int sdkSitId = 555;
        var date = DateTime.UtcNow.Date;

        // A soft-removed flex row already exists for this date/site.
        var removed = new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = date,
            CommentOffice = "ORIGINAL",
            PaiedOutFlex = 5,
            SumFlexEnd = 100,
            StatusCaseId = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removed.Create(TimePlanningPnDbContext);
        await removed.Delete(TimePlanningPnDbContext); // WorkflowState -> Removed
        var removedId = removed.Id;

        var model = new List<TimePlanningFlexUpdateModel>
        {
            new TimePlanningFlexUpdateModel
            {
                Date = date,
                Worker = new CommonDictionaryModel { Id = sdkSitId },
                CommentOffice = "NEW",
                CommentOfficeAll = "NEW-ALL",
                PaidOutFlex = 9,
                SumFlexStart = 50
            }
        };

        var result = await _service.UpdateCreate(model);
        Assert.That(result.Success, Is.True, result.Message);

        // The removed row must be untouched.
        var reloadedRemoved = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == removedId);
        Assert.That(reloadedRemoved.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        Assert.That(reloadedRemoved.CommentOffice, Is.EqualTo("ORIGINAL"),
            "Flex UpdateCreate must not overwrite a removed row's comment");
        Assert.That(reloadedRemoved.PaiedOutFlex, Is.EqualTo(5),
            "Flex UpdateCreate must not overwrite a removed row's paid-out flex");

        // A brand-new active row must carry the update.
        var activeRows = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == sdkSitId && x.Date == date
                        && x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        Assert.That(activeRows.Count, Is.EqualTo(1),
            "A fresh active flex row must be created instead of reusing the removed one");
        Assert.That(activeRows[0].CommentOffice, Is.EqualTo("NEW"));
        Assert.That(activeRows[0].PaiedOutFlex, Is.EqualTo(9));
    }

    // ---- day lock -----------------------------------------------------------

    private async Task<PlanRegistration> SeedFlexRow(
        int sdkSitId, DateTime date, string commentOffice, bool reconciled = false, int statusCaseId = 0)
    {
        var row = new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = date,
            CommentOffice = commentOffice,
            PaiedOutFlex = 1,
            SumFlexEnd = 10,
            StatusCaseId = statusCaseId,
            Reconciled = reconciled,
            ReconciledAt = reconciled ? new DateTime(2026, 1, 20, 9, 12, 0) : (DateTime?)null,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await row.Create(TimePlanningPnDbContext);
        return row;
    }

    private static TimePlanningFlexUpdateModel FlexEntry(int sdkSitId, DateTime date, string commentOffice) =>
        new()
        {
            Date = date,
            Worker = new CommonDictionaryModel { Id = sdkSitId },
            CommentOffice = commentOffice,
            CommentOfficeAll = commentOffice,
            PaidOutFlex = 4,
            SumFlexStart = 20
        };

    /// <summary>
    /// The office edits specific days, so a locked day in the batch refuses
    /// the WHOLE batch with a message before anything is written. The open
    /// entry is posted FIRST: without the guard it is saved, and then the
    /// locked entry's save is refused, leaving the batch half applied and
    /// answering with the generic error.
    ///
    /// The message says what the blocking day IS: a day below the boundary
    /// is locked by it, while the reconciled boundary day is reconciled.
    /// </summary>
    [TestCase(false, "DayIsLockedByReconciledDay",
        TestName = "UpdateCreate_RejectsTheWholeBatch_WhenAnEntryIsBelowTheBoundary")]
    [TestCase(true, "DayIsReconciled",
        TestName = "UpdateCreate_RejectsTheWholeBatch_WhenAnEntryIsTheReconciledDay")]
    public async Task UpdateCreate_RejectsTheWholeBatch_WhenAnyEntryIsOnALockedDay(
        bool postTheReconciledDay, string expectedMessage)
    {
        const int sdkSitId = 556;
        var lockedDay = DateTime.Now.Date.AddDays(-4);
        var boundaryDay = DateTime.Now.Date.AddDays(-3);
        var openDay = DateTime.Now.Date.AddDays(-1);

        // Lock-safe order: the row below the boundary, then the boundary, then
        // the row above it.
        var locked = await SeedFlexRow(sdkSitId, lockedDay, "LOCKED-ORIG");
        var boundary = await SeedFlexRow(sdkSitId, boundaryDay, "BOUNDARY-ORIG", reconciled: true);
        var open = await SeedFlexRow(sdkSitId, openDay, "OPEN-ORIG");
        // Snapshots, not the seeded instances: the service loads the same
        // tracked objects, so their Version would move along with any write.
        var before = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().Where(x => x.SdkSitId == sdkSitId).ToListAsync();

        var result = await _service.UpdateCreate(
        [
            FlexEntry(sdkSitId, openDay, "NEW-OPEN"),
            FlexEntry(sdkSitId, postTheReconciledDay ? boundaryDay : lockedDay, "NEW-LOCKED")
        ]);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo(expectedMessage));

        foreach (var (id, comment) in new[]
                 {
                     (locked.Id, "LOCKED-ORIG"), (boundary.Id, "BOUNDARY-ORIG"), (open.Id, "OPEN-ORIG")
                 })
        {
            var after = await TimePlanningPnDbContext.PlanRegistrations
                .AsNoTracking().FirstAsync(x => x.Id == id);
            Assert.That(after.CommentOffice, Is.EqualTo(comment), "nothing in a refused batch is written");
            Assert.That(after.PaiedOutFlex, Is.EqualTo(1));
            Assert.That(after.Version, Is.EqualTo(before.Single(x => x.Id == id).Version));
        }

        var rowCount = await TimePlanningPnDbContext.PlanRegistrations.CountAsync(x => x.SdkSitId == sdkSitId);
        Assert.That(rowCount, Is.EqualTo(3), "a refused batch creates no rows");
    }

    /// <summary>
    /// A worker whose YESTERDAY is reconciled can still get today's flex entry:
    /// the batch guard checks each posted day, not whether the worker has a
    /// boundary at all. The follow-up loop then walks rows dated after
    /// now minus two days, which includes that reconciled yesterday. The
    /// loop's own skip is pinned by the next test.
    /// </summary>
    [Test]
    public async Task UpdateCreate_AcceptsToday_WhenYesterdayIsReconciled_AndLeavesYesterdayAlone()
    {
        const int sdkSitId = 557;
        var yesterday = DateTime.Now.Date.AddDays(-1);
        var today = DateTime.Now.Date;

        await SeedSdkSite(sdkSitId, "FlexLockSite");

        var reconciledYesterday = await SeedFlexRow(
            sdkSitId, yesterday, "YESTERDAY-ORIG", reconciled: true, statusCaseId: 42);
        var yesterdayBefore = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == reconciledYesterday.Id);

        var result = await _service.UpdateCreate([FlexEntry(sdkSitId, today, "NEW-TODAY")]);

        Assert.That(result.Success, Is.True, result.Message);

        var yesterdayAfter = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == reconciledYesterday.Id);
        Assert.That(yesterdayAfter.CommentOffice, Is.EqualTo("YESTERDAY-ORIG"));
        Assert.That(yesterdayAfter.Version, Is.EqualTo(yesterdayBefore.Version));
        Assert.That(yesterdayAfter.UpdatedAt, Is.EqualTo(yesterdayBefore.UpdatedAt));
        Assert.That(yesterdayAfter.Reconciled, Is.True);

        var todayRow = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == sdkSitId && x.Date == today
                              && x.WorkflowState != Constants.WorkflowStates.Removed);
        Assert.That(todayRow.CommentOffice, Is.EqualTo("NEW-TODAY"));
    }

    /// <summary>
    /// Pins the follow-up loop's WhereOpen skip. That loop calls Update on
    /// every row it loads without changing it, and PnBase.Update saves only
    /// when the context has ANY pending change, bumping the row's Version and
    /// UpdatedAt as it does. So an unrelated unsaved change on the shared
    /// context is enough: without the skip, the reconciled yesterday is
    /// loaded, its Update carries that save, the interceptor refuses the
    /// bumped locked row, and UpdateCreate fails. An EMPTY batch keeps the
    /// posted-day guard out of it.
    /// </summary>
    [Test]
    public async Task UpdateCreate_FollowUpLoop_NeverLoadsAReconciledYesterday()
    {
        const int sdkSitId = 558;
        var yesterday = DateTime.Now.Date.AddDays(-1);

        // What the loop needs to select and walk the row: StatusCaseId != 0,
        // a date after now minus two days, and an SDK site with a language.
        await SeedSdkSite(sdkSitId, "FlexFollowUpSite");
        var reconciledYesterday = await SeedFlexRow(
            sdkSitId, yesterday, "YESTERDAY-ORIG", reconciled: true, statusCaseId: 42);
        var yesterdayBefore = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == reconciledYesterday.Id);

        // An unrelated pending change, tracked but deliberately NOT saved.
        var unrelated = new AssignedSiteEntity { SiteId = 900, CreatedByUserId = 1, UpdatedByUserId = 1 };
        await unrelated.Create(TimePlanningPnDbContext);
        unrelated.UpdatedByUserId = 2;

        var result = await _service.UpdateCreate([]);

        Assert.That(result.Success, Is.True, result.Message);

        var yesterdayAfter = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == reconciledYesterday.Id);
        Assert.That(yesterdayAfter.Version, Is.EqualTo(yesterdayBefore.Version));
        Assert.That(yesterdayAfter.UpdatedAt, Is.EqualTo(yesterdayBefore.UpdatedAt));
    }

    /// <summary>
    /// An SDK site with a language: the follow-up loop resolves both for every
    /// row it loads, as production has them.
    /// </summary>
    private async Task SeedSdkSite(int sdkSitId, string name)
    {
        var core = await GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var language = await sdkDbContext.Languages.FirstAsync();
        await new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = name, MicrotingUid = sdkSitId, LanguageId = language.Id
        }.Create(sdkDbContext);
    }
}
