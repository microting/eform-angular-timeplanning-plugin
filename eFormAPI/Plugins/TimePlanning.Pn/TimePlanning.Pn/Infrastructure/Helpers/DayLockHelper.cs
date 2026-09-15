// NOTE: a deliberate copy of this file lives in eform-service-timeplanning-plugin
// (ServiceTimePlanningPlugin/Infrastructure/Helpers/DayLockHelper.cs). The two
// repos share only the base NuGet package. If you change the lock logic here,
// change the twin too: a divergence lets background jobs write days the web
// refuses. The twin omits the message and reconcile members, which background
// jobs never need.
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

/// <summary>
/// The single source of truth for "is this day locked".
///
/// The lock is DERIVED, never stored. A worker's boundary is the latest date
/// they have a Reconciled registration on; every day at or before it is locked.
/// Earlier days are therefore locked WITHOUT being marked Reconciled, and a day
/// below the boundary cannot be unlocked because unlocking it would not move
/// MAX(Date) -- both requirements fall out of the model instead of needing a
/// job to keep flags in sync.
/// </summary>
public static class DayLockHelper
{
    /// <summary>
    /// The latest reconciled date for one worker, or null when they have none.
    /// Soft-deleted rows never hold the boundary.
    /// </summary>
    public static async Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)
    {
        return await BoundaryRows(db)
            .Where(x => x.SdkSitId == sdkSitId)
            .MaxAsync(x => (DateTime?)x.Date)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Boundaries for many workers in ONE query. Callers that render a grid
    /// resolve this once per request rather than once per day cell.
    /// Every requested site gets an entry; sites with no reconciled day map to null.
    /// </summary>
    public static async Task<Dictionary<int, DateTime?>> LockedThroughForSitesAsync(
        TimePlanningPnDbContext db, IReadOnlyCollection<int> sdkSitIds,
        CancellationToken cancellationToken = default)
    {
        var found = await BoundaryRows(db)
            .Where(x => sdkSitIds.Contains(x.SdkSitId))
            .GroupBy(x => x.SdkSitId)
            .Select(g => new { SdkSitId = g.Key, Max = g.Max(x => x.Date) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = found.ToDictionary(x => x.SdkSitId, x => (DateTime?)x.Max);
        foreach (var id in sdkSitIds)
        {
            map.TryAdd(id, null);
        }
        return map;
    }

    /// <summary>
    /// Pure predicate, so callers can resolve the boundary once and test many
    /// dates against it without touching the database again.
    /// </summary>
    public static bool IsLocked(DateTime? lockedThrough, DateTime date)
        => lockedThrough.HasValue && date.Date <= lockedThrough.Value.Date;

    /// <summary>
    /// <see cref="IsLocked(DateTime?, DateTime)"/> against a boundary map from
    /// <see cref="LockedThroughForSitesAsync"/>. A site missing from the map
    /// has no boundary, so its days are open.
    /// </summary>
    public static bool IsLocked(
        IReadOnlyDictionary<int, DateTime?> lockedThroughBySite, int sdkSitId, DateTime date)
        => IsLocked(lockedThroughBySite.GetValueOrDefault(sdkSitId), date);

    /// <summary>
    /// The rows NOT locked by <paramref name="lockedThrough"/>, as a filter the
    /// database runs. Exactly equivalent to <c>!IsLocked(lockedThrough, x.Date)</c>,
    /// time of day included: date.Date &lt;= lockedThrough.Date holds exactly
    /// when date &lt; lockedThrough.Date + 1 day.
    ///
    /// For bulk writers: a locked row that is never loaded is never tracked,
    /// so no later SaveChanges on the context can flush a change into it.
    /// </summary>
    public static IQueryable<PlanRegistration> WhereOpen(
        this IQueryable<PlanRegistration> query, DateTime? lockedThrough)
    {
        if (lockedThrough is not { } boundary)
        {
            return query;
        }

        var firstOpenDay = boundary.Date.AddDays(1);
        return query.Where(x => x.Date >= firstOpenDay);
    }

    /// <summary>
    /// The message key for a write the lock refuses. It states what the
    /// blocking day IS: reconciled itself, or locked by a later reconciled day.
    /// One rule for every path, so web and mobile say the same (spec §11.3).
    /// </summary>
    public static string LockedMessageKey(bool blockingRowIsReconciled)
        => blockingRowIsReconciled ? "DayIsReconciled" : "DayIsLockedByReconciledDay";

    /// <summary>
    /// <see cref="LockedMessageKey"/> for a locked day whose row is not loaded,
    /// or does not exist (then the day is only locked). One cheap query, so
    /// call it only once the day is known to be locked.
    /// </summary>
    public static async Task<string> LockedMessageKeyAsync(
        TimePlanningPnDbContext db, int sdkSitId, DateTime date)
    {
        var day = date.Date;
        var reconciled = await db.PlanRegistrations
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .AnyAsync(x => x.SdkSitId == sdkSitId && x.Date == day && x.Reconciled)
            .ConfigureAwait(false);
        return LockedMessageKey(reconciled);
    }

    /// <summary>
    /// Invariant I2: today and future days must stay open so time can still be
    /// registered. This is also what makes the forward flex cascades unable to
    /// reach a locked day -- see the design doc before relaxing it.
    ///
    /// DateTime.Now, not UtcNow: PlanRegistration.Date is a local midnight, and
    /// the existing mobile guard compares the same way.
    /// </summary>
    public static bool CanReconcile(DateTime date) => date.Date < DateTime.Now.Date;

    /// <summary>
    /// What counts as a boundary row, in one place: Reconciled and not
    /// soft-deleted. Both public queries compose their own site predicate
    /// over this so the two never drift apart.
    /// </summary>
    private static IQueryable<PlanRegistration> BoundaryRows(TimePlanningPnDbContext db)
        => db.PlanRegistrations
            .Where(x => x.Reconciled)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);
}
