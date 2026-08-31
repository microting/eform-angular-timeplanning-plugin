using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data;
using AssignedSite = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistration = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Reconstructs the history of an AssignedSite's <c>UseOneMinuteIntervals</c>
/// flag from its <c>AssignedSiteVersions</c> audit rows so read/display/calc
/// paths can resolve the mode that was IN FORCE when a given row was
/// registered ("mode at registration") instead of the site's current flag.
///
/// Why: a row registered under 5-minute mode carries tick ids as its truth;
/// a row registered under one-minute mode carries exact stamps. When a site
/// flips the flag, per-site forking silently reinterprets historical rows
/// (tick rows suddenly render/pay from raw stamps → drift in every total).
/// Discriminating on stamp-nullness does not work either, because exact
/// stamps exist on virtually all tick rows (devices record them alongside
/// the tick ids). The reliable discriminator is this timeline.
///
/// Schema quirk (verified in prod): <c>PnBase.MapVersion</c> copies EVERY
/// property of the base entity onto the version row — including
/// <c>CreatedAt</c>, which therefore always holds the BASE entity's original
/// creation time on every version row. The actual save time of a version row
/// is its <c>UpdatedAt</c> (PnBase sets the base entity's UpdatedAt to
/// UtcNow immediately before mapping the version). Change points are hence
/// derived from consecutive version rows' flag values using UpdatedAt as the
/// transition instant, ordered by version row Id (insert order).
///
/// Date granularity: comparisons are DATE-ONLY. A flag flip saved mid-day
/// governs that WHOLE day under the new value — a PlanRegistration's Date is
/// a midnight anchor with no time-of-day, so a finer resolution is not
/// representable; making the flip day take the new mode matches the
/// operational reality that the flip is done before/with the first
/// registrations the admin wants under the new mode.
///
/// Edge cases:
///  - No version rows at all → the site's CURRENT flag for all dates.
///  - Flag already true in the earliest version row → true from the
///    beginning of time (dates before the first row exist only for rows
///    created before the audit row — same mode as at creation).
///  - Multiple toggles → interval walk over the change points; several
///    toggles on the same date → the last save wins.
///  - Divergence correction: when the entity's CURRENT flag differs from the
///    last audited version value, the flag was flipped OUTSIDE the audited
///    path (raw-SQL ops change, or a CI seed whose dump predates the column —
///    PnBase writes a version row on every API save, so a complete trail
///    always ends on the current value). The exact flip time is unknowable,
///    so the current flag takes over from the LAST audited save date — the
///    earliest possible un-audited flip point. Audited history before that
///    date is preserved; for sites flipped through the API this is a no-op.
///
/// Authoritative override: <c>AssignedSite.UseOneMinuteIntervalsFrom</c>.
/// The derived timeline above is a RECONSTRUCTION; when ops (or the
/// settings save in <c>TimeSettingService.UpdateAssignedSite</c>) has
/// recorded the date the flag actually took effect, that stored date is the
/// truth and the reconstruction is not consulted at all:
///   <c>UseOneMinuteIntervals &amp;&amp; rowDate &gt;= UseOneMinuteIntervalsFrom</c>
/// (date-only, same granularity rule as above). A NULL column means "nothing
/// recorded" and falls through to the derived timeline, which keeps today's
/// behaviour for every site ops has not backfilled.
///
/// Full per-row precedence (see <see cref="ResolveRowModeAsync"/>):
///   1. <c>PlanRegistration.RegisteredUnderOneMinuteIntervals</c> — the
///      write-time marker, ground truth for rows that carry one.
///   2. <c>UseOneMinuteIntervalsFrom</c> — the stored effective date.
///   3. the AssignedSiteVersions-derived timeline.
///
/// The class also OWNS THE WRITE SIDE of that column:
/// <see cref="StampEffectiveDateOnEnable"/> is what records the date when the
/// settings save flips the flag on, so the read rule and the write rule cannot
/// drift apart.
///
/// Cost: ONE query per site (<see cref="BuildAsync"/>); lookups are pure
/// in-memory. Build once per site per request scope — never per row.
/// </summary>
public sealed class OneMinuteModeTimeline
{
    private readonly bool _initialValue;

    /// <summary>The site's CURRENT flag (also the effective-date verdict's value).</summary>
    private readonly bool _currentFlag;

    /// <summary>
    /// The authoritative date the current flag took effect, when recorded;
    /// NULL means "not recorded" and the derived timeline is used instead.
    /// </summary>
    private readonly DateTime? _effectiveFrom;

    /// <summary>Date-only change points in save order (date, value-from-that-date).</summary>
    private readonly List<(DateTime Date, bool Value)> _changePoints;

    /// <summary>
    /// In-memory constructor (also used directly by unit tests).
    /// <paramref name="versionFlags"/> must be in version-row Id (save) order;
    /// pass an empty list to fall back to <paramref name="currentFlag"/>.
    /// <paramref name="currentFlag"/> is the entity's CURRENT flag — the
    /// no-version-rows fallback AND the divergence-correction authority (see
    /// class docs): when the trail does not end on this value, the current
    /// flag takes over from the last audited save date.
    /// <paramref name="effectiveFrom"/> is the site's recorded
    /// <c>UseOneMinuteIntervalsFrom</c>; when non-null it OVERRIDES the derived
    /// timeline entirely (see <see cref="ResolveByEffectiveDate"/>).
    /// </summary>
    internal OneMinuteModeTimeline(
        bool currentFlag,
        IReadOnlyList<(bool UseOneMinuteIntervals, DateTime SavedAt)> versionFlags,
        DateTime? effectiveFrom = null)
    {
        _currentFlag = currentFlag;
        _effectiveFrom = effectiveFrom;
        _changePoints = new List<(DateTime, bool)>();

        if (versionFlags == null || versionFlags.Count == 0)
        {
            _initialValue = currentFlag;
            return;
        }

        // The earliest version row's value holds from the beginning of time.
        _initialValue = versionFlags[0].UseOneMinuteIntervals;
        var current = _initialValue;
        foreach (var (value, savedAt) in versionFlags)
        {
            if (value == current)
            {
                continue;
            }
            current = value;
            _changePoints.Add((savedAt.Date, current));
        }

        // Divergence correction: an audit trail written by PnBase always ends
        // on the entity's current value; when it doesn't, the flag was flipped
        // outside the audited path (raw-SQL ops change / legacy seed). Trust
        // the CURRENT flag from the last audited save date — the earliest
        // possible un-audited flip point — appended LAST so it wins over an
        // audited toggle on that same date (see WasOneMinuteAt walk order).
        if (current != currentFlag)
        {
            _changePoints.Add((versionFlags[^1].SavedAt.Date, currentFlag));
        }
    }

    /// <summary>
    /// Builds the timeline for one AssignedSite with a single
    /// AssignedSiteVersions query. An unsaved entity (Id == 0) or a site
    /// without audit rows yields a constant timeline of the current flag;
    /// a null site (no AssignedSite row for the worker) yields a constant
    /// 5-minute timeline, so callers never need their own empty-timeline
    /// fallback.
    /// </summary>
    public static async Task<OneMinuteModeTimeline> BuildAsync(
        TimePlanningPnDbContext dbContext, AssignedSite? assignedSite)
    {
        if (assignedSite == null)
        {
            return new OneMinuteModeTimeline(false, Array.Empty<(bool, DateTime)>());
        }

        var versionFlags = await dbContext.AssignedSiteVersions
            .AsNoTracking()
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .OrderBy(x => x.Id)
            // UpdatedAt is the save time of the version row (see class docs);
            // CreatedAt (a copy of the base entity's creation time) is the
            // stand-in for legacy rows whose UpdatedAt is NULL.
            .Select(x => new { x.UseOneMinuteIntervals, x.UpdatedAt, x.CreatedAt })
            .ToListAsync();

        return new OneMinuteModeTimeline(
            assignedSite.UseOneMinuteIntervals,
            versionFlags
                .Select(x => (x.UseOneMinuteIntervals, x.UpdatedAt ?? x.CreatedAt))
                .ToList(),
            assignedSite.UseOneMinuteIntervalsFrom);
    }

    /// <summary>
    /// The ONE place the stored effective date is turned into a verdict.
    /// Returns <c>null</c> when nothing is recorded (<paramref name="effectiveFrom"/>
    /// is NULL) so the caller falls through to the derived timeline; otherwise
    /// the flag applies only from that date onwards. DATE-ONLY comparison — a
    /// <c>PlanRegistration.Date</c> is a midnight anchor with no time-of-day,
    /// matching the timeline's own granularity rule.
    /// </summary>
    internal static bool? ResolveByEffectiveDate(
        bool currentFlag, DateTime? effectiveFrom, DateTime rowDate)
        => effectiveFrom == null
            ? null
            : currentFlag && rowDate.Date >= effectiveFrom.Value.Date;

    /// <summary>
    /// Records WHEN one-minute intervals took effect, on the false→true
    /// transition only. Must be called BEFORE the caller ORs the incoming value
    /// into the stored flag: <c>UseOneMinuteIntervals</c> is deliberately
    /// one-way (commit 994c9cd4), so after the OR a real transition is
    /// indistinguishable from "was already true".
    ///
    /// The <c>UseOneMinuteIntervalsFrom == null</c> guard is required: an ops
    /// backfill of recovered historical dates must not be clobbered with
    /// today's date by an unrelated later settings save. The column is ops-only
    /// — written by script or by this stamp, never exposed on a DTO.
    /// </summary>
    public static void StampEffectiveDateOnEnable(
        AssignedSite dbAssignedSite, bool incomingUseOneMinuteIntervals, DateTime now)
    {
        if (!dbAssignedSite.UseOneMinuteIntervals
            && incomingUseOneMinuteIntervals
            && dbAssignedSite.UseOneMinuteIntervalsFrom == null)
        {
            dbAssignedSite.UseOneMinuteIntervalsFrom = now;
        }
    }

    /// <summary>
    /// Resolves the mode for ONE row under the full precedence (write-time
    /// marker → stored effective date → derived timeline), querying
    /// AssignedSiteVersions only when neither of the first two can answer.
    /// Use this from calc paths that hold a single row; loops that already
    /// build a timeline should keep using
    /// <c>row.RegisteredUnderOneMinuteIntervals ?? timeline.WasOneMinuteAt(row.Date)</c>,
    /// which carries the same precedence because <see cref="WasOneMinuteAt"/>
    /// consults the effective date first.
    ///
    /// NEVER call this in a loop: on a legacy row of an un-backfilled site it
    /// falls through to <see cref="BuildAsync"/>, so a per-row call is the
    /// exact N+1 this class exists to avoid. Build a timeline once instead.
    /// </summary>
    public static async Task<bool> ResolveRowModeAsync(
        TimePlanningPnDbContext dbContext, AssignedSite? assignedSite, PlanRegistration row)
    {
        if (row.RegisteredUnderOneMinuteIntervals.HasValue)
        {
            return row.RegisteredUnderOneMinuteIntervals.Value;
        }

        if (assignedSite == null)
        {
            return false;
        }

        var byEffectiveDate = ResolveByEffectiveDate(
            assignedSite.UseOneMinuteIntervals, assignedSite.UseOneMinuteIntervalsFrom, row.Date);
        if (byEffectiveDate.HasValue)
        {
            return byEffectiveDate.Value;
        }

        var timeline = await BuildAsync(dbContext, assignedSite);
        return timeline.WasOneMinuteAt(row.Date);
    }

    /// <summary>
    /// The <c>UseOneMinuteIntervals</c> value in force on <paramref name="rowDate"/>
    /// (date-only comparison; the time component is ignored). The site's
    /// recorded <c>UseOneMinuteIntervalsFrom</c> wins when present; only when
    /// nothing is recorded does the AssignedSiteVersions-derived walk answer.
    /// </summary>
    public bool WasOneMinuteAt(DateTime rowDate)
    {
        var byEffectiveDate = ResolveByEffectiveDate(_currentFlag, _effectiveFrom, rowDate);
        if (byEffectiveDate.HasValue)
        {
            return byEffectiveDate.Value;
        }

        var date = rowDate.Date;
        var value = _initialValue;
        // Walk ALL change points in save order (no early break): the LAST save
        // whose date is on/before the row's date wins, which stays correct even
        // if UpdatedAt values are not strictly monotonic across version rows.
        foreach (var (changeDate, newValue) in _changePoints)
        {
            if (changeDate <= date)
            {
                value = newValue;
            }
        }
        return value;
    }
}
