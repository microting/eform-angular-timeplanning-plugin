using System;
using System.Linq;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pure-function regression coverage for <c>TimePlanningWorkingHoursService.EnumerateShiftSegments</c>,
/// the helper that drives time-band pay-line attribution by yielding one (start, stop)
/// seconds-of-day pair per populated shift. Non-5-min stamps must flow through unchanged;
/// shifts with either stamp missing are skipped per documented intent at
/// <c>TimePlanningWorkingHoursService.cs:3795-3801</c>.
/// </summary>
[TestFixture]
public class EnumerateShiftSegmentsTests
{
    private static DateTime D(int hour, int min) => new DateTime(2026, 5, 15, hour, min, 0);

    [Test]
    public void TwoNonRoundShifts_BothYielded_WithExactDurations()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
            Start2StartedAt = D(13, 7),
            Stop2StoppedAt = D(17, 21),
        };

        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(2));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(7560), "08:04→10:10 = 2h06m = 7560 s");
        Assert.That(segs[1].Stop - segs[1].Start, Is.EqualTo(15240), "13:07→17:21 = 4h14m = 15240 s");
    }

    [Test]
    public void AllStampsNull_NoSegmentsYielded()
    {
        var model = new TimePlanningWorkingHoursModel { Date = new DateTime(2026, 5, 15) };
        Assert.That(TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList(), Is.Empty);
    }

    [Test]
    public void Shift3OnlyPopulated_OnlyShift3Yielded()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start3StartedAt = D(22, 13),
            Stop3StoppedAt = D(23, 47),
        };
        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(1));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(5640), "22:13→23:47 = 1h34m = 5640 s");
    }

    [Test]
    public void Shift1MissingStop_SkippedAsIncomplete()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = null,
            Start2StartedAt = D(13, 7),
            Stop2StoppedAt = D(17, 21),
        };
        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(1));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(15240),
            "Incomplete shift 1 dropped; shift 2 still attributed");
    }

    [Test]
    public void TwoRoundShifts_BothYielded_WithExactDurations()
    {
        // Legacy 5-min-aligned path: ensure no off-by-one when stamps land on
        // slot boundaries (the path used by sites with UseOneMinuteIntervals=false).
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 0),
            Stop1StoppedAt = D(10, 5),
            Start2StartedAt = D(13, 0),
            Stop2StoppedAt = D(17, 15),
        };

        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(2));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(7500), "08:00→10:05 = 2h05m = 7500 s");
        Assert.That(segs[1].Stop - segs[1].Start, Is.EqualTo(15300), "13:00→17:15 = 4h15m = 15300 s");
    }

    [Test]
    public void AllFiveShiftsPopulated_AllYielded()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(6, 4),  Stop1StoppedAt = D(7, 11),
            Start2StartedAt = D(8, 13), Stop2StoppedAt = D(9, 27),
            Start3StartedAt = D(10, 3), Stop3StoppedAt = D(11, 19),
            Start4StartedAt = D(12, 7), Stop4StoppedAt = D(13, 33),
            Start5StartedAt = D(14, 2), Stop5StoppedAt = D(15, 9),
        };
        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(5));
    }

    // ------------------------------------------------------------------
    // EnumerateWorkedSegments invariant: per shift the worked seconds are
    // max(0, span - canonical pause), the canonical pause being
    // FlexChain.ComputeShiftPauseSeconds (override > stamps > legacy id) as
    // carried in Shift{N}PauseMinutes. The stamps may only position the pause,
    // never change its size.
    // ------------------------------------------------------------------

    private static int WorkedSeconds(TimePlanningWorkingHoursModel model)
        => TimePlanningWorkingHoursService.EnumerateWorkedSegments(model).Sum(s => s.Stop - s.Start);

    [TestCase(0, 8 * 3600, TestName = "WorkedSegments_OverrideZeroBelowStamps_WorksFullSpan")]
    [TestCase(10, 8 * 3600 - 10 * 60, TestName = "WorkedSegments_OverrideTenBelowStamps_SubtractsOverrideOnly")]
    public void WorkedSegments_OverrideSmallerThanStamps_OverrideDecides(int overrideMinutes, int expectedSeconds)
    {
        // 07:00-15:00 with a 30-min stamp, but the office set a smaller override:
        // the override is the pause, so the stamps must not be cut on top of it.
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(7, 0), Stop1StoppedAt = D(15, 0),
            Pause1StartedAt = D(12, 0), Pause1StoppedAt = D(12, 30),
            Pause1OverrideMinutes = overrideMinutes,
            Shift1PauseMinutes = overrideMinutes,
        };

        Assert.That(WorkedSeconds(model), Is.EqualTo(expectedSeconds),
            "worked = span - Pause1OverrideMinutes; 7h30 means the 30-min stamp was cut despite the override");
    }

    [Test]
    public void WorkedSegments_FiveMinuteRow_PauseInsideOneTick_CutsNothing()
    {
        // A 5-minute (tick) row: 12:01-12:04 stays inside one 5-min cell, so the
        // canonical tick pause is floor5(12:04) - floor5(12:01) = 0 and netto is the
        // full 8 h. The exact 3 minutes must not be cut. Built the way the export
        // builds it: registration -> PopulateCanonicalPauses -> mode normalization.
        var registration = new PlanRegistration
        {
            Date = new DateTime(2026, 5, 15),
            Start1Id = 85,  // 07:00
            Stop1Id = 181,  // 15:00
            Pause1Id = 1,
            Pause1StartedAt = D(12, 1), Pause1StoppedAt = D(12, 4),
        };
        var model = new TimePlanningWorkingHoursModel
        {
            Date = registration.Date,
            Shift1Start = registration.Start1Id,
            Shift1Stop = registration.Stop1Id,
        };
        TimePlanningWorkingHoursService.PopulateCanonicalPauses(registration, model, isOneMinute: false);
        TimePlanningWorkingHoursService.ApplyModeAtRegistrationStamps(model, rowIsOneMinute: false);

        Assert.That(model.Shift1PauseMinutes, Is.EqualTo(0), "sanity: canonical tick pause is 0");
        Assert.That(WorkedSeconds(model), Is.EqualTo(8 * 3600),
            "tick row: the pause counts 0 canonically, so 8 h are worked; 7h57 means the exact stamp was cut");
    }

    [Test]
    public void WorkedSegments_ShiftOnePauseStampInsideShiftTwo_CountedOnce()
    {
        // Shift 1 07:00-11:00 owns a 30-min pause whose stamp lies inside shift 2
        // (12:00-16:00). It belongs to shift 1 only: shift 1 works 3h30, shift 2 the
        // full 4 h — 7h30 in total, never 7h (pause counted against both shifts).
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(7, 0), Stop1StoppedAt = D(11, 0),
            Start2StartedAt = D(12, 0), Stop2StoppedAt = D(16, 0),
            Pause1StartedAt = D(13, 0), Pause1StoppedAt = D(13, 30),
            Shift1PauseMinutes = 30,
            Shift2PauseMinutes = 0,
        };

        Assert.That(WorkedSeconds(model), Is.EqualTo(7 * 3600 + 30 * 60),
            "shift 1's pause must be subtracted once, from shift 1, not cut out of shift 2 as well");
    }

    [Test]
    public void WorkedSegments_StampedPause_CutAtItsClockPosition()
    {
        // Guard for the normal case: an un-overridden stamped pause keeps its clock
        // position (07:00-12:00 and 12:30-15:00), not trimmed from the shift end.
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(7, 0), Stop1StoppedAt = D(15, 0),
            Pause1StartedAt = D(12, 0), Pause1StoppedAt = D(12, 30),
            Shift1PauseMinutes = 30,
        };

        var segs = TimePlanningWorkingHoursService.EnumerateWorkedSegments(model).ToList();
        Assert.That(segs, Is.EqualTo(new[] { (7 * 3600, 12 * 3600), (12 * 3600 + 1800, 15 * 3600) }));
    }

    /// <summary>
    /// Builds the day model the way the export does: registration ->
    /// PopulateCanonicalPauses -> mode-at-registration normalization.
    /// </summary>
    private static TimePlanningWorkingHoursModel ExportModel(PlanRegistration registration, bool isOneMinute)
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = registration.Date,
            Shift1Start = registration.Start1Id,
            Shift1Stop = registration.Stop1Id,
            Start1StartedAt = registration.Start1StartedAt,
            Stop1StoppedAt = registration.Stop1StoppedAt,
        };
        TimePlanningWorkingHoursService.PopulateCanonicalPauses(registration, model, isOneMinute);
        TimePlanningWorkingHoursService.ApplyModeAtRegistrationStamps(model, isOneMinute);
        return model;
    }

    [Test]
    public void WorkedSegments_TickShiftEndingAt2400_LegacyPauseId_SubtractsPause()
    {
        // 18:00 -> 24:00 (Stop Id 289 derives next-day 00:00) is NOT a cross-midnight
        // shift: the whole span is on this day, so the 30-min legacy pause applies.
        var model = ExportModel(new PlanRegistration
        {
            Date = new DateTime(2026, 5, 15),
            Start1Id = 217, // 18:00
            Stop1Id = 289,  // 24:00
            Pause1Id = 7,   // 30 min, no stamps
        }, isOneMinute: false);

        Assert.That(WorkedSeconds(model), Is.EqualTo(5 * 3600 + 30 * 60),
            "18:00-24:00 minus the 30-min Pause1Id = 5h30; 6h00 means the shift was treated as crossing midnight");
    }

    [Test]
    public void WorkedSegments_TickShiftEndingAt2400_PauseOverride_SubtractsOverride()
    {
        var model = ExportModel(new PlanRegistration
        {
            Date = new DateTime(2026, 5, 15),
            Start1Id = 217, // 18:00
            Stop1Id = 289,  // 24:00
            Pause1OverrideMinutes = 30,
        }, isOneMinute: false);

        Assert.That(WorkedSeconds(model), Is.EqualTo(5 * 3600 + 30 * 60),
            "18:00-24:00 minus Pause1OverrideMinutes=30 = 5h30; 6h00 means the override was skipped as cross-midnight");
    }

    [Test]
    public void WorkedSegments_TickRow_PauseCrossingFiveMinuteBoundaries_CountsFlooredTicks()
    {
        // 11:58-12:07 on a tick row counts floor5(12:07) - floor5(11:58) = 12:05 - 11:55
        // = 10 min (FlexChain flooring), so 07:00-15:00 works 7h50.
        var model = ExportModel(new PlanRegistration
        {
            Date = new DateTime(2026, 5, 15),
            Start1Id = 85,  // 07:00
            Stop1Id = 181,  // 15:00
            Pause1Id = 1,
            Pause1StartedAt = D(11, 58), Pause1StoppedAt = D(12, 7),
        }, isOneMinute: false);

        Assert.That(model.Shift1PauseMinutes, Is.EqualTo(10), "sanity: canonical tick pause is 10 min");
        Assert.That(WorkedSeconds(model), Is.EqualTo(8 * 3600 - 10 * 60));
    }

    [Test]
    public void WorkedSegments_OverlappingStamps_MatchCanonicalPause()
    {
        // Two overlapping one-minute stamps (12:00-12:30 and 12:15-12:45): the canonical
        // pause sums them (60 min), so 07:00-15:00 works span - 60 min = 7 h, matching netto.
        var model = ExportModel(new PlanRegistration
        {
            Date = new DateTime(2026, 5, 15),
            Start1Id = 85,
            Stop1Id = 181,
            Start1StartedAt = D(7, 0), Stop1StoppedAt = D(15, 0),
            Pause1Id = 1,
            Pause1StartedAt = D(12, 0), Pause1StoppedAt = D(12, 30),
            Pause10StartedAt = D(12, 15), Pause10StoppedAt = D(12, 45),
        }, isOneMinute: true);

        Assert.That(model.Shift1PauseMinutes, Is.EqualTo(60), "sanity: canonical pause sums the slots");
        Assert.That(WorkedSeconds(model), Is.EqualTo(7 * 3600));
    }
}
