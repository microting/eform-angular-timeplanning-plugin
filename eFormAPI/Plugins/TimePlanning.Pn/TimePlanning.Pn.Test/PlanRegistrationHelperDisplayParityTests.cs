using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Stage 3 tick-exact display parity for the plannings projection
/// (<see cref="PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod"/>).
///
/// The mode a row was REGISTERED under — resolved per row from the
/// AssignedSiteVersions audit trail via <see cref="OneMinuteModeTimeline"/>,
/// NOT the site's current flag — decides its Start/Stop rendering:
///  - tick row (5-minute mode at registration): ALWAYS the Id-derived tick
///    time, even when exact device stamps coexist on the row — bit-identical
///    to what the row showed before a site flip;
///  - one-minute row: the exact stamp first, falling back to the Id-derived
///    time (<c>stamp ?? (id &gt; 0 ? midnight + (id*5 - 5) min : null)</c>)
///    so a row never renders blank while an Id exists.
///
/// ReadBySiteAndDate is deliberately given NO synthesis at all: it feeds the
/// app's read-modify-write loop (getPlanRegistrationForDate → the funnel
/// echoes the whole registration back on save), so a stamp synthesized there
/// would be persisted to the DB on the next save — de-facto data modification.
/// The last test locks that no-echo-materialization guarantee.
/// </summary>
[TestFixture]
public class PlanRegistrationHelperDisplayParityTests : TestBaseSetup
{
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    [SetUp]
    public void SetUpOptions()
    {
        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });
    }

    private async Task<TimePlanningPlanningPrDayModel> ProjectSingleDay(
        AssignedSiteEntity assignedSite, DateTime date)
    {
        // Re-pull the registration the same shape Index() does (Id + Date only —
        // UpdatePlanRegistrationsInPeriod re-fetches the full row internally).
        var planningsInPeriod = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == assignedSite.SiteId)
            .Select(x => new PlanRegistration { Id = x.Id, Date = x.Date })
            .ToListAsync();

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = assignedSite.SiteId,
            SiteName = $"Test site {assignedSite.SiteId}",
            UseOneMinuteIntervals = assignedSite.UseOneMinuteIntervals,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };
        var sdkSite = new SdkSite
        {
            Name = $"Test site {assignedSite.SiteId}",
            MicrotingUid = assignedSite.SiteId
        };

        var result = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            TimePlanningPnDbContext,
            assignedSite,
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            sdkSite,
            date.AddDays(-1),
            date.AddDays(1),
            _options);

        return result.PlanningPrDayModels.Single(x => x.Date.Date == date.Date);
    }

    /// <summary>
    /// (a) One-minute site, NULL stamps but interval Ids &gt; 0 on all 5 shifts
    /// (the 5-minute-mode legacy shape) → the projection shows the Id-derived
    /// times, exactly like the Excel export: midnight + (Id*5 - 5) minutes.
    /// </summary>
    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_OneMinuteSite_NullStampsWithIds_ProjectsIdDerivedTimes()
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 910,
            UseOneMinuteIntervals = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var date = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc); // Wednesday
        var planning = new PlanRegistration
        {
            SdkSitId = 910,
            Date = date,
            // 5-minute-mode legacy shape: Ids only, no exact stamps.
            // Id 97 → 08:00, 121 → 10:00, 133 → 11:00, 157 → 13:00,
            // 169 → 14:00, 181 → 15:00, 193 → 16:00, 205 → 17:00,
            // 217 → 18:00, 229 → 19:00 (midnight + (Id-1)*5 min).
            Start1Id = 97, Stop1Id = 121,
            Start2Id = 133, Stop2Id = 157,
            Start3Id = 169, Stop3Id = 181,
            Start4Id = 193, Stop4Id = 205,
            Start5Id = 217, Stop5Id = 229,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        var prDay = await ProjectSingleDay(assignedSite, date);

        Assert.Multiple(() =>
        {
            Assert.That(prDay.Start1StartedAt, Is.EqualTo(date.AddHours(8)),
                "Start1: NULL stamp + Id 97 must project 08:00.");
            Assert.That(prDay.Stop1StoppedAt, Is.EqualTo(date.AddHours(10)),
                "Stop1: NULL stamp + Id 121 must project 10:00.");
            Assert.That(prDay.Start2StartedAt, Is.EqualTo(date.AddHours(11)),
                "Start2: NULL stamp + Id 133 must project 11:00.");
            Assert.That(prDay.Stop2StoppedAt, Is.EqualTo(date.AddHours(13)),
                "Stop2: NULL stamp + Id 157 must project 13:00.");
            Assert.That(prDay.Start3StartedAt, Is.EqualTo(date.AddHours(14)),
                "Start3: NULL stamp + Id 169 must project 14:00.");
            Assert.That(prDay.Stop3StoppedAt, Is.EqualTo(date.AddHours(15)),
                "Stop3: NULL stamp + Id 181 must project 15:00.");
            Assert.That(prDay.Start4StartedAt, Is.EqualTo(date.AddHours(16)),
                "Start4: NULL stamp + Id 193 must project 16:00.");
            Assert.That(prDay.Stop4StoppedAt, Is.EqualTo(date.AddHours(17)),
                "Stop4: NULL stamp + Id 205 must project 17:00.");
            Assert.That(prDay.Start5StartedAt, Is.EqualTo(date.AddHours(18)),
                "Start5: NULL stamp + Id 217 must project 18:00.");
            Assert.That(prDay.Stop5StoppedAt, Is.EqualTo(date.AddHours(19)),
                "Stop5: NULL stamp + Id 229 must project 19:00.");
        });
    }

    /// <summary>
    /// (b) One-minute site, exact stamps present alongside Ids → the exact
    /// stamps win unchanged. The stamps deliberately sit OFF the 5-minute grid
    /// (08:07/16:03) so an accidental Id-derivation would be caught.
    /// </summary>
    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_OneMinuteSite_StampsPresent_StampsWinUnchanged()
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 911,
            UseOneMinuteIntervals = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var date = new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc); // Thursday
        var start = date.AddHours(8).AddMinutes(7);
        var stop = date.AddHours(16).AddMinutes(3);
        var planning = new PlanRegistration
        {
            SdkSitId = 911,
            Date = date,
            Start1StartedAt = start,
            Stop1StoppedAt = stop,
            Start1Id = 97,   // 08:00 in the 5-min grid — must NOT override 08:07
            Stop1Id = 193,   // 16:00 in the 5-min grid — must NOT override 16:03
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        var prDay = await ProjectSingleDay(assignedSite, date);

        Assert.Multiple(() =>
        {
            Assert.That(prDay.Start1StartedAt, Is.EqualTo(start),
                "Exact stamp 08:07 must win over the Id-derived 08:00.");
            Assert.That(prDay.Stop1StoppedAt, Is.EqualTo(stop),
                "Exact stamp 16:03 must win over the Id-derived 16:00.");
        });
    }

    /// <summary>
    /// (c) One-minute site, Ids = 0 and NULL stamps → the projection stays
    /// NULL; the fallback must not fabricate times for untouched shifts.
    /// </summary>
    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_OneMinuteSite_ZeroIdsAndNullStamps_ProjectsNull()
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 912,
            UseOneMinuteIntervals = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var date = new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc); // Friday
        var planning = new PlanRegistration
        {
            SdkSitId = 912,
            Date = date,
            // Everything untouched: all Ids 0, all stamps NULL.
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        var prDay = await ProjectSingleDay(assignedSite, date);

        Assert.Multiple(() =>
        {
            Assert.That(prDay.Start1StartedAt, Is.Null);
            Assert.That(prDay.Stop1StoppedAt, Is.Null);
            Assert.That(prDay.Start2StartedAt, Is.Null);
            Assert.That(prDay.Stop2StoppedAt, Is.Null);
            Assert.That(prDay.Start3StartedAt, Is.Null);
            Assert.That(prDay.Stop3StoppedAt, Is.Null);
            Assert.That(prDay.Start4StartedAt, Is.Null);
            Assert.That(prDay.Stop4StoppedAt, Is.Null);
            Assert.That(prDay.Start5StartedAt, Is.Null);
            Assert.That(prDay.Stop5StoppedAt, Is.Null);
        });
    }

    /// <summary>
    /// Seeds an AssignedSite whose flag flipped false→true saved mid-day on
    /// <paramref name="flipDate"/>, with a fully controlled AssignedSiteVersions
    /// audit trail. The site and its version rows are inserted through the raw
    /// DbSets (NOT PnBase.Create/Update) so the version rows' UpdatedAt values
    /// — the save times the timeline reconstructs change points from — are
    /// exactly the test's chosen dates. Mirrors the prod schema quirk: the
    /// version rows' CreatedAt is a COPY of the base entity's creation time.
    /// </summary>
    private async Task<AssignedSiteEntity> CreateSiteWithFlipHistory(int siteId, DateTime flipDate)
    {
        var createdAt = flipDate.AddYears(-1);
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = siteId,
            UseOneMinuteIntervals = true, // current flag (post-flip)
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            CreatedAt = createdAt,
            UpdatedAt = flipDate,
            Version = 2
        };
        TimePlanningPnDbContext.AssignedSites.Add(assignedSite);
        await TimePlanningPnDbContext.SaveChangesAsync();

        TimePlanningPnDbContext.AssignedSiteVersions.AddRange(
            new AssignedSiteVersion
            {
                AssignedSiteId = assignedSite.Id,
                SiteId = siteId,
                UseOneMinuteIntervals = false,
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedAt = createdAt,
                UpdatedAt = createdAt, // saved at creation
                Version = 1
            },
            new AssignedSiteVersion
            {
                AssignedSiteId = assignedSite.Id,
                SiteId = siteId,
                UseOneMinuteIntervals = true,
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedAt = createdAt, // prod quirk: copies the BASE CreatedAt
                UpdatedAt = flipDate.Date.AddHours(14).AddMinutes(45), // mid-day save
                Version = 2
            });
        await TimePlanningPnDbContext.SaveChangesAsync();
        return assignedSite;
    }

    /// <summary>
    /// Tick-exact parity across a flag flip: rows registered BEFORE the flip
    /// (5-minute mode) must render their Id-derived tick times even though
    /// exact off-grid stamps exist on the rows; rows registered AFTER the flip
    /// render their exact stamps. Id 98 → 08:05, Id 193 → 16:00; the stamps
    /// deliberately sit off the tick grid (08:07/16:03) so any wrong-leg
    /// routing is caught.
    /// </summary>
    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_FlippedSite_PreFlipRowsTick_PostFlipRowsStamp()
    {
        var flipDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var assignedSite = await CreateSiteWithFlipHistory(915, flipDate);

        var preFlipDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var postFlipDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        foreach (var date in new[] { preFlipDate, postFlipDate })
        {
            await new PlanRegistration
            {
                SdkSitId = 915,
                Date = date,
                // Both rows carry BOTH representations, exactly like real rows
                // recorded by the device: tick ids plus exact wall-clock stamps.
                Start1Id = 98,   // 08:05 on the 5-min grid
                Stop1Id = 193,   // 16:00 on the 5-min grid
                Start1StartedAt = date.AddHours(8).AddMinutes(7),
                Stop1StoppedAt = date.AddHours(16).AddMinutes(3),
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            }.Create(TimePlanningPnDbContext);
        }

        var preFlipDay = await ProjectSingleDay(assignedSite, preFlipDate);
        var postFlipDay = await ProjectSingleDay(assignedSite, postFlipDate);

        Assert.Multiple(() =>
        {
            Assert.That(preFlipDay.Start1StartedAt, Is.EqualTo(preFlipDate.AddHours(8).AddMinutes(5)),
                "Pre-flip (tick) row: Id-derived 08:05 must win over the exact stamp 08:07.");
            Assert.That(preFlipDay.Stop1StoppedAt, Is.EqualTo(preFlipDate.AddHours(16)),
                "Pre-flip (tick) row: Id-derived 16:00 must win over the exact stamp 16:03.");
            Assert.That(postFlipDay.Start1StartedAt, Is.EqualTo(postFlipDate.AddHours(8).AddMinutes(7)),
                "Post-flip (one-minute) row: the exact stamp 08:07 must win.");
            Assert.That(postFlipDay.Stop1StoppedAt, Is.EqualTo(postFlipDate.AddHours(16).AddMinutes(3)),
                "Post-flip (one-minute) row: the exact stamp 16:03 must win.");
        });
    }

    /// <summary>
    /// Regression: a never-flipped 5-minute site (flag false, born false) keeps
    /// rendering Id-derived tick times — including for rows that carry exact
    /// stamps. This is the pre-stage-3 false-leg behavior, now routed through
    /// the per-row timeline (which is constant-false for such a site).
    /// </summary>
    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_NeverFlippedFiveMinuteSite_RendersIdDerivedTimes()
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 916,
            UseOneMinuteIntervals = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var date = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc); // Monday
        await new PlanRegistration
        {
            SdkSitId = 916,
            Date = date,
            Start1Id = 98,   // 08:05
            Stop1Id = 193,   // 16:00
            Start1StartedAt = date.AddHours(8).AddMinutes(7), // must NOT surface
            Stop1StoppedAt = date.AddHours(16).AddMinutes(3), // must NOT surface
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        var prDay = await ProjectSingleDay(assignedSite, date);

        Assert.Multiple(() =>
        {
            Assert.That(prDay.Start1StartedAt, Is.EqualTo(date.AddHours(8).AddMinutes(5)));
            Assert.That(prDay.Stop1StoppedAt, Is.EqualTo(date.AddHours(16)));
        });
    }

    /// <summary>
    /// Write-time mode marker (RegisteredUnderOneMinuteIntervals) beats the
    /// AssignedSiteVersions timeline in BOTH directions; the timeline only
    /// resolves legacy rows whose marker is NULL:
    ///  - marker=true on a pre-flip-DATED row (the l1m scenario: an admin
    ///    exact-minute edit AFTER the flip re-registers the row under
    ///    one-minute mode) → exact stamps render;
    ///  - marker NULL on a pre-flip row (untouched legacy row) → ticks;
    ///  - marker=false on a post-flip-DATED row (written under 5-minute mode)
    ///    → ticks even though the timeline says one-minute.
    /// </summary>
    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_WriteTimeMarker_WinsOverTimeline()
    {
        var flipDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var assignedSite = await CreateSiteWithFlipHistory(918, flipDate);

        var markerTrueDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);  // pre-flip
        var markerNullDate = new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);  // pre-flip
        var markerFalseDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc); // post-flip
        foreach (var (date, marker) in new (DateTime, bool?)[]
                 {
                     (markerTrueDate, true),
                     (markerNullDate, null),
                     (markerFalseDate, false)
                 })
        {
            await new PlanRegistration
            {
                SdkSitId = 918,
                Date = date,
                Start1Id = 98,   // 08:05 on the 5-min grid
                Stop1Id = 193,   // 16:00 on the 5-min grid
                Start1StartedAt = date.AddHours(8).AddMinutes(7),
                Stop1StoppedAt = date.AddHours(16).AddMinutes(3),
                RegisteredUnderOneMinuteIntervals = marker,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            }.Create(TimePlanningPnDbContext);
        }

        var markerTrueDay = await ProjectSingleDay(assignedSite, markerTrueDate);
        var markerNullDay = await ProjectSingleDay(assignedSite, markerNullDate);
        var markerFalseDay = await ProjectSingleDay(assignedSite, markerFalseDate);

        Assert.Multiple(() =>
        {
            Assert.That(markerTrueDay.Start1StartedAt, Is.EqualTo(markerTrueDate.AddHours(8).AddMinutes(7)),
                "marker=true (admin exact edit) on a pre-flip day must render the exact stamp 08:07.");
            Assert.That(markerTrueDay.Stop1StoppedAt, Is.EqualTo(markerTrueDate.AddHours(16).AddMinutes(3)),
                "marker=true (admin exact edit) on a pre-flip day must render the exact stamp 16:03.");
            Assert.That(markerNullDay.Start1StartedAt, Is.EqualTo(markerNullDate.AddHours(8).AddMinutes(5)),
                "marker NULL (untouched legacy row) resolves via the timeline → tick 08:05.");
            Assert.That(markerNullDay.Stop1StoppedAt, Is.EqualTo(markerNullDate.AddHours(16)),
                "marker NULL (untouched legacy row) resolves via the timeline → tick 16:00.");
            Assert.That(markerFalseDay.Start1StartedAt, Is.EqualTo(markerFalseDate.AddHours(8).AddMinutes(5)),
                "marker=false must render ticks even on a date the timeline says is one-minute.");
            Assert.That(markerFalseDay.Stop1StoppedAt, Is.EqualTo(markerFalseDate.AddHours(16)),
                "marker=false must render ticks even on a date the timeline says is one-minute.");
        });
    }

    /// <summary>
    /// No-echo-materialization guard: ReadBySiteAndDate must return NULL
    /// stamps VERBATIM even when interval Ids exist.
    ///
    /// This projection feeds the app's read-modify-write loop
    /// (getPlanRegistrationForDate → the shift-confirm funnel echoes the whole
    /// registration back on the next save). If ReadBySiteAndDate synthesized a
    /// stamp from the Id, that fabricated value would be PERSISTED to the DB on
    /// the next save — de-facto data modification from a read path. Display
    /// parity therefore lives only in UpdatePlanRegistrationsInPeriod (a pure
    /// display projection); this test pins ReadBySiteAndDate to the raw row.
    /// </summary>
    [Test]
    public async Task ReadBySiteAndDate_NullStampsWithIds_ReturnsNullStampsVerbatim()
    {
        var sdkSiteId = 913;
        var date = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc); // Saturday
        var planning = new PlanRegistration
        {
            SdkSitId = sdkSiteId,
            Date = date,
            // 5-minute-mode legacy shape: Ids only, no exact stamps.
            Start1Id = 97, Stop1Id = 193,
            Start2Id = 205, Stop2Id = 217,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        var result = await PlanRegistrationHelper.ReadBySiteAndDate(
            TimePlanningPnDbContext, sdkSiteId, date, null);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // Ids pass through untouched...
            Assert.That(result.Shift1Start, Is.EqualTo(97));
            Assert.That(result.Shift1Stop, Is.EqualTo(193));
            Assert.That(result.Shift2Start, Is.EqualTo(205));
            Assert.That(result.Shift2Stop, Is.EqualTo(217));
            // ...but the NULL stamps stay NULL — never Id-derived.
            Assert.That(result.Start1StartedAt, Is.Null,
                "ReadBySiteAndDate must NOT synthesize Start1StartedAt from Start1Id.");
            Assert.That(result.Stop1StoppedAt, Is.Null,
                "ReadBySiteAndDate must NOT synthesize Stop1StoppedAt from Stop1Id.");
            Assert.That(result.Start2StartedAt, Is.Null,
                "ReadBySiteAndDate must NOT synthesize Start2StartedAt from Start2Id.");
            Assert.That(result.Stop2StoppedAt, Is.Null,
                "ReadBySiteAndDate must NOT synthesize Stop2StoppedAt from Stop2Id.");
        });
    }
}
