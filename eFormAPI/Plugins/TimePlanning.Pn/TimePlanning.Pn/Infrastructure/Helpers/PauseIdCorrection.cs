using System;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Single source of truth for detecting the absolute-stop-tick pauseNId
/// corruption (5-minute sites) and computing the corrected duration id.
/// Used by both the startup batch repair (CorruptedPauseIdRepair) and the
/// on-save guard (TimePlanningWorkingHoursService.SelfHealCorruptPauseIds).
/// </summary>
public static class PauseIdCorrection
{
    public const int MinutesPerTick = 5;
    public const int CorruptionToleranceMinutes = 15;

    /// <summary>
    /// Returns the corrected pauseNId when the slot carries the absolute-tick
    /// corruption (decoded break overstates the real pause, from the
    /// timestamps, by more than the tolerance), otherwise null (correct,
    /// off-by-one, zero, or no usable timestamps — leave as-is).
    /// </summary>
    public static int? CorrectedPauseId(DateTime? start, DateTime? stop, int currentId)
    {
        if (currentId <= 0 || start is null || stop is null || stop <= start)
            return null;

        var actualMinutes = (stop.Value - start.Value).TotalMinutes;
        var decodedMinutes = (currentId - 1) * MinutesPerTick;
        if (decodedMinutes - actualMinutes <= CorruptionToleranceMinutes)
            return null;

        var corrected = (int)(actualMinutes / (double)MinutesPerTick) + 1;
        return corrected == currentId ? null : corrected;
    }
}
