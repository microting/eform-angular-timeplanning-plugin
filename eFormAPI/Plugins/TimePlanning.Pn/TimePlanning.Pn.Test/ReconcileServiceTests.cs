using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
using TimePlanning.Pn.Controllers;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class ReconcileServiceTests : TestBaseSetup
{
    /// <summary>The user BuildAdminIndexServiceAsync creates; the current-user paths find "my site" by it.</summary>
    private const string AdminEmail = "admin@planning-index.test";

    /// <summary>
    /// Stored values on a locked day that UpdatePlanRegistrationsInPeriod
    /// would overwrite if the day were open (SeedAssignedSiteAsync plans 0
    /// hours on every weekday, and no earlier row carries a flex balance in):
    ///  - PlanHours: the plan recompute (which would write 0) is skipped for
    ///    locked days outright, so this pins that skip.
    ///  - SumFlexEnd: the flex chain still runs on the tracked entity and
    ///    writes 0 + 0 - 7.5 = -7.5 in memory, so this pins the revert before
    ///    the projection.
    /// </summary>
    private const double StoredPlanHours = 7.5;
    private const double StoredSumFlexEnd = 3.5;

    private ITimePlanningPlanningService _service;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private ITimePlanningDbContextHelper _dbContextHelper;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;
    /// <summary>The logger BuildAdminIndexServiceAsync's service writes to; see AssertNoErrorLogged.</summary>
    private ILogger<TimePlanningPlanningService> _indexLogger;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });
        // The fixture's default caller IS the first user; the gate tests override this.
        _userService.GetFirstUserIdInDb().Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        // The format overload echoes its arguments, so a test can assert what
        // a message names (e.g. the day to unlock first), not just its key.
        _localizationService.GetString(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(x => x[0] + "|" + string.Join("|", (object[])x[1]));

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
    /// Also wires _userService to that user, which is what the current-user
    /// paths (IndexByCurrentUserName, UpdateByCurrentUserNam, the personal
    /// UpdateWorkingHour) resolve "me" from.
    /// </summary>
    private async Task<ITimePlanningPlanningService> BuildAdminIndexServiceAsync(BaseDbContext baseDbContext)
    {
        var role = new EformRole { Name = "admin", NormalizedName = "ADMIN" };
        baseDbContext.Roles.Add(role);
        await baseDbContext.SaveChangesAsync();

        var user = new EformUser
        {
            UserName = AdminEmail,
            Email = AdminEmail,
            FirstName = "Admin",
            LastName = "PlanningIndex"
        };
        baseDbContext.Users.Add(user);
        await baseDbContext.SaveChangesAsync();

        baseDbContext.UserRoles.Add(new EformUserRole { UserId = user.Id, RoleId = role.Id });
        await baseDbContext.SaveChangesAsync();

        _userService.UserId.Returns(user.Id);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = user.Id });
        // This admin user is also treated as the first user here: these tests
        // exercise Index()'s recompute/lock-display behaviour, not the
        // reconcile gate, and SeedReconciledBoundaryAsync/
        // SeedReconciledDayWithStaleStoredValuesAsync below reconcile THROUGH
        // this same _userService substitute as a setup step.
        _userService.GetFirstUserIdInDb().Returns(user.Id);
        _dbContextHelper.GetDbContext().Returns(_ => CreateTimePlanningPnDbContext());

        _indexLogger = Substitute.For<ILogger<TimePlanningPlanningService>>();
        return new TimePlanningPlanningService(
            _indexLogger,
            _options,
            TimePlanningPnDbContext,
            _dbContextHelper,
            _userService,
            _localizationService,
            baseDbContext,
            _coreService);
    }

    /// <summary>
    /// UpdatePlanRegistrationsInPeriod's catch-all logs and SWALLOWS whatever
    /// its try throws, except a lock rejection, which it lets through to fail
    /// Index. Anything else swallowed there would not fail Index, but it does
    /// log at Error level, which this catches. Inspects ReceivedCalls, because
    /// LogError is an extension method over the generic Log&lt;TState&gt; and
    /// cannot be matched directly.
    /// </summary>
    private void AssertNoErrorLogged()
    {
        var errors = _indexLogger.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log) && c.GetArguments()[0] is LogLevel.Error);
        Assert.That(errors, Is.Zero, "nothing on the Index path may be rejected and swallowed");
    }

    /// <summary>
    /// The dashboard grid reads days BY POSITION, so the list must hold
    /// exactly one entry per date of the window, in order (spec §6.3); a
    /// missing entry would shift every later day one column left.
    /// </summary>
    private static void AssertOneDayPerDate(
        IReadOnlyList<TimePlanningPlanningPrDayModel> days, TimePlanningPlanningRequestModel window)
    {
        var from = window.DateFrom!.Value.Date;
        var count = (window.DateTo!.Value.Date - from).Days + 1;
        Assert.That(days, Has.Count.EqualTo(count), "one entry per date, locked gaps included");
        for (var i = 0; i < count; i++)
        {
            Assert.That(days[i].Date, Is.EqualTo(from.AddDays(i)), $"column {i} must be {from.AddDays(i):yyyy-MM-dd}");
        }
    }

    /// <summary>
    /// A registration device, so the kiosk UpdateWorkingHour overload accepts
    /// its token. Returns the generated token for the call under test.
    /// </summary>
    private async Task<string> SeedKioskDeviceAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await new RegistrationDevice
        {
            Token = token,
            Name = "Kiosk Device",
            OtpCode = "10001",
            SoftwareVersion = "1.0.0",
            Manufacturer = "Test",
            Model = "Test",
            OsVersion = "1.0",
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
        return token;
    }

    /// <summary>A plain day, reconciled, so it becomes the boundary.</summary>
    private async Task<PlanRegistration> SeedReconciledBoundaryAsync(int siteUid, DateTime date)
    {
        var row = await SeedPlain(siteUid, date);
        var reconciled = await _service.Reconcile(row.Id);
        Assert.That(reconciled.Success, Is.True, reconciled.Message);
        return row;
    }

    /// <summary>
    /// Everything Index() needs to actually process a site: the SDK Site the
    /// row takes its name from and the plugin AssignedSite it iterates. Without
    /// both, Index() skips the site, so a lock test would pass without ever
    /// exercising the lock.
    /// </summary>
    private async Task<SdkSite> SeedAssignedSiteAsync(int siteUid, bool useGoogleSheetAsDefault = false)
    {
        var sdkDbContext = (await _coreService.GetCore()).DbContextHelper.GetDbContext();
        var sdkSite = new SdkSite { Name = $"Reconcile site {siteUid}", MicrotingUid = siteUid };
        await sdkSite.Create(sdkDbContext);

        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            // The recompute has two branches, each with its own guarded Update
            // inside the catch-all. Default to the weekday-plan branch (the
            // entity itself defaults to the Google-sheet one), whose all-zero
            // plan is what makes StoredPlanHours stale.
            UseGoogleSheetAsDefault = useGoogleSheetAsDefault,
            // The personal mobile write path refuses past days without this,
            // before its lock guard is ever reached.
            AllowEditOfRegistrations = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        return sdkSite;
    }

    /// <summary>
    /// Makes <paramref name="sdkSite"/> the current user's site: a worker with
    /// the admin's e-mail, attached to it. That is how the current-user paths
    /// resolve "my site".
    /// </summary>
    private async Task SeedCurrentUserWorkerAsync(SdkSite sdkSite)
    {
        var sdkDbContext = (await _coreService.GetCore()).DbContextHelper.GetDbContext();
        var worker = new SdkWorker
        {
            FirstName = "Admin",
            LastName = "PlanningIndex",
            Email = AdminEmail,
            MicrotingUid = 1000 + sdkSite.MicrotingUid!.Value
        };
        await worker.Create(sdkDbContext);
        await new SdkSiteWorker
        {
            SiteId = sdkSite.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2000 + sdkSite.MicrotingUid!.Value
        }.Create(sdkDbContext);
    }

    /// <summary>
    /// A reconciled day (so the boundary) storing values a dashboard load
    /// rewrites in memory. That makes the loaded, TRACKED entity dirty -- the
    /// precondition for the flush trap: the next open day's save would flush
    /// the dirty locked row and fail the load. Without it, merely skipping the
    /// Update calls would look sufficient.
    ///
    /// IsSaturday is stored WRONG for the date, so the unconditional weekday
    /// assignment before the first Update (the one outside the try, whose
    /// throw nothing swallows) always dirties the row, whatever day of the
    /// week the test runs on.
    /// </summary>
    private async Task<PlanRegistration> SeedReconciledDayWithStaleStoredValuesAsync(int siteUid, DateTime date)
    {
        var row = await SeedPlain(siteUid, date);
        row.PlanHours = StoredPlanHours;
        row.SumFlexEnd = StoredSumFlexEnd;
        row.IsSaturday = date.DayOfWeek != DayOfWeek.Saturday;
        await row.Update(TimePlanningPnDbContext!);

        var reconciled = await _service.Reconcile(row.Id);
        Assert.That(reconciled.Success, Is.True, reconciled.Message);
        return row;
    }

    private TimePlanningWorkingHoursService BuildWorkingHoursService(BaseDbContext baseDbContext = null) =>
        new(Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            _userService,
            _localizationService,
            baseDbContext,
            _options,
            _coreService);

    /// <summary>
    /// One row as the working-hours page posts it (the page posts every row it
    /// shows). Tests assert MessageId because the recompute inside
    /// UpdatePlanning never touches it, unlike PlanHours.
    /// </summary>
    private static TimePlanningWorkingHoursModel Posted(DateTime date) => new()
    {
        Date = date,
        Message = 3,
        PlanText = "",
        PaidOutFlex = "0",
        CommentOffice = "",
        CommentOfficeAll = ""
    };

    private static TimePlanningPlanningPrDayModel EditOf(PlanRegistration row) => new()
    {
        Id = row.Id,
        Date = row.Date,
        CommentOffice = ""
    };

    /// <summary>
    /// With the boundary seeded on -5, this window holds the locked day AND
    /// open days after it, which gap-fill creates and the loop then saves.
    /// </summary>
    private static TimePlanningPlanningRequestModel LastTenDaysThroughToday() => new()
    {
        DateFrom = DateTime.Now.Date.AddDays(-10),
        DateTo = DateTime.Now.Date
    };

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

    /// <summary>
    /// UtcNow, deliberately: this date is fed to CanReconcile, which compares
    /// against UtcNow.Date, and it sits exactly ON that boundary -- so a local
    /// clock an offset away from UTC flips the outcome.
    ///
    /// The fixture's other DateTime.Now dates stay as they are, including the
    /// many that DO reach CanReconcile via _service.Reconcile. Every date that
    /// reaches it is -3 or older, or +3 forward, and the largest real UTC offset
    /// is 14 hours, so no offset can move one across the boundary. The file's -1
    /// dates never reach CanReconcile: they are open days and window bounds,
    /// tested by IsLocked against a boundary the fixture stored on the same
    /// clock. Only a date sitting exactly ON the boundary can be flipped, and
    /// this test holds the only one.
    ///
    /// Reconcile_AFutureDay_IsRejected below moved too, even though +3 days is
    /// safe at any offset, so the pair of tests naming this one rule reads on
    /// one clock rather than two.
    /// </summary>
    [Test]
    public async Task Reconcile_Today_IsRejected()
    {
        var row = await SeedPlain(901, DateTime.UtcNow.Date);

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("CannotReconcileTodayOrFuture"),
            "I2: today must stay open so time can still be registered");
    }

    [Test]
    public async Task Reconcile_AFutureDay_IsRejected()
    {
        var row = await SeedPlain(902, DateTime.UtcNow.Date.AddDays(3));

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
        Assert.That(result.Message,
            Is.EqualTo("OnlyLatestReconciledDayCanBeUnlocked|"
                       + later.Date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)),
            "the puzzle rule: free the outermost piece first, and the message names it");
    }

    [Test]
    public async Task Unreconcile_WhenNothingIsReconciled_SaysSo()
    {
        // No boundary at all, so there is no day the message could name.
        var row = await SeedPlain(915, DateTime.Now.Date.AddDays(-5));

        var result = await _service.Unreconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("NothingIsReconciled"));
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

    // ---------------------------------------------------------------------
    // Layer 2: user-facing write paths answer with a message. Without the
    // guards, each of these reaches the interceptor and comes back as a
    // generic error (or, where the method has no try/catch, a raw exception).
    // ---------------------------------------------------------------------

    [Test]
    public async Task Update_ADayBelowTheBoundary_ReturnsDayIsLockedByReconciledDay()
    {
        // Plain rows below a boundary must exist before it: afterwards the
        // interceptor refuses to create them.
        var earlier = await SeedPlain(920, DateTime.Now.Date.AddDays(-8));
        await SeedReconciledBoundaryAsync(920, DateTime.Now.Date.AddDays(-3));

        var result = await _service.Update(earlier.Id, EditOf(earlier));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsLockedByReconciledDay"));
    }

    [Test]
    public async Task Update_TheBoundaryDay_ReturnsDayIsReconciled()
    {
        var boundary = await SeedReconciledBoundaryAsync(921, DateTime.Now.Date.AddDays(-3));

        var result = await _service.Update(boundary.Id, EditOf(boundary));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsReconciled"),
            "the reconciled day itself says what it is, not that something else locks it");
    }

    [Test]
    public async Task Update_AnOpenDayAboveTheBoundary_StillSucceeds()
    {
        await SeedAssignedSiteAsync(922);
        await SeedReconciledBoundaryAsync(922, DateTime.Now.Date.AddDays(-3));
        var open = await SeedPlain(922, DateTime.Now.Date.AddDays(-1));

        var result = await _service.Update(open.Id, EditOf(open));

        Assert.That(result.Success, Is.True, result.Message);
    }

    [Test]
    public async Task UpdateByCurrentUserNam_TheBoundaryDay_ReturnsDayIsReconciled()
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedCurrentUserWorkerAsync(await SeedAssignedSiteAsync(923));
        var boundary = await SeedReconciledBoundaryAsync(923, DateTime.Now.Date.AddDays(-3));

        var result = await svc.UpdateByCurrentUserNam(EditOf(boundary));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsReconciled"),
            "spec 11.3: the mobile path answers with the same message as web");
    }

    [Test]
    public async Task CreateUpdate_AcrossTheBoundary_SkipsLockedRowsAndSavesOpenOnes()
    {
        // UpdatePlanning dereferences the AssignedSite.
        await SeedAssignedSiteAsync(924);
        var boundary = await SeedReconciledBoundaryAsync(924, DateTime.Now.Date.AddDays(-3));
        var existingOpen = await SeedPlain(924, DateTime.Now.Date.AddDays(-1));
        var before = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == boundary.Id);
        var missingOpenDate = DateTime.Now.Date.AddDays(-2);

        var result = await BuildWorkingHoursService().CreateUpdate(new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = 924,
            Plannings = new List<TimePlanningWorkingHoursModel>
            {
                // First, like the page's carried-over row: a locked first row
                // must not stop the next row from being created.
                Posted(boundary.Date),
                Posted(missingOpenDate),
                Posted(existingOpen.Date)
            }
        });

        Assert.That(result.Success, Is.True, result.Message);
        var lockedAfter = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == boundary.Id);
        var created = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SdkSitId == 924 && x.Date == missingOpenDate);
        var updated = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == existingOpen.Id);
        Assert.Multiple(() =>
        {
            Assert.That(lockedAfter.Version, Is.EqualTo(before.Version),
                "the locked row is skipped, not re-saved");
            Assert.That(lockedAfter.UpdatedAt, Is.EqualTo(before.UpdatedAt));
            Assert.That(lockedAfter.MessageId, Is.Null, "the posted change to the locked row is ignored");
            Assert.That(created, Is.Not.Null, "an open day missing from the DB is still created");
            Assert.That(created?.MessageId, Is.EqualTo(3));
            Assert.That(updated.MessageId, Is.EqualTo(3), "an open day in the same request still saves");
        });
    }

    /// <summary>
    /// Pins CreateUpdate's loop skip, which WhereOpen does not make redundant:
    /// WhereOpen keeps locked ROWS out of the load, but a locked date with NO
    /// row (a gap row the working-hours Index emits, and the page posts every
    /// row) is not in the load either way. Without the skip, CreatePlanning
    /// creates -5 inside the lock, the interceptor refuses it, and the refusal
    /// propagates through CreatePlanning's catch and fails the whole request.
    /// </summary>
    [Test]
    public async Task CreateUpdate_ALockedGapRowInTheMiddle_IsNotCreatedAndTheRestSaves()
    {
        await SeedAssignedSiteAsync(932);
        var below = await SeedPlain(932, DateTime.Now.Date.AddDays(-8));
        await SeedReconciledBoundaryAsync(932, DateTime.Now.Date.AddDays(-3));
        var existingOpen = await SeedPlain(932, DateTime.Now.Date.AddDays(-1));
        var lockedGapDate = DateTime.Now.Date.AddDays(-5);
        var openGapDate = DateTime.Now.Date.AddDays(-2);

        var result = await BuildWorkingHoursService().CreateUpdate(new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = 932,
            Plannings = new List<TimePlanningWorkingHoursModel>
            {
                Posted(below.Date),
                // Not first, so without the skip CreatePlanning would create it.
                Posted(lockedGapDate),
                Posted(openGapDate),
                Posted(existingOpen.Date)
            }
        });

        Assert.That(result.Success, Is.True, result.Message);
        var lockedGap = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .AnyAsync(x => x.SdkSitId == 932 && x.Date == lockedGapDate);
        var created = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SdkSitId == 932 && x.Date == openGapDate);
        var updated = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == existingOpen.Id);
        Assert.Multiple(() =>
        {
            Assert.That(lockedGap, Is.False, "a locked period does not grow new rows");
            Assert.That(created?.MessageId, Is.EqualTo(3), "the open gap row is created");
            Assert.That(updated.MessageId, Is.EqualTo(3), "the open stored row is updated");
        });
    }

    /// <summary>
    /// Pins CreateUpdate's DB-side WhereOpen filter, which looks redundant next
    /// to the loop's skip but is the only thing keeping the forward cascade
    /// out of the lock. Posting only the locked -8 skips it in the loop; the
    /// cascade then runs over every row after -8. Without the filter it would
    /// re-chain -6 (stored balance 5, recomputed 0) and save it inside the
    /// lock, the interceptor would reject that, and the request would fail.
    /// </summary>
    [Test]
    public async Task CreateUpdate_PostingALockedDay_CascadesPastTheLockWithoutTouchingIt()
    {
        await SeedAssignedSiteAsync(931);
        var below = await SeedPlain(931, DateTime.Now.Date.AddDays(-8));
        var insideLock = await SeedPlain(931, DateTime.Now.Date.AddDays(-6));
        insideLock.SumFlexStart = 5;
        insideLock.SumFlexEnd = 5;
        await insideLock.Update(TimePlanningPnDbContext!);
        var boundary = await SeedReconciledBoundaryAsync(931, DateTime.Now.Date.AddDays(-3));
        // Stale balance, so the cascade's re-chain off the boundary visibly changes it.
        var open = await SeedPlain(931, DateTime.Now.Date.AddDays(-1));
        open.SumFlexStart = 9;
        open.SumFlexEnd = 9;
        await open.Update(TimePlanningPnDbContext!);
        async Task<PlanRegistration> Stored(PlanRegistration row) =>
            await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking().FirstAsync(x => x.Id == row.Id);
        var insideBefore = await Stored(insideLock);
        var boundaryBefore = await Stored(boundary);
        var openBefore = await Stored(open);

        var result = await BuildWorkingHoursService().CreateUpdate(new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = 931,
            Plannings = new List<TimePlanningWorkingHoursModel> { new() { Date = below.Date } }
        });

        Assert.That(result.Success, Is.True, result.Message);
        var insideAfter = await Stored(insideLock);
        var boundaryAfter = await Stored(boundary);
        var openAfter = await Stored(open);
        Assert.Multiple(() =>
        {
            Assert.That(insideAfter.Version, Is.EqualTo(insideBefore.Version), "a locked row the cascade passes");
            Assert.That(boundaryAfter.Version, Is.EqualTo(boundaryBefore.Version), "the boundary");
            Assert.That(openAfter.Version, Is.GreaterThan(openBefore.Version),
                "the open day after the lock is still recomputed by the cascade");
            Assert.That(openAfter.SumFlexStart, Is.EqualTo(boundaryAfter.SumFlexEnd),
                "re-chained off the stored boundary balance");
        });
    }

    [Test]
    public async Task WorkingHoursIndex_MarksReconciledLockedDaysAsIsLocked()
    {
        // Index scopes the requested site to the signed-in caller; this seeds
        // the admin user and points _userService at it ("me").
        await using var baseDbContext = GetBaseDbContext();
        await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedAssignedSiteAsync(930);
        // Keep the MaxDaysEditable window out of the way, so only the
        // reconciled lock can set IsLocked on these past days.
        _options.Value.MaxDaysEditable = 365;
        _userService.GetCurrentUserLanguage().Returns(new SdkLanguage { LanguageCode = "da" });
        await SeedPlain(930, DateTime.Now.Date.AddDays(-8));
        await SeedReconciledBoundaryAsync(930, DateTime.Now.Date.AddDays(-3));
        await SeedPlain(930, DateTime.Now.Date.AddDays(-1));

        var result = await BuildWorkingHoursService(baseDbContext).Index(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = 930,
            DateFrom = DateTime.Now.Date.AddDays(-10),
            DateTo = DateTime.Now.Date.AddDays(-1)
        });

        Assert.That(result.Success, Is.True, result.Message);
        bool IsLockedOn(int daysAgo) =>
            result.Model.Single(x => x.Date == DateTime.Now.Date.AddDays(-daysAgo)).IsLocked;
        Assert.Multiple(() =>
        {
            Assert.That(IsLockedOn(8), Is.True, "a stored day below the boundary");
            Assert.That(IsLockedOn(3), Is.True, "the reconciled day itself");
            Assert.That(IsLockedOn(5), Is.True, "an empty day inside the lock (the gap-row path)");
            Assert.That(IsLockedOn(1), Is.False, "a stored day above the boundary stays editable");
            Assert.That(IsLockedOn(2), Is.False, "an empty day above the boundary stays editable");
        });
    }

    [Test]
    public async Task UpdateWorkingHour_Kiosk_ADayBelowTheBoundary_IsRejectedAndCreatesNothing()
    {
        var token = await SeedKioskDeviceAsync();
        await SeedReconciledBoundaryAsync(925, DateTime.Now.Date.AddDays(-3));
        // No row on -4: without the guard the kiosk would CREATE one inside the
        // lock, and this method has no try/catch to turn the rejection into a message.
        var lockedDate = DateTime.Now.Date.AddDays(-4);

        var result = await BuildWorkingHoursService().UpdateWorkingHour(925,
            new TimePlanningWorkingHoursUpdateModel { Date = lockedDate }, token);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsLockedByReconciledDay"));
        Assert.That(await TimePlanningPnDbContext!.PlanRegistrations
            .AnyAsync(x => x.SdkSitId == 925 && x.Date == lockedDate), Is.False);
    }

    /// <summary>
    /// Separates the message rule (the row's own Reconciled flag, as on web)
    /// from the earlier "is it the boundary date" rule: -8 was reconciled
    /// first, then -3, so -8 is reconciled but lies below the boundary. The
    /// boundary-date rule answered DayIsLockedByReconciledDay here; web says
    /// DayIsReconciled for the same day, and mobile must too (spec 11.3).
    /// </summary>
    [Test]
    public async Task UpdateWorkingHour_Kiosk_AnOlderReconciledDay_ReturnsDayIsReconciled()
    {
        var token = await SeedKioskDeviceAsync();
        var older = await SeedReconciledBoundaryAsync(933, DateTime.Now.Date.AddDays(-8));
        await SeedReconciledBoundaryAsync(933, DateTime.Now.Date.AddDays(-3));

        var result = await BuildWorkingHoursService().UpdateWorkingHour(933,
            new TimePlanningWorkingHoursUpdateModel { Date = older.Date }, token);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsReconciled"));
    }

    /// <summary>
    /// The lock's unknown case must REFUSE, not permit. The kiosk overload takes
    /// an int? site id with no null guard of its own, so before the fix a null
    /// sailed straight past the guard (RefuseIfNotWritableAsync's null answer
    /// means "writable, proceed") and then died on a `sdkSiteId!.Value`
    /// dereference further down. Now it answers with a message and writes
    /// nothing.
    ///
    /// Note what the null case is NOT: with no site id the lock is never
    /// evaluated at all, so relaxing the refusal would not "let a locked day
    /// through" -- it would crash on that dereference instead, which is how the
    /// bug presented. The seeded boundary is here to make the scenario realistic
    /// (a real kiosk posting into a frozen period), not because the assertion
    /// depends on the day being locked.
    /// </summary>
    [Test]
    public async Task UpdateWorkingHour_Kiosk_WithNoSiteId_IsRefused_AndWritesNothing()
    {
        var token = await SeedKioskDeviceAsync();
        await SeedReconciledBoundaryAsync(934, DateTime.Now.Date.AddDays(-3));
        var lockedDate = DateTime.Now.Date.AddDays(-4);

        var result = await BuildWorkingHoursService().UpdateWorkingHour(
            null, new TimePlanningWorkingHoursUpdateModel { Date = lockedDate }, token);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("SiteNotFound"));
        Assert.That(await TimePlanningPnDbContext!.PlanRegistrations
            .AnyAsync(x => x.Date == lockedDate), Is.False,
            "an unresolvable site must not create a row on a locked day");
    }

    [Test]
    public async Task UpdateWorkingHour_Personal_TheBoundaryDay_ReturnsDayIsReconciled()
    {
        await using var baseDbContext = GetBaseDbContext();
        // Seeds the admin user and points _userService at it ("me").
        await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedCurrentUserWorkerAsync(await SeedAssignedSiteAsync(926));
        var boundary = await SeedReconciledBoundaryAsync(926, DateTime.Now.Date.AddDays(-3));

        var result = await BuildWorkingHoursService(baseDbContext).UpdateWorkingHour(
            new TimePlanningWorkingHoursUpdateModel { Date = boundary.Date });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("DayIsReconciled"),
            "spec 11.3: the mobile path answers with the same message as web");
    }

    // ---------------------------------------------------------------------
    // Layer 3: recalculation skips locked days, and a dashboard load over a
    // closed period still succeeds (a dirty locked entity left behind would be
    // flushed by the next open day's save and fail the load). Each asserts the
    // positional one-entry-per-date contract (spec §6.3) and that nothing on
    // the way was logged as an error.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Run once per recompute branch: each branch has its own guarded Update
    /// inside the catch-all. A missing guard there throws the lock rejection
    /// through the catch-all, which fails Index and so result.Success.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task Index_OverALockedRange_LeavesLockedRowsByteIdentical(bool useGoogleSheetAsDefault)
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedAssignedSiteAsync(927, useGoogleSheetAsDefault);
        var row = await SeedReconciledDayWithStaleStoredValuesAsync(927, DateTime.Now.Date.AddDays(-5));
        var before = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        var window = LastTenDaysThroughToday();

        var result = await svc.Index(window);

        Assert.That(result.Success, Is.True, result.Message);
        AssertNoErrorLogged();
        var days = result.Model.Single(x => x.SiteId == 927).PlanningPrDayModels;
        AssertOneDayPerDate(days, window);
        var lockedDay = days.Single(x => x.Date == row.Date);
        var after = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(after.Version, Is.EqualTo(before.Version),
                "a no-op re-save still bumps Version — this catches silent rewrites");
            Assert.That(after.UpdatedAt, Is.EqualTo(before.UpdatedAt));
            Assert.That(lockedDay.Id, Is.EqualTo(row.Id));
            Assert.That(lockedDay.PlanHours, Is.EqualTo(StoredPlanHours),
                "the grid shows the STORED, reconciled plan, not a recomputation");
            Assert.That(lockedDay.SumFlexEnd, Is.EqualTo(StoredSumFlexEnd),
                "the grid shows the STORED, reconciled balance, not a recomputation");
        });
    }

    [Test]
    public async Task Index_OverALockedRange_CreatesNoNewRows()
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedAssignedSiteAsync(928);
        var boundary = DateTime.Now.Date.AddDays(-5);
        await SeedReconciledDayWithStaleStoredValuesAsync(928, boundary);
        var window = LastTenDaysThroughToday();

        var result = await svc.Index(window);

        Assert.That(result.Success, Is.True, result.Message);
        AssertNoErrorLogged();
        var days = result.Model.Single(x => x.SiteId == 928).PlanningPrDayModels;
        AssertOneDayPerDate(days, window);
        var lockedRows = await TimePlanningPnDbContext!.PlanRegistrations
            .CountAsync(x => x.SdkSitId == 928 && x.Date <= boundary);
        var openRows = await TimePlanningPnDbContext!.PlanRegistrations
            .CountAsync(x => x.SdkSitId == 928 && x.Date > boundary);
        Assert.Multiple(() =>
        {
            Assert.That(lockedRows, Is.EqualTo(1),
                "gap-fill must not materialise rows inside a frozen period");
            Assert.That(openRows, Is.EqualTo(5),
                "gap-fill still fills the open days after the boundary (-4 .. today)");
            Assert.That(days.Where(x => x.Date < boundary).Select(x => x.Id), Is.All.EqualTo(0),
                "the missing locked dates (-10 .. -6) are placeholders, with no row behind them");
        });
    }

    [Test]
    public async Task IndexByCurrentUserName_OverALockedRange_Succeeds()
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedCurrentUserWorkerAsync(await SeedAssignedSiteAsync(929));
        var row = await SeedReconciledDayWithStaleStoredValuesAsync(929, DateTime.Now.Date.AddDays(-5));
        var before = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        var window = LastTenDaysThroughToday();

        var result = await svc.IndexByCurrentUserName(window, null, null, null, null);

        Assert.That(result.Success, Is.True, result.Message);
        AssertNoErrorLogged();
        Assert.That(result.Model.SiteId, Is.EqualTo(929));
        var days = result.Model.PlanningPrDayModels;
        AssertOneDayPerDate(days, window);
        var lockedDay = days.Single(x => x.Date == row.Date);
        var after = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        var lockedRows = await TimePlanningPnDbContext!.PlanRegistrations
            .CountAsync(x => x.SdkSitId == 929 && x.Date <= row.Date);
        Assert.Multiple(() =>
        {
            Assert.That(after.Version, Is.EqualTo(before.Version));
            Assert.That(after.UpdatedAt, Is.EqualTo(before.UpdatedAt));
            Assert.That(lockedDay.PlanHours, Is.EqualTo(StoredPlanHours),
                "the mobile fetch shows the STORED, reconciled plan, not a recomputation");
            Assert.That(lockedDay.SumFlexEnd, Is.EqualTo(StoredSumFlexEnd));
            Assert.That(lockedRows, Is.EqualTo(1),
                "the mobile path's gap-fill must not grow the frozen period either");
            Assert.That(days.Where(x => x.Date < row.Date).Select(x => x.Id), Is.All.EqualTo(0),
                "the missing locked dates are placeholders on the mobile path too");
        });
    }

    // ---------------------------------------------------------------------
    // The read model carries the lock state -- the row-level boundary
    // once per site, and Reconciled/ReconciledAt per day, so the client can
    // render locked and reconciled days without scanning cells.
    // ---------------------------------------------------------------------

    [Test]
    public async Task Index_ProjectsReconciledStateOntoTheReadModel()
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedAssignedSiteAsync(912);
        // Locked by derivation only -- earlier than the boundary, never itself
        // marked Reconciled.
        var earlier = await SeedPlain(912, DateTime.Now.Date.AddDays(-6));
        var boundary = await SeedReconciledBoundaryAsync(912, DateTime.Now.Date.AddDays(-4));
        var window = new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.Now.Date.AddDays(-6),
            DateTo = DateTime.Now.Date
        };

        var result = await svc.Index(window);

        Assert.That(result.Success, Is.True, result.Message);
        var siteRow = result.Model.Single(x => x.SiteId == 912);
        var boundaryDay = siteRow.PlanningPrDayModels.Single(d => d.Date.Date == boundary.Date.Date);
        var earlierDay = siteRow.PlanningPrDayModels.Single(d => d.Date.Date == earlier.Date.Date);
        Assert.Multiple(() =>
        {
            Assert.That(siteRow.LockedThrough, Is.EqualTo(boundary.Date),
                "the client needs the boundary once per row, not per cell");
            Assert.That(boundaryDay.Reconciled, Is.True);
            Assert.That(boundaryDay.ReconciledAt, Is.Not.Null);
            Assert.That(earlierDay.Reconciled, Is.False,
                "locked by derivation, not individually marked -- LockedThrough covers it");
        });
    }

    /// <summary>
    /// The stamp goes out as a UTC INSTANT, not as naked digits.
    ///
    /// The datetime(6) column carries no offset, so EF hands back
    /// DateTimeKind.Unspecified, and Newtonsoft (RoundtripKind) writes a suffix
    /// only for Kind Utc or Local. An Unspecified value therefore serialises as
    /// "2026-09-14T10:32:11" with nothing after it, and `new Date(...)` in the
    /// browser reads that as the VIEWER's local wall clock -- the tooltip
    /// showing an hour or two early in Denmark, every day of the year. The
    /// projection re-tags the value Utc so the JSON ends in "Z"; this pins that.
    ///
    /// The two assertions guard different things on different hosts:
    ///  - The KIND assertion bites EVERYWHERE, CI included. EF materialises the
    ///    offsetless column as Unspecified on every host, UTC ones too, so
    ///    dropping the SpecifyKind in the projection turns this red in CI. It
    ///    is this diff's real guard -- do not weaken it.
    ///  - The FRESHNESS assertion catches a regression to DateTime.Now on a
    ///    host at a NON-ZERO offset, which is the case that would make the "Z"
    ///    a lie. Its tolerance is 5 minutes, so it is inert not only on CI but
    ///    anywhere at UTC+0 (Europe/London in winter, Atlantic/Reykjavik,
    ///    Africa/Abidjan), where the two clocks coincide anyway.
    /// </summary>
    [Test]
    public async Task Index_ProjectsReconciledAtAsAUtcInstant()
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedAssignedSiteAsync(916);
        // UtcNow.Date, not Now.Date: this whole chain is meant to run on one
        // clock, and SeedReconciledBoundaryAsync has to satisfy CanReconcile,
        // which compares against UtcNow.Date.
        var open = await SeedPlain(916, DateTime.UtcNow.Date.AddDays(-1));
        var boundary = await SeedReconciledBoundaryAsync(916, DateTime.UtcNow.Date.AddDays(-4));
        var window = new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.UtcNow.Date.AddDays(-4),
            DateTo = DateTime.UtcNow.Date
        };

        var result = await svc.Index(window);

        Assert.That(result.Success, Is.True, result.Message);
        var siteRow = result.Model.Single(x => x.SiteId == 916);
        var boundaryDay = siteRow.PlanningPrDayModels.Single(d => d.Date.Date == boundary.Date.Date);
        var openDay = siteRow.PlanningPrDayModels.Single(d => d.Date.Date == open.Date.Date);
        Assert.Multiple(() =>
        {
            Assert.That(boundaryDay.ReconciledAt, Is.Not.Null);
            Assert.That(boundaryDay.ReconciledAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc),
                "Kind Utc is what puts the trailing Z on the wire");
            Assert.That(boundaryDay.ReconciledAt!.Value,
                Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(5)),
                "the column holds UTC -- a local-clock write would land an offset away");
            Assert.That(openDay.ReconciledAt, Is.Null,
                "an unreconciled day stays null: the tagging must not invent a value");
        });
    }

    /// <summary>
    /// The trap: UpdatePlanRegistrationsInPeriod's per-day loop
    /// runs over planningsInPeriod, and a window that is entirely locked with
    /// no existing rows leaves that list empty for the whole call -- the loop
    /// never executes once. LockedThrough must still be set, because it is
    /// resolved unconditionally before the loop, not inside it.
    /// </summary>
    [Test]
    public async Task Index_WhenTheEntireWindowIsLockedWithNoRows_StillReturnsLockedThrough()
    {
        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await SeedAssignedSiteAsync(914);
        var boundary = await SeedReconciledBoundaryAsync(914, DateTime.Now.Date.AddDays(-5));
        // Entirely before the boundary, so every day here is locked -- and
        // gap-fill must not create rows inside a frozen period, so this
        // window has zero PlanRegistrations of its own.
        var window = new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.Now.Date.AddDays(-20),
            DateTo = DateTime.Now.Date.AddDays(-15)
        };

        var result = await svc.Index(window);

        Assert.That(result.Success, Is.True, result.Message);
        var siteRow = result.Model.Single(x => x.SiteId == 914);
        var rowsInWindow = await TimePlanningPnDbContext!.PlanRegistrations
            .CountAsync(x => x.SdkSitId == 914 && x.Date >= window.DateFrom && x.Date <= window.DateTo);
        Assert.Multiple(() =>
        {
            Assert.That(rowsInWindow, Is.Zero, "the window must genuinely have no rows of its own");
            Assert.That(siteRow.LockedThrough, Is.EqualTo(boundary.Date),
                "a fully-locked window with no rows must still carry the boundary, " +
                "or the client renders an entirely locked worker as fully editable");
        });
    }

    // ---------------------------------------------------------------------
    // Server-side enforcement of "only the FIRST USER may reconcile or unlock
    // a day" (corrected product decision — it is not a role at all, admin or
    // otherwise). The gate lives in the SERVICE layer
    // (TimePlanningPlanningService.Reconcile/Unreconcile/ReconcileThrough,
    // via FirstUserHelper.IsFirstUserAsync), because that is the only path to
    // these writes (see the controller, which now carries a bare
    // [Authorize] — anonymous is still refused by the pipeline, but which
    // signed-in caller may proceed is decided here). These are therefore
    // proper behaviour tests, not reflection: success/refusal and, on
    // refusal, that nothing was written.
    // ---------------------------------------------------------------------

    [Test]
    public async Task Reconcile_TheFirstUser_Succeeds()
    {
        // Deliberately explicit, not redundant with SeedReconciledBoundaryAsync's
        // internal assert: on a gate feature, the ALLOWED case deserves a pin a
        // reader can find by name, not one inferred from a fixture's internals.
        var row = await SeedPlain(940, DateTime.Now.Date.AddDays(-5));

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.True, result.Message);
    }

    [Test]
    public async Task Reconcile_ADifferentSignedInUser_IsRefused_AndWritesNothing()
    {
        var row = await SeedPlain(941, DateTime.Now.Date.AddDays(-5));
        // Someone else is signed in; user 1 remains the first user.
        _userService.UserId.Returns(2);

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("OnlyTheFirstUserCanReconcileOrUnlock"));
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Reconciled, Is.False, "a refused caller must not reconcile the day");
            Assert.That(reloaded.ReconciledAt, Is.Null);
        });
    }

    [Test]
    public async Task Reconcile_CallerWithNoUserId_IsRefused_EvenWhenTheUsersTableIsEmpty()
    {
        // UserId 0 (no signed-in user) paired with GetFirstUserIdInDb also
        // answering 0 (an empty users table) must NOT satisfy 0 == 0 -- the
        // house rule's whole point (FirstUserHelper.IsFirstUserAsync).
        var row = await SeedPlain(942, DateTime.Now.Date.AddDays(-5));
        _userService.UserId.Returns(0);
        _userService.GetFirstUserIdInDb().Returns(0);

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("OnlyTheFirstUserCanReconcileOrUnlock"));
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.That(reloaded.Reconciled, Is.False,
            "userId 0 must never pass, even against an empty/zero first-user id");
    }

    [Test]
    public async Task Unreconcile_ADifferentSignedInUser_IsRefused_AndWritesNothing()
    {
        var boundary = await SeedReconciledBoundaryAsync(943, DateTime.Now.Date.AddDays(-3));
        _userService.UserId.Returns(2);

        var result = await _service.Unreconcile(boundary.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("OnlyTheFirstUserCanReconcileOrUnlock"));
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == boundary.Id);
        Assert.That(reloaded.Reconciled, Is.True, "a refused caller must not unlock the day");
    }

    [Test]
    public async Task ReconcileThrough_ADifferentSignedInUser_IsRefused_AndWritesNothing()
    {
        var row = await SeedPlain(944, DateTime.Now.Date.AddDays(-6));
        _userService.UserId.Returns(2);

        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = DateTime.Now.Date.AddDays(-6),
            SiteIds = new List<int> { 944 }
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("OnlyTheFirstUserCanReconcileOrUnlock"));
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.That(reloaded.Reconciled, Is.False, "a refused caller must not bulk-reconcile anything");
    }

    /// <summary>
    /// Cheap and still worth pinning: anonymous access must stay blocked even
    /// though the role check is gone -- these three keep a bare [Authorize] --
    /// AND that none of them carry a role restriction any more, since the
    /// mechanism really changed to the service-layer first-user check, not
    /// merely gained one. The real control is the behaviour tests above; this
    /// only guards against either half of that shape drifting silently.
    /// </summary>
    [Test]
    public void Reconcile_Unreconcile_ReconcileThrough_RequireAuthorize_ButCarryNoRole()
    {
        foreach (var methodName in new[] { "Reconcile", "Unreconcile", "ReconcileThrough" })
        {
            var method = typeof(TimePlanningPlanningController).GetMethod(methodName);
            Assert.That(method, Is.Not.Null, $"TimePlanningPlanningController.{methodName} must exist");

            var authorizeAttributes = method!
                .GetCustomAttributes<AuthorizeAttribute>(true)
                .ToList();

            Assert.That(authorizeAttributes, Is.Not.Empty,
                $"{methodName} must carry an AuthorizeAttribute so anonymous callers are refused");

            var roleRestricted = authorizeAttributes.Where(a => !string.IsNullOrWhiteSpace(a.Roles)).ToList();
            Assert.That(roleRestricted, Is.Empty,
                $"{methodName} must NOT be role-restricted -- the first-user check lives in the service, " +
                "not a role -- found: " + string.Join(", ", roleRestricted.Select(a => a.Roles)));
        }
    }

    /// <summary>
    /// Reading and editing OPEN days is unrelated to the reconcile gate,
    /// whichever mechanism guards reconcile itself (admin role, then first
    /// user). Update is the named open-day action here, and this assertion
    /// stands on its own regardless of which gate reconcile currently uses.
    /// </summary>
    [Test]
    public void Update_OpenDayAction_NeverAcquiresARoleRestriction()
    {
        var method = typeof(TimePlanningPlanningController).GetMethod("Update");
        Assert.That(method, Is.Not.Null, "TimePlanningPlanningController.Update must exist");

        var roleRestricted = method!
            .GetCustomAttributes<AuthorizeAttribute>(true)
            .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
            .ToList();

        Assert.That(roleRestricted, Is.Empty,
            "Update (editing an open day) must not gain a role restriction from the reconcile gate — found: " +
            string.Join(", ", roleRestricted.Select(a => a.Roles)));
    }
}
