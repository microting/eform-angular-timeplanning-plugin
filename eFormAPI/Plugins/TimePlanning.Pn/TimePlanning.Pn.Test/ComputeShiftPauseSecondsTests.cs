using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression locks for PlanRegistrationHelper.ComputeShiftPauseSeconds — the
/// canonical per-shift pause total that must sum EVERY populated pause slot
/// (primary Pause{N} plus the multi-pause sub-slots), not just the primary.
///
/// Shape is taken from production PlanRegistration 16288 (an OFF / 5-minute
/// site) which carried 4 genuine shift-1 pauses but only deducted ~5 min of
/// pause because the netto math read the primary Pause1 slot alone.
///
/// Worked oracle (all UTC, computed purely from the stored DateTimes):
///   shift 1 (OFF, floor-to-5min clock-tick per slot):
///     Pause1  05:21:57 -> 05:24:39  = 0  min (no 5-min boundary crossed)
///     Pause10 05:25:06 -> 05:33:49  = 5  min (one boundary: 05:30)
///     Pause11 05:40:46 -> 06:01:38  = 20 min (05:45,05:50,05:55,06:00)
///     Pause12 19:09:23 -> 19:10:11  = 5  min (19:10)
///     => shift 1 = 30 min
///   shift 2 (OFF):
///     Pause2  19:13:26 -> 19:16:52  = 5  min (19:15)
///     => shift 2 = 5 min
///   day total = 35 min.
///
///   shift 1 (ON, exact second deltas):
///     162 + 523 + 1252 + 48 = 1985 s (≈ 33 min).
/// </summary>
[TestFixture]
public class ComputeShiftPauseSecondsTests
{
    private static PlanRegistration BuildRow16288Shape()
    {
        var date = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

        return new PlanRegistration
        {
            Date = date,
            // Shift boundaries so a gross work span is computable. Start1 05:00,
            // Stop1 20:00 via 5-min IDs (Id = minutes/5 + 1): 05:00 -> 61, 20:00 -> 241.
            Start1Id = 61,
            Stop1Id = 241,
            Start2Id = 61,
            Stop2Id = 241,

            // Shift 1 pauses — primary + three sub-slots (matches 16288).
            Pause1StartedAt = new DateTime(2026, 6, 19, 5, 21, 57, DateTimeKind.Utc),
            Pause1StoppedAt = new DateTime(2026, 6, 19, 5, 24, 39, DateTimeKind.Utc),
            Pause10StartedAt = new DateTime(2026, 6, 19, 5, 25, 6, DateTimeKind.Utc),
            Pause10StoppedAt = new DateTime(2026, 6, 19, 5, 33, 49, DateTimeKind.Utc),
            Pause11StartedAt = new DateTime(2026, 6, 19, 5, 40, 46, DateTimeKind.Utc),
            Pause11StoppedAt = new DateTime(2026, 6, 19, 6, 1, 38, DateTimeKind.Utc),
            Pause12StartedAt = new DateTime(2026, 6, 19, 19, 9, 23, DateTimeKind.Utc),
            Pause12StoppedAt = new DateTime(2026, 6, 19, 19, 10, 11, DateTimeKind.Utc),

            // Shift 2 pause — single primary slot.
            Pause2StartedAt = new DateTime(2026, 6, 19, 19, 13, 26, DateTimeKind.Utc),
            Pause2StoppedAt = new DateTime(2026, 6, 19, 19, 16, 52, DateTimeKind.Utc),
        };
    }

    [Test]
    public void OffMode_Shift1_SumsAllSlots_FloorTo5Min_Returns30Min()
    {
        var reg = BuildRow16288Shape();

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals: false);

        Assert.That(result, Is.EqualTo(30 * 60),
            "Shift 1 OFF: 0 + 5 + 20 + 5 = 30 min across Pause1/Pause10/Pause11/Pause12");
    }

    [Test]
    public void OffMode_Shift2_SinglePrimarySlot_Returns5Min()
    {
        var reg = BuildRow16288Shape();

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 2, useOneMinuteIntervals: false);

        Assert.That(result, Is.EqualTo(5 * 60),
            "Shift 2 OFF: Pause2 19:13:26 -> 19:16:52 crosses 19:15 = 5 min");
    }

    [Test]
    public void OnMode_Shift1_SumsExactSecondDeltasAcrossAllSlots_Returns1985s()
    {
        var reg = BuildRow16288Shape();

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals: true);

        // 162 (2m42s) + 523 (8m43s) + 1252 (20m52s) + 48 (0m48s) = 1985 s.
        Assert.That(result, Is.EqualTo(1985),
            "Shift 1 ON: exact second deltas 162 + 523 + 1252 + 48 = 1985 s");
    }

    [Test]
    public void OnMode_Shift2_ExactSecondDelta_Returns206s()
    {
        var reg = BuildRow16288Shape();

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 2, useOneMinuteIntervals: true);

        // 19:16:52 - 19:13:26 = 3m26s = 206 s.
        Assert.That(result, Is.EqualTo(206),
            "Shift 2 ON: Pause2 exact delta = 3m26s = 206 s");
    }

    /// <summary>
    /// Day-total guard: the OFF-mode pause aggregation (primary + sub-slots
    /// across both shifts) must total 35 min, matching the 16288 oracle. This
    /// is the value that NettoHours must deduct, not the ~5 min the old
    /// primary-slot-only math produced.
    /// </summary>
    [Test]
    public void OffMode_DayTotalAcrossShifts_Returns35Min()
    {
        var reg = BuildRow16288Shape();

        var shift1 = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals: false);
        var shift2 = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 2, useOneMinuteIntervals: false);

        Assert.That(shift1 + shift2, Is.EqualTo(35 * 60),
            "Day total OFF = 30 + 5 = 35 min");
    }

    /// <summary>
    /// Fallback contract: a shift with NO timestamped pause slot falls back to
    /// the legacy primary Pause{N}Id tick value only. Pause3Id = 4 => (4-1)*5
    /// = 15 min = 900 s, regardless of flag.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void NoTimestampedSlot_FallsBackToLegacyPrimaryPauseId(bool useOneMinuteIntervals)
    {
        var reg = new PlanRegistration
        {
            Date = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc),
            Pause3Id = 4, // legacy 15-min pause: (4 - 1) * 5 = 15 min
        };

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 3, useOneMinuteIntervals);

        Assert.That(result, Is.EqualTo(15 * 60),
            "No timestamped slot => legacy primary Pause3Id fallback = 15 min");
    }

    /// <summary>
    /// Orphaned (incomplete) slot contract: a shift whose only pause stamp has a
    /// start but a null stop (kiosk crash / partial edit) does NOT count as a
    /// complete, measurable slot — so the legacy primary Pause{N}Id fallback is
    /// honored instead of silently dropping the slot AND returning 0. With
    /// Pause1StartedAt set, Pause1StoppedAt null and Pause1Id = 4 the result must
    /// fall back to (4-1)*5 = 15 min = 900 s, regardless of flag.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void OrphanedStartOnlySlot_FallsBackToLegacyPrimaryPauseId(bool useOneMinuteIntervals)
    {
        var reg = new PlanRegistration
        {
            Date = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc),
            Pause1StartedAt = new DateTime(2026, 6, 19, 5, 21, 57, DateTimeKind.Utc),
            Pause1StoppedAt = null, // orphaned: no stop => not a complete slot
            Pause1Id = 4, // legacy 15-min pause: (4 - 1) * 5 = 15 min
        };

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals);

        Assert.That(result, Is.EqualTo(15 * 60),
            "Orphaned start-only slot is not a complete slot => legacy Pause1Id fallback = 15 min");
    }

    /// <summary>
    /// A half-record (StartedAt set, StoppedAt null — e.g. kiosk crashed
    /// mid-break) is not a complete, measurable slot, so it must NOT suppress
    /// the legacy Pause{N}Id fallback. Pause1Id = 4 => (4-1)*5 = 15 min = 900 s.
    /// </summary>
    [Test]
    public void PartialTimestamp_StartedAtOnly_FallsBackToLegacyPauseId()
    {
        var reg = new PlanRegistration {
            Date = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc),
            Pause1Id = 4,  // legacy 15 min
            Pause1StartedAt = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc),
            Pause1StoppedAt = null,
        };
        Assert.That(PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals: false), Is.EqualTo(900),
            "Half-record (StartedAt only) must not suppress the legacy PauseId fallback");
    }
}
