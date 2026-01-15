using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class TimePlanningWorkingHoursExportTests
{
    /// <summary>
    /// Test that validates the Total Hours calculation includes all worked hours.
    /// Total Hours should be the sum of all NettoHours from all days.
    /// </summary>
    [Test]
    public void TotalSheet_TotalHours_IncludesAllWorkedHours()
    {
        // Arrange - Create a mix of workdays, weekends, and holidays
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            // Regular weekday - 8 hours
            CreateWorkDay(new DateTime(2026, 1, 5), 8.0, false, false), // Monday
            // Saturday - 5 hours
            CreateWorkDay(new DateTime(2026, 1, 10), 5.0, true, false),
            // Sunday - 4 hours
            CreateWorkDay(new DateTime(2026, 1, 11), 4.0, false, true),
            // Christmas Day (holiday) - 6 hours
            CreateWorkDay(new DateTime(2026, 12, 25), 6.0, false, false)
        };
        
        // Act
        var totalHours = workingHours.Sum(x => x.NettoHours);
        
        // Assert
        Assert.That(totalHours, Is.EqualTo(23.0), "Total hours should be sum of all worked hours");
    }
    
    /// <summary>
    /// Test that validates Normal Hours (NettoHours in export) excludes Sunday and holiday hours.
    /// Normal Hours = Total Hours - Saturday Hours - Sunday/Holiday Hours
    /// </summary>
    [Test]
    public void TotalSheet_NormalHours_ExcludesSundaysAndHolidays()
    {
        // Arrange
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            // Regular weekday - 8 hours
            CreateWorkDay(new DateTime(2026, 1, 5), 8.0, false, false),
            // Another weekday - 7.5 hours  
            CreateWorkDay(new DateTime(2026, 1, 6), 7.5, false, false),
            // Saturday - 5 hours
            CreateWorkDay(new DateTime(2026, 1, 10), 5.0, true, false),
            // Sunday - 4 hours
            CreateWorkDay(new DateTime(2026, 1, 11), 4.0, false, true),
            // Christmas Day (holiday) - 6 hours
            CreateWorkDay(new DateTime(2026, 12, 25), 6.0, false, false)
        };
        
        // Act
        var totalHours = workingHours.Sum(x => x.NettoHours);
        var saturdayHours = workingHours.Where(x => x.IsSaturday).Sum(x => x.NettoHours);
        var sundayAndHolidayHours = CalculateSundayAndHolidayHours(workingHours);
        var normalHours = totalHours - sundayAndHolidayHours;
        
        // Assert
        Assert.That(totalHours, Is.EqualTo(30.5), "Total should be 30.5 hours");
        Assert.That(saturdayHours, Is.EqualTo(5.0), "Saturday hours should be 5.0");
        Assert.That(sundayAndHolidayHours, Is.EqualTo(10.0), "Sunday and holiday hours should be 10.0 (4 + 6)");
        Assert.That(normalHours, Is.EqualTo(20.5), "Normal hours should be 20.5 (8 + 7.5 + 5)");
    }
    
    /// <summary>
    /// Test that validates Saturday hours are calculated correctly.
    /// </summary>
    [Test]
    public void TotalSheet_SaturdayHours_CalculatedCorrectly()
    {
        // Arrange
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            CreateWorkDay(new DateTime(2026, 1, 3), 8.0, true, false), // Saturday - 8 hours
            CreateWorkDay(new DateTime(2026, 1, 10), 5.0, true, false), // Saturday - 5 hours
            CreateWorkDay(new DateTime(2026, 1, 5), 8.0, false, false), // Monday - 8 hours
        };
        
        // Act
        var saturdayHours = workingHours.Where(x => x.IsSaturday).Sum(x => x.NettoHours);
        
        // Assert
        Assert.That(saturdayHours, Is.EqualTo(13.0), "Saturday hours should be 13.0 (8 + 5)");
    }
    
    /// <summary>
    /// Test that validates Sunday and Holiday hours includes Sundays and official holidays.
    /// </summary>
    [Test]
    public void TotalSheet_SundayAndHolidayHours_IncludesSundaysAndOfficialHolidays()
    {
        // Arrange
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            CreateWorkDay(new DateTime(2026, 1, 4), 6.0, false, true), // Sunday - 6 hours
            CreateWorkDay(new DateTime(2026, 1, 11), 4.0, false, true), // Sunday - 4 hours
            CreateWorkDay(new DateTime(2026, 12, 25), 8.0, false, false), // Christmas (holiday) - 8 hours
            CreateWorkDay(new DateTime(2026, 1, 1), 5.0, false, false), // New Year (holiday) - 5 hours
            CreateWorkDay(new DateTime(2026, 1, 5), 8.0, false, false), // Monday - 8 hours (not included)
        };
        
        // Act
        var sundayAndHolidayHours = CalculateSundayAndHolidayHours(workingHours);
        
        // Assert
        Assert.That(sundayAndHolidayHours, Is.EqualTo(23.0), 
            "Sunday and holiday hours should be 23.0 (6 + 4 + 8 + 5)");
    }
    
    /// <summary>
    /// Test that validates Grundlovsdag (Constitution Day) only counts hours after 12:00 as holiday hours.
    /// Hours before 12:00 are treated as normal hours.
    /// </summary>
    [Test]
    public void TotalSheet_Grundlovsdag_OnlyCountsAfternoonHoursAsHoliday()
    {
        // Arrange - Worker works 8:00-16:00 (8 hours) on Grundlovsdag
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            CreateWorkDayWithShifts(
                new DateTime(2026, 6, 5), // Grundlovsdag
                start1: new DateTime(2026, 6, 5, 8, 0, 0),
                stop1: new DateTime(2026, 6, 5, 16, 0, 0),
                nettoHours: 8.0
            )
        };
        
        // Act
        var totalHours = workingHours.Sum(x => x.NettoHours);
        var sundayAndHolidayHours = CalculateSundayAndHolidayHours(workingHours);
        var normalHours = totalHours - sundayAndHolidayHours;
        
        // Assert
        Assert.That(totalHours, Is.EqualTo(8.0), "Total hours should be 8.0");
        // Only hours after 12:00 (12:00-16:00 = 4 hours) count as holiday
        Assert.That(sundayAndHolidayHours, Is.EqualTo(4.0), 
            "Only afternoon hours (12:00-16:00) should count as holiday hours");
        Assert.That(normalHours, Is.EqualTo(4.0), 
            "Morning hours (8:00-12:00) should count as normal hours");
    }
    
    /// <summary>
    /// Test that validates Grundlovsdag morning shift (before 12:00) is counted as normal hours.
    /// </summary>
    [Test]
    public void TotalSheet_Grundlovsdag_MorningShiftCountedAsNormalHours()
    {
        // Arrange - Worker only works morning shift 8:00-11:00 (3 hours) on Grundlovsdag
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            CreateWorkDayWithShifts(
                new DateTime(2026, 6, 5), // Grundlovsdag
                start1: new DateTime(2026, 6, 5, 8, 0, 0),
                stop1: new DateTime(2026, 6, 5, 11, 0, 0),
                nettoHours: 3.0
            )
        };
        
        // Act
        var totalHours = workingHours.Sum(x => x.NettoHours);
        var sundayAndHolidayHours = CalculateSundayAndHolidayHours(workingHours);
        var normalHours = totalHours - sundayAndHolidayHours;
        
        // Assert
        Assert.That(totalHours, Is.EqualTo(3.0), "Total hours should be 3.0");
        Assert.That(sundayAndHolidayHours, Is.EqualTo(0.0), 
            "No hours should count as holiday since all work is before 12:00");
        Assert.That(normalHours, Is.EqualTo(3.0), 
            "All morning hours should count as normal hours");
    }
    
    /// <summary>
    /// Test for complete period with mixed day types - validates all calculations work together.
    /// </summary>
    [Test]
    public void TotalSheet_CompletePeriod_AllCalculationsCorrect()
    {
        // Arrange - One week period with various day types
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            CreateWorkDay(new DateTime(2026, 1, 5), 8.0, false, false),  // Monday
            CreateWorkDay(new DateTime(2026, 1, 6), 8.5, false, false),  // Tuesday
            CreateWorkDay(new DateTime(2026, 1, 7), 7.5, false, false),  // Wednesday
            CreateWorkDay(new DateTime(2026, 1, 8), 8.0, false, false),  // Thursday
            CreateWorkDay(new DateTime(2026, 1, 9), 6.0, false, false),  // Friday
            CreateWorkDay(new DateTime(2026, 1, 10), 5.0, true, false),  // Saturday
            CreateWorkDay(new DateTime(2026, 1, 11), 3.0, false, true),  // Sunday
        };
        
        // Act
        var totalHours = workingHours.Sum(x => x.NettoHours);
        var saturdayHours = workingHours.Where(x => x.IsSaturday).Sum(x => x.NettoHours);
        var sundayAndHolidayHours = CalculateSundayAndHolidayHours(workingHours);
        var normalHours = totalHours - sundayAndHolidayHours;
        
        // Assert
        Assert.That(totalHours, Is.EqualTo(46.0), 
            "Total hours should be 46.0 (8+8.5+7.5+8+6+5+3)");
        Assert.That(saturdayHours, Is.EqualTo(5.0), 
            "Saturday hours should be 5.0");
        Assert.That(sundayAndHolidayHours, Is.EqualTo(3.0), 
            "Sunday hours should be 3.0");
        Assert.That(normalHours, Is.EqualTo(43.0), 
            "Normal hours should be 43.0 (total 46 - sunday 3)");
    }
    
    /// <summary>
    /// Test with Christmas period including Christmas Eve (overenskomstfastsat fridag).
    /// </summary>
    [Test]
    public void TotalSheet_ChristmasPeriod_CalculationsCorrect()
    {
        // Arrange
        var workingHours = new List<TimePlanningWorkingHoursModel>
        {
            CreateWorkDay(new DateTime(2026, 12, 23), 8.0, false, false), // Regular day before Christmas
            CreateWorkDay(new DateTime(2026, 12, 24), 6.0, false, false), // Christmas Eve (fridag)
            CreateWorkDay(new DateTime(2026, 12, 25), 5.0, false, false), // Christmas Day (holiday)
            CreateWorkDay(new DateTime(2026, 12, 26), 5.0, false, false), // Boxing Day (holiday)
        };
        
        // Act
        var totalHours = workingHours.Sum(x => x.NettoHours);
        var sundayAndHolidayHours = CalculateSundayAndHolidayHours(workingHours);
        var normalHours = totalHours - sundayAndHolidayHours;
        
        // Assert
        Assert.That(totalHours, Is.EqualTo(24.0), "Total hours should be 24.0");
        Assert.That(sundayAndHolidayHours, Is.EqualTo(16.0), 
            "Holiday hours should include Christmas Eve, Christmas Day, and Boxing Day (6+5+5)");
        Assert.That(normalHours, Is.EqualTo(8.0), 
            "Normal hours should only include Dec 23");
    }
    
    // Helper methods
    
    private TimePlanningWorkingHoursModel CreateWorkDay(DateTime date, double nettoHours, bool isSaturday, bool isSunday)
    {
        return new TimePlanningWorkingHoursModel
        {
            Date = date,
            NettoHours = nettoHours,
            IsSaturday = isSaturday,
            IsSunday = isSunday
        };
    }
    
    private TimePlanningWorkingHoursModel CreateWorkDayWithShifts(DateTime date, DateTime start1, DateTime stop1, double nettoHours)
    {
        return new TimePlanningWorkingHoursModel
        {
            Date = date,
            NettoHours = nettoHours,
            Start1StartedAt = start1,
            Stop1StoppedAt = stop1,
            IsSaturday = date.DayOfWeek == DayOfWeek.Saturday,
            IsSunday = date.DayOfWeek == DayOfWeek.Sunday
        };
    }
    
    /// <summary>
    /// Simulates the calculation logic from TimePlanningWorkingHoursService.
    /// Calculates Sunday and Holiday hours including special handling for Grundlovsdag.
    /// </summary>
    private double CalculateSundayAndHolidayHours(List<TimePlanningWorkingHoursModel> workingHours)
    {
        var sumHoursSundayAndHoliday = 0.0;
        
        foreach (var day in workingHours)
        {
            // Check if it's Sunday or a holiday
            var isSundayOrHoliday = day.IsSunday || PlanRegistrationHelper.IsOfficialHoliday(day.Date);
            
            if (isSundayOrHoliday)
            {
                // Special handling for Grundlovsdag - only count hours after 12:00
                if (PlanRegistrationHelper.IsGrundlovsdag(day.Date))
                {
                    // Calculate hours after 12:00
                    var hoursAfterNoon = CalculateHoursAfterNoon(day);
                    sumHoursSundayAndHoliday += hoursAfterNoon;
                }
                else
                {
                    // For other Sundays/holidays, count all hours
                    sumHoursSundayAndHoliday += day.NettoHours;
                }
            }
        }
        
        return sumHoursSundayAndHoliday;
    }
    
    /// <summary>
    /// Simulates the CalculateHoursAfterNoon logic from TimePlanningWorkingHoursService.
    /// </summary>
    private double CalculateHoursAfterNoon(TimePlanningWorkingHoursModel day)
    {
        var noonTime = new DateTime(day.Date.Year, day.Date.Month, day.Date.Day, 12, 0, 0);
        double totalSecondsAfterNoon = 0;
        
        // Helper to calculate overlap with period after noon
        double CalculateOverlap(DateTime? start, DateTime? stop)
        {
            if (!start.HasValue || !stop.HasValue || start >= stop)
                return 0;
            
            // If the entire period is before noon, no overlap
            if (stop <= noonTime)
                return 0;
            
            // Calculate the overlapping portion
            var effectiveStart = start < noonTime ? noonTime : start.Value;
            var effectiveEnd = stop.Value;
            
            return (effectiveEnd - effectiveStart).TotalSeconds;
        }
        
        // Calculate overlap for each shift
        totalSecondsAfterNoon += CalculateOverlap(day.Start1StartedAt, day.Stop1StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start2StartedAt, day.Stop2StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start3StartedAt, day.Stop3StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start4StartedAt, day.Stop4StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start5StartedAt, day.Stop5StoppedAt);
        
        // Subtract pauses that occur after noon
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause1StartedAt, day.Pause1StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause2StartedAt, day.Pause2StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause3StartedAt, day.Pause3StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause4StartedAt, day.Pause4StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause5StartedAt, day.Pause5StoppedAt);
        
        // Convert to hours and ensure non-negative
        return Math.Max(0, totalSecondsAfterNoon / 3600.0);
    }
}
