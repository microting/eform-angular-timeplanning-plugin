using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Pure derivation of a PauseNId from the detailed pause start/stop slots of a
/// <see cref="PlanRegistration"/>, in the canonical 5-minute tick encoding:
/// <c>PauseNId = minutes / 5 + 1</c> (0 = no pause), decoded everywhere as
/// <c>(id - 1) * 5</c> (base <c>FlexChain.ComputeShiftPauseSeconds</c>). The slot
/// set per shift is <see cref="PlanRegistrationHelper.EnumerateShiftPauseStampPairs"/>,
/// which mirrors the base <c>FlexChain</c> slot set.
/// Never writes <c>ShiftNPauseNumber</c>, which the punch-clock path uses as a
/// slot counter.
/// </summary>
public static class PauseMinutesCalculator
{
    /// <summary>
    /// Sums the duration (in whole minutes, truncated) of every populated pause slot
    /// belonging to <paramref name="shift"/> and returns the tick <c>totalMinutes / 5 + 1</c>,
    /// or 0 when the shift has no pause minutes.
    /// Reads only; never mutates <paramref name="planning"/>.
    /// </summary>
    /// <param name="planning">The plan registration to read pause slots from.</param>
    /// <param name="shift">The shift to derive the pause id for; must be 1 or 2.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="planning"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="shift"/> is not 1 or 2.</exception>
    public static int DerivePauseId(PlanRegistration planning, int shift)
    {
        ArgumentNullException.ThrowIfNull(planning);

        if (shift is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(shift), shift, "Only shift 1 and 2 are supported.");
        }

        var totalMinutes = 0;
        foreach (var (startedAt, stoppedAt) in PlanRegistrationHelper.EnumerateShiftPauseStampPairs(planning, shift))
        {
            totalMinutes += SlotMinutes(startedAt, stoppedAt);
        }

        return totalMinutes > 0 ? totalMinutes / 5 + 1 : 0;
    }

    private static int SlotMinutes(DateTime? startedAt, DateTime? stoppedAt)
    {
        if (startedAt == null || stoppedAt == null)
        {
            return 0;
        }

        return (int)(stoppedAt.Value - startedAt.Value).TotalMinutes;
    }
}
