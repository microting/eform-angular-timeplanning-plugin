using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data;
using AssignedSite = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

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
/// Cost: ONE query per site (<see cref="BuildAsync"/>); lookups are pure
/// in-memory. Build once per site per request scope — never per row.
/// </summary>
public sealed class OneMinuteModeTimeline
{
    private readonly bool _initialValue;

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
    /// </summary>
    internal OneMinuteModeTimeline(
        bool currentFlag,
        IReadOnlyList<(bool UseOneMinuteIntervals, DateTime SavedAt)> versionFlags)
    {
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
    /// without audit rows yields a constant timeline of the current flag.
    /// </summary>
    public static async Task<OneMinuteModeTimeline> BuildAsync(
        TimePlanningPnDbContext dbContext, AssignedSite assignedSite)
    {
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
                .ToList());
    }

    /// <summary>
    /// The <c>UseOneMinuteIntervals</c> value in force on <paramref name="rowDate"/>
    /// (date-only comparison; the time component is ignored).
    /// </summary>
    public bool WasOneMinuteAt(DateTime rowDate)
    {
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
