using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// One-shot, idempotent repair for pauseNId corruption introduced by the
/// mobile punch-clock sending the absolute stop-time tick instead of the
/// pause duration on 5-minute sites. Scoped to the last 7 days and to
/// 5-minute sites. Recomputes pauseNId from the intact pause timestamps,
/// writing only when a row is both clearly corrupted and confidently
/// repairable. Safe to run on every startup.
///
/// Netto consistency: on 5-minute (flag-off) sites the mobile save path
/// (TimePlanningPlanningService.UpdateByCurrentUserNam) persists netto via the
/// legacy 5-minute-tick pause math — the final writer on that path is
/// PlanRegistrationHelper.ComputeTimeTrackingFields, which derives
/// NettoHoursInSeconds/NettoHours as work − (pauseNId − 1) * 300 seconds. With
/// the corrupt absolute-tick pauseNId that yields a grossly wrong (huge) break,
/// so the persisted netto for the corrupted rows is ALSO corrupt. After fixing
/// pauseNId we therefore re-run that exact same writer
/// (ComputeTimeTrackingFields) on each corrected row, so the persisted netto is
/// recomputed from the now-correct pauseNId, byte-identical to what the save
/// path would have written. (We deliberately do NOT use
/// ApplyNettoFlexChainSecondPrecision here: that is the flag-ON / seconds-column
/// writer and is never invoked for flag-off sites in production; its SumFlex*
/// seed column SumFlexEndInSeconds is not maintained on flag-off rows.) The
/// flag-off SumFlex* doubles are recomputed on read by the working-hours
/// overview from the corrected NettoHours, so no separate flex-chain rewrite is
/// needed here.
///
/// Limitation: the repair only acts on rows whose pause timestamps are intact
/// and overstate the decoded pauseNId by &gt; the tolerance. A corrupted row
/// whose pause timestamps happen to have been back-filled from the corrupt id
/// (EnsureTimestampsFromIds, only when both stamps were null on a
/// non-detailed-pause save) will have stamps that agree with the corrupt id and
/// is conservatively left untouched — such a row carries no independent record
/// of the true pause duration, so it cannot be confidently repaired.
/// </summary>
public static class CorruptedPauseIdRepair
{
    /// <summary>
    /// Fallback "implausibly large" span used by the anomaly heuristic when the
    /// shift-1 worked span is unknown. 12h (720 min).
    /// </summary>
    private const double UnknownSpanFallbackMinutes = 720;

    public static async Task Run(TimePlanningPnDbContext dbContext)
    {
        var windowStart = DateTime.UtcNow.Date.AddDays(-7);

        // 5-minute sites only.
        var fiveMinuteSiteIds = await dbContext.AssignedSites
            .Where(s => s.WorkflowState != Constants.WorkflowStates.Removed
                        && !s.UseOneMinuteIntervals)
            .Select(s => s.SiteId)
            .ToListAsync()
            .ConfigureAwait(false);

        if (fiveMinuteSiteIds.Count == 0) return;

        var rows = await dbContext.PlanRegistrations
            .AsTracking()
            .Where(p => p.WorkflowState != Constants.WorkflowStates.Removed
                        && p.Date >= windowStart
                        && fiveMinuteSiteIds.Contains(p.SdkSitId))
            .ToListAsync()
            .ConfigureAwait(false);

        // Observability counters (steps 1-3). They do not influence detection
        // or correction behaviour in any way.
        var rowsScanned = 0;
        var rowsCorrected = 0;
        var slotsCorrected = 0;
        var anomaliesFlagged = 0;

        foreach (var pr in rows)
        {
            rowsScanned++;

            // Worked span of shift 1, used only to gauge whether an
            // unrepairable id is implausibly large (anomaly heuristic).
            double? workedMinutes = null;
            if (pr.Start1StartedAt is not null && pr.Stop1StoppedAt is not null
                && pr.Stop1StoppedAt > pr.Start1StartedAt)
            {
                workedMinutes = (pr.Stop1StoppedAt.Value - pr.Start1StartedAt.Value).TotalMinutes;
            }

            // Evaluate every slot (no short-circuit) so each FixSlot side
            // effect (assign) runs and every slot is inspected for anomalies.
            var r1 = FixSlot(pr.Pause1StartedAt, pr.Pause1StoppedAt, pr.Pause1Id, v => pr.Pause1Id = v);
            var r2 = FixSlot(pr.Pause2StartedAt, pr.Pause2StoppedAt, pr.Pause2Id, v => pr.Pause2Id = v);
            var r3 = FixSlot(pr.Pause3StartedAt, pr.Pause3StoppedAt, pr.Pause3Id, v => pr.Pause3Id = v);
            var r4 = FixSlot(pr.Pause4StartedAt, pr.Pause4StoppedAt, pr.Pause4Id, v => pr.Pause4Id = v);
            var r5 = FixSlot(pr.Pause5StartedAt, pr.Pause5StoppedAt, pr.Pause5Id, v => pr.Pause5Id = v);

            var slotResults = new[] { r1, r2, r3, r4, r5 };
            var changed = false;

            for (var i = 0; i < slotResults.Length; i++)
            {
                var slot = i + 1;
                var result = slotResults[i];

                if (result.Corrected)
                {
                    changed = true;
                    slotsCorrected++;
                    // Step 1: log on the actual write path only.
                    Console.WriteLine($"[CorruptedPauseIdRepair] fixed PlanRegistration {pr.Id} site {pr.SdkSitId} pause{slot}: {result.OldValue} -> {result.NewValue} (actual {result.ActualMinutes:F1} min)");
                }
                else if (IsUnrepairableAnomaly(result, workedMinutes))
                {
                    anomaliesFlagged++;
                    // Step 2: corrupt-looking but no usable timestamps to repair
                    // from -> warn to console AND Sentry for manual review.
                    Console.WriteLine($"[CorruptedPauseIdRepair] WARNING: PlanRegistration {pr.Id} site {pr.SdkSitId} pause{slot} id={result.OldValue} looks corrupt but has no usable timestamps to repair from; needs manual review.");
                    SentrySdk.CaptureMessage($"CorruptedPauseIdRepair: unrepairable corrupt pauseId on PlanRegistration {pr.Id} (site {pr.SdkSitId}, pause{slot}, id={result.OldValue})", SentryLevel.Warning);
                }
            }

            if (!changed) continue;

            rowsCorrected++;

            // Re-establish persisted netto from the now-correct pauseNId using
            // the same writer the flag-off save path uses as its final step, so
            // the stored netto matches what a normal save would have written.
            PlanRegistrationHelper.ComputeTimeTrackingFields(pr);

            await pr.Update(dbContext).ConfigureAwait(false);
        }

        // Step 3: run summary.
        Console.WriteLine($"[CorruptedPauseIdRepair] summary: scanned {rowsScanned} rows, corrected {rowsCorrected} rows ({slotsCorrected} slots), flagged {anomaliesFlagged} anomalies.");
    }

    /// <summary>
    /// Per-slot outcome of <see cref="FixSlot"/>. Carries enough data for the
    /// caller to log a correction or flag an unrepairable anomaly. Purely
    /// observational — detection/correction behaviour is unchanged.
    /// </summary>
    private readonly struct SlotResult
    {
        public bool Corrected { get; init; }

        /// <summary>The slot's pauseNId as found before any correction.</summary>
        public int OldValue { get; init; }

        /// <summary>The corrected pauseNId (only meaningful when Corrected).</summary>
        public int NewValue { get; init; }

        /// <summary>Timestamp-derived pause duration in minutes, or null when
        /// the pause timestamps were missing/unusable.</summary>
        public double? ActualMinutes { get; init; }
    }

    /// <summary>
    /// A slot is an unrepairable anomaly when it has a positive pauseNId but no
    /// usable pause timestamps to repair from (ActualMinutes is null), and the
    /// id alone is implausibly large: its decoded minutes (pauseNId-1)*5 exceed
    /// the shift-1 worked span, or — when that span is unknown — exceed 12h.
    /// </summary>
    private static bool IsUnrepairableAnomaly(SlotResult result, double? workedMinutes)
    {
        if (result.Corrected) return false;
        if (result.OldValue <= 0) return false;
        // Only the rows we could NOT repair from timestamps qualify.
        if (result.ActualMinutes is not null) return false;

        var decodedMinutes = (result.OldValue - 1) * PauseIdCorrection.MinutesPerTick;
        var implausibleSpan = workedMinutes ?? UnknownSpanFallbackMinutes;
        return decodedMinutes > implausibleSpan;
    }

    // Returns the slot outcome: corrected (with old/new/actualMinutes) when the
    // slot was clearly absolute-tick corrupted and confidently repairable,
    // otherwise untouched. Detection and correction logic are byte-identical to
    // the pre-logging version.
    private static SlotResult FixSlot(DateTime? start, DateTime? stop, int currentId, Action<int> assign)
    {
        // Delegate the detect+correct decision to the shared single-source-of-truth
        // helper, so the batch repair and the on-save guard apply byte-identical
        // rules. The corrected value and the >15 min tolerance are unchanged.
        var corrected = PauseIdCorrection.CorrectedPauseId(start, stop, currentId);
        if (corrected is null)
        {
            // Preserve the actualMinutes value used by the anomaly heuristic /
            // logging when the timestamps exist.
            double? actual = (start.HasValue && stop.HasValue && stop.Value > start.Value)
                ? (stop.Value - start.Value).TotalMinutes
                : (double?)null;
            return new SlotResult { Corrected = false, OldValue = currentId, ActualMinutes = actual };
        }

        var actualMinutes = (stop!.Value - start!.Value).TotalMinutes;
        assign(corrected.Value);
        return new SlotResult
        {
            Corrected = true,
            OldValue = currentId,
            NewValue = corrected.Value,
            ActualMinutes = actualMinutes,
        };
    }
}
