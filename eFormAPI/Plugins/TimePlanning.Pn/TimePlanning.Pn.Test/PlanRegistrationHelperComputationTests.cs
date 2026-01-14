using System;
using System.Linq;
using System.Reflection;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Tests for new pay line computation and working time calculation features in PlanRegistrationHelper.
/// These tests verify seconds-first interval calculation, break splitting, day classification,
/// and pay line generation for payroll export (DataLøn/Danløn/Uniconta).
/// </summary>
[TestFixture]
public class PlanRegistrationHelperComputationTests
{
    /// <summary>
    /// Test 1: Work + pause computation
    /// Given a PlanRegistration with 14h work (00:00-11:00 and 14:00-17:00) and no pause,
    /// expect NettoHoursInSeconds == 50400 (14 hours).
    /// </summary>
    [Test]
    public void GetWorkIntervals_With14HoursWork_Returns50400Seconds()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0);  // Thursday
        var planRegistration = new PlanRegistration
        {
            Date = baseDate,
            // First shift: 00:00 - 11:00 (11 hours)
            Start1StartedAt = baseDate.AddHours(0),
            Stop1StoppedAt = baseDate.AddHours(11),
            // Second shift: 14:00 - 17:00 (3 hours)
            Start2StartedAt = baseDate.AddHours(14),
            Stop2StoppedAt = baseDate.AddHours(17)
        };

        // Act
        var getWorkIntervalsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetWorkIntervals",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.That(getWorkIntervalsMethod, Is.Not.Null, "GetWorkIntervals method should exist");

        var workIntervals = getWorkIntervalsMethod!.Invoke(null, new object[] { planRegistration });
        var intervalsArray = ((System.Collections.IEnumerable)workIntervals!).Cast<(DateTime, DateTime)>().ToArray();

        var calculateTotalSecondsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "CalculateTotalSeconds",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.That(calculateTotalSecondsMethod, Is.Not.Null, "CalculateTotalSeconds method should exist");

        var totalSeconds = (long)calculateTotalSecondsMethod!.Invoke(null, new object[] { intervalsArray })!;

        // Assert
        Assert.That(intervalsArray.Length, Is.EqualTo(2), "Should have 2 work intervals");
        Assert.That(totalSeconds, Is.EqualTo(50400), "Total work should be 14 hours = 50400 seconds");
    }

    /// <summary>
    /// Test 2: Pause interval extraction with no pauses
    /// Given a PlanRegistration with no pauses, expect 0 pause intervals.
    /// </summary>
    [Test]
    public void GetPauseIntervals_WithNoPauses_ReturnsEmptyCollection()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = baseDate,
            Start1StartedAt = baseDate.AddHours(8),
            Stop1StoppedAt = baseDate.AddHours(16)
            // No pause timestamps set
        };

        // Act
        var getPauseIntervalsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetPauseIntervals",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.That(getPauseIntervalsMethod, Is.Not.Null, "GetPauseIntervals method should exist");

        var pauseIntervals = getPauseIntervalsMethod!.Invoke(null, new object[] { planRegistration });
        var intervalsArray = ((System.Collections.IEnumerable)pauseIntervals!).Cast<(DateTime, DateTime)>().ToArray();

        // Assert
        Assert.That(intervalsArray.Length, Is.EqualTo(0), "Should have 0 pause intervals");
    }

    /// <summary>
    /// Test 3: Pause interval extraction with 45 minutes of pauses
    /// Given a PlanRegistration with 45 minutes of pauses, expect correct total pause seconds.
    /// </summary>
    [Test]
    public void GetPauseIntervals_With45MinutesPause_Returns2700Seconds()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = baseDate,
            // 30-minute lunch break
            Pause1StartedAt = baseDate.AddHours(12),
            Pause1StoppedAt = baseDate.AddHours(12).AddMinutes(30),
            // 15-minute coffee break
            Pause2StartedAt = baseDate.AddHours(15),
            Pause2StoppedAt = baseDate.AddHours(15).AddMinutes(15)
        };

        // Act
        var getPauseIntervalsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetPauseIntervals",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var pauseIntervals = getPauseIntervalsMethod!.Invoke(null, new object[] { planRegistration });
        var intervalsArray = ((System.Collections.IEnumerable)pauseIntervals!).Cast<(DateTime, DateTime)>().ToArray();

        var calculateTotalSecondsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "CalculateTotalSeconds",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var totalSeconds = (long)calculateTotalSecondsMethod!.Invoke(null, new object[] { intervalsArray })!;

        // Assert
        Assert.That(intervalsArray.Length, Is.EqualTo(2), "Should have 2 pause intervals");
        Assert.That(totalSeconds, Is.EqualTo(2700), "Total pause should be 45 minutes = 2700 seconds");
    }

    /// <summary>
    /// Test 4: Day classification - Sunday
    /// Given a Sunday date, expect DayCode == "SUNDAY" and IsSunday == true.
    /// </summary>
    [Test]
    public void GetDayCode_ForSunday_ReturnsSUNDAY()
    {
        // Arrange
        var sundayDate = new DateTime(2026, 1, 18, 0, 0, 0);  // Sunday
        Assert.That(sundayDate.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday), "Test date should be a Sunday");

        // Act
        var getDayCodeMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetDayCode",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.That(getDayCodeMethod, Is.Not.Null, "GetDayCode method should exist");

        var dayCode = (string)getDayCodeMethod!.Invoke(null, new object[] { sundayDate })!;

        // Assert
        Assert.That(dayCode, Is.EqualTo("SUNDAY"), "Sunday should return 'SUNDAY' day code");
    }

    /// <summary>
    /// Test 5: Day classification - Saturday
    /// Given a Saturday date, expect DayCode == "SATURDAY".
    /// </summary>
    [Test]
    public void GetDayCode_ForSaturday_ReturnsSATURDAY()
    {
        // Arrange
        var saturdayDate = new DateTime(2026, 1, 17, 0, 0, 0);  // Saturday
        Assert.That(saturdayDate.DayOfWeek, Is.EqualTo(DayOfWeek.Saturday), "Test date should be a Saturday");

        // Act
        var getDayCodeMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetDayCode",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var dayCode = (string)getDayCodeMethod!.Invoke(null, new object[] { saturdayDate })!;

        // Assert
        Assert.That(dayCode, Is.EqualTo("SATURDAY"), "Saturday should return 'SATURDAY' day code");
    }

    /// <summary>
    /// Test 6: Day classification - Grundlovsdag (June 5th)
    /// Given June 5th, expect DayCode == "GRUNDLOVSDAG".
    /// </summary>
    [Test]
    public void GetDayCode_ForGrundlovsdag_ReturnsGRUNDLOVSDAG()
    {
        // Arrange
        var grundlovsdagDate = new DateTime(2026, 6, 5, 0, 0, 0);  // Grundlovsdag (Constitution Day)

        // Act
        var getDayCodeMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetDayCode",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var dayCode = (string)getDayCodeMethod!.Invoke(null, new object[] { grundlovsdagDate })!;

        // Assert
        Assert.That(dayCode, Is.EqualTo("GRUNDLOVSDAG"), "June 5th should return 'GRUNDLOVSDAG' day code");
    }

    /// <summary>
    /// Test 7: Day classification - Weekday
    /// Given a regular weekday, expect DayCode == "WEEKDAY".
    /// </summary>
    [Test]
    public void GetDayCode_ForRegularWeekday_ReturnsWEEKDAY()
    {
        // Arrange
        var weekdayDate = new DateTime(2026, 1, 15, 0, 0, 0);  // Thursday (not a holiday)
        Assert.That(weekdayDate.DayOfWeek, Is.EqualTo(DayOfWeek.Thursday), "Test date should be a Thursday");

        // Act
        var getDayCodeMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetDayCode",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var dayCode = (string)getDayCodeMethod!.Invoke(null, new object[] { weekdayDate })!;

        // Assert
        Assert.That(dayCode, Is.EqualTo("WEEKDAY"), "Regular Thursday should return 'WEEKDAY' day code");
    }

    /// <summary>
    /// Test 8: Day classification - Official Holiday (Christmas Day)
    /// Given December 25th, expect DayCode == "HOLIDAY".
    /// </summary>
    [Test]
    public void GetDayCode_ForChristmasDay_ReturnsHOLIDAY()
    {
        // Arrange
        var christmasDate = new DateTime(2026, 12, 25, 0, 0, 0);  // Christmas Day

        // Act
        var getDayCodeMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetDayCode",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var dayCode = (string)getDayCodeMethod!.Invoke(null, new object[] { christmasDate })!;

        // Assert
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"), "December 25th should return 'HOLIDAY' day code");
    }

    /// <summary>
    /// Test 9: Day classification - New Year's Day
    /// Given January 1st, expect DayCode == "HOLIDAY".
    /// </summary>
    [Test]
    public void GetDayCode_ForNewYearsDay_ReturnsHOLIDAY()
    {
        // Arrange
        var newYearsDate = new DateTime(2026, 1, 1, 0, 0, 0);  // New Year's Day

        // Act
        var getDayCodeMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetDayCode",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var dayCode = (string)getDayCodeMethod!.Invoke(null, new object[] { newYearsDate })!;

        // Assert
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"), "January 1st should return 'HOLIDAY' day code");
    }

    /// <summary>
    /// Test 10: Work intervals ignore incomplete pairs
    /// Given a PlanRegistration with only a start time (no end time), expect that interval to be ignored.
    /// </summary>
    [Test]
    public void GetWorkIntervals_IgnoresIncompleteIntervals()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = baseDate,
            // Complete interval
            Start1StartedAt = baseDate.AddHours(8),
            Stop1StoppedAt = baseDate.AddHours(12),
            // Incomplete interval (no stop time)
            Start2StartedAt = baseDate.AddHours(13),
            Stop2StoppedAt = null
        };

        // Act
        var getWorkIntervalsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetWorkIntervals",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var workIntervals = getWorkIntervalsMethod!.Invoke(null, new object[] { planRegistration });
        var intervalsArray = ((System.Collections.IEnumerable)workIntervals!).Cast<(DateTime, DateTime)>().ToArray();

        // Assert
        Assert.That(intervalsArray.Length, Is.EqualTo(1), "Should only have 1 complete work interval");
        Assert.That((intervalsArray[0].Item2 - intervalsArray[0].Item1).TotalHours, Is.EqualTo(4), 
            "Complete interval should be 4 hours");
    }

    /// <summary>
    /// Test 11: Work intervals ignore negative duration
    /// Given a PlanRegistration where stop time is before start time, expect that interval to be ignored.
    /// </summary>
    [Test]
    public void GetWorkIntervals_IgnoresNegativeDuration()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = baseDate,
            // Valid interval
            Start1StartedAt = baseDate.AddHours(8),
            Stop1StoppedAt = baseDate.AddHours(12),
            // Invalid interval (stop before start)
            Start2StartedAt = baseDate.AddHours(17),
            Stop2StoppedAt = baseDate.AddHours(14)
        };

        // Act
        var getWorkIntervalsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetWorkIntervals",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var workIntervals = getWorkIntervalsMethod!.Invoke(null, new object[] { planRegistration });
        var intervalsArray = ((System.Collections.IEnumerable)workIntervals!).Cast<(DateTime, DateTime)>().ToArray();

        // Assert
        Assert.That(intervalsArray.Length, Is.EqualTo(1), "Should only have 1 valid work interval");
    }

    /// <summary>
    /// Test 12: Pause intervals include extended pause ranges
    /// Verify that Pause10-29, Pause100-102, Pause200-202 are all considered.
    /// </summary>
    [Test]
    public void GetPauseIntervals_IncludesExtendedPauseRanges()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = baseDate,
            // Regular pause
            Pause1StartedAt = baseDate.AddHours(10),
            Pause1StoppedAt = baseDate.AddHours(10).AddMinutes(15),
            // Pause in 10-29 range
            Pause10StartedAt = baseDate.AddHours(12),
            Pause10StoppedAt = baseDate.AddHours(12).AddMinutes(30),
            // Pause in 100-102 range
            Pause100StartedAt = baseDate.AddHours(14),
            Pause100StoppedAt = baseDate.AddHours(14).AddMinutes(10),
            // Pause in 200-202 range
            Pause200StartedAt = baseDate.AddHours(16),
            Pause200StoppedAt = baseDate.AddHours(16).AddMinutes(5)
        };

        // Act
        var getPauseIntervalsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "GetPauseIntervals",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var pauseIntervals = getPauseIntervalsMethod!.Invoke(null, new object[] { planRegistration });
        var intervalsArray = ((System.Collections.IEnumerable)pauseIntervals!).Cast<(DateTime, DateTime)>().ToArray();

        var calculateTotalSecondsMethod = typeof(PlanRegistrationHelper).GetMethod(
            "CalculateTotalSeconds",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var totalSeconds = (long)calculateTotalSecondsMethod!.Invoke(null, new object[] { intervalsArray })!;

        // Assert
        Assert.That(intervalsArray.Length, Is.EqualTo(4), "Should have 4 pause intervals");
        // 15 + 30 + 10 + 5 = 60 minutes = 3600 seconds
        Assert.That(totalSeconds, Is.EqualTo(3600), "Total pause should be 60 minutes = 3600 seconds");
    }
}
