using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Pure derivation of a PauseNId from the detailed pause start/stop slots of a
/// <see cref="PlanRegistration"/>.
///
/// Historically the admin path accumulated total pause minutes into
/// <c>ShiftNPauseNumber</c> only to derive <c>PauseNId = minutes / 5</c>. That
/// overloaded the column, which the punch-clock path uses as a slot counter.
/// This helper reproduces the exact same integer math without writing the column,
/// so the resulting PauseNId is byte-identical to the previous behaviour.
/// </summary>
public static class PauseMinutesCalculator
{
    /// <summary>
    /// Sums the duration (in whole minutes, truncated) of every populated pause slot
    /// belonging to <paramref name="shift"/> and returns <c>totalMinutes / 5</c>.
    /// Reads only; never mutates <paramref name="planning"/>.
    /// </summary>
    /// <param name="planning">The plan registration to read pause slots from.</param>
    /// <param name="shift">The shift to derive the pause id for; must be 1 or 2.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="planning"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="shift"/> is not 1 or 2.</exception>
    public static int DerivePauseId(PlanRegistration planning, int shift)
    {
        ArgumentNullException.ThrowIfNull(planning);

        var totalMinutes = shift switch
        {
            // Exact slot set the admin path accumulated for shift 1 (15 slots):
            // Pause1, Pause2, Pause10..Pause19, Pause100, Pause101, Pause102.
            1 => SlotMinutes(planning.Pause1StartedAt, planning.Pause1StoppedAt)
                 + SlotMinutes(planning.Pause2StartedAt, planning.Pause2StoppedAt)
                 + SlotMinutes(planning.Pause10StartedAt, planning.Pause10StoppedAt)
                 + SlotMinutes(planning.Pause11StartedAt, planning.Pause11StoppedAt)
                 + SlotMinutes(planning.Pause12StartedAt, planning.Pause12StoppedAt)
                 + SlotMinutes(planning.Pause13StartedAt, planning.Pause13StoppedAt)
                 + SlotMinutes(planning.Pause14StartedAt, planning.Pause14StoppedAt)
                 + SlotMinutes(planning.Pause15StartedAt, planning.Pause15StoppedAt)
                 + SlotMinutes(planning.Pause16StartedAt, planning.Pause16StoppedAt)
                 + SlotMinutes(planning.Pause17StartedAt, planning.Pause17StoppedAt)
                 + SlotMinutes(planning.Pause18StartedAt, planning.Pause18StoppedAt)
                 + SlotMinutes(planning.Pause19StartedAt, planning.Pause19StoppedAt)
                 + SlotMinutes(planning.Pause100StartedAt, planning.Pause100StoppedAt)
                 + SlotMinutes(planning.Pause101StartedAt, planning.Pause101StoppedAt)
                 + SlotMinutes(planning.Pause102StartedAt, planning.Pause102StoppedAt),

            // Exact slot set the admin path accumulated for shift 2 (13 slots):
            // Pause20..Pause29, Pause200, Pause201, Pause202.
            2 => SlotMinutes(planning.Pause20StartedAt, planning.Pause20StoppedAt)
                 + SlotMinutes(planning.Pause21StartedAt, planning.Pause21StoppedAt)
                 + SlotMinutes(planning.Pause22StartedAt, planning.Pause22StoppedAt)
                 + SlotMinutes(planning.Pause23StartedAt, planning.Pause23StoppedAt)
                 + SlotMinutes(planning.Pause24StartedAt, planning.Pause24StoppedAt)
                 + SlotMinutes(planning.Pause25StartedAt, planning.Pause25StoppedAt)
                 + SlotMinutes(planning.Pause26StartedAt, planning.Pause26StoppedAt)
                 + SlotMinutes(planning.Pause27StartedAt, planning.Pause27StoppedAt)
                 + SlotMinutes(planning.Pause28StartedAt, planning.Pause28StoppedAt)
                 + SlotMinutes(planning.Pause29StartedAt, planning.Pause29StoppedAt)
                 + SlotMinutes(planning.Pause200StartedAt, planning.Pause200StoppedAt)
                 + SlotMinutes(planning.Pause201StartedAt, planning.Pause201StoppedAt)
                 + SlotMinutes(planning.Pause202StartedAt, planning.Pause202StoppedAt),

            _ => throw new ArgumentOutOfRangeException(
                nameof(shift), shift, "Only shift 1 and 2 are supported."),
        };

        return totalMinutes / 5;
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
