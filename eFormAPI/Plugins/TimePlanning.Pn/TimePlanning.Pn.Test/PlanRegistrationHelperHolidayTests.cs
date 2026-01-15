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
        Assert.That(planRegistration.NettoHours, Is.EqualTo(7.5).Within(0.01));
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

    // Helper method to test the private GetDayCode method via reflection
    private string GetDayCodePublic(DateTime date)
    {
        var method = typeof(PlanRegistrationHelper).GetMethod("GetDayCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)method.Invoke(null, new object[] { date });
    }
}
