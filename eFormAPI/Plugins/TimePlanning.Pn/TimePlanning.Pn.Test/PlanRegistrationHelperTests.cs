using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanRegistrationHelperTests
{
    [TestCase(DayOfWeek.Monday, 180, 30, 2, 60, 12, 96, 13)]
    [TestCase(DayOfWeek.Tuesday, 180, 30, 2, 60, 24, 30, 0)]
    [TestCase(DayOfWeek.Wednesday, 180, 30, 2, 60, 96, 192, 13)]
    [TestCase(DayOfWeek.Thursday, 180, 30, 2, 60, 96, 132, 7)]
    [TestCase(DayOfWeek.Friday, 180, 30, 2, 60, 96, 132, 7)]
    [TestCase(DayOfWeek.Saturday, 120, 30, 2, 60, 96, 132, 7)]
    [TestCase(DayOfWeek.Sunday, 120, 30, 2, 60, 96, 192, 13)]
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

    /// <summary>
    /// Phase 0 plumbing test: proves that toggling
    /// <see cref="AssignedSite.UseOneMinuteIntervals"/> on/off has no observable
    /// effect today. The new if-branch in
    /// <see cref="PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive"/>
    /// is a TODO stub that falls through to the existing 5-minute logic, so
    /// pause IDs must be byte-identical between flag-on and flag-off runs.
    /// </summary>
    [Test]
    public void CalculatePauseAutoBreakCalculationActive_FlagOnAndFlagOff_ReturnSamePauseId()
    {
        // Arrange — identical seed for both runs except for the flag.
        AssignedSite BuildAssignedSite(bool useOneMinuteIntervals) => new()
        {
            UseOneMinuteIntervals = useOneMinuteIntervals,
            AutoBreakCalculationActive = true,
            MondayBreakMinutesDivider = 180,
            MondayBreakMinutesPrDivider = 30,
            MondayBreakMinutesUpperLimit = 60,
            TuesdayBreakMinutesDivider = 180,
            TuesdayBreakMinutesPrDivider = 30,
            TuesdayBreakMinutesUpperLimit = 60,
            WednesdayBreakMinutesDivider = 180,
            WednesdayBreakMinutesPrDivider = 30,
            WednesdayBreakMinutesUpperLimit = 60,
            ThursdayBreakMinutesDivider = 180,
            ThursdayBreakMinutesPrDivider = 30,
            ThursdayBreakMinutesUpperLimit = 60,
            FridayBreakMinutesDivider = 180,
            FridayBreakMinutesPrDivider = 30,
            FridayBreakMinutesUpperLimit = 60,
            SaturdayBreakMinutesDivider = 120,
            SaturdayBreakMinutesPrDivider = 30,
            SaturdayBreakMinutesUpperLimit = 60,
            SundayBreakMinutesDivider = 120,
            SundayBreakMinutesPrDivider = 30,
            SundayBreakMinutesUpperLimit = 60,
        };

        PlanRegistration BuildPlanning() => new()
        {
            Date = DateTime.Today.AddDays(DayOfWeek.Wednesday - DateTime.Today.DayOfWeek),
            Start1Id = 96,
            Stop1Id = 192,
            Start2Id = 0,
            Stop2Id = 0,
            Start3Id = 0,
            Stop3Id = 0,
            Start4Id = 0,
            Stop4Id = 0,
            Start5Id = 0,
            Stop5Id = 0
        };

        // Act
        var resultFlagOff =
            PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(BuildAssignedSite(false), BuildPlanning());
        var resultFlagOn =
            PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(BuildAssignedSite(true), BuildPlanning());

        // Assert — every pause ID is byte-identical between the two runs.
        Assert.Multiple(() =>
        {
            Assert.That(resultFlagOn.Pause1Id, Is.EqualTo(resultFlagOff.Pause1Id), "Pause1Id must match flag-off run");
            Assert.That(resultFlagOn.Pause2Id, Is.EqualTo(resultFlagOff.Pause2Id), "Pause2Id must match flag-off run");
            Assert.That(resultFlagOn.Pause3Id, Is.EqualTo(resultFlagOff.Pause3Id), "Pause3Id must match flag-off run");
            Assert.That(resultFlagOn.Pause4Id, Is.EqualTo(resultFlagOff.Pause4Id), "Pause4Id must match flag-off run");
            Assert.That(resultFlagOn.Pause5Id, Is.EqualTo(resultFlagOff.Pause5Id), "Pause5Id must match flag-off run");
        });
    }

    /// <summary>
    /// Phase 0 plumbing test: the new 2-arg
    /// <see cref="PlanRegistrationHelper.RecalculatePlanHoursFromShifts(PlanRegistration, bool)"/>
    /// overload must delegate to the existing 1-arg version regardless of
    /// the <c>useOneMinuteIntervals</c> flag, because planned-shift precision
    /// stays minute-only per the rollout plan. So both flag values plus the
    /// 1-arg call must produce identical PlanHours / PlanHoursInSeconds.
    /// </summary>
    [Test]
    public void RecalculatePlanHoursFromShifts_FlagOnAndFlagOff_ReturnSamePlanHours()
    {
        // Arrange — three identical PlanRegistrations exercised down three paths.
        PlanRegistration BuildPlan() => new()
        {
            PlannedStartOfShift1 = 420,
            PlannedEndOfShift1 = 900,
            PlannedBreakOfShift1 = 30,
        };

        var prOneArg = BuildPlan();
        var prFlagOff = BuildPlan();
        var prFlagOn = BuildPlan();

        // Act
        PlanRegistrationHelper.RecalculatePlanHoursFromShifts(prOneArg);
        PlanRegistrationHelper.RecalculatePlanHoursFromShifts(prFlagOff, useOneMinuteIntervals: false);
        PlanRegistrationHelper.RecalculatePlanHoursFromShifts(prFlagOn, useOneMinuteIntervals: true);

        // Assert — all three paths produce identical totals.
        Assert.Multiple(() =>
        {
            Assert.That(prFlagOff.PlanHours, Is.EqualTo(prOneArg.PlanHours),
                "Flag-off 2-arg path must match 1-arg path");
            Assert.That(prFlagOff.PlanHoursInSeconds, Is.EqualTo(prOneArg.PlanHoursInSeconds),
                "Flag-off 2-arg PlanHoursInSeconds must match 1-arg");
            Assert.That(prFlagOn.PlanHours, Is.EqualTo(prOneArg.PlanHours),
                "Flag-on 2-arg path must match 1-arg path");
            Assert.That(prFlagOn.PlanHoursInSeconds, Is.EqualTo(prOneArg.PlanHoursInSeconds),
                "Flag-on 2-arg PlanHoursInSeconds must match 1-arg");
        });
    }

    /// <summary>
    /// Phase 0 plumbing test for the new
    /// <c>RoundDownToNearestFiveMinutesAndFormat(DateTime, int, bool)</c>
    /// overload on <c>TimePlanningWorkingHoursService</c>. The helper is
    /// <c>private static</c> and there is no <c>InternalsVisibleTo</c>
    /// attribute, so the only public consumer today is
    /// <c>TimePlanningWorkingHoursService.ReadSimple</c>, which requires
    /// the full SDK / DB fixture (sdkContext, baseDbContext, userService,
    /// currentUser, sdkSiteWorker, etc.) that this test fixture does not
    /// yet wire up. Per the rollout plan, mirror the existing
    /// <c>[Ignore]</c> carve-out used elsewhere for pre-fixture tests so
    /// the assertions are captured for future fixture work without
    /// blocking Phase 0 CI. The intent is identical to the two tests
    /// above: flag-on and flag-off must produce byte-identical strings
    /// in Phase 0 because the new overload simply delegates to the
    /// existing 2-arg helper.
    /// </summary>
    [Test]
    [Ignore("Phase 0 carve-out: ReadSimple requires SDK/DB fixture not wired here; assertion captured for future fixture work.")]
    public void RoundDownToNearestFiveMinutesAndFormat_FlagOnAndFlagOff_ReturnSameString()
    {
        // Arrange / Act / Assert (intent, to be enabled when fixture lands):
        //
        //   var midnight = new DateTime(2026, 1, 5, 0, 0, 0);
        //   var resultFlagOff = service.ReadSimpleForTest(midnight, useOneMinuteIntervals: false);
        //   var resultFlagOn  = service.ReadSimpleForTest(midnight, useOneMinuteIntervals: true);
        //   Assert.That(resultFlagOn.Start1StartedAt, Is.EqualTo(resultFlagOff.Start1StartedAt));
        //
        // Phase 0 contract: the new 3-arg private helper is a pass-through
        // to the existing 2-arg helper, so all formatted strings must match
        // byte-for-byte regardless of the flag.
        Assert.Pass("Captured for future fixture work; see XML doc above.");
    }
}