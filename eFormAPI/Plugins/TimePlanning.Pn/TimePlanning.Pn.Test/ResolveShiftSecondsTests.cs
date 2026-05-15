using System;
using NUnit.Framework;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pure-function regression coverage for <c>TimePlanningWorkingHoursService.ResolveShiftSeconds</c>,
/// the helper that converts a precise (DateTime?, DateTime?) shift pair to a
/// (startSecondOfDay, stopSecondOfDay) tuple used by time-band pay-line attribution.
/// Locks the user-reported 08:04→10:10 = 7560 s example so a future change that
/// reintroduces 5-minute snapping fails immediately.
/// </summary>
[TestFixture]
public class ResolveShiftSecondsTests
{
    private static (int Start, int Stop)? Resolve(DateTime? start, DateTime? stop) =>
        TimePlanningWorkingHoursService.ResolveShiftSeconds(start, stop);

    [Test]
    public void NonRoundMinutes_08_04_To_10_10_ReturnsExactSecondsOfDay()
    {
        var date = new DateTime(2026, 5, 15);
        var res = Resolve(date.AddHours(8).AddMinutes(4), date.AddHours(10).AddMinutes(10));
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Value.Start, Is.EqualTo(29040), "08:04 = 8*3600 + 4*60 = 29040 s");
        Assert.That(res.Value.Stop, Is.EqualTo(36600), "10:10 = 10*3600 + 10*60 = 36600 s");
        Assert.That(res.Value.Stop - res.Value.Start, Is.EqualTo(7560), "2h06m = 7560 s");
    }

    [Test]
    public void NullStart_ReturnsNull()
    {
        Assert.That(Resolve(null, new DateTime(2026, 5, 15, 10, 0, 0)), Is.Null);
    }

    [Test]
    public void NullStop_ReturnsNull()
    {
        Assert.That(Resolve(new DateTime(2026, 5, 15, 8, 0, 0), null), Is.Null);
    }

    [Test]
    public void StopEqualsStart_ReturnsNull()
    {
        var t = new DateTime(2026, 5, 15, 8, 4, 0);
        Assert.That(Resolve(t, t), Is.Null);
    }

    [Test]
    public void StopBeforeStart_ReturnsNull()
    {
        var s = new DateTime(2026, 5, 15, 10, 10, 0);
        var t = new DateTime(2026, 5, 15, 8, 4, 0);
        Assert.That(Resolve(s, t), Is.Null);
    }

    [Test]
    public void StopCrossesMidnight_ClampedToEndOfDay()
    {
        var start = new DateTime(2026, 5, 15, 22, 13, 0);
        var stop  = new DateTime(2026, 5, 16,  6, 47, 0);
        var res = Resolve(start, stop);
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Value.Start, Is.EqualTo(22 * 3600 + 13 * 60));
        Assert.That(res.Value.Stop, Is.EqualTo(86400), "Stop on the next day clamps to 86400 (end of day)");
    }

    [Test]
    public void RoundMinutes_08_00_To_10_05_ReturnsExactSecondsOfDay()
    {
        // Legacy 5-min-aligned path must produce the same exact arithmetic — no
        // off-by-one when stamps land on slot boundaries.
        var date = new DateTime(2026, 5, 15);
        var res = Resolve(date.AddHours(8), date.AddHours(10).AddMinutes(5));
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Value.Start, Is.EqualTo(28800), "08:00 = 8*3600 = 28800 s");
        Assert.That(res.Value.Stop, Is.EqualTo(36300), "10:05 = 10*3600 + 5*60 = 36300 s");
        Assert.That(res.Value.Stop - res.Value.Start, Is.EqualTo(7500), "2h05m = 7500 s");
    }

    [Test]
    public void SubSecondPrecision_TruncatedToWholeSeconds()
    {
        var start = new DateTime(2026, 5, 15, 8, 4, 0).AddMilliseconds(500);
        var stop  = new DateTime(2026, 5, 15, 10, 10, 30);
        var res = Resolve(start, stop);
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Value.Stop - res.Value.Start, Is.EqualTo(7590),
            "TimeOfDay.TotalSeconds cast to int truncates toward zero; 2h06m30s = 7590 s");
    }
}
