using System.Threading.Tasks;
using NUnit.Framework;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Intended regression coverage for TimePlanningPlanningService.UpdateByCurrentUserNam
/// ignoring a soft-removed PlanRegistration (the fix adds
/// "WorkflowState != Removed" so it mirrors the sibling Update(int id, ...),
/// which IS covered by PlanningServiceMultiShiftTests).
///
/// UpdateByCurrentUserNam resolves the caller via BaseDbContext.Users -> SDK
/// Worker-by-email -> SiteWorker -> AssignedSite BEFORE it loads the planning
/// row, so the removed-row check can only be reached after seeding a real
/// (Identity) BaseDbContext plus SDK Worker/SiteWorker. That fixture gap is a
/// documented, suite-wide limitation of this C# harness (see the identical
/// [Ignore] rationale on the GetInboxAsync / GetAvailableSitesByCurrentUser /
/// Index-by-current-user tests). The caller-resolution + removed-row behaviour
/// is exercised end-to-end by the Dart gRPC contract suite instead.
/// </summary>
[TestFixture]
public class PlanningUpdateByCurrentUserRemovedRowTests
{
    [Test]
    [Ignore("Follow-up: requires real BaseDbContext.Users (Identity) + SDK Worker/SiteWorker-by-email seeding to reach the planning load. Same suite-wide fixture gap as the other by-current-user tests. Removed-row exclusion is covered end-to-end by the Dart gRPC contract suite, and the sibling Update(int id, ...) removed-row guard is covered by PlanningServiceMultiShiftTests.")]
    public Task UpdateByCurrentUserNam_IgnoresRemovedPlanRegistration_ReturnsPlanningNotFound()
    {
        return Task.CompletedTask;
    }
}
