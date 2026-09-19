#nullable enable
namespace TimePlanning.Pn.Services.ReconciliationSummaryService;

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;

/// <summary>
/// Read-only Afstem statistics for the last closed payroll period (spec §3).
/// Lock semantics come exclusively from DayLockHelper; nothing here decides
/// what "locked" means.
/// </summary>
public class ReconciliationSummaryService(
    TimePlanningPnDbContext dbContext,
    ILogger<ReconciliationSummaryService> logger) : IReconciliationSummaryService
{
    /// <summary>Matches the PayrollIntegrationSettings.CutoffDay entity default.</summary>
    public const int DefaultCutoffDay = 19;

    public const string ErrorMessage = "ErrorWhileReadingReconciliationSummary";

    private const string DateFormat = "yyyy-MM-dd";

    public Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync()
        => GetSummaryAsync(DateTime.UtcNow.Date);

    /// <summary>Test seam: "today" injected so the period is deterministic.</summary>
    internal async Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync(DateTime todayUtc)
    {
        try
        {
            var configuredCutoff = await dbContext.PayrollIntegrationSettings
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.CutoffDay)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            var cutoffDay = Math.Clamp(configuredCutoff ?? DefaultCutoffDay, 1, 31);

            var period = PayrollPeriod.LastClosed(todayUtc, cutoffDay);
            var dayAfterEnd = period.End.AddDays(1);

            // Planned OR worked. Worked time comes from the one-minute fields
            // only (NettoHoursInSeconds, Start1StartedAt): every customer runs
            // UseOneMinuteIntervals=true, so those are authoritative and the
            // legacy NettoHours double is deliberately NOT consulted. Planned
            // time comes from the PlanHours double, not PlanHoursInSeconds:
            // normal planning writers only ever set PlanHours -- the seconds
            // column is written only after a content handover -- so requiring
            // PlanHoursInSeconds would miss ordinary planned-but-never-clocked
            // workers.
            var siteIds = await dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Date >= period.Start && x.Date < dayAfterEnd)
                .Where(x => x.NettoHoursInSeconds > 0 || x.Start1StartedAt != null
                            || x.PlanHoursInSeconds > 0 || x.PlanHours > 0)
                .Select(x => x.SdkSitId)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            var boundaries = await DayLockHelper.LockedThroughForSitesAsync(dbContext, siteIds)
                .ConfigureAwait(false);

            var locked = boundaries.Values.Count(b => DayLockHelper.IsLocked(b, period.End));
            var never = boundaries.Values.Count(b => b is null);
            var oldest = boundaries.Values.Min(); // Min over DateTime? skips nulls; null when none.

            // One aggregate query for both adoption numbers instead of two
            // full scans of BoundaryRows. GroupBy(_ => 1) collapses every row
            // into a single group so Pomelo emits one SELECT with COUNT(DISTINCT ...)
            // and MAX(...); no group means no row, so a null result means "no
            // reconciled rows at all" -- map that to 0 / null explicitly.
            var boundarySummary = await DayLockHelper.BoundaryRows(dbContext)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Sites = g.Select(x => x.SdkSitId).Distinct().Count(),
                    Last = g.Max(x => x.ReconciledAt),
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            var withAny = boundarySummary?.Sites ?? 0;
            var lastReconciledAt = boundarySummary?.Last;

            var model = new ReconciliationSummaryModel
            {
                CutoffDay = cutoffDay,
                PeriodStart = period.Start.ToString(DateFormat, CultureInfo.InvariantCulture),
                PeriodEnd = period.End.ToString(DateFormat, CultureInfo.InvariantCulture),
                WorkersInPeriod = siteIds.Count,
                WorkersLockedThroughPeriod = locked,
                WorkersNeverReconciled = never,
                CoveragePercent = siteIds.Count == 0
                    ? null
                    : Math.Round(100.0 * locked / siteIds.Count, 1, MidpointRounding.AwayFromZero),
                OldestBoundary = oldest?.ToString(DateFormat, CultureInfo.InvariantCulture),
                WorkersWithAnyReconciled = withAny,
                // datetime(6) has no offset, so EF returns Kind Unspecified;
                // SetReconciledAsync writes UtcNow, so re-tag it Utc to put the
                // "Z" on the wire (same as PlanRegistrationHelper's ReconciledAt).
                LastReconciledAt = lastReconciledAt is { } at
                    ? DateTime.SpecifyKind(at, DateTimeKind.Utc)
                    : null,
            };

            return new OperationDataResult<ReconciliationSummaryModel>(true, model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ReconciliationSummaryService.GetSummaryAsync: catch");
            return new OperationDataResult<ReconciliationSummaryModel>(false, ErrorMessage);
        }
    }
}
