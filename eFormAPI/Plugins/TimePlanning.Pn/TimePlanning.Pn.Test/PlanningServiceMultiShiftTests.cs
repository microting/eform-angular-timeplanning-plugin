using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanningServiceMultiShiftTests : TestBaseSetup
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

    [Test]
    public async Task Update_PersistsAllFiveShifts_RoundTrip()
    {
        // Arrange — seed AssignedSite + PlanRegistration
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 900,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var planning = new PlanRegistration
        {
            SdkSitId = 900,
            Date = DateTime.UtcNow.Date,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Build model with 5 shifts:
        // Shift 1: 00:00-01:00 (0-60)   break 5
        // Shift 2: 02:00-03:00 (120-180) break 10
        // Shift 3: 04:00-05:00 (240-300) break 15
        // Shift 4: 06:00-07:00 (360-420) break 20
        // Shift 5: 07:00-08:00 (420-480) break 25
        var model = new TimePlanningPlanningPrDayModel
        {
            Id = planning.Id,
            Date = planning.Date,
            CommentOffice = "",
            PlannedStartOfShift1 = 0,   PlannedEndOfShift1 = 60,   PlannedBreakOfShift1 = 5,
            PlannedStartOfShift2 = 120, PlannedEndOfShift2 = 180,  PlannedBreakOfShift2 = 10,
            PlannedStartOfShift3 = 240, PlannedEndOfShift3 = 300,  PlannedBreakOfShift3 = 15,
            PlannedStartOfShift4 = 360, PlannedEndOfShift4 = 420,  PlannedBreakOfShift4 = 20,
            PlannedStartOfShift5 = 420, PlannedEndOfShift5 = 480,  PlannedBreakOfShift5 = 25,
        };

        // Act
        var result = await _service.Update(planning.Id, model);

        // Assert
        Assert.That(result.Success, Is.True, result.Message);

        var reloaded = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == planning.Id);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.PlannedStartOfShift1, Is.EqualTo(0));
            Assert.That(reloaded.PlannedEndOfShift1,   Is.EqualTo(60));
            Assert.That(reloaded.PlannedBreakOfShift1, Is.EqualTo(5));

            Assert.That(reloaded.PlannedStartOfShift2, Is.EqualTo(120));
            Assert.That(reloaded.PlannedEndOfShift2,   Is.EqualTo(180));
            Assert.That(reloaded.PlannedBreakOfShift2, Is.EqualTo(10));

            Assert.That(reloaded.PlannedStartOfShift3, Is.EqualTo(240));
            Assert.That(reloaded.PlannedEndOfShift3,   Is.EqualTo(300));
            Assert.That(reloaded.PlannedBreakOfShift3, Is.EqualTo(15));

            Assert.That(reloaded.PlannedStartOfShift4, Is.EqualTo(360));
            Assert.That(reloaded.PlannedEndOfShift4,   Is.EqualTo(420));
            Assert.That(reloaded.PlannedBreakOfShift4, Is.EqualTo(20));

            Assert.That(reloaded.PlannedStartOfShift5, Is.EqualTo(420));
            Assert.That(reloaded.PlannedEndOfShift5,   Is.EqualTo(480));
            Assert.That(reloaded.PlannedBreakOfShift5, Is.EqualTo(25));
        });
    }

    [Test]
    public async Task Update_ClearsPause1WhenSetToNull_PersistsZero()
    {
        // Arrange — site with simple pause editing (the buggy code path)
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 901,
            UseDetailedPauseEditing = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Seed a PlanRegistration with non-zero Pause1Id (5 = 25 minutes).
        var planning = new PlanRegistration
        {
            SdkSitId = 901,
            Date = DateTime.UtcNow.Date,
            Pause1Id = 5,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Build the update model the way the trashcan-clear path sends it:
        // Pause1Id = null, all other shift1 fields preserved.
        var model = new TimePlanningPlanningPrDayModel
        {
            Id = planning.Id,
            Date = planning.Date,
            CommentOffice = "",
            Pause1Id = null,
            PlannedStartOfShift1 = 0, PlannedEndOfShift1 = 60, PlannedBreakOfShift1 = 5
        };

        // Act
        var result = await _service.Update(planning.Id, model);

        // Assert
        Assert.That(result.Success, Is.True, result.Message);

        var reloaded = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == planning.Id);

        // The bug was: server's `?? planning.Pause1Id` kept the old 5 instead
        // of clearing to 0. After the fix, null on the wire must persist as 0.
        Assert.That(reloaded.Pause1Id, Is.EqualTo(0));
    }

    /// <summary>
    /// Regression for the Phase 2 second-precision recompute NRE that PR #1545
    /// surfaced via the b1m playwright variant.
    ///
    /// Repro shape (matches the b1m fixture):
    ///   - AssignedSite.UseOneMinuteIntervals = true, UseDetailedPauseEditing = false
    ///   - Only shift 1 is populated; shifts 2-5 ride the default-zero
    ///     int Id columns AND their corresponding *StartedAt / *StoppedAt
    ///     DateTime stamps stay null (front-end never sends them when there's
    ///     no shift)
    ///   - Off-grid actual stamps for shift 1: 08:01-11:13, pause 00:27.
    ///     The int-Id columns the front-end sends are quasi-precise
    ///     (Newtonsoft truncates the off-grid float to int):
    ///       Start1Id = 97  (≈ 08:00 in the legacy 5-min grid)
    ///       Stop1Id  = 135 (≈ 11:10)
    ///       Pause1Id = 6   (≈ 25 minutes via (Pause1Id - 1) * 5)
    ///
    /// Pre-fix: Update() flowed into PlanRegistrationHelper.ApplyNettoFlexChain-
    /// SecondPrecision → ComputeNettoSecondsFromDateTimeShifts and the
    /// downstream EnsureTimestampsFromIds / ComputeTimeTrackingFields chain.
    /// Somewhere in that chain a default-zero shift's null DateTime stamp was
    /// dereferenced, the catch block swallowed the NRE as a generic
    /// "ErrorWhileUpdatingPlanning", and the PUT returned 200 with
    /// success=false. The b1m playwright spec then timed out waiting for
    /// the /index POST that the frontend only fires after a successful save.
    ///
    /// Post-fix: the call returns Success = true and the NettoHoursInSeconds
    /// column reflects the precise DateTime delta (08:01-11:13 = 11,520 s
    /// minus 27 min pause = 9,900 s).  Persistence of the int columns is
    /// verified to make sure the partial-shift path doesn't truncate them.
    /// </summary>
    [Test]
    public async Task Update_FlagOnPartialShiftWithOffGridActualStamps_DoesNotNre()
    {
        // Arrange — assigned site with UseOneMinuteIntervals on (the b1m
        // fixture path).  Other flags stay at defaults.
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 902,
            UseOneMinuteIntervals = true,
            UseDetailedPauseEditing = false,
            UseOnlyPlanHours = false,
            AutoBreakCalculationActive = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Seed a PlanRegistration on a fixed date so the netto computation is
        // deterministic.  Use a Wednesday (2026-04-29) to avoid weekend day-
        // classification noise.
        var date = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc);
        var planning = new PlanRegistration
        {
            SdkSitId = 902,
            Date = date,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Build the update model the way the dashboard cell sends it for an
        // off-grid actual-stamp edit on shift 1 only.  Shifts 2-5 are
        // default-zero with null DateTime stamps — the partial-shift case
        // that triggered the NRE.
        var model = new TimePlanningPlanningPrDayModel
        {
            Id = planning.Id,
            Date = date,
            CommentOffice = "",
            Start1Id = 97,                 // 08:01 truncated via Newtonsoft from 97.2
            Stop1Id = 135,                 // 11:13 truncated from 135.6
            Pause1Id = 6,                  // 00:27 → 6.4 → 6
            Start1StartedAt = date.AddHours(8).AddMinutes(1),
            Stop1StoppedAt  = date.AddHours(11).AddMinutes(13),
            // Shifts 2-5 explicitly null/zero so the test mirrors the b1m
            // payload byte-for-byte:
            Start2Id = null, Stop2Id = null, Pause2Id = null,
            Start3Id = null, Stop3Id = null, Pause3Id = null,
            Start4Id = null, Stop4Id = null, Pause4Id = null,
            Start5Id = null, Stop5Id = null, Pause5Id = null,
            Start2StartedAt = null, Stop2StoppedAt = null,
            Start3StartedAt = null, Stop3StoppedAt = null,
            Start4StartedAt = null, Stop4StoppedAt = null,
            Start5StartedAt = null, Stop5StoppedAt = null
        };

        // Act
        var result = await _service.Update(planning.Id, model);

        // Assert — must NOT swallow an NRE.  Before the fix this returned
        // Success = false with the localized "ErrorWhileUpdatingPlanning"
        // message; after the fix it persists cleanly.
        Assert.That(result.Success, Is.True, result.Message);

        var reloaded = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == planning.Id);

        // The DateTime delta is the source of truth on the flag-on path:
        // 11:13 - 08:01 = 3h12m = 11,520 s, minus the 27-minute observed pause
        // (the int Pause1Id=6 falls back via (6-1)*5 = 25 min when there's no
        // Pause1StartedAt/StoppedAt).  Working from the precise DateTime stamps
        // for start/stop and the legacy int for the pause:
        //   work    = 11,520 s
        //   pause   = 1,500 s   (legacy 5-min snap: (Pause1Id - 1) * 5 * 60)
        //   netto   = 10,020 s
        // The exact value isn't the load-bearing assertion — the NRE-free
        // round-trip is.  But sanity-check that the persisted *InSeconds is
        // in the right neighborhood and matches the formula.
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Start1Id, Is.EqualTo(97));
            Assert.That(reloaded.Stop1Id, Is.EqualTo(135));
            Assert.That(reloaded.Pause1Id, Is.EqualTo(6));
            Assert.That(reloaded.Start1StartedAt, Is.EqualTo(date.AddHours(8).AddMinutes(1)));
            Assert.That(reloaded.Stop1StoppedAt, Is.EqualTo(date.AddHours(11).AddMinutes(13)));
            Assert.That(reloaded.NettoHoursInSeconds, Is.EqualTo(11520 - 1500));
        });
    }

    /// <summary>
    /// Regression for the second NRE site PR #1545's b1m playwright variant
    /// surfaced after PR #1546 patched the `currentUserAsync.Id` site.
    ///
    /// Server stack frame on b1m round 5 confirmed that `[FromBody] model`
    /// itself can be null when the front-end PUTs a body that fails model-
    /// binding (the dashboard sends a partial-actual-only payload that the
    /// server treats as an empty body in some branches). The first dereference
    /// in Update() — `planning.PlannedStartOfShift1 = model.PlannedStartOfShift1`
    /// — then NRE'd inside the catch block as a generic 200/{success:false}.
    ///
    /// Post-fix: the call returns Success = false WITHOUT throwing. The
    /// localized "ErrorWhileUpdatingPlanning" message is matched here because
    /// the test's _localizationService is wired to echo the key back.
    /// </summary>
    [Test]
    public async Task Update_NullModel_ReturnsFailureWithoutException()
    {
        // Arrange — seed an AssignedSite + PlanRegistration so any code path
        // that dereferences `planning` after the null-model guard would still
        // have something to work with. The fix must short-circuit BEFORE
        // those lookups, but seeding makes the test robust against future
        // refactors that move the guard around.
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 903,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var planning = new PlanRegistration
        {
            SdkSitId = 903,
            Date = DateTime.UtcNow.Date,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Act — simulate the [FromBody] null path. Must not throw.
        var result = await _service.Update(planning.Id, null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False,
            "Update(null) must return a failure result, not silently succeed.");
        Assert.That(result.Message, Is.EqualTo("ErrorWhileUpdatingPlanning"),
            "Localization key must match the existing catch-block fallback so " +
            "the front-end's error surfacing is unchanged.");
    }
}
