using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanRegistrationHelperTests
{
    [TestCase(DayOfWeek.Monday, 180, 30, 2, 60, 12, 96, 12)]
    [TestCase(DayOfWeek.Tuesday, 180, 30, 2, 60, 24, 30, 0)]
    [TestCase(DayOfWeek.Wednesday, 180, 30, 2, 60, 96, 192, 12)]
    [TestCase(DayOfWeek.Thursday, 180, 30, 2, 60, 96, 132, 6)]
    [TestCase(DayOfWeek.Friday, 180, 30, 2, 60, 96, 132, 6)]
    [TestCase(DayOfWeek.Saturday, 120, 30, 2, 60, 96, 132, 6)]
    [TestCase(DayOfWeek.Sunday, 120, 30, 2, 60, 96, 192, 12)]
    public void CalculatePauseAutoBreakCalculationActive_SetsPause1Id_Correctly(
        DayOfWeek dayOfWeek,
        int breakMinutesDivider,
        int breakMinutesPrDivider,
        int minutesActualAtWorkDivisor,
        int breakMinutesUpperLimit,
        int startId,
        int stopId,
        int expectedPause1Id)
    {
        // Arrange
        var assignedSite = new AssignedSite
        {
            AutoBreakCalculationActive = true,
            MondayBreakMinutesDivider = breakMinutesDivider,
            MondayBreakMinutesPrDivider = breakMinutesPrDivider,
            MondayBreakMinutesUpperLimit = breakMinutesUpperLimit,
            TuesdayBreakMinutesDivider = breakMinutesDivider,
            TuesdayBreakMinutesPrDivider = breakMinutesPrDivider,
            TuesdayBreakMinutesUpperLimit = breakMinutesUpperLimit,
            WednesdayBreakMinutesDivider = breakMinutesDivider,
            WednesdayBreakMinutesPrDivider = breakMinutesPrDivider,
            WednesdayBreakMinutesUpperLimit = breakMinutesUpperLimit,
            ThursdayBreakMinutesDivider = breakMinutesDivider,
            ThursdayBreakMinutesPrDivider = breakMinutesPrDivider,
            ThursdayBreakMinutesUpperLimit = breakMinutesUpperLimit,
            FridayBreakMinutesDivider = breakMinutesDivider,
            FridayBreakMinutesPrDivider = breakMinutesPrDivider,
            FridayBreakMinutesUpperLimit = breakMinutesUpperLimit,
            SaturdayBreakMinutesDivider = breakMinutesDivider,
            SaturdayBreakMinutesPrDivider = breakMinutesPrDivider,
            SaturdayBreakMinutesUpperLimit = breakMinutesUpperLimit,
            SundayBreakMinutesDivider = breakMinutesDivider,
            SundayBreakMinutesPrDivider = breakMinutesPrDivider,
            SundayBreakMinutesUpperLimit = breakMinutesUpperLimit,
        };

        var planning = new PlanRegistration
        {
            Date = DateTime.Today.AddDays(dayOfWeek - DateTime.Today.DayOfWeek),
            Start1Id = startId,
            Stop1Id = stopId,
            Start2Id = 0,
            Stop2Id = 0,
            Start3Id = 0,
            Stop3Id = 0,
            Start4Id = 0,
            Stop4Id = 0,
            Start5Id = 0,
            Stop5Id = 0
        };

        // Calculate expected Pause1Id
        var minutesActualAtWork = (stopId - startId) * 5;
        var numberOfBreaks = minutesActualAtWork / breakMinutesDivider;
        var breakTime = numberOfBreaks * breakMinutesPrDivider;
        // var expectedPause1Id = breakTime < breakMinutesUpperLimit ? breakTime : breakMinutesUpperLimit;

        // Act
        var result = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planning);

        // Assert
        // Assert.That(expectedPause1Id, result.Pause1Id, $"Failed for {dayOfWeek}");
        Assert.That(result.Pause1Id, Is.EqualTo(expectedPause1Id), $"Failed for {dayOfWeek}");
    }
}