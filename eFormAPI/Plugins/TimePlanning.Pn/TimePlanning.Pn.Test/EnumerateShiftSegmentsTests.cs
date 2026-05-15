using System;
using System.Linq;
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
}
