#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using Sentry;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// #5489 guarded addition 1, shared by every plugin place that writes the
/// weekday template into a PlanRegistration row
/// (PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod,
/// PlanRegistrationHelper.UpdatePlanRegistration,
/// TimeSettingService.UpdateAssignedSite) and by PlanPatternApplyService.
///
/// When the worker has an active PlanPattern that governs the row's date, the
/// row takes its plan from the pattern instead of the template. Otherwise
/// nothing here touches the row and the caller's existing path runs
/// unchanged, so a worker without a pattern behaves exactly as before.
///
/// Callers keep their own skip rules (PlanChangedByAdmin, the payment
/// cut-off, the day lock); this class only decides what the plan is.
/// </summary>
public static class PlanPatternPlanner
{
    /// <summary>
    /// The worker's active pattern with its live days, or null when they have
    /// none. Load it once per worker, not once per row.
    /// </summary>
    public static Task<PlanPattern?> LoadActivePatternAsync(TimePlanningPnDbContext dbContext, int assignedSiteId) =>
        dbContext.PlanPatterns
            .AsNoTracking()
            .Include(x => x.Days.Where(d => d.WorkflowState != Constants.WorkflowStates.Removed))
            .Where(x => x.AssignedSiteId == assignedSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// The plan text <paramref name="pattern"/> prescribes for
    /// <paramref name="date"/>: null when there is no pattern or it does not
    /// govern the date, "" for a governed day with no plan.
    ///
    /// A pattern the resolver rejects (rotation outside 1..8, an anchor that
    /// is not a Monday) is reported and treated as not governing, so the
    /// caller falls back to its existing path rather than failing the request.
    /// </summary>
    public static string? ResolvePlanText(PlanPattern? pattern, DateTime date, ILogger? logger = null)
    {
        if (pattern == null)
        {
            return null;
        }

        try
        {
            return PlanPatternResolver.PlanTextFor(pattern, pattern.Days, date);
        }
        catch (ArgumentOutOfRangeException e)
        {
            SentrySdk.CaptureException(e);
            Console.WriteLine(
                $"PlanPattern {pattern.Id} for AssignedSite {pattern.AssignedSiteId} is invalid; using the existing plan path: {e.Message}");
            logger?.LogError(e,
                "PlanPattern {PlanPatternId} for AssignedSite {AssignedSiteId} is invalid; using the existing plan path",
                pattern.Id, pattern.AssignedSiteId);
            return null;
        }
    }

    /// <summary>
    /// Writes <paramref name="planText"/> into the row: PlanText, the five
    /// planned shifts and PlanHours. "" means no plan: every shift and
    /// PlanHours become 0. MessageId and the absence flags are not touched.
    /// </summary>
    public static void ApplyPlanText(PlanRegistration planRegistration, string planText)
    {
        planRegistration.PlanText = planText;
        // ParseInto only derives PlanHours from text that describes a shift;
        // a pattern day is the whole plan, so anything else means no hours.
        planRegistration.PlanHours = 0;
        PlanRegistrationPlanText.ParseInto(planRegistration);
    }

    /// <summary>
    /// True when the row carries content an accepted content handover moved
    /// onto or off it. ContentHandoverService.AcceptAsync is the only writer
    /// of ContentHandoverRequestId, so a stamped row is always the result of
    /// an accepted handover, and the pattern must not regenerate it.
    /// </summary>
    public static bool IsAcceptedHandoverDay(PlanRegistration planRegistration) =>
        planRegistration.ContentHandoverRequestId != null;

    /// <summary>
    /// Applies the pattern to the row when it governs the row's date.
    /// A governed day that holds an accepted content handover is left as it
    /// is: the pattern owns the day, so the template must not run either.
    /// </summary>
    /// <returns>
    /// true when the pattern governs the row (it now carries the pattern's
    /// plan, or keeps its handover); false when the caller must write the plan
    /// the way it always has.
    /// </returns>
    public static bool TryApply(PlanPattern? pattern, PlanRegistration planRegistration, ILogger? logger = null)
    {
        var planText = ResolvePlanText(pattern, planRegistration.Date, logger);
        if (planText == null)
        {
            return false;
        }

        if (!IsAcceptedHandoverDay(planRegistration))
        {
            ApplyPlanText(planRegistration, planText);
        }

        return true;
    }
}
