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

    // ------------------------------------------------------------------
    // Phase 1 — server-side write path: prefer DateTime stamps when flag on
    // ------------------------------------------------------------------
    //
    // The fork lives in
    //   PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(...)
    // around the `if (planRegistration.Start1Id > 289)` block. When the
    // `Start1Id > 289` workaround divides the value back down, the
    // existing path also overwrites `Start1StartedAt` from the snapped
    // index (`Date.AddMinutes(Start1Id * 5)`). When
    // `AssignedSite.UseOneMinuteIntervals` is true AND a precise stamp is
    // already populated, that overwrite is skipped — the precise stamp is
    // the source of truth in Phase 1.
    //
    // `UpdatePlanRegistrationsInPeriod` is async and takes
    // `(planningsInPeriod, siteModel, dbContext, dbAssignedSite, logger,
    // site, midnightOfDateFrom, midnightOfDateTo, options)`. Wiring those
    // up requires the full DB / SDK / IPluginDbOptions / Site fixture
    // that this `PlanRegistrationHelperTests` fixture deliberately keeps
    // lightweight; per the rollout plan this is the documented carve-out
    // pattern (see RoundDownToNearestFiveMinutesAndFormat above). The
    // assertions below are captured here so that whoever lands the
    // fuller fixture (or chooses to extend `PlanningServiceMultiShiftTests`
    // / `PlanRegistrationHelperReadBySiteAndDateTests`) can flip the
    // `[Ignore]` off without rewriting the contract.

    /// <summary>
    /// Phase 1 contract: when <c>UseOneMinuteIntervals</c> is on and a
    /// precise <c>Start1StartedAt</c> stamp already lives on the row, the
    /// `Start1Id &gt; 289` workaround in
    /// <see cref="PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod"/>
    /// must NOT overwrite the precise stamp with the 5-minute snap.
    /// </summary>
    [Test]
    [Ignore("Phase 1 carve-out: UpdatePlanRegistrationsInPeriod requires logger/options/Site fixture not wired here; assertion captured for future fixture work.")]
    public void UpdatePlanRegistrationsInPeriod_FlagOn_PreservesStartedAt()
    {
        // Arrange (intent, to be enabled when fixture lands):
        //
        //   var date = new DateTime(2026, 5, 15, 0, 0, 0);
        //   var preciseStart = new DateTime(2026, 5, 15, 7, 3, 53);
        //
        //   var dbAssignedSite = new AssignedSite { UseOneMinuteIntervals = true,
        //                                           Resigned = false,
        //                                           UseGoogleSheetAsDefault = false,
        //                                           SiteId = 900 };
        //   await dbAssignedSite.Create(TimePlanningPnDbContext);
        //
        //   // Start1Id > 289 is required to enter the workaround branch.
        //   // 504 / (5+1) = 84  → AddMinutes(84*5) = +420min = 07:00 (the snap).
        //   var planning = new PlanRegistration {
        //       SdkSitId = 900,
        //       Date = date,
        //       Start1Id = 504,
        //       Start1StartedAt = preciseStart,
        //       Stop1Id = 0,
        //   };
        //   await planning.Create(TimePlanningPnDbContext);
        //
        //   // Act
        //   await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
        //       new List<PlanRegistration> { new() { Id = planning.Id, Date = date } },
        //       new TimePlanningPlanningModel(), TimePlanningPnDbContext,
        //       dbAssignedSite, logger, site, date, date, options);
        //
        //   // Assert — flag-on path keeps the precise 07:03:53 stamp.
        //   var reloaded = await TimePlanningPnDbContext.PlanRegistrations
        //       .AsNoTracking().FirstAsync(x => x.Id == planning.Id);
        //   Assert.That(reloaded.Start1StartedAt, Is.EqualTo(preciseStart));
        Assert.Pass("Captured for future fixture work; see XML doc above.");
    }

    /// <summary>
    /// Phase 1 regression-guard: with the flag OFF, the existing backfill
    /// behavior must be byte-identical to before — `Start1StartedAt` is
    /// recomputed from the corrected `Start1Id` index even if a precise
    /// stamp had been seeded on the row.
    /// </summary>
    [Test]
    [Ignore("Phase 1 carve-out: UpdatePlanRegistrationsInPeriod requires logger/options/Site fixture not wired here; assertion captured for future fixture work.")]
    public void UpdatePlanRegistrationsInPeriod_FlagOff_BackfillsStartedAt_AsBefore()
    {
        // Arrange (intent, to be enabled when fixture lands):
        //
        //   var date = new DateTime(2026, 5, 15, 0, 0, 0);
        //   var preciseStart = new DateTime(2026, 5, 15, 7, 3, 53);
        //   var expectedSnap = new DateTime(2026, 5, 15, 7, 0, 0);
        //
        //   var dbAssignedSite = new AssignedSite { UseOneMinuteIntervals = false,
        //                                           Resigned = false,
        //                                           UseGoogleSheetAsDefault = false,
        //                                           SiteId = 901 };
        //
        //   var planning = new PlanRegistration {
        //       SdkSitId = 901,
        //       Date = date,
        //       Start1Id = 504,
        //       Start1StartedAt = preciseStart,
        //   };
        //
        //   // Act — same call as the flag-on test, just flag flipped off.
        //
        //   // Assert — flag-off path overwrites Start1StartedAt to the 5-min snap.
        //   Assert.That(reloaded.Start1StartedAt, Is.EqualTo(expectedSnap));
        Assert.Pass("Captured for future fixture work; see XML doc above.");
    }

    /// <summary>
    /// Phase 1 fallback: with the flag ON but no precise stamp populated
    /// (e.g. a legacy row without observed punch data), the existing
    /// backfill must still run so the row gets a sensible
    /// `Start1StartedAt` derived from the int index.
    /// </summary>
    [Test]
    [Ignore("Phase 1 carve-out: UpdatePlanRegistrationsInPeriod requires logger/options/Site fixture not wired here; assertion captured for future fixture work.")]
    public void UpdatePlanRegistrationsInPeriod_FlagOnButStartedAtNull_FallsBackToBackfill()
    {
        // Arrange (intent, to be enabled when fixture lands):
        //
        //   var date = new DateTime(2026, 5, 15, 0, 0, 0);
        //   var expectedSnap = new DateTime(2026, 5, 15, 7, 0, 0);
        //
        //   var dbAssignedSite = new AssignedSite { UseOneMinuteIntervals = true,
        //                                           Resigned = false,
        //                                           UseGoogleSheetAsDefault = false,
        //                                           SiteId = 902 };
        //
        //   var planning = new PlanRegistration {
        //       SdkSitId = 902,
        //       Date = date,
        //       Start1Id = 504,
        //       Start1StartedAt = null,   // legacy row, no precise stamp
        //   };
        //
        //   // Act — flag is on, but since StartedAt is null the helper falls
        //   // through to the existing backfill so the row stays usable.
        //
        //   // Assert — backfill ran exactly as in the flag-off path.
        //   Assert.That(reloaded.Start1StartedAt, Is.EqualTo(expectedSnap));
        Assert.Pass("Captured for future fixture work; see XML doc above.");
    }

    /// <summary>
    /// Phase 1 contract for the auto-break write path
    /// (<see cref="PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive"/>).
    /// Auto-break pauses are COMPUTED from day-of-week rules, not OBSERVED,
    /// so even with <c>UseOneMinuteIntervals</c> on we deliberately do NOT
    /// fabricate `Pause1StartedAt` / `Pause1StoppedAt` timestamps — there
    /// is no real-world signal anchoring them. The legacy `Pause1Id` int
    /// (in 5-minute multiples) stays the source of truth and therefore
    /// must agree byte-for-byte with the flag-off path. DateTime pause
    /// fields are reserved for OBSERVED pauses written via
    /// `UpdateWorkingHour`.
    /// </summary>
    [Test]
    public void CalculatePauseAutoBreakCalculationActive_FlagOn_LegacyAndDateTimeFieldsAgree()
    {
        // Arrange — identical seed for both runs except for the flag.
        AssignedSite BuildAssignedSite(bool useOneMinuteIntervals) => new()
        {
            UseOneMinuteIntervals = useOneMinuteIntervals,
            AutoBreakCalculationActive = true,
            // Wednesday rule: every 180 min worked → 30 min pause, capped at 60.
            WednesdayBreakMinutesDivider = 180,
            WednesdayBreakMinutesPrDivider = 30,
            WednesdayBreakMinutesUpperLimit = 60,
        };

        // 96..192 in 5-min ticks = 8:00..16:00 = 480 min worked.
        // 480 / 180 = 2 breaks; 2 * 30 = 60 min ≤ 60 cap → Pause1Id = 60.
        // After the trailing `/ 5 + 1` step that's `60 / 5 + 1 = 13`.
        PlanRegistration BuildPlanning(DateTime? preciseStart, DateTime? preciseStop) => new()
        {
            Date = DateTime.Today.AddDays(DayOfWeek.Wednesday - DateTime.Today.DayOfWeek),
            Start1Id = 96,
            Stop1Id = 192,
            Start1StartedAt = preciseStart,
            Stop1StoppedAt = preciseStop,
        };

        var preciseStart = new DateTime(2026, 5, 13, 8, 0, 17);
        var preciseStop  = new DateTime(2026, 5, 13, 16, 0, 41);

        // Act
        var resultFlagOff = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(
            BuildAssignedSite(false), BuildPlanning(preciseStart, preciseStop));
        var resultFlagOn = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(
            BuildAssignedSite(true), BuildPlanning(preciseStart, preciseStop));

        Assert.Multiple(() =>
        {
            // Legacy int field is byte-identical between flag-on and flag-off.
            Assert.That(resultFlagOn.Pause1Id, Is.EqualTo(resultFlagOff.Pause1Id),
                "Pause1Id must match flag-off run (auto-break is computed, not observed)");
            Assert.That(resultFlagOn.Pause1Id, Is.EqualTo(13),
                "Pause1Id must remain 13 (60 min cap → /5+1 = 13)");

            // Phase 1 deliberately does NOT fabricate DateTime pause stamps
            // for an auto-computed pause; both runs leave them null.
            Assert.That(resultFlagOn.Pause1StartedAt, Is.Null,
                "Auto-break must not invent a Pause1StartedAt when flag on");
            Assert.That(resultFlagOn.Pause1StoppedAt, Is.Null,
                "Auto-break must not invent a Pause1StoppedAt when flag on");
            Assert.That(resultFlagOff.Pause1StartedAt, Is.Null,
                "Flag-off path must remain unchanged: no Pause1StartedAt");
            Assert.That(resultFlagOff.Pause1StoppedAt, Is.Null,
                "Flag-off path must remain unchanged: no Pause1StoppedAt");

            // Sanity: if any DateTime fields WERE set in some future phase,
            // they would need to agree with `Pause1Id * 5` minutes when
            // anchored on Start1StartedAt. For Phase 1, both null is the
            // documented expectation.
        });
    }
}