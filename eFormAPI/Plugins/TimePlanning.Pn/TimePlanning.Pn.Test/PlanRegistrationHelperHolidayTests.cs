using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanRegistrationHelperHolidayTests
{
    [Test]
    [TestCase("2026-01-01", true, "Nytårsdag")] // New Year's Day
    [TestCase("2026-04-02", true, "Skærtorsdag")] // Maundy Thursday
    [TestCase("2026-04-03", true, "Langfredag")] // Good Friday
    [TestCase("2026-04-05", true, "Påskedag")] // Easter Sunday
    [TestCase("2026-04-06", true, "2. påskedag")] // Easter Monday
    [TestCase("2026-05-14", true, "Kristi himmelfartsdag")] // Ascension Day
    [TestCase("2026-05-24", true, "Pinsedag")] // Whit Sunday
    [TestCase("2026-05-25", true, "2. pinsedag")] // Whit Monday
    [TestCase("2026-06-05", true, "Grundlovsdag")] // Constitution Day
    [TestCase("2026-12-24", true, "Juleaften")] // Christmas Eve
    [TestCase("2026-12-25", true, "Juledag")] // Christmas Day
    [TestCase("2026-12-26", true, "2. juledag")] // Boxing Day
    [TestCase("2026-01-02", false, "Regular weekday")]
    [TestCase("2026-07-04", false, "Regular weekday in summer")]
    [TestCase("2026-11-15", false, "Regular weekday in November")]
    public void GetDayCode_ReturnsCorrectCodeForHolidays(string dateString, bool isHoliday, string description)
    {
        // Arrange
        var date = DateTime.Parse(dateString);

        // Act
        var dayCode = GetDayCodePublic(date);

        // Assert
        if (isHoliday)
        {
            if (date.Month == 6 && date.Day == 5)
            {
                Assert.That(dayCode, Is.EqualTo("GRUNDLOVSDAG"), $"Failed for {description} on {dateString}");
            }
            else
            {
                Assert.That(dayCode, Is.EqualTo("HOLIDAY"), $"Failed for {description} on {dateString}");
            }
        }
        else
        {
            Assert.That(dayCode, Is.Not.EqualTo("HOLIDAY"), $"Should not be a holiday: {description} on {dateString}");
        }
    }

    [Test]
    [TestCase("2027-03-25", "Skærtorsdag 2027")]
    [TestCase("2027-03-26", "Langfredag 2027")]
    [TestCase("2027-03-28", "Påskedag 2027")]
    [TestCase("2027-03-29", "2. påskedag 2027")]
    [TestCase("2028-04-13", "Skærtorsdag 2028")]
    [TestCase("2028-04-14", "Langfredag 2028")]
    [TestCase("2028-04-16", "Påskedag 2028")]
    [TestCase("2028-04-17", "2. påskedag 2028")]
    [TestCase("2029-03-29", "Skærtorsdag 2029")]
    [TestCase("2029-03-30", "Langfredag 2029")]
    [TestCase("2029-04-01", "Påskedag 2029")]
    [TestCase("2029-04-02", "2. påskedag 2029")]
    public void GetDayCode_RecognizesMovableHolidays(string dateString, string description)
    {
        // Arrange
        var date = DateTime.Parse(dateString);

        // Act
        var dayCode = GetDayCodePublic(date);

        // Assert
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"), $"Failed to recognize {description}");
    }

    [Test]
    [TestCase("2026-06-06", false, "Day after Grundlovsdag")]
    [TestCase("2026-06-04", false, "Day before Grundlovsdag")]
    [TestCase("2026-12-23", false, "Day before Christmas Eve")]
    [TestCase("2026-12-27", false, "Day after Boxing Day")]
    public void GetDayCode_DoesNotFlagNonHolidays(string dateString, bool shouldBeHoliday, string description)
    {
        // Arrange
        var date = DateTime.Parse(dateString);

        // Act
        var dayCode = GetDayCodePublic(date);

        // Assert
        Assert.That(dayCode, Is.Not.EqualTo("HOLIDAY"), $"Should not be a holiday: {description}");
        Assert.That(dayCode, Is.Not.EqualTo("GRUNDLOVSDAG"), $"Should not be Grundlovsdag: {description}");
    }

    [Test]
    public void GetDayCode_RecognizesSaturday()
    {
        // Arrange - June 13, 2026 is a Saturday
        var saturday = new DateTime(2026, 6, 13);

        // Act
        var dayCode = GetDayCodePublic(saturday);

        // Assert
        Assert.That(dayCode, Is.EqualTo("SATURDAY"));
    }

    [Test]
    public void GetDayCode_RecognizesSunday()
    {
        // Arrange - June 7, 2026 is a Sunday
        var sunday = new DateTime(2026, 6, 7);

        // Act
        var dayCode = GetDayCodePublic(sunday);

        // Assert
        Assert.That(dayCode, Is.EqualTo("SUNDAY"));
    }

    [Test]
    public void GetDayCode_RecognizesWeekday()
    {
        // Arrange - June 8, 2026 is a Monday (weekday, not a holiday)
        var weekday = new DateTime(2026, 6, 8);

        // Act
        var dayCode = GetDayCodePublic(weekday);

        // Assert
        Assert.That(dayCode, Is.EqualTo("WEEKDAY"));
    }

    [Test]
    public void ComputeTimeTrackingFields_SetsIsSaturdayCorrectly()
    {
        // Arrange - June 13, 2026 is a Saturday
        var saturday = new DateTime(2026, 6, 13);
        var planRegistration = new PlanRegistration
        {
            Date = saturday,
            Start1StartedAt = saturday.AddHours(8),
            Stop1StoppedAt = saturday.AddHours(16)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        Assert.That(planRegistration.IsSaturday, Is.True);
        Assert.That(planRegistration.IsSunday, Is.False);
    }

    [Test]
    public void ComputeTimeTrackingFields_SetsIsSundayCorrectly()
    {
        // Arrange - June 7, 2026 is a Sunday
        var sunday = new DateTime(2026, 6, 7);
        var planRegistration = new PlanRegistration
        {
            Date = sunday,
            Start1StartedAt = sunday.AddHours(8),
            Stop1StoppedAt = sunday.AddHours(16)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        Assert.That(planRegistration.IsSunday, Is.True);
        Assert.That(planRegistration.IsSaturday, Is.False);
    }

    [Test]
    public void ComputeTimeTrackingFields_CalculatesCorrectlyOnHoliday()
    {
        // Arrange - December 25, 2026 is Christmas Day (a holiday)
        var christmas = new DateTime(2026, 12, 25);
        var planRegistration = new PlanRegistration
        {
            Date = christmas,
            Start1StartedAt = christmas.AddHours(8),
            Stop1StoppedAt = christmas.AddHours(16),
            Pause1StartedAt = christmas.AddHours(12),
            Pause1StoppedAt = christmas.AddHours(12.5)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // 8 hours work - 0.5 hours pause = 7.5 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.5));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(27000)); // 7.5 * 3600
        Assert.That(planRegistration.EffectiveNetHoursInSeconds, Is.EqualTo(27000));
    }

    [Test]
    public void ComputeTimeTrackingFields_CalculatesCorrectlyOnWeekday()
    {
        // Arrange - June 9, 2026 is a regular Monday
        var weekday = new DateTime(2026, 6, 9);
        var planRegistration = new PlanRegistration
        {
            Date = weekday,
            Start1StartedAt = weekday.AddHours(8),
            Stop1StoppedAt = weekday.AddHours(16),
            Pause1StartedAt = weekday.AddHours(12),
            Pause1StoppedAt = weekday.AddHours(12.5)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // 8 hours work - 0.5 hours pause = 7.5 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.5));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(27000));
        Assert.That(planRegistration.EffectiveNetHoursInSeconds, Is.EqualTo(27000));
    }

    [Test]
    public void ComputeTimeTrackingFields_HandlesMultipleShifts()
    {
        // Arrange
        var date = new DateTime(2026, 6, 9, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = date,
            Start1StartedAt = date.AddHours(8),
            Stop1StoppedAt = date.AddHours(12),
            Start2StartedAt = date.AddHours(14),
            Stop2StoppedAt = date.AddHours(18),
            Pause1StartedAt = date.AddHours(10),
            Pause1StoppedAt = date.AddHours(10.25),
            Pause2StartedAt = date.AddHours(16),
            Pause2StoppedAt = date.AddHours(16.25)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Shift 1: 4 hours, Shift 2: 4 hours = 8 hours total
        // Pause 1: 0.25 hours, Pause 2: 0.25 hours = 0.5 hours total
        // Net: 8 - 0.5 = 7.5 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.5));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(27000));
    }

    [Test]
    public void ComputeTimeTrackingFields_HandlesNettoHoursOverride()
    {
        // Arrange
        var date = new DateTime(2026, 6, 9, 0, 0, 0);
        var planRegistration = new PlanRegistration
        {
            Date = date,
            Start1StartedAt = date.AddHours(8),
            Stop1StoppedAt = date.AddHours(16),
            NettoHoursOverrideActive = true,
            NettoHoursOverride = 6.0 // Override to 6 hours
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Actual work: 8 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(8.0));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(28800));
        // But effective should use override
        Assert.That(planRegistration.EffectiveNetHoursInSeconds, Is.EqualTo(21600)); // 6 * 3600
    }

    // ====================================================================
    // Tests for accumulated hours on different day types
    // ====================================================================

    [Test]
    public void PlanRegistration_OnWeekday_CalculatesCorrectHours()
    {
        // Arrange - Tuesday, January 6, 2026 (regular weekday)
        var weekday = new DateTime(2026, 1, 6);
        var planRegistration = new PlanRegistration
        {
            Date = weekday,
            // Morning shift: 8:00 - 12:00 (4 hours)
            Start1StartedAt = weekday.AddHours(8),
            Stop1StoppedAt = weekday.AddHours(12),
            // Coffee break during morning: 10:00 - 10:15 (0.25 hours)
            Pause1StartedAt = weekday.AddHours(10),
            Pause1StoppedAt = weekday.AddHours(10.25),
            // Afternoon shift: 13:00 - 16:00 (3 hours)
            Start2StartedAt = weekday.AddHours(13),
            Stop2StoppedAt = weekday.AddHours(16)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Total work: 7 hours (4 + 3)
        // Pause: 0.25 hours
        // Net: 6.75 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(6.75));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(24300)); // 6.75 * 3600
        Assert.That(planRegistration.IsSaturday, Is.False);
        Assert.That(planRegistration.IsSunday, Is.False);
    }

    [Test]
    public void PlanRegistration_OnSaturday_CalculatesCorrectHours()
    {
        // Arrange - Saturday, June 13, 2026
        var saturday = new DateTime(2026, 6, 13);
        var planRegistration = new PlanRegistration
        {
            Date = saturday,
            // Single shift: 9:00 - 14:00 (5 hours)
            Start1StartedAt = saturday.AddHours(9),
            Stop1StoppedAt = saturday.AddHours(14),
            // Coffee break: 11:00 - 11:15 (0.25 hours)
            Pause1StartedAt = saturday.AddHours(11),
            Pause1StoppedAt = saturday.AddHours(11.25)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Total work: 5 hours
        // Pause: 0.25 hours
        // Net: 4.75 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(4.75));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(17100)); // 4.75 * 3600
        Assert.That(planRegistration.IsSaturday, Is.True);
        Assert.That(planRegistration.IsSunday, Is.False);
    }

    [Test]
    public void PlanRegistration_OnSunday_CalculatesCorrectHours()
    {
        // Arrange - Sunday, June 7, 2026
        var sunday = new DateTime(2026, 6, 7);
        var planRegistration = new PlanRegistration
        {
            Date = sunday,
            // Single shift: 10:00 - 15:00 (5 hours)
            Start1StartedAt = sunday.AddHours(10),
            Stop1StoppedAt = sunday.AddHours(15),
            // Lunch break: 12:00 - 12:30 (0.5 hours)
            Pause1StartedAt = sunday.AddHours(12),
            Pause1StoppedAt = sunday.AddHours(12.5)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Total work: 5 hours
        // Pause: 0.5 hours
        // Net: 4.5 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(4.5));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(16200)); // 4.5 * 3600
        Assert.That(planRegistration.IsSaturday, Is.False);
        Assert.That(planRegistration.IsSunday, Is.True);
    }

    [Test]
    public void PlanRegistration_OnOfficialHoliday_CalculatesCorrectHours()
    {
        // Arrange - Christmas Day, December 25, 2026 (official holiday)
        var christmas = new DateTime(2026, 12, 25);
        var planRegistration = new PlanRegistration
        {
            Date = christmas,
            // Emergency shift: 8:00 - 16:00 (8 hours)
            Start1StartedAt = christmas.AddHours(8),
            Stop1StoppedAt = christmas.AddHours(16),
            // Break: 12:00 - 12:45 (0.75 hours)
            Pause1StartedAt = christmas.AddHours(12),
            Pause1StoppedAt = christmas.AddHours(12.75)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);
        var dayCode = GetDayCodePublic(christmas);

        // Assert
        // Total work: 8 hours
        // Pause: 0.75 hours
        // Net: 7.25 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.25));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(26100)); // 7.25 * 3600
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"));
    }

    [Test]
    public void PlanRegistration_OnGrundlovsdag_CalculatesCorrectHours()
    {
        // Arrange - Grundlovsdag, June 5, 2026 (Constitution Day)
        var grundlovsdag = new DateTime(2026, 6, 5);
        var planRegistration = new PlanRegistration
        {
            Date = grundlovsdag,
            // Morning shift only: 8:00 - 12:00 (4 hours, before noon)
            Start1StartedAt = grundlovsdag.AddHours(8),
            Stop1StoppedAt = grundlovsdag.AddHours(12),
            // No pause
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);
        var dayCode = GetDayCodePublic(grundlovsdag);

        // Assert
        // Total work: 4 hours
        // Pause: 0 hours
        // Net: 4 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(4.0));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(14400)); // 4 * 3600
        Assert.That(dayCode, Is.EqualTo("GRUNDLOVSDAG"));
    }

    [Test]
    public void PlanRegistration_OnGrundlovsdagAfternoon_CalculatesCorrectHours()
    {
        // Arrange - Grundlovsdag, June 5, 2026 (Constitution Day)
        // Work after 12:00 should be counted differently
        var grundlovsdag = new DateTime(2026, 6, 5);
        var planRegistration = new PlanRegistration
        {
            Date = grundlovsdag,
            // Full day: 8:00 - 16:00 (8 hours)
            Start1StartedAt = grundlovsdag.AddHours(8),
            Stop1StoppedAt = grundlovsdag.AddHours(16),
            // Lunch break: 12:00 - 12:30 (0.5 hours)
            Pause1StartedAt = grundlovsdag.AddHours(12),
            Pause1StoppedAt = grundlovsdag.AddHours(12.5)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);
        var dayCode = GetDayCodePublic(grundlovsdag);

        // Assert
        // Total work: 8 hours
        // Pause: 0.5 hours
        // Net: 7.5 hours
        // Note: Hours before and after 12:00 would be split differently for payroll
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.5));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(27000)); // 7.5 * 3600
        Assert.That(dayCode, Is.EqualTo("GRUNDLOVSDAG"));
    }

    [Test]
    public void PlanRegistration_OnNewYearsDay_CalculatesCorrectHours()
    {
        // Arrange - New Year's Day, January 1, 2026 (official holiday)
        var newYear = new DateTime(2026, 1, 1);
        var planRegistration = new PlanRegistration
        {
            Date = newYear,
            // Night shift: 22:00 previous day carried over, but dated on this day
            // Single shift: 6:00 - 14:00 (8 hours)
            Start1StartedAt = newYear.AddHours(6),
            Stop1StoppedAt = newYear.AddHours(14),
            // Two breaks
            Pause1StartedAt = newYear.AddHours(9),
            Pause1StoppedAt = newYear.AddHours(9.25),
            Pause2StartedAt = newYear.AddHours(11),
            Pause2StoppedAt = newYear.AddHours(11.25)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);
        var dayCode = GetDayCodePublic(newYear);

        // Assert
        // Total work: 8 hours
        // Pause: 0.5 hours (2 x 0.25)
        // Net: 7.5 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.5));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(27000));
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"));
    }

    [Test]
    public void PlanRegistration_WithMultipleShiftsOnWeekday_CalculatesCorrectHours()
    {
        // Arrange - Regular weekday with split shifts
        var weekday = new DateTime(2026, 3, 10); // Tuesday
        var planRegistration = new PlanRegistration
        {
            Date = weekday,
            // Early morning: 6:00 - 10:00 (4 hours)
            Start1StartedAt = weekday.AddHours(6),
            Stop1StoppedAt = weekday.AddHours(10),
            // Late morning/midday: 11:00 - 14:00 (3 hours)
            Start2StartedAt = weekday.AddHours(11),
            Stop2StoppedAt = weekday.AddHours(14),
            // Evening: 18:00 - 21:00 (3 hours)
            Start3StartedAt = weekday.AddHours(18),
            Stop3StoppedAt = weekday.AddHours(21),
            // Breaks during shifts
            Pause1StartedAt = weekday.AddHours(8),
            Pause1StoppedAt = weekday.AddHours(8.25),
            Pause2StartedAt = weekday.AddHours(12),
            Pause2StoppedAt = weekday.AddHours(12.5),
            Pause3StartedAt = weekday.AddHours(19.5),
            Pause3StoppedAt = weekday.AddHours(19.75)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Total work: 10 hours (4 + 3 + 3)
        // Pause: 1.0 hours (0.25 + 0.5 + 0.25)
        // Net: 9.0 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(9.0));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(32400)); // 9 * 3600
    }

    [Test]
    public void PlanRegistration_OnChristmasEve_CalculatesCorrectHours()
    {
        // Arrange - Christmas Eve, December 24, 2026 (overenskomstfastsat fridag)
        var christmasEve = new DateTime(2026, 12, 24);
        var planRegistration = new PlanRegistration
        {
            Date = christmasEve,
            // Short shift: 8:00 - 12:00 (4 hours)
            Start1StartedAt = christmasEve.AddHours(8),
            Stop1StoppedAt = christmasEve.AddHours(12),
            // No breaks
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);
        var dayCode = GetDayCodePublic(christmasEve);

        // Assert
        // Total work: 4 hours
        // Net: 4 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(4.0));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(14400));
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"));
    }

    [Test]
    public void PlanRegistration_OnEaster_CalculatesCorrectHours()
    {
        // Arrange - Easter Sunday, April 5, 2026 (movable holiday)
        var easter = new DateTime(2026, 4, 5);
        var planRegistration = new PlanRegistration
        {
            Date = easter,
            // Single shift: 10:00 - 18:00 (8 hours)
            Start1StartedAt = easter.AddHours(10),
            Stop1StoppedAt = easter.AddHours(18),
            // Lunch break: 13:00 - 14:00 (1 hour)
            Pause1StartedAt = easter.AddHours(13),
            Pause1StoppedAt = easter.AddHours(14)
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);
        var dayCode = GetDayCodePublic(easter);

        // Assert
        // Total work: 8 hours
        // Pause: 1 hour
        // Net: 7 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.0));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(25200)); // 7 * 3600
        Assert.That(dayCode, Is.EqualTo("HOLIDAY"));
    }

    [Test]
    public void PlanRegistration_LongShiftOnWeekday_WithMultiplePauses_CalculatesCorrectHours()
    {
        // Arrange - Long shift with multiple short breaks
        var weekday = new DateTime(2026, 5, 12); // Tuesday
        var planRegistration = new PlanRegistration
        {
            Date = weekday,
            // Long shift: 7:00 - 19:00 (12 hours)
            Start1StartedAt = weekday.AddHours(7),
            Stop1StoppedAt = weekday.AddHours(19),
            // Multiple breaks throughout the day
            Pause1StartedAt = weekday.AddHours(9),
            Pause1StoppedAt = weekday.AddHours(9.25),    // 15 min
            Pause2StartedAt = weekday.AddHours(12),
            Pause2StoppedAt = weekday.AddHours(12.5),    // 30 min
            Pause3StartedAt = weekday.AddHours(15),
            Pause3StoppedAt = weekday.AddHours(15.25),   // 15 min
            Pause4StartedAt = weekday.AddHours(17),
            Pause4StoppedAt = weekday.AddHours(17.25)    // 15 min
        };

        // Act
        PlanRegistrationHelper.ComputeTimeTrackingFields(planRegistration);

        // Assert
        // Total work: 12 hours
        // Pause: 1.25 hours (15 + 30 + 15 + 15 min)
        // Net: 10.75 hours
        Assert.That(planRegistration.NettoHours, Is.EqualTo(10.75));
        Assert.That(planRegistration.NettoHoursInSeconds, Is.EqualTo(38700)); // 10.75 * 3600
    }

    // ------------------------------------------------------------------
    // IsStatutoryHoliday: TRUE only for JSON entries whose category is
    // "official_holiday". The agreement-based days off
    // ("overenskomstfastsat_fridag" -- Grundlovsdag and Juleaften) are days
    // off by collective agreement, NOT statutory holidays, so they are
    // excluded. IsOfficialHoliday deliberately keeps counting them; the two
    // predicates are NOT interchangeable.
    // ------------------------------------------------------------------

    [Test]
    [TestCase("2026-01-01", "Nytårsdag 2026")]
    [TestCase("2026-04-02", "Skærtorsdag 2026")]
    [TestCase("2026-04-03", "Langfredag 2026")]
    [TestCase("2026-04-06", "2. påskedag 2026")]
    [TestCase("2026-05-14", "Kristi himmelfartsdag 2026")]
    [TestCase("2026-12-25", "Juledag 2026")]
    [TestCase("2026-12-26", "2. juledag 2026")]
    [TestCase("2027-03-25", "Skærtorsdag 2027")]
    public void IsStatutoryHoliday_OfficialHolidayCategory_ReturnsTrue(string dateString, string description)
    {
        var date = DateTime.Parse(dateString);

        Assert.That(PlanRegistrationHelper.IsStatutoryHoliday(date), Is.True,
            $"{description} ({dateString}) carries category 'official_holiday' and must be statutory");
    }

    [Test]
    [TestCase("2026-06-05", "Grundlovsdag 2026")]
    [TestCase("2027-06-05", "Grundlovsdag 2027")]
    [TestCase("2026-12-24", "Juleaften 2026")]
    [TestCase("2027-12-24", "Juleaften 2027")]
    public void IsStatutoryHoliday_OverenskomstfastsatFridag_ReturnsFalse(string dateString, string description)
    {
        var date = DateTime.Parse(dateString);

        Assert.That(PlanRegistrationHelper.IsStatutoryHoliday(date), Is.False,
            $"{description} ({dateString}) is 'overenskomstfastsat_fridag', not a statutory holiday");

        // ...but it is still an official holiday for the Søn- og helligdagstimer
        // column: the two predicates must NOT collapse into each other.
        Assert.That(PlanRegistrationHelper.IsOfficialHoliday(date), Is.True,
            $"{description} ({dateString}) must still count as an official holiday");
    }

    [Test]
    [TestCase("2026-11-16", "Ordinary Monday")]
    [TestCase("2026-07-06", "Ordinary Monday in summer")]
    [TestCase("2026-12-23", "Ordinary Wednesday before Christmas")]
    public void IsStatutoryHoliday_OrdinaryWeekday_ReturnsFalse(string dateString, string description)
    {
        var date = DateTime.Parse(dateString);

        Assert.That(PlanRegistrationHelper.IsStatutoryHoliday(date), Is.False,
            $"{description} ({dateString}) is not in the holiday calendar at all");
    }

    [Test]
    [TestCase("2024-12-25", "Juledag before the calendar's from-date")]
    [TestCase("2025-01-01", "Nytårsdag 2025 -- calendar starts 2025-12-24")]
    [TestCase("2031-12-25", "Juledag after the calendar's to-date")]
    public void IsStatutoryHoliday_DateOutsideCalendarRange_ReturnsFalse(string dateString, string description)
    {
        var date = DateTime.Parse(dateString);

        Assert.That(PlanRegistrationHelper.IsStatutoryHoliday(date), Is.False,
            $"{description} ({dateString}) is outside danish_holidays_2025_2030.json and must fail closed");
    }

    [Test]
    public void IsStatutoryHoliday_IgnoresTimeOfDay()
    {
        var justBeforeMidnight = new DateTime(2026, 12, 25, 23, 59, 59);

        Assert.That(PlanRegistrationHelper.IsStatutoryHoliday(justBeforeMidnight), Is.True,
            "The date is normalized to midnight before comparison, like IsOfficialHoliday");
    }

    [Test]
    public void IsStatutoryHoliday_IsAStrictSubsetOfIsOfficialHoliday_OverTheWholeCalendar()
    {
        // The Total sheet's column K (Helligdagstimer) may never exceed column J
        // (Søn- og helligdagstimer); that rests on this subset relation holding
        // for EVERY day in the calendar, not just the sampled ones.
        var statutoryCount = 0;
        var officialOnlyCount = 0;

        for (var date = new DateTime(2025, 1, 1); date <= new DateTime(2031, 12, 31); date = date.AddDays(1))
        {
            var statutory = PlanRegistrationHelper.IsStatutoryHoliday(date);
            var official = PlanRegistrationHelper.IsOfficialHoliday(date);

            if (statutory)
            {
                statutoryCount++;
                Assert.That(official, Is.True,
                    $"{date:yyyy-MM-dd} is statutory but not official -- the subset relation is broken");
            }
            else if (official)
            {
                officialOnlyCount++;
            }
        }

        Assert.That(statutoryCount, Is.EqualTo(52),
            "danish_holidays_2025_2030.json carries exactly 52 'official_holiday' entries");
        // 11 'overenskomstfastsat_fridag' entries exist, but 2028-06-05 carries
        // BOTH a Grundlovsdag entry and a '2. pinsedag' official_holiday entry
        // (they collide that year), so only 10 DATES are official-but-not-statutory.
        Assert.That(officialOnlyCount, Is.EqualTo(10),
            "...and 10 dates that are official but NOT statutory (11 agreement entries minus the 2028-06-05 collision)");
    }

    [Test]
    public void IsStatutoryHoliday_Grundlovsdag2028_CollidesWithSecondWhitMonday_AndIsStatutory()
    {
        // KNOWN COLLISION, pinned deliberately: in 2028 Grundlovsdag (5 June)
        // falls on 2. pinsedag, so the JSON carries TWO entries for that date --
        // one 'overenskomstfastsat_fridag' and one 'official_holiday'. The
        // category match therefore makes it statutory, which is the right call
        // for a full public holiday.
        //
        // Consequence, called out in the change report: on this single date the
        // Total sheet's column K (full netto hours) can EXCEED column J, because
        // column J short-circuits on IsGrundlovsdag and applies the noon split.
        // Column J's Grundlovsdag handling is out of scope here.
        var collision = new DateTime(2028, 6, 5);

        Assert.That(PlanRegistrationHelper.IsStatutoryHoliday(collision), Is.True,
            "2028-06-05 carries a '2. pinsedag' official_holiday entry and must be statutory");
        Assert.That(PlanRegistrationHelper.IsGrundlovsdag(collision), Is.True,
            "...while still being Grundlovsdag by the date rule");
    }

    // Helper method to test the private GetDayCode method via reflection
    private string GetDayCodePublic(DateTime date)
    {
        var method = typeof(PlanRegistrationHelper).GetMethod("GetDayCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)method.Invoke(null, new object[] { date });
    }
}
