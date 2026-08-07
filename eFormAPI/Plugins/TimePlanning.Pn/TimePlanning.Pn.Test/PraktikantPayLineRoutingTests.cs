using System;
using System.Collections.Generic;
using System.Linq;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using TimePlanning.Pn.Test.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// End-to-end coverage of the two "Udenlandske praktikanter Landbrug" presets (GLS-A / 3F,
/// § 50) THROUGH <see cref="TimePlanningWorkingHoursService.CalculatePayLinesForDay"/> —
/// the real router — rather than through the generators directly.
///
/// The headline behaviour under test (2026-08-07 spec): normal time and overtime are
/// SEQUENTIAL, not cumulative. § 50 stk. 4 d pays the stald supplements only "for arbejde
/// i normal arbejdstid", so minutes up to the daily norm (7 h 24 m = 26640 s, the first
/// tier's UpToSeconds) are attributed by clock-time BANDS, and every minute past that
/// boundary goes to the overtime TIERS from tier 2 onward, carrying no supplement code.
/// Before the fix the routing was exclusive — any band for the day meant the tiers never
/// ran and all overtime minutes were silently lost.
///
/// THAT READING IS OPT-IN BY RULE-SET NAME, not by rule-set shape — thirteen other
/// preset/day combinations have the same shape but encode tier 1 as a mirror of their
/// clock split, and are out of scope. Section 9 guards that boundary; section 10 guards
/// that pause seconds never consume the normal-time budget (totalSeconds is NETTO, so
/// charging pauses to it would steal afternoon supplement from the worker).
///
/// Day classification is date-driven via <see cref="TimePlanningWorkingHoursService.GetDayCodeForDate"/>:
/// Grundlovsdag (5 June) is checked FIRST and beats Sunday, then official holidays from
/// Resources/danish_holidays_2025_2030.json, then Sunday, Saturday, else weekday.
///
/// Every test runs the <see cref="AssertPayLines"/> conservation invariant: the sum of
/// HoursInSeconds across all returned lines must equal the expected worked seconds, and
/// the per-code expectations must themselves sum to that same number. No minute may be
/// lost or double-counted.
///
/// Fixtures live in <see cref="PraktikantFixtures"/> — see the three-way sync note there.
/// </summary>
[TestFixture]
public class PraktikantPayLineRoutingTests
{
    // ---- Verified calendar anchors (checked against danish_holidays_2025_2030.json) ----

    /// <summary>Monday 11 May 2026 — plain weekday, not an official holiday.</summary>
    private static readonly DateTime Weekday = new(2026, 5, 11);

    /// <summary>Saturday 16 May 2026 — not an official holiday.</summary>
    private static readonly DateTime Saturday = new(2026, 5, 16);

    /// <summary>Sunday 17 May 2026 — not an official holiday.</summary>
    private static readonly DateTime Sunday = new(2026, 5, 17);

    /// <summary>Thursday 14 May 2026 — Kristi himmelfartsdag, category official_holiday.</summary>
    private static readonly DateTime Holiday = new(2026, 5, 14);

    /// <summary>Friday 5 June 2026 — Grundlovsdag; the 5-June check wins over every other rule.</summary>
    private static readonly DateTime Grundlovsdag = new(2026, 6, 5);

    private const int Hour = 3600;
    private const int Norm = PraktikantFixtures.NormSeconds; // 26640 s = 7 h 24 m

    // ------------------------------------------------------------------
    // 1. Daily-norm boundary on a WEEKDAY (pure tier path — neither preset
    //    defines day-type bands for Monday, so totalSeconds drives it).
    // ------------------------------------------------------------------

    [Test]
    public void Stald_Weekday_6h_BelowNorm_AllNormal()
        => RunWeekdayBoundaryCase(PraktikantFixtures.Staldarbejde(), 6 * Hour,
            ("NORMAL", 21600));

    [Test]
    public void Stald_Weekday_ExactlyAtNorm_7h24m_AllNormal_NoOvertime()
        => RunWeekdayBoundaryCase(PraktikantFixtures.Staldarbejde(), 26640,
            ("NORMAL", 26640));

    [Test]
    public void Stald_Weekday_7h25m_JustOverNorm_OneMinuteToOvertime50()
        => RunWeekdayBoundaryCase(PraktikantFixtures.Staldarbejde(), 26700,
            ("NORMAL", 26640), ("OVERTIME_50", 60));

    [Test]
    public void Stald_Weekday_9h24m_TopOfOvertime50Tier_NothingAt80()
        => RunWeekdayBoundaryCase(PraktikantFixtures.Staldarbejde(), 33840,
            ("NORMAL", 26640), ("OVERTIME_50", 7200));

    [Test]
    public void Stald_Weekday_12h_SpillsIntoOvertime80()
        => RunWeekdayBoundaryCase(PraktikantFixtures.Staldarbejde(), 12 * Hour,
            ("NORMAL", 26640), ("OVERTIME_50", 7200), ("OVERTIME_80", 9360));

    [Test]
    public void Andet_Weekday_6h_BelowNorm_AllNormal()
        => RunWeekdayBoundaryCase(PraktikantFixtures.AndetArbejde(), 6 * Hour,
            ("NORMAL", 21600));

    [Test]
    public void Andet_Weekday_ExactlyAtNorm_7h24m_AllNormal_NoOvertime()
        => RunWeekdayBoundaryCase(PraktikantFixtures.AndetArbejde(), 26640,
            ("NORMAL", 26640));

    [Test]
    public void Andet_Weekday_7h25m_JustOverNorm_OneMinuteToOvertime50()
        => RunWeekdayBoundaryCase(PraktikantFixtures.AndetArbejde(), 26700,
            ("NORMAL", 26640), ("OVERTIME_50", 60));

    [Test]
    public void Andet_Weekday_9h24m_TopOfOvertime50Tier_NothingAt80()
        => RunWeekdayBoundaryCase(PraktikantFixtures.AndetArbejde(), 33840,
            ("NORMAL", 26640), ("OVERTIME_50", 7200));

    [Test]
    public void Andet_Weekday_12h_SpillsIntoOvertime80()
        => RunWeekdayBoundaryCase(PraktikantFixtures.AndetArbejde(), 12 * Hour,
            ("NORMAL", 26640), ("OVERTIME_50", 7200), ("OVERTIME_80", 9360));

    // ------------------------------------------------------------------
    // 2. Staldarbejde SATURDAY — bands for normal time, tiers for the rest.
    //    This is the headline fix.
    // ------------------------------------------------------------------

    [Test]
    public void Stald_Saturday_12h_BandsUntilNorm_ThenOvertimeTiers()
    {
        // Shift 06:00 → 18:00 on Sat 16 May 2026 = 43200 s worked.
        //   bandSeconds     = min(43200, 26640) = 26640
        //   overtimeSeconds = 43200 - 26640     = 16560
        // Truncated segment: (21600, 21600 + 26640 = 48240) — i.e. 06:00 → 13:24.
        //   band 00:00–12:00 SAT_NORMAL            : 21600 → 43200 = 21600 s
        //   band 12:00–24:00 SAT_ANIMAL_AFTERNOON  : 43200 → 48240 =  5040 s
        // Overtime, cumulative starts at 26640:
        //   tier 2 UpTo 33840 → cap 33840 - 26640 = 7200 → OVERTIME_50 = 7200 s
        //   tier 3 UpTo null  → remainder                → OVERTIME_80 = 9360 s
        // 21600 + 5040 + 7200 + 9360 = 43200.
        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Saturday, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("SAT_NORMAL", 21600),
            ("SAT_ANIMAL_AFTERNOON", 5040),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
    }

    [Test]
    public void Stald_Saturday_ExactlyAtNorm_AllBanded_NoOvertimeLines()
    {
        // 06:00 → 13:24 = 26640 s, exactly the norm. Nothing spills into the tiers.
        //   SAT_NORMAL           06:00 → 12:00 = 21600 s
        //   SAT_ANIMAL_AFTERNOON 12:00 → 13:24 =  5040 s
        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Saturday, 6 * Hour, Norm), Norm);

        AssertPayLines(lines, 26640,
            ("SAT_NORMAL", 21600),
            ("SAT_ANIMAL_AFTERNOON", 5040));
    }

    [Test]
    public void Stald_Saturday_OneMinuteOverNorm_BandsUnchanged_OneMinuteToOvertime50()
    {
        // 26700 s from 06:00. Bands still see only the first 26640 s (06:00 → 13:24),
        // so the band split is identical to the exactly-at-norm case; the extra 60 s
        // becomes overtime and carries NO supplement code.
        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Saturday, 6 * Hour, 26700), 26700);

        AssertPayLines(lines, 26700,
            ("SAT_NORMAL", 21600),
            ("SAT_ANIMAL_AFTERNOON", 5040),
            ("OVERTIME_50", 60));
    }

    [Test]
    public void Stald_Saturday_4h_EntirelyBeforeNoon_AllSatNormal_NoOvertime()
    {
        // 08:00 → 12:00 = 14400 s, below the norm, wholly inside the 00:00–12:00 band.
        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Saturday, 8 * Hour, 4 * Hour), 4 * Hour);

        AssertPayLines(lines, 14400, ("SAT_NORMAL", 14400));
    }

    [Test]
    public void Stald_Saturday_4h_EntirelyAfterNoon_AllSatAnimalAfternoon_NoOvertime()
    {
        // 13:00 → 17:00 = 14400 s, below the norm, wholly inside the 12:00–24:00 band.
        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Saturday, 13 * Hour, 4 * Hour), 4 * Hour);

        AssertPayLines(lines, 14400, ("SAT_ANIMAL_AFTERNOON", 14400));
    }

    [Test]
    public void Stald_Saturday_TwoShifts_TruncationSplitsSecondShiftAtNorm()
    {
        // Shift 1 06:00 → 11:00 = 18000 s, shift 2 12:00 → 19:00 = 25200 s. Total 43200 s.
        //   bandSeconds = 26640. EnumerateShiftSegmentsTruncated consumes shift 1 whole
        //   (18000 ≤ 26640, remaining 8640), then splits shift 2 at 12:00 + 8640 s = 14:24,
        //   yielding (43200, 51840). Later time is not band-attributed.
        //     SAT_NORMAL           06:00 → 11:00 = 18000 s
        //     SAT_ANIMAL_AFTERNOON 12:00 → 14:24 =  8640 s   (18000 + 8640 = 26640 ✓)
        //   overtimeSeconds = 43200 - 26640 = 16560 → OVERTIME_50 7200, OVERTIME_80 9360.
        var model = new TimePlanningWorkingHoursModel
        {
            Date = Saturday,
            Start1StartedAt = Saturday.AddHours(6),
            Stop1StoppedAt = Saturday.AddHours(11),
            Start2StartedAt = Saturday.AddHours(12),
            Stop2StoppedAt = Saturday.AddHours(19),
        };

        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(), model, 43200);

        AssertPayLines(lines, 43200,
            ("SAT_NORMAL", 18000),
            ("SAT_ANIMAL_AFTERNOON", 8640),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
    }

    // ------------------------------------------------------------------
    // 3. Staldarbejde SUNDAY / HOLIDAY — one all-day ANIMAL_SUN_HOLIDAY band
    //    for normal time, then the overtime tiers.
    // ------------------------------------------------------------------

    [Test]
    public void Stald_Sunday_6h_BelowNorm_AllAnimalSunHoliday()
    {
        var lines = Run(Sunday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Sunday, 6 * Hour, 6 * Hour), 6 * Hour);

        AssertPayLines(lines, 21600, ("ANIMAL_SUN_HOLIDAY", 21600));
    }

    [Test]
    public void Stald_Sunday_12h_AnimalUntilNorm_ThenOvertimeTiers()
    {
        // 06:00 → 18:00 = 43200 s. Bands see the first 26640 s only, and the single
        // 00:00–24:00 band puts all of it on ANIMAL_SUN_HOLIDAY. Remaining 16560 s:
        // OVERTIME_50 7200 (up to 33840 cumulative), OVERTIME_80 9360.
        var lines = Run(Sunday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Sunday, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("ANIMAL_SUN_HOLIDAY", 26640),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
    }

    [Test]
    public void Stald_Holiday_6h_BelowNorm_AllAnimalSunHoliday()
    {
        // Thu 14 May 2026 (Kristi himmelfartsdag) → dayCode HOLIDAY → DayType.Holiday,
        // NOT DayType.Thursday.
        var lines = Run(Holiday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Holiday, 6 * Hour, 6 * Hour), 6 * Hour);

        AssertPayLines(lines, 21600, ("ANIMAL_SUN_HOLIDAY", 21600));
    }

    [Test]
    public void Stald_Holiday_12h_AnimalUntilNorm_ThenOvertimeTiers()
    {
        var lines = Run(Holiday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Holiday, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("ANIMAL_SUN_HOLIDAY", 26640),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
    }

    // ------------------------------------------------------------------
    // 4. Andet arbejde SUNDAY / HOLIDAY — unchanged all-overtime behaviour.
    //    Sundays and holidays are outside the permitted Mon–Sat 06–18 window.
    // ------------------------------------------------------------------

    [Test]
    public void Andet_Sunday_8h_AllOvertime_TwoHoursAt50_RemainderAt80()
    {
        // Andet arbejde declares no day-type rules at all, so this is the pure tier
        // path: SUNDAY tiers are 7200 s OVERTIME_50 then OVERTIME_80 for the rest.
        var lines = Run(Sunday, PraktikantFixtures.AndetArbejde(),
            SingleShift(Sunday, 6 * Hour, 8 * Hour), 8 * Hour);

        AssertPayLines(lines, 28800,
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 21600));
    }

    [Test]
    public void Andet_Holiday_8h_AllOvertime_TwoHoursAt50_RemainderAt80()
    {
        var lines = Run(Holiday, PraktikantFixtures.AndetArbejde(),
            SingleShift(Holiday, 6 * Hour, 8 * Hour), 8 * Hour);

        AssertPayLines(lines, 28800,
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 21600));
    }

    [Test]
    public void Andet_Sunday_1h_BelowFirstTier_AllOvertime50()
    {
        var lines = Run(Sunday, PraktikantFixtures.AndetArbejde(),
            SingleShift(Sunday, 6 * Hour, 1 * Hour), 1 * Hour);

        AssertPayLines(lines, 3600, ("OVERTIME_50", 3600));
    }

    // ------------------------------------------------------------------
    // 5. GRUNDLOVSDAG — the § 29 noon split, applied plugin-side.
    //
    // Jordbrug § 29 makes Grundlovsdag a half day: minutes before 12:00 are ordinary
    // working time, minutes from 12:00 follow søgnehelligdag treatment (decision 4 of
    // the 2026-08-07 spec). TryGetDayType returns FALSE for GRUNDLOVSDAG, so there is
    // no DayType to hang time bands on and the split cannot live in the preset data —
    // CalculateGrundlovsdagPayLines applies it instead, for the two opted-in presets.
    //
    // PRECEDENCE the tests below lock in (the two rules compose, they do not conflict):
    //   1. the 26640 s normal-time boundary partitions the day FIRST and absolutely —
    //      only those seconds are eligible for a day-classification code at all;
    //   2. within them, the 12:00 clock split picks NORMAL vs søgnehelligdag;
    //   3. "søgnehelligdag treatment" is read off the preset's own SUNDAY rule —
    //      an ANIMAL_SUN_HOLIDAY band for stald, the 7200/rest OVERTIME_50/OVERTIME_80
    //      ladder for andet arbejde.
    // For andet arbejde, step 3 and the step-1 overflow both emit OVERTIME_50, and the
    // merged line is simply their sum — the minute sets are disjoint by construction.
    // ------------------------------------------------------------------

    [Test]
    public void Stald_Grundlovsdag_SpanningNoon_BelowNorm_SplitsAtNoon()
    {
        // 08:00 → 14:00 = 21600 s, below the 26640 s norm, so nothing is overtime and
        // the whole day is eligible for a day-classification code.
        //   08:00 → 12:00 = 14400 s → NORMAL             (ordinary working time)
        //   12:00 → 14:00 =  7200 s → ANIMAL_SUN_HOLIDAY (stald Sunday band)
        var lines = Run(Grundlovsdag, PraktikantFixtures.Staldarbejde(),
            SingleShift(Grundlovsdag, 8 * Hour, 6 * Hour), 6 * Hour);

        AssertPayLines(lines, 21600,
            ("NORMAL", 14400),
            ("ANIMAL_SUN_HOLIDAY", 7200));
    }

    [Test]
    public void Stald_Grundlovsdag_EntirelyBeforeNoon_AllNormal()
    {
        // 06:00 → 11:00 = 18000 s, wholly in the ordinary-working-time half of the day.
        var lines = Run(Grundlovsdag, PraktikantFixtures.Staldarbejde(),
            SingleShift(Grundlovsdag, 6 * Hour, 5 * Hour), 5 * Hour);

        AssertPayLines(lines, 18000, ("NORMAL", 18000));
    }

    [Test]
    public void Stald_Grundlovsdag_EntirelyAfterNoon_AllAnimalSunHoliday()
    {
        // 13:00 → 17:00 = 14400 s, wholly in the søgnehelligdag half. No NORMAL line.
        var lines = Run(Grundlovsdag, PraktikantFixtures.Staldarbejde(),
            SingleShift(Grundlovsdag, 13 * Hour, 4 * Hour), 4 * Hour);

        AssertPayLines(lines, 14400, ("ANIMAL_SUN_HOLIDAY", 14400));
    }

    [Test]
    public void Stald_Grundlovsdag_12h_NoonSplitThenNormalTimeBoundary()
    {
        // 06:00 → 18:00 = 43200 s — the noon split AND the norm boundary both bite.
        //   Step 1: normal time = min(43200, 26640) = 26640 → 06:00 → 13:24;
        //           overtime    = 43200 - 26640     = 16560.
        //   Step 2: 06:00 → 12:00 = 21600 s → NORMAL
        //           12:00 → 13:24 =  5040 s → ANIMAL_SUN_HOLIDAY
        //   Step 1 overflow: OVERTIME_50 7200 (up to 33840 cumulative), OVERTIME_80 9360.
        // The overtime minutes carry NEITHER NORMAL nor ANIMAL_SUN_HOLIDAY — that is the
        // boundary winning over the noon split, as it does on a banded Saturday.
        // 21600 + 5040 + 7200 + 9360 = 43200.
        var lines = Run(Grundlovsdag, PraktikantFixtures.Staldarbejde(),
            SingleShift(Grundlovsdag, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("NORMAL", 21600),
            ("ANIMAL_SUN_HOLIDAY", 5040),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
    }

    [Test]
    public void Andet_Grundlovsdag_SpanningNoon_BelowNorm_AfternoonTakesSundayLadder()
    {
        // 08:00 → 14:00 = 21600 s, below the norm.
        //   08:00 → 12:00 = 14400 s → NORMAL
        //   12:00 → 14:00 =  7200 s → søgnehelligdag treatment. Andet arbejde has no
        //     Sunday BANDS, so its SUNDAY TIER LADDER runs over those 7200 s, restarting
        //     at zero: tier 1 caps at 7200 → OVERTIME_50 7200, nothing reaches tier 2.
        var lines = Run(Grundlovsdag, PraktikantFixtures.AndetArbejde(),
            SingleShift(Grundlovsdag, 8 * Hour, 6 * Hour), 6 * Hour);

        AssertPayLines(lines, 21600,
            ("NORMAL", 14400),
            ("OVERTIME_50", 7200));
    }

    [Test]
    public void Andet_Grundlovsdag_EntirelyBeforeNoon_AllNormal()
    {
        var lines = Run(Grundlovsdag, PraktikantFixtures.AndetArbejde(),
            SingleShift(Grundlovsdag, 6 * Hour, 5 * Hour), 5 * Hour);

        AssertPayLines(lines, 18000, ("NORMAL", 18000));
    }

    [Test]
    public void Andet_Grundlovsdag_EntirelyAfterNoon_AllSundayLadder()
    {
        // 13:00 → 17:00 = 14400 s, wholly søgnehelligdag: the SUNDAY ladder gives
        // OVERTIME_50 7200 then OVERTIME_80 7200. No NORMAL line at all.
        var lines = Run(Grundlovsdag, PraktikantFixtures.AndetArbejde(),
            SingleShift(Grundlovsdag, 13 * Hour, 4 * Hour), 4 * Hour);

        AssertPayLines(lines, 14400,
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 7200));
    }

    [Test]
    public void Andet_Grundlovsdag_14h_AfternoonLadderAndOvertimeOverflowMergeByCode()
    {
        // THE COMPOSITION CASE. 06:00 → 20:00 = 50400 s.
        //   Step 1: normal time 26640 → 06:00 → 13:24; overtime 50400 - 26640 = 23760.
        //   Step 2: 06:00 → 12:00 = 21600 s → NORMAL
        //           12:00 → 13:24 =  5040 s → SUNDAY ladder, restarting at zero →
        //                                     OVERTIME_50 5040 (below the 7200 cap)
        //   Step 1 overflow over the GRUNDLOVSDAG tiers 2..3, cumulative from 26640:
        //           OVERTIME_50 7200, OVERTIME_80 16560.
        // Both sources emit OVERTIME_50; MergeByPayCode SUMS them: 5040 + 7200 = 12240.
        // That is addition of two DISJOINT minute sets, not a double count — the norm
        // boundary partitions the day before the noon split ever runs.
        // 21600 + 12240 + 16560 = 50400.
        var lines = Run(Grundlovsdag, PraktikantFixtures.AndetArbejde(),
            SingleShift(Grundlovsdag, 6 * Hour, 14 * Hour), 14 * Hour);

        AssertPayLines(lines, 50400,
            ("NORMAL", 21600),
            ("OVERTIME_50", 12240),
            ("OVERTIME_80", 16560));
    }

    [Test]
    public void Stald_Grundlovsdag_BeatsFridayClassification_UsesGrundlovsdagRule()
    {
        // 5 June 2026 is a Friday. GetDayCodeForDate checks 5 June FIRST, so the
        // GRUNDLOVSDAG handling wins over any weekday handling. 06:00 → 12:00 is
        // entirely before noon and below the norm, so it stays all NORMAL.
        var lines = Run(Grundlovsdag, PraktikantFixtures.Staldarbejde(),
            SingleShift(Grundlovsdag, 6 * Hour, 6 * Hour), 6 * Hour);

        AssertPayLines(lines, 21600, ("NORMAL", 21600));
    }

    [Test]
    public void Grundlovsdag_NonPraktikantPreset_KeepsPureTierPath_NoNoonSplit()
    {
        // The noon split is opted into by rule-set NAME, exactly like the normal-time
        // split. A preset that is not one of the two praktikant presets keeps the
        // historical behaviour: GRUNDLOVSDAG is just another day code fed to the tiers,
        // with no 12:00 boundary anywhere.
        var payRuleSet = PraktikantFixtures.Staldarbejde();
        payRuleSet.Name = "Some Customer Custom Rule Set";

        var lines = Run(Grundlovsdag, payRuleSet,
            SingleShift(Grundlovsdag, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("NORMAL", 26640),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
        Assert.That(lines.Any(l => l.PayCode == "ANIMAL_SUN_HOLIDAY"), Is.False,
            "A non-opted-in preset must not get the søgnehelligdag afternoon treatment");
    }

    // ------------------------------------------------------------------
    // 6. Shift crossing midnight — documents day clamping, not an aspiration.
    // ------------------------------------------------------------------

    [Test]
    public void Stald_Saturday_ShiftCrossingMidnight_ClampedAtEndOfDay_PostMidnightSecondsNotBandAttributed()
    {
        // Sat 16 May 22:00 → Sun 17 May 02:00. The worker worked 14400 s, but pay rules
        // are scoped per-day: ResolveShiftSeconds sees realStop.Date > realStart.Date and
        // CLAMPS the stop to 86400, so the segment is (79200, 86400) = 7200 s only.
        //
        // CONSEQUENCE (real, current behaviour — NOT an aspiration): the 7200 s worked
        // after midnight are dropped on this day and are never re-attributed here. They
        // are not carried into the Sunday model by this method either. So the returned
        // lines conserve the CLAMPED 7200 s, not the 14400 s passed as totalSeconds.
        //
        // The banded branch is still entered (bandSeconds = min(14400, 26640) = 14400,
        // which exceeds the clamped segment, so the whole 7200 s is band-attributed) and
        // overtimeSeconds = max(0, 14400 - 26640) = 0, so no overtime line is produced
        // even though the worker was on site for 14400 s across the two calendar days.
        var model = new TimePlanningWorkingHoursModel
        {
            Date = Saturday,
            Start1StartedAt = Saturday.AddHours(22),
            Stop1StoppedAt = Saturday.AddDays(1).AddHours(2),
        };

        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(), model, 4 * Hour);

        // 22:00 → 24:00 lies entirely in the 12:00–24:00 Saturday band.
        AssertPayLines(lines, 7200, ("SAT_ANIMAL_AFTERNOON", 7200));

        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.LessThan(4 * Hour),
            "Documents the clamping loss: seconds worked after midnight are not attributed to this day");
    }

    // ------------------------------------------------------------------
    // 7. No-rule day → every minute lands on NORMAL (DEFAULT → NORMAL mapping).
    // ------------------------------------------------------------------

    [Test]
    public void NoMatchingDayRule_AllSecondsMappedToNormal()
    {
        // A pay-rule-set with a WEEKDAY rule only, evaluated on a Sunday, and with no
        // day-type rules — so the tier path runs, PayLineGenerator finds no PayDayRule
        // for "SUNDAY" and emits its "DEFAULT" fallback line. CalculatePayLinesForDay is
        // the single exit point and rewrites DEFAULT → NORMAL.
        var payRuleSet = new PayRuleSet
        {
            Id = 9001,
            Name = "WeekdayOnly",
            DayRules = new List<PayDayRule>
            {
                new()
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new() { Order = 1, UpToSeconds = null, PayCode = "NORMAL" },
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>(),
        };

        var lines = Run(Sunday, payRuleSet, SingleShift(Sunday, 6 * Hour, 6 * Hour), 6 * Hour);

        Assert.That(lines, Has.Count.EqualTo(1), "Expected exactly one fallback line");
        AssertPayLines(lines, 21600, ("NORMAL", 21600));
        Assert.That(lines.Any(l => l.PayCode == "DEFAULT"), Is.False,
            "DEFAULT must never leak out of CalculatePayLinesForDay");
    }

    // ------------------------------------------------------------------
    // 8. Regression guards for the branches the fix deliberately left alone.
    // ------------------------------------------------------------------

    [Test]
    public void Regression_BandedDayWithSingleTier_YieldsBandsOnly_NoOvertimeLines()
    {
        // Even on an OPTED-IN preset, the split needs a day rule that describes an
        // overtime progression (>1 tier AND a non-null first UpToSeconds). With exactly
        // ONE tier the historical bands-only behaviour must survive untouched. (What
        // keeps the OTHER presets from changing is the name gate, not this check — see
        // Gate_NonPraktikantBandedPresetWithMultipleTiers_StillTakesBandsOnlyPath.)
        // 06:00 → 18:00 is attributed by clock position across the whole 12 h, with NO
        // truncation at the norm:
        //   SAT_NORMAL           06:00 → 12:00 = 21600 s
        //   SAT_ANIMAL_AFTERNOON 12:00 → 18:00 = 21600 s
        var payRuleSet = PraktikantFixtures.Staldarbejde();
        payRuleSet.DayRules.Single(r => r.DayCode == "SATURDAY").Tiers = new List<PayTierRule>
        {
            new() { Order = 1, UpToSeconds = 26640, PayCode = "SAT_NORMAL" },
        };

        var lines = Run(Saturday, payRuleSet, SingleShift(Saturday, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("SAT_NORMAL", 21600),
            ("SAT_ANIMAL_AFTERNOON", 21600));
        Assert.That(lines.Any(l => l.PayCode!.StartsWith("OVERTIME")), Is.False,
            "A single-tier banded day must not produce overtime lines");
    }

    [Test]
    public void Regression_DayWithoutBands_YieldsTiersOnly()
    {
        // Staldarbejde declares day-type rules for Saturday/Sunday/Holiday only. On a
        // Monday there is no band, so the tier path runs and no supplement code appears.
        var lines = Run(Weekday, PraktikantFixtures.Staldarbejde(),
            SingleShift(Weekday, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("NORMAL", 26640),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
        Assert.That(lines.Any(l => l.PayCode == "SAT_NORMAL"
                                   || l.PayCode == "SAT_ANIMAL_AFTERNOON"
                                   || l.PayCode == "ANIMAL_SUN_HOLIDAY"), Is.False,
            "A band-less day must not produce any clock-time supplement code");
    }

    [Test]
    public void Regression_NullPayRuleSet_ReturnsEmpty()
    {
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: Saturday,
            dayModel: SingleShift(Saturday, 6 * Hour, 12 * Hour),
            totalSeconds: 12 * Hour, payRuleSet: null!);

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void Regression_ZeroTotalSeconds_TierPath_ReturnsEmpty()
    {
        var lines = Run(Weekday, PraktikantFixtures.Staldarbejde(),
            new TimePlanningWorkingHoursModel { Date = Weekday }, 0);

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void Regression_ZeroTotalSeconds_BandedPath_ReturnsEmpty()
    {
        // Saturday has bands AND a 3-tier day rule, so the split branch is entered, but
        // bandSeconds = min(0, 26640) = 0 truncates every segment away and
        // overtimeSeconds = max(0, 0 - 26640) = 0, so nothing is emitted.
        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(),
            new TimePlanningWorkingHoursModel { Date = Saturday }, 0);

        Assert.That(lines, Is.Empty);
    }

    // ------------------------------------------------------------------
    // 9. THE SCOPE GUARD — the split is opted into by rule-set NAME, not by shape.
    // ------------------------------------------------------------------

    [Test]
    public void Gate_NonPraktikantBandedPresetWithMultipleTiers_StillTakesBandsOnlyPath()
    {
        // REGRESSION GUARD FOR THE SCOPE BREACH. This rule set has exactly the shape the
        // split used to key off — Saturday time bands AND a >1-tier Saturday day rule
        // whose tier 1 has a non-null UpToSeconds — but it is NOT a praktikant preset.
        // It resembles Jordbrug Standard, where tier 1 is a deliberate MIRROR of the
        // clock split rather than a normal-time boundary. Thirteen preset/day
        // combinations across Jordbrug, Gartneri, Skovbrug and KA share that shape and
        // are explicitly out of scope in the 2026-08-07 spec.
        //
        // The work is 04:36 → 12:00 = 26640 s, a MORNING-ONLY Saturday.
        //   Correct (bands-only): the whole 26640 s is inside the 00:00–12:00 band →
        //     SAT_NORMAL 26640, nothing else.
        //   What the shape gate produced: bandSeconds = min(26640, 21600) = 21600, so
        //     the segment was truncated to 04:36 → 10:36 (SAT_NORMAL 21600) and the
        //     remaining 5040 s fell through to tier 2 → SAT_AFTERNOON 5040 — an
        //     afternoon supplement on a worker who went home at noon.
        var payRuleSet = new PayRuleSet
        {
            Id = 7001,
            Name = "GLS-A / 3F - Jordbrug Standard 2026-2029",
            DayRules = new List<PayDayRule>
            {
                new()
                {
                    DayCode = "SATURDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new() { Order = 1, UpToSeconds = 21600, PayCode = "SAT_NORMAL" },
                        new() { Order = 2, UpToSeconds = null, PayCode = "SAT_AFTERNOON" },
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>
            {
                new()
                {
                    DayType = DayType.Saturday,
                    DefaultPayCode = "SAT_NORMAL",
                    Priority = 1,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new() { StartSecondOfDay = 0, EndSecondOfDay = 43200, PayCode = "SAT_NORMAL", Priority = 1 },
                        new() { StartSecondOfDay = 43200, EndSecondOfDay = 86400, PayCode = "SAT_AFTERNOON", Priority = 1 },
                    }
                }
            },
        };

        var lines = Run(Saturday, payRuleSet, SingleShift(Saturday, 43200 - Norm, Norm), Norm);

        AssertPayLines(lines, 26640, ("SAT_NORMAL", 26640));
        Assert.That(lines.Any(l => l.PayCode == "SAT_AFTERNOON"), Is.False,
            "A morning-only Saturday must never earn the afternoon supplement");
        Assert.That(lines.Any(l => l.PayCode!.StartsWith("OVERTIME")), Is.False,
            "A non-opted-in preset must not produce overtime lines from its mirror tiers");
    }

    [Test]
    public void Gate_PraktikantPresetWithLegacyValidityPeriod_StillTakesSplitPath()
    {
        // Customer rows may still carry the previous agreement period in their name. The
        // gate normalizes the trailing " YYYY-YYYY" away (PayRuleSetLock.NormalizePresetName),
        // so a "… 2024-2026" row behaves exactly like the "… 2026-2029" catalogue entry.
        // Expectations are identical to Stald_Saturday_12h_BandsUntilNorm_ThenOvertimeTiers.
        var payRuleSet = PraktikantFixtures.Staldarbejde();
        payRuleSet.Name = "GLS-A / 3F - Udenlandske praktikanter Landbrug Staldarbejde 2024-2026";

        var lines = Run(Saturday, payRuleSet, SingleShift(Saturday, 6 * Hour, 12 * Hour), 12 * Hour);

        AssertPayLines(lines, 43200,
            ("SAT_NORMAL", 21600),
            ("SAT_ANIMAL_AFTERNOON", 5040),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 9360));
    }

    // ------------------------------------------------------------------
    // 10. PAUSES must not consume the normal-time budget.
    // ------------------------------------------------------------------

    [Test]
    public void Stald_Saturday_PauseInsideNormalTime_DoesNotShiftTheBoundary()
    {
        // Shift 06:00 → 18:00 with a 11:00 → 12:00 pause.
        //
        // NOTE THE TOTAL. totalSeconds is NETTO — every caller computes it as
        // nettoHours * 3600 — so a 12 h span with a 1 h pause is 39600 s of work, not
        // 43200. (The review note quoted 43200-style expectations; the conservation
        // helper would reject them, because only 39600 s were actually worked.)
        //
        //   Worked segments (pauses cut out, real clock positions kept):
        //     06:00 → 11:00 = 18000 s
        //     12:00 → 18:00 = 21600 s
        //   Normal time = min(39600, 26640) = 26640 → consumes segment 1 whole (18000,
        //   remaining 8640) then splits segment 2 at 12:00 + 8640 s = 14:24.
        //     SAT_NORMAL           06:00 → 11:00 = 18000 s
        //     SAT_ANIMAL_AFTERNOON 12:00 → 14:24 =  8640 s   (18000 + 8640 = 26640 ✓)
        //   Overtime = 39600 - 26640 = 12960 → OVERTIME_50 7200, OVERTIME_80 5760.
        //
        // BEFORE THE FIX the budget was consumed against GROSS segments, so the pause hour
        // was charged to the normal-time budget: the boundary landed at 13:24 instead of
        // 14:24 and the worker lost a full hour of afternoon supplement
        // (SAT_NORMAL 21600 / SAT_ANIMAL_AFTERNOON 5040).
        var model = new TimePlanningWorkingHoursModel
        {
            Date = Saturday,
            Start1StartedAt = Saturday.AddHours(6),
            Stop1StoppedAt = Saturday.AddHours(18),
            Pause1StartedAt = Saturday.AddHours(11),
            Pause1StoppedAt = Saturday.AddHours(12),
        };

        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(), model, 39600);

        AssertPayLines(lines, 39600,
            ("SAT_NORMAL", 18000),
            ("SAT_ANIMAL_AFTERNOON", 8640),
            ("OVERTIME_50", 7200),
            ("OVERTIME_80", 5760));
    }

    [Test]
    public void Stald_Saturday_PauseSpanningNoon_PauseSecondsAreNeverAttributed()
    {
        // Shift 08:00 → 16:00 with a 11:30 → 12:30 pause that STRADDLES the band
        // boundary. Netto = 28800 - 3600 = 25200 s, below the norm, so nothing is
        // overtime and every worked second is band-attributed by clock position:
        //   08:00 → 11:30 = 12600 s → SAT_NORMAL           (before noon)
        //   12:30 → 16:00 = 12600 s → SAT_ANIMAL_AFTERNOON (after noon)
        // The 30 min of pause before noon and 30 min after are attributed to NEITHER.
        var model = new TimePlanningWorkingHoursModel
        {
            Date = Saturday,
            Start1StartedAt = Saturday.AddHours(8),
            Stop1StoppedAt = Saturday.AddHours(16),
            Pause1StartedAt = Saturday.AddHours(11).AddMinutes(30),
            Pause1StoppedAt = Saturday.AddHours(12).AddMinutes(30),
        };

        var lines = Run(Saturday, PraktikantFixtures.Staldarbejde(), model, 25200);

        AssertPayLines(lines, 25200,
            ("SAT_NORMAL", 12600),
            ("SAT_ANIMAL_AFTERNOON", 12600));
    }

    [Test]
    public void Stald_Grundlovsdag_PauseAcrossNoon_NoonSplitUsesWorkedSecondsOnly()
    {
        // The Grundlovsdag noon split rides on the same truncation, so it inherits the
        // pause awareness. 08:00 → 15:00 with a 11:30 → 12:30 pause = 21600 s netto,
        // below the norm:
        //   08:00 → 11:30 = 12600 s → NORMAL
        //   12:30 → 15:00 =  9000 s → ANIMAL_SUN_HOLIDAY
        var model = new TimePlanningWorkingHoursModel
        {
            Date = Grundlovsdag,
            Start1StartedAt = Grundlovsdag.AddHours(8),
            Stop1StoppedAt = Grundlovsdag.AddHours(15),
            Pause1StartedAt = Grundlovsdag.AddHours(11).AddMinutes(30),
            Pause1StoppedAt = Grundlovsdag.AddHours(12).AddMinutes(30),
        };

        var lines = Run(Grundlovsdag, PraktikantFixtures.Staldarbejde(), model, 21600);

        AssertPayLines(lines, 21600,
            ("NORMAL", 12600),
            ("ANIMAL_SUN_HOLIDAY", 9000));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static List<PlanRegistrationPayLine> Run(
        DateTime date, PayRuleSet payRuleSet, TimePlanningWorkingHoursModel model, int totalSeconds)
        => TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: date, dayModel: model,
            totalSeconds: totalSeconds, payRuleSet: payRuleSet);

    /// <summary>
    /// Builds a day model with a single populated shift starting at
    /// <paramref name="startSecondOfDay"/> and running for <paramref name="seconds"/>.
    /// Only Start{N}StartedAt / Stop{N}StoppedAt are read by EnumerateShiftSegments —
    /// the legacy 5-minute slot fields are deliberately left unset.
    /// </summary>
    private static TimePlanningWorkingHoursModel SingleShift(DateTime date, int startSecondOfDay, int seconds)
        => new()
        {
            Date = date,
            Start1StartedAt = date.AddSeconds(startSecondOfDay),
            Stop1StoppedAt = date.AddSeconds(startSecondOfDay + seconds),
        };

    private static void RunWeekdayBoundaryCase(
        PayRuleSet payRuleSet, int totalSeconds, params (string PayCode, int Seconds)[] expected)
    {
        var lines = Run(Weekday, payRuleSet, SingleShift(Weekday, 6 * Hour, totalSeconds), totalSeconds);
        AssertPayLines(lines, totalSeconds, expected);
    }

    /// <summary>
    /// Asserts the exact seconds per pay code AND the conservation invariant: the sum of
    /// HoursInSeconds across every returned line equals <paramref name="expectedTotalSeconds"/>,
    /// so no minute is lost or double-counted. Also cross-checks that the per-code
    /// expectations themselves sum to that total, which catches arithmetic slips in the
    /// test data rather than letting them mask an engine bug.
    /// </summary>
    private static void AssertPayLines(
        IReadOnlyList<PlanRegistrationPayLine> lines,
        int expectedTotalSeconds,
        params (string PayCode, int Seconds)[] expected)
    {
        Assert.That(expected.Sum(e => e.Seconds), Is.EqualTo(expectedTotalSeconds),
            "Test data is self-inconsistent: the expected per-code seconds do not sum to the expected total");

        Assert.That(lines.Select(l => l.PayCode).Distinct().Count(), Is.EqualTo(lines.Count),
            "Pay lines must be merged by pay code — no duplicate codes");

        Assert.That(lines.Select(l => l.PayCode), Is.EquivalentTo(expected.Select(e => e.PayCode)),
            "Unexpected set of pay codes: " + string.Join(", ",
                lines.Select(l => $"{l.PayCode}={l.HoursInSeconds}")));

        foreach (var (payCode, seconds) in expected)
        {
            var line = lines.Single(l => l.PayCode == payCode);
            Assert.That(line.HoursInSeconds, Is.EqualTo(seconds), $"{payCode} seconds");
            Assert.That(line.Hours, Is.EqualTo(seconds / 3600.0).Within(0.0001), $"{payCode} hours");
        }

        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.EqualTo(expectedTotalSeconds),
            "Conservation invariant: attributed seconds must equal worked seconds exactly");
    }
}
