using System;
using System.Collections.Generic;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

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

    // ---- Approach C: pause override ----

    /// <summary>
    /// Override wins over the recorded slot sum. The 16288 shape sums to 30 min
    /// across the shift-1 slots, but an override of 12 min must replace it
    /// entirely (the slots stay in the entity as documentation, but are not
    /// summed). Holds in BOTH flag modes — the override short-circuits before
    /// any slot math.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Override_WinsOverSlotSum(bool useOneMinuteIntervals)
    {
        var reg = BuildRow16288Shape();
        reg.Pause1OverrideMinutes = 12;

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals);

        Assert.That(result, Is.EqualTo(12 * 60),
            "Override must replace the slot sum entirely.");
    }

    /// <summary>
    /// Override of 0 means an explicit zero pause for the shift, even though the
    /// recorded slots would otherwise sum to 30 min.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Override_Zero_MeansZeroPause(bool useOneMinuteIntervals)
    {
        var reg = BuildRow16288Shape();
        reg.Pause1OverrideMinutes = 0;

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals);

        Assert.That(result, Is.EqualTo(0),
            "Override = 0 means zero pause, not the slot sum.");
    }

    /// <summary>
    /// Null override (the default) leaves the existing slot-sum behavior intact:
    /// shift 1 OFF = 30 min, exactly as the non-override tests assert.
    /// </summary>
    [Test]
    public void Override_Null_FallsBackToSlotSum()
    {
        var reg = BuildRow16288Shape();
        reg.Pause1OverrideMinutes = null;

        var result = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, useOneMinuteIntervals: false);

        Assert.That(result, Is.EqualTo(30 * 60),
            "Null override must fall back to the all-slots sum (30 min).");
    }

    /// <summary>
    /// A non-null override on shift 1 must NOT leak into shift 2's computation —
    /// shift 2 (no override) still sums its own slot (5 min OFF).
    /// </summary>
    [Test]
    public void Override_IsPerShift_DoesNotLeakAcrossShifts()
    {
        var reg = BuildRow16288Shape();
        reg.Pause1OverrideMinutes = 99;

        var shift2 = PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 2, useOneMinuteIntervals: false);

        Assert.That(shift2, Is.EqualTo(5 * 60),
            "Shift 2 has no override and must still sum its own slot (5 min).");
    }
}

/// <summary>
/// No-Docker unit locks for the pause-override WRITE path (Approach C, Phase 2):
/// the change-detection inference (FIX 1), the explicit web clear (FIX 2), the
/// exact-minute-wins ordering (FIX 4), and EnsureTimestampsFromIds not fabricating
/// observation timestamps under an active override (FIX 3). These exercise the
/// service's internal helpers directly (InternalsVisibleTo) with no DB.
/// </summary>
[TestFixture]
public class PauseOverrideInferenceTests
{
    private static readonly DateTime Date = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

    private static ISet<int> NoneHandled() => new HashSet<int>();

    /// <summary>
    /// FIX 1 (the 16288-shape unit-mismatch bug). A punch-clock row whose recorded
    /// slots sum to a NON-5-minute total (33 min on a one-minute site) and whose
    /// legacy Pause1Id is 0. The client round-trips Break1Shift = pre-edit Pause1Id
    /// = 0 (the value the read path emits) while only start/stop changed. The
    /// override must STAY NULL (no spurious lock) and netto must keep reflecting the
    /// recorded slot sum.
    ///
    /// Pre-fix the baseline compared the coarse tick (0) against the exact slot-sum
    /// (33); 0 ≠ 33 ALWAYS locked an override on every save. The fix compares
    /// Break1Shift against the pre-edit Pause1Id instead.
    /// </summary>
    [Test]
    public void Inference_EditOnlyStartStop_NonFiveMinSlotSum_LeavesOverrideNull()
    {
        var reg = new PlanRegistration
        {
            Date = Date,
            Pause1Id = 0,
            // Two recorded sub-slots summing to 33 min exactly (one-minute site).
            Pause1StartedAt = Date.AddHours(10),
            Pause1StoppedAt = Date.AddHours(10).AddMinutes(13),
            Pause10StartedAt = Date.AddHours(11),
            Pause10StoppedAt = Date.AddHours(11).AddMinutes(20),
        };
        // Sanity: the slot sum is 33 min, not a 5-min multiple.
        Assert.That(PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, true),
            Is.EqualTo(33 * 60), "Pre-condition: slot sum is 33 min.");

        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        var model = new TimePlanningPlanningPrDayModel { Break1Shift = 0 }; // == pre-edit Pause1Id

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, NoneHandled());

        Assert.That(reg.Pause1OverrideMinutes, Is.Null,
            "Unchanged pause (Break == pre-edit Pause1Id) must NOT lock an override.");
        Assert.That(PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, true),
            Is.EqualTo(33 * 60), "Netto pause still reflects the recorded slot sum.");
    }

    /// <summary>
    /// FIX 1: editing the pause to a new coarse value (Break1Shift differs from the
    /// pre-edit Pause1Id) DOES set the override to (Break1Shift - 1) * 5 minutes.
    /// </summary>
    [Test]
    public void Inference_PauseChanged_SetsOverride()
    {
        var reg = new PlanRegistration { Date = Date, Pause1Id = 2 }; // pre-edit tick 2 (= 5 min)
        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        var model = new TimePlanningPlanningPrDayModel { Break1Shift = 5 }; // (5-1)*5 = 20 min

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, NoneHandled());

        Assert.That(reg.Pause1OverrideMinutes, Is.EqualTo(20),
            "Changed Break1Shift (5 ≠ pre-edit 2) sets override to (5-1)*5 = 20 min.");
    }

    /// <summary>
    /// IDEMPOTENT RE-SAVE (conf 82 corruption). A shift already carries a
    /// non-5-multiple exact override (33 min, set via the one-minute path). The
    /// read path serves Break1Shift = (33/5)+1 = 7. On a later UNRELATED save (only
    /// start/stop changed) the client round-trips that served tick (7). The
    /// captured pre-edit SHOWN tick is also 7, so inference must NOT fire and the
    /// exact 33-min override must SURVIVE — not be silently rounded down to
    /// (7-1)*5 = 30. (Pre-fix the baseline was the raw Pause1Id = 6, so 7 ≠ 6 fired
    /// inference and corrupted 33 → 30 on every save.)
    /// </summary>
    [Test]
    public void Inference_ReSaveSameShownTick_NonFiveMinOverride_StaysExact()
    {
        var reg = new PlanRegistration
        {
            Date = Date,
            Pause1OverrideMinutes = 33, // exact one-minute override (not a 5-multiple)
            Pause1Id = 6, // raw legacy tick differs from the served shown tick (7)
        };
        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        // Sanity: the captured shown tick mirrors the read projection ((33/5)+1 = 7).
        Assert.That(preEditShownTicks[1], Is.EqualTo(7),
            "Pre-condition: shown tick = (33/5)+1 = 7 (the value the client round-trips).");

        // Unrelated re-save: client submits the served tick unchanged.
        var model = new TimePlanningPlanningPrDayModel { Break1Shift = 7 };

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, NoneHandled());

        Assert.That(reg.Pause1OverrideMinutes, Is.EqualTo(33),
            "Re-save with the served shown tick is idempotent: the exact 33-min override survives (not rounded to 30).");
    }

    /// <summary>
    /// RE-EDIT of an already-overridden shift still works. Same exact 33-min
    /// override (shown as tick 7), but the user picks a genuinely different break
    /// value (Break1Shift = 9). 9 ≠ shown tick 7 → user changed the pause →
    /// the override updates to (9-1)*5 = 40 min.
    /// </summary>
    [Test]
    public void Inference_ReEditOverriddenShift_DifferentTick_UpdatesOverride()
    {
        var reg = new PlanRegistration
        {
            Date = Date,
            Pause1OverrideMinutes = 33,
            Pause1Id = 6,
        };
        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        var model = new TimePlanningPlanningPrDayModel { Break1Shift = 9 }; // (9-1)*5 = 40 min

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, NoneHandled());

        Assert.That(reg.Pause1OverrideMinutes, Is.EqualTo(40),
            "Re-editing to a different break tick (9 ≠ shown 7) updates the override to (9-1)*5 = 40 min.");
    }

    /// <summary>
    /// FIX 2: an explicit web clear (ClearPauseOverrides) reverts an active override
    /// back to null (compute-from-slots), and SKIPS inference for that shift, so the
    /// pause falls back to the recorded slot sum.
    /// </summary>
    [Test]
    public void ExplicitClear_RevertsOverrideToNull_FallsBackToSlotSum()
    {
        var reg = new PlanRegistration
        {
            Date = Date,
            Pause1OverrideMinutes = 12, // an active override...
            Pause1Id = 0,
            // ...over recorded slots summing to 5 min (OFF grid: 19:13:26 -> 19:16:52).
            Pause1StartedAt = new DateTime(2026, 6, 19, 19, 13, 26, DateTimeKind.Utc),
            Pause1StoppedAt = new DateTime(2026, 6, 19, 19, 16, 52, DateTimeKind.Utc),
        };
        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        // Web sends an explicit clear; Break1Shift would otherwise look "changed".
        var model = new TimePlanningPlanningPrDayModel { ClearPauseOverrides = true, Break1Shift = 99 };

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, NoneHandled());

        Assert.That(reg.Pause1OverrideMinutes, Is.Null,
            "Explicit clear must revert the override to null.");
        Assert.That(PlanRegistrationHelper.ComputeShiftPauseSeconds(reg, 1, false),
            Is.EqualTo(5 * 60), "After clear, pause falls back to the recorded slot sum (5 min).");
    }

    /// <summary>
    /// FIX 2: a per-shift explicit value signal (Pause{N}OverrideMinutesSpecified
    /// with a value) sets that exact override and skips inference for the shift.
    /// </summary>
    [Test]
    public void ExplicitPerShiftValue_SetsOverride_SkipsInference()
    {
        var reg = new PlanRegistration { Date = Date, Pause1Id = 0 };
        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        var model = new TimePlanningPlanningPrDayModel
        {
            Pause1OverrideMinutes = 25,
            Pause1OverrideMinutesSpecified = true,
            Break1Shift = 3, // would infer 10 min, but the explicit value wins
        };

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, NoneHandled());

        Assert.That(reg.Pause1OverrideMinutes, Is.EqualTo(25),
            "Explicit per-shift value wins over the Break1Shift inference.");
    }

    /// <summary>
    /// FIX 4: a shift whose override was already set by the flag-ON exact-minute
    /// path (passed in the handled set) must NOT be overwritten by the coarse
    /// Break{N}Shift inference — the exact-minute value wins.
    /// </summary>
    [Test]
    public void ExactMinuteHandledShift_NotOverwrittenByInference()
    {
        var reg = new PlanRegistration { Date = Date, Pause1Id = 0 };
        // Simulate the exact-minute loop having set 33 min on shift 1.
        PlanRegistrationHelper.SetShiftPauseOverrideMinutes(reg, 1, 33);
        var preEditShownTicks = TimePlanningPlanningService.CaptureCurrentShiftShownTicks(reg);
        var handled = new HashSet<int> { 1 };
        var model = new TimePlanningPlanningPrDayModel { Break1Shift = 5 }; // would infer 20 min

        TimePlanningPlanningService.ApplyInferredPauseOverrides(reg, model, preEditShownTicks, handled);

        Assert.That(reg.Pause1OverrideMinutes, Is.EqualTo(33),
            "Exact-minute override must survive; inference must skip the handled shift.");
    }

    /// <summary>
    /// FIX 3: EnsureTimestampsFromIds must NOT fabricate Pause{N}StartedAt/StoppedAt
    /// for a shift carrying an active override, even when the legacy Pause{N}Id is
    /// non-zero and the observation columns are empty. The override drives the
    /// total; the documentation columns stay untouched (here: null).
    /// </summary>
    [Test]
    public void EnsureTimestampsFromIds_OverrideActive_DoesNotFabricatePauseStamps()
    {
        var reg = new PlanRegistration
        {
            Date = Date,
            Start1StartedAt = Date.AddHours(8),
            Stop1StoppedAt = Date.AddHours(16),
            Pause1Id = 4, // legacy 15-min tick that WOULD synthesize a pause pair...
            Pause1OverrideMinutes = 10, // ...but an override is active.
            Pause1StartedAt = null,
            Pause1StoppedAt = null,
        };

        TimePlanningPlanningService.EnsureTimestampsFromIds(reg);

        Assert.Multiple(() =>
        {
            Assert.That(reg.Pause1StartedAt, Is.Null,
                "Under an active override the pause start must NOT be fabricated from Pause1Id.");
            Assert.That(reg.Pause1StoppedAt, Is.Null,
                "Under an active override the pause stop must NOT be fabricated from Pause1Id.");
        });
    }

    /// <summary>
    /// FIX 3 negative companion: with NO override, the legacy Pause{N}Id synthesis
    /// still runs (existing behavior preserved).
    /// </summary>
    [Test]
    public void EnsureTimestampsFromIds_NoOverride_StillSynthesizesFromPauseId()
    {
        var reg = new PlanRegistration
        {
            Date = Date,
            Start1StartedAt = Date.AddHours(8),
            Stop1StoppedAt = Date.AddHours(16),
            Pause1Id = 4, // (4-1)*5 = 15 min
            Pause1OverrideMinutes = null,
            Pause1StartedAt = null,
            Pause1StoppedAt = null,
        };

        TimePlanningPlanningService.EnsureTimestampsFromIds(reg);

        Assert.Multiple(() =>
        {
            Assert.That(reg.Pause1StartedAt, Is.Not.Null,
                "With no override the legacy synthesis still runs.");
            Assert.That(
                (reg.Pause1StoppedAt!.Value - reg.Pause1StartedAt!.Value).TotalMinutes,
                Is.EqualTo(15), "Synthesized pause spans (Pause1Id-1)*5 = 15 min.");
        });
    }
}
