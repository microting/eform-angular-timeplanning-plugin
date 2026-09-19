#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.Reconciliation;

using System;

/// <summary>
/// GET api/time-planning-pn/reconciliation/summary. Consumed by my-microting's
/// customer-stats scan, so the wire shape is a contract: the host serialises
/// with Newtonsoft + camelCase. Dates are pre-formatted "yyyy-MM-dd" strings
/// so no serializer setting can turn them into instants.
/// </summary>
public class ReconciliationSummaryModel
{
    /// <summary>The cutoff actually used, clamped to 1..31 (19 when no settings row).</summary>
    public int CutoffDay { get; set; }

    /// <summary>yyyy-MM-dd, inclusive.</summary>
    public string PeriodStart { get; set; } = "";

    /// <summary>yyyy-MM-dd, inclusive; always before today (UTC).</summary>
    public string PeriodEnd { get; set; } = "";

    /// <summary>
    /// Workers with planned or worked hours on a live row in the period.
    /// Worked time is read from the one-minute fields (NettoHoursInSeconds,
    /// Start1StartedAt); planned time is read from the PlanHours double,
    /// since PlanHoursInSeconds is written only after a content handover.
    /// </summary>
    public int WorkersInPeriod { get; set; }

    /// <summary>Of those, workers whose boundary is on or after PeriodEnd.</summary>
    public int WorkersLockedThroughPeriod { get; set; }

    /// <summary>Of those, workers with no boundary at all.</summary>
    public int WorkersNeverReconciled { get; set; }

    /// <summary>100 * locked / inPeriod, 1 decimal; null when WorkersInPeriod is 0.</summary>
    public double? CoveragePercent { get; set; }

    /// <summary>yyyy-MM-dd, earliest non-null boundary among WorkersInPeriod; null if none.</summary>
    public string? OldestBoundary { get; set; }

    /// <summary>Distinct workers with any live reconciled row, in any period (adoption).</summary>
    public int WorkersWithAnyReconciled { get; set; }

    /// <summary>Latest ReconciledAt over live reconciled rows; Kind Utc so the JSON ends in "Z".</summary>
    public DateTime? LastReconciledAt { get; set; }
}
