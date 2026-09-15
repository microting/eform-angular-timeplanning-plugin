using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class ReconcileServiceTests : TestBaseSetup
{
    private ITimePlanningPlanningService _service;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private ITimePlanningDbContextHelper _dbContextHelper;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        _dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        // NB: this hands out the SHARED fixture context. TimePlanningPlanningService
        // .Index() disposes every context it takes from the helper, so a test that
        // calls Index() through this stub would dispose the fixture out from under
        // itself. The Index() tests below go through BuildAdminIndexServiceAsync,
        // which overrides this with a fresh context per call -- do the same for any
        // new one.
        _dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        _service = new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            _options,
            TimePlanningPnDbContext,
            _dbContextHelper,
            _userService,
            _localizationService,
            null,
            _coreService);
    }

    /// <summary>
    /// One plain PlanRegistration. Returns the tracked entity so tests can
    /// reconcile it by Id. Note PnBase.Create forces WorkflowState to "created".
    /// </summary>
    private async Task<PlanRegistration> SeedPlain(int siteId, DateTime date)
    {
        var row = new PlanRegistration
        {
            SdkSitId = siteId,
            Date = date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    /// <summary>
    /// Builds a TimePlanningPlanningService wired for the full Index() path:
    /// real BaseDbContext seeded with an admin user (role "admin"), and an
    /// ITimePlanningDbContextHelper that hands out a FRESH plugin context per
    /// call — Index() fans out per-site work concurrently, and production's
    /// helper also returns a new context per call.
    ///
    /// Not used by this task's own tests, but copied verbatim so Tasks 5 and 6
    /// can append Index() tests to this fixture without duplicating it.
    /// </summary>
    private async Task<ITimePlanningPlanningService> BuildAdminIndexServiceAsync(BaseDbContext baseDbContext)
    {
        var role = new EformRole { Name = "admin", NormalizedName = "ADMIN" };
        baseDbContext.Roles.Add(role);
        await baseDbContext.SaveChangesAsync();

        var user = new EformUser
        {
            UserName = "admin@planning-index.test",
            Email = "admin@planning-index.test",
            FirstName = "Admin",
            LastName = "PlanningIndex"
        };
        baseDbContext.Users.Add(user);
        await baseDbContext.SaveChangesAsync();

        baseDbContext.UserRoles.Add(new EformUserRole { UserId = user.Id, RoleId = role.Id });
        await baseDbContext.SaveChangesAsync();

        _userService.UserId.Returns(user.Id);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = user.Id });
        _dbContextHelper.GetDbContext().Returns(_ => CreateTimePlanningPnDbContext());

        return new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            _options,
            TimePlanningPnDbContext,
            _dbContextHelper,
            _userService,
            _localizationService,
            baseDbContext,
            _coreService);
    }

    [Test]
    public async Task Reconcile_APastDay_SetsFlagAndTimestamp()
    {
        var row = await SeedPlain(900, DateTime.Now.Date.AddDays(-5));

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.True, result.Message);
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Reconciled, Is.True);
            Assert.That(reloaded.ReconciledAt, Is.Not.Null, "I1: the timestamp is written with the flag");
        });
    }

    [Test]
    public async Task Reconcile_Today_IsRejected()
    {
        var row = await SeedPlain(901, DateTime.Now.Date);

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("CannotReconcileTodayOrFuture"),
            "I2: today must stay open so time can still be registered");
    }

    [Test]
    public async Task Reconcile_AFutureDay_IsRejected()
    {
        var row = await SeedPlain(902, DateTime.Now.Date.AddDays(3));

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("CannotReconcileTodayOrFuture"));
    }

    [Test]
    public async Task Reconcile_AnAlreadyReconciledDay_IsAnIdempotentSuccess()
    {
        var row = await SeedPlain(903, DateTime.Now.Date.AddDays(-5));
        await _service.Reconcile(row.Id);

        var again = await _service.Reconcile(row.Id);

        Assert.That(again.Success, Is.True, "re-reconciling is a no-op, not an error");
    }

    [Test]
    public async Task Unreconcile_BelowTheBoundary_IsRejected_AndNamesTheBlockingDay()
    {
        var earlier = await SeedPlain(904, DateTime.Now.Date.AddDays(-8));
        var later   = await SeedPlain(904, DateTime.Now.Date.AddDays(-3));
        await _service.Reconcile(earlier.Id);
        await _service.Reconcile(later.Id);

        var result = await _service.Unreconcile(earlier.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("OnlyLatestReconciledDayCanBeUnlocked"),
            "the puzzle rule: free the outermost piece first");
    }

    [Test]
    public async Task Unreconcile_AtTheBoundary_MovesItBackToTheNextNewest()
    {
        var earlier = await SeedPlain(905, DateTime.Now.Date.AddDays(-8));
        var later   = await SeedPlain(905, DateTime.Now.Date.AddDays(-3));
        await _service.Reconcile(earlier.Id);
        await _service.Reconcile(later.Id);

        var result = await _service.Unreconcile(later.Id);

        Assert.That(result.Success, Is.True, result.Message);
        var boundary = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 905);
        Assert.That(boundary, Is.EqualTo(earlier.Date),
            "the boundary moves back one notch, it does not vanish");
    }

    [Test]
    public async Task Unreconcile_ClearsBothFlagAndTimestamp()
    {
        var row = await SeedPlain(909, DateTime.Now.Date.AddDays(-5));
        await _service.Reconcile(row.Id);

        var result = await _service.Unreconcile(row.Id);

        Assert.That(result.Success, Is.True, result.Message);
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Reconciled, Is.False);
            Assert.That(reloaded.ReconciledAt, Is.Null, "I1: unlock clears both together");
        });
    }

    [Test]
    public async Task Reconcile_ADayBelowAnExistingBoundary_IsRejectedWithAMessage()
    {
        var later = await SeedPlain(910, DateTime.Now.Date.AddDays(-3));
        var earlier = await SeedPlain(910, DateTime.Now.Date.AddDays(-8));
        await _service.Reconcile(later.Id);

        var result = await _service.Reconcile(earlier.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsLockedByReconciledDay"),
            "the explicit Layer-2 IsLocked guard rejects this with a message before any write is attempted");
    }

    [Test]
    public async Task Reconcile_DoesNotTouchTransferredToPayroll()
    {
        // false is TransferredToPayroll's default, so seeding it false would
        // pass even if Reconcile blindly reset the flag. Seed it TRUE (and
        // stamp TransferredToPayrollAt) so the assertion actually catches a
        // reconcile that resets an already-exported flag -- the real §11.2 risk.
        var row = await SeedPlain(911, DateTime.Now.Date.AddDays(-5));
        row.TransferredToPayroll = true;
        row.TransferredToPayrollAt = DateTime.Now;
        await row.Update(TimePlanningPnDbContext!);

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.True, result.Message);
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.TransferredToPayroll, Is.True,
                "spec 11.2: Reconciled and TransferredToPayroll are independent");
            Assert.That(reloaded.TransferredToPayrollAt, Is.Not.Null,
                "spec 11.2: reconciling must not clear the export timestamp either");
        });
    }

    [Test]
    public async Task ReconcileThrough_MarksOneDayPerWorker_AndSkipsThoseAlreadyFurtherForward()
    {
        var aEarly = await SeedPlain(906, DateTime.Now.Date.AddDays(-6));
        var bEarly = await SeedPlain(907, DateTime.Now.Date.AddDays(-6));
        var bLate  = await SeedPlain(907, DateTime.Now.Date.AddDays(-2));
        await _service.Reconcile(bLate.Id);   // 907's boundary is already newer

        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = DateTime.Now.Date.AddDays(-6),
            SiteIds = new List<int> { 906, 907 }
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Model.Applied, Is.EqualTo(1));
            Assert.That(result.Model.SkippedAlreadyFurtherForward, Is.EquivalentTo(new[] { 907 }),
                "moving 907 backwards would be an unlock, which is a separate action");
            Assert.That(result.Model.SkippedNoRegistration, Is.Empty,
                "907 was skipped for the other reason -- the two must not be conflated");
            Assert.That(result.Model.LandedOnBySiteId[906], Is.EqualTo(aEarly.Date));
            Assert.That(result.Model.LandedOnBySiteId.ContainsKey(907), Is.False);
        });
    }

    [Test]
    public async Task ReconcileThrough_LandsOnTheLatestDayWithARegistration()
    {
        await SeedPlain(908, DateTime.Now.Date.AddDays(-9));
        // nothing on -8 or -7
        var target = DateTime.Now.Date.AddDays(-7);

        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = target, SiteIds = new List<int> { 908 }
        });

        Assert.That(result.Model.LandedOnBySiteId[908], Is.EqualTo(DateTime.Now.Date.AddDays(-9)),
            "a seal on a day with no registration would mean nothing");
    }

    [Test]
    public async Task ReconcileThrough_SkipsWorkersWithNoRegistration()
    {
        // Site 912 has no PlanRegistration rows at all.
        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = DateTime.Now.Date.AddDays(-6),
            SiteIds = new List<int> { 912 }
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Model.SkippedNoRegistration, Is.EquivalentTo(new[] { 912 }));
            Assert.That(result.Model.SkippedAlreadyFurtherForward, Is.Empty,
                "no registration is a distinct reason from already-further-forward");
            Assert.That(result.Model.LandedOnBySiteId.ContainsKey(912), Is.False);
        });
    }

    [Test]
    public async Task ReconcileThrough_ADayAlreadyReconciledOnExactlyTheLandingDay_IsANoOp()
    {
        var row = await SeedPlain(913, DateTime.Now.Date.AddDays(-6));
        await _service.Reconcile(row.Id);

        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = DateTime.Now.Date.AddDays(-4),
            SiteIds = new List<int> { 913 }
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Model.AlreadyReconciledSiteIds, Is.EquivalentTo(new[] { 913 }));
            Assert.That(result.Model.Applied, Is.EqualTo(0));
            Assert.That(result.Model.SkippedAlreadyFurtherForward, Is.Empty,
                "913's only registration IS the landing day -- it must not also appear here");
            Assert.That(result.Model.SkippedNoRegistration, Is.Empty);
        });
    }
}
