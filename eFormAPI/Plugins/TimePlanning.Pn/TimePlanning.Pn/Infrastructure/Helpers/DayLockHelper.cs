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
