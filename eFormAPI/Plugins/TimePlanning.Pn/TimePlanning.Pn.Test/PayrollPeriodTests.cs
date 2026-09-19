using System;
using System.Globalization;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Spec §3 "Period rule". Pure arithmetic, so this fixture does NOT derive
/// TestBaseSetup and starts no database container.
/// </summary>
[TestFixture]
public class PayrollPeriodTests
{
    private static DateTime D(string s) => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [TestCase("2026-09-18", 19, "2026-07-20", "2026-08-19", TestName = "Cutoff19_DayBeforeCutoff")]
    [TestCase("2026-09-19", 19, "2026-07-20", "2026-08-19", TestName = "Cutoff19_OnCutoffDay_PeriodNotYetClosed")]
    [TestCase("2026-09-20", 19, "2026-08-20", "2026-09-19", TestName = "Cutoff19_DayAfterCutoff")]
    [TestCase("2026-03-01", 31, "2026-02-01", "2026-02-28", TestName = "Cutoff31_ClampsToFeb28")]
    [TestCase("2026-03-31", 31, "2026-02-01", "2026-02-28", TestName = "Cutoff31_OnMarch31_StillFebruary")]
    [TestCase("2026-04-01", 31, "2026-03-01", "2026-03-31", TestName = "Cutoff31_AfterMarch31")]
    [TestCase("2028-03-01", 31, "2028-02-01", "2028-02-29", TestName = "Cutoff31_LeapFebruary")]
    [TestCase("2028-03-15", 30, "2028-01-31", "2028-02-29", TestName = "Cutoff30_LeapFebruary_StartAfterJan30")]
    [TestCase("2026-09-01", 1, "2026-07-02", "2026-08-01", TestName = "Cutoff1_OnCutoffDay")]
    [TestCase("2026-09-02", 1, "2026-08-02", "2026-09-01", TestName = "Cutoff1_DayAfter")]
    [TestCase("2026-01-19", 19, "2025-11-20", "2025-12-19", TestName = "YearBoundary_JanuaryOnCutoff")]
    [TestCase("2026-01-20", 19, "2025-12-20", "2026-01-19", TestName = "YearBoundary_JanuaryAfterCutoff")]
    [TestCase("2026-01-10", 31, "2025-12-01", "2025-12-31", TestName = "YearBoundary_Cutoff31")]
    public void LastClosed_KnownDates(string today, int cutoff, string expectedStart, string expectedEnd)
    {
        var period = PayrollPeriod.LastClosed(D(today), cutoff);

        Assert.Multiple(() =>
        {
            Assert.That(period.Start, Is.EqualTo(D(expectedStart)), "start");
            Assert.That(period.End, Is.EqualTo(D(expectedEnd)), "end");
        });
    }

    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(32, 31)]
    [TestCase(45, 31)]
    public void LastClosed_OutOfRangeCutoff_IsClamped(int given, int clampedTo)
    {
        var today = D("2026-09-20");

        Assert.That(PayrollPeriod.LastClosed(today, given),
            Is.EqualTo(PayrollPeriod.LastClosed(today, clampedTo)));
    }

    [Test]
    public void LastClosed_IgnoresTimeOfDay()
    {
        var midnight = PayrollPeriod.LastClosed(D("2026-09-20"), 19);
        var lateEvening = PayrollPeriod.LastClosed(D("2026-09-20").AddHours(23).AddMinutes(59), 19);

        Assert.That(lateEvening, Is.EqualTo(midnight));
    }

    /// <summary>
    /// Brute-force oracle straight from the spec's wording: End is the LATEST
    /// d &lt; today with d.Day == min(cutoff, DaysInMonth(d)); Start is the day
    /// after the previous such d. Covers every day of 2027-2028 (a leap year)
    /// for every cutoff, so no month-length corner is left to a hand-picked case.
    /// </summary>
    [Test]
    public void LastClosed_MatchesSpecDefinition_ForEveryDayAndCutoff()
    {
        static bool IsCutoffDay(DateTime d, int c) => d.Day == Math.Min(c, DateTime.DaysInMonth(d.Year, d.Month));

        static DateTime LatestCutoffBefore(DateTime exclusive, int c)
        {
            var d = exclusive.AddDays(-1);
            while (!IsCutoffDay(d, c)) d = d.AddDays(-1);
            return d;
        }

        for (var today = D("2027-01-01"); today <= D("2028-12-31"); today = today.AddDays(1))
        {
            for (var cutoff = 1; cutoff <= 31; cutoff++)
            {
                var expectedEnd = LatestCutoffBefore(today, cutoff);
                var expectedStart = LatestCutoffBefore(expectedEnd, cutoff).AddDays(1);

                var actual = PayrollPeriod.LastClosed(today, cutoff);

                if (actual.End != expectedEnd || actual.Start != expectedStart)
                {
                    Assert.Fail($"today {today:yyyy-MM-dd} cutoff {cutoff}: expected " +
                                $"{expectedStart:yyyy-MM-dd}..{expectedEnd:yyyy-MM-dd}, got " +
                                $"{actual.Start:yyyy-MM-dd}..{actual.End:yyyy-MM-dd}");
                }
            }
        }
    }
}
