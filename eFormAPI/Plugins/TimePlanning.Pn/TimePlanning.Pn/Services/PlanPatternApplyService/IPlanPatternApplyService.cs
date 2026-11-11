#nullable enable
namespace TimePlanning.Pn.Services.PlanPatternApplyService;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.PlanPattern;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

/// <summary>
/// Saves and deletes a worker's PlanPattern and writes the PlanRegistration
/// rows it governs (#5489). The only writer of PlanPattern rows: it keeps one
/// active pattern per worker, which the database index deliberately does not.
/// </summary>
public interface IPlanPatternApplyService
{
    /// <summary>
    /// Replaces the worker's active pattern and takes the worker off the
    /// Google Sheet, in one transaction; then writes rows from
    /// max(<paramref name="validFrom"/>, today) to today + 180 days and
    /// carries the flex balance forward once. Days an admin changed by hand
    /// are kept unless <paramref name="overwriteManualDays"/>; locked days are
    /// never touched and are returned.
    /// </summary>
    Task<OperationDataResult<PlanPatternApplyResult>> SavePatternAsync(
        int assignedSiteId,
        int rotationWeeks,
        DateTime anchorDate,
        DateTime validFrom,
        IReadOnlyCollection<PlanPatternDayModel> days,
        bool overwriteManualDays);

    /// <summary>
    /// Soft-deletes the worker's active pattern. The worker returns to the
    /// weekday template; they are NOT put back on the Google Sheet. Their rows
    /// from today to today + 180 days are rewritten from the template (manual,
    /// locked and accepted-handover days are kept) and flex is carried once.
    /// </summary>
    Task<OperationResult> DeletePatternAsync(int assignedSiteId);

    /// <summary>
    /// The days an admin changed by hand (PlanChangedByAdmin) that saving a
    /// pattern valid from <paramref name="validFrom"/> would reach.
    /// </summary>
    Task<List<DateTime>> GetManualDaysInRangeAsync(int assignedSiteId, DateTime validFrom);
}
