using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pure in-memory unit tests (no DbContext) for
/// <c>TimePlanningWorkingHoursService.ApplyRunningFlexChain</c> across a
/// UseOneMinuteIntervals MODE BOUNDARY inside a single list — the production
/// scenario of a worker whose displayed period spans the site's flip.
///
/// The chain forks per row: a one-minute row runs in the integer
/// <c>*InSeconds</c> columns and back-derives the doubles; a five-minute row
/// runs in the legacy 2-decimal doubles and its <c>*InSeconds</c> DTO fields are
/// CLEARED (ops reads a zero there as the signal that the row does not carry a
/// seconds balance — echoing back a stale value the database row still holds
/// from an earlier one-minute write is what let the displayed balance disagree
/// with the recomputed one).
/// Both running accumulators are nonetheless advanced on every row so the
/// balance carries across the boundary — that hand-off is what these tests pin.
///
/// The chain only reads <c>logger</c> from inside its catch blocks, so the
/// service is constructed with a substitute logger and nulls for every other
/// dependency; nothing here touches a database.
/// </summary>
[TestFixture]
public class RunningFlexChainModeBoundaryTests
{
    private TimePlanningWorkingHoursService _service = null!;

    /// <summary>A timeline that is never consulted (every row carries a marker).</summary>
    private static OneMinuteModeTimeline UnusedTimeline
        => new(false, Array.Empty<(bool, DateTime)>());

    [SetUp]
    public void SetUp()
    {
        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            dbContext: null!,
            userService: null!,
            localizationService: null!,
            baseDbContext: null!,
            options: null!,
            coreHelper: null!);
    }

    private static TimePlanningWorkingHoursModel FiveMinuteRow(
        int dayOfMonth, double flexHours, string paidOutFlex = "0",
        double sumFlexStart = 0, int sumFlexStartInSeconds = 0, int sumFlexEndInSeconds = 0)
        => new()
        {
            Date = new DateTime(2026, 6, dayOfMonth),
            RegisteredUnderOneMinuteIntervals = false,
            FlexHours = flexHours,
            PaidOutFlex = paidOutFlex,
            SumFlexStart = sumFlexStart,
            SumFlexStartInSeconds = sumFlexStartInSeconds,
            SumFlexEndInSeconds = sumFlexEndInSeconds
        };

    private static TimePlanningWorkingHoursModel OneMinuteRow(
        int dayOfMonth, int flexInSeconds, int paiedOutFlexInSeconds = 0,
        double sumFlexStart = 0, int sumFlexStartInSeconds = 0)
        => new()
        {
            Date = new DateTime(2026, 6, dayOfMonth),
            RegisteredUnderOneMinuteIntervals = true,
            FlexInSeconds = flexInSeconds,
            PaidOutFlex = "0",
            PaiedOutFlexInSeconds = paiedOutFlexInSeconds,
            SumFlexStart = sumFlexStart,
            SumFlexStartInSeconds = sumFlexStartInSeconds
        };

    // ------------------------------------------------------------------ //
    // 1. Five-minute rows followed by one-minute rows (the real flip)     //
    // ------------------------------------------------------------------ //

    [Test]
    public void FiveMinuteThenOneMinute_CarriesTheBalanceToTheSecond()
    {
        // 2.00 h opening, +1.50 h, -0.50 h  →  3.00 h at the boundary,
        // then +30 m 37 s and -37 s in one-minute mode.
        var rows = new List<TimePlanningWorkingHoursModel>
        {
            FiveMinuteRow(1, flexHours: 1.5, sumFlexStart: 2.0),
            FiveMinuteRow(2, flexHours: -0.5),
            OneMinuteRow(3, flexInSeconds: 1837),
            OneMinuteRow(4, flexInSeconds: -37)
        };

        _service.ApplyRunningFlexChain(rows, UnusedTimeline);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(3.0).Within(1e-9));

            // The hand-off: the first post-flip row opens on exactly the
            // pre-flip closing balance, in seconds.
            Assert.That(rows[2].SumFlexStartInSeconds, Is.EqualTo(10800),
                "3.00 h carried across the boundary as 10800 s.");
            Assert.That(rows[2].SumFlexEndInSeconds, Is.EqualTo(12637),
                "10800 + 1837 — the 37 s survives the boundary.");
            Assert.That(rows[3].SumFlexStartInSeconds, Is.EqualTo(12637));
            Assert.That(rows[3].SumFlexEndInSeconds, Is.EqualTo(12600));
            Assert.That(rows[3].SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));

            // Lockstep on every one-minute row: the double is the exact
            // back-derivation of the integer source of truth.
            Assert.That(rows[2].SumFlexEnd, Is.EqualTo(rows[2].SumFlexEndInSeconds / 3600.0));
            Assert.That(rows[3].SumFlexEnd, Is.EqualTo(rows[3].SumFlexEndInSeconds / 3600.0));

            // Five-minute rows carry no seconds — by design, not by omission
            // (see the fixture summary).
            Assert.That(rows[0].SumFlexEndInSeconds, Is.EqualTo(0));
            Assert.That(rows[1].SumFlexEndInSeconds, Is.EqualTo(0));
        });
    }

    // ------------------------------------------------------------------ //
    // 2. One-minute rows followed by five-minute rows (the reverse)       //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The seconds → double hand-off is LOSSLESS: the five-minute row opens on
    /// the full-precision <c>SumFlexEndInSeconds / 3600.0</c>, not on a
    /// whole-minute or 2-decimal truncation of it.
    ///
    /// The five-minute row's own OUTPUT is still <c>Math.Round(…, 2)</c> — that
    /// is the pre-existing legacy formula for every five-minute row and is not
    /// changed here — so a subsequent return to one-minute mode rebuilds the
    /// seconds accumulator from that rounded double and can lose up to 18 s.
    /// That is pinned below rather than left silent. It does not arise in
    /// production: <c>UseOneMinuteIntervals</c> is one-way, so a chain never
    /// crosses back from one-minute to five-minute.
    /// </summary>
    [Test]
    public void OneMinuteThenFiveMinute_HandsOffFullPrecision()
    {
        // 1 h 0 m 37 s — deliberately not a whole number of minutes, so any
        // truncation in the hand-off would show.
        var rows = new List<TimePlanningWorkingHoursModel>
        {
            OneMinuteRow(1, flexInSeconds: 3637),
            FiveMinuteRow(2, flexHours: 0.5),
            OneMinuteRow(3, flexInSeconds: 0)
        };

        _service.ApplyRunningFlexChain(rows, UnusedTimeline);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexEndInSeconds, Is.EqualTo(3637));
            Assert.That(rows[0].SumFlexEnd, Is.EqualTo(3637 / 3600.0));

            Assert.That(rows[1].SumFlexStart, Is.EqualTo(3637 / 3600.0),
                "The five-minute row opens on the EXACT seconds balance — the "
                + "hand-off applies no rounding of its own.");
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(1.51).Within(1e-9),
                "Its own output is 2-decimal rounded by the pre-existing legacy "
                + "formula (1.5102777… h → 1.51).");

            Assert.That(rows[2].SumFlexStartInSeconds, Is.EqualTo(5436),
                "Returning to one-minute mode rebuilds seconds from the rounded "
                + "double: 5437 s becomes 5436 s. Inherent to the legacy "
                + "five-minute rounding; unreachable in production because the "
                + "flag is one-way.");
        });
    }

    // ------------------------------------------------------------------ //
    // 3. Regression guard: a uniformly five-minute list is unchanged      //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The common case. Expected values are the pre-change legacy formulas
    /// computed by hand:
    ///   row 0: SumFlexStart = Round(1.234567, 2) = 1.23
    ///          SumFlexEnd   = Round(1.23 + 2.5 - 0.25, 2) = 3.48
    ///   row 1: SumFlexEnd   = Round(3.48 - 1.1 - 0, 2)    = 2.38
    ///   row 2: SumFlexEnd   = Round(2.38 + 0.333333 - 0.5, 2) = 2.21
    /// The sentinel <c>*InSeconds</c> values prove a five-minute row is returned
    /// carrying NO seconds balance: whatever the database row still holds from an
    /// earlier one-minute write is cleared, so no consumer — and no later
    /// recompute seeded off this response — can mistake it for a live balance.
    /// </summary>
    [Test]
    public void UniformlyFiveMinute_MatchesTheLegacyFormulasAndClearsSeconds()
    {
        const int sentinel = 424242;
        var rows = new List<TimePlanningWorkingHoursModel>
        {
            FiveMinuteRow(1, flexHours: 2.5, paidOutFlex: "0.25",
                sumFlexStart: 1.234567, sumFlexEndInSeconds: sentinel),
            FiveMinuteRow(2, flexHours: -1.1, sumFlexEndInSeconds: sentinel),
            FiveMinuteRow(3, flexHours: 0.333333, paidOutFlex: "0.5",
                sumFlexEndInSeconds: sentinel)
        };
        foreach (var row in rows)
        {
            row.SumFlexStartInSeconds = sentinel;
        }

        _service.ApplyRunningFlexChain(rows, UnusedTimeline);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexStart, Is.EqualTo(1.23).Within(1e-9));
            Assert.That(rows[0].SumFlexEnd, Is.EqualTo(3.48).Within(1e-9));
            Assert.That(rows[1].SumFlexStart, Is.EqualTo(3.48).Within(1e-9));
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(2.38).Within(1e-9));
            Assert.That(rows[2].SumFlexStart, Is.EqualTo(2.38).Within(1e-9));
            Assert.That(rows[2].SumFlexEnd, Is.EqualTo(2.21).Within(1e-9));

            foreach (var row in rows)
            {
                Assert.That(row.SumFlexStartInSeconds, Is.EqualTo(0),
                    "The sentinel must not survive: a five-minute row carries "
                    + "its balance in the decimals only.");
                Assert.That(row.SumFlexEndInSeconds, Is.EqualTo(0));
            }
        });
    }

    // ------------------------------------------------------------------ //
    // 4. The anchor seed — the balance-collapse mechanism itself          //
    // ------------------------------------------------------------------ //

    [Test]
    public void OneMinuteAnchor_WithZeroSecondsColumn_StartsFromTheDecimalBalance()
    {
        // SumFlexEndInSeconds / SumFlexStartInSeconds are 0 on ~97% of rows
        // (migration 20260108054344, defaultValue 0, no backfill). Seeding the
        // chain from the raw column discarded the whole 4.25 h opening balance.
        var rows = new List<TimePlanningWorkingHoursModel>
        {
            OneMinuteRow(1, flexInSeconds: 900, sumFlexStart: 4.25, sumFlexStartInSeconds: 0)
        };

        _service.ApplyRunningFlexChain(rows, UnusedTimeline);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexStartInSeconds, Is.EqualTo(15300),
                "4.25 h — NOT 0.");
            Assert.That(rows[0].SumFlexEndInSeconds, Is.EqualTo(16200));
            Assert.That(rows[0].SumFlexEnd, Is.EqualTo(4.5).Within(1e-9));
        });
    }

    [Test]
    public void OneMinuteAnchor_WithPopulatedSecondsColumn_IgnoresTheDecimal()
    {
        var rows = new List<TimePlanningWorkingHoursModel>
        {
            OneMinuteRow(1, flexInSeconds: 0, sumFlexStart: 99, sumFlexStartInSeconds: 7200)
        };

        _service.ApplyRunningFlexChain(rows, UnusedTimeline);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexStartInSeconds, Is.EqualTo(7200),
                "A populated seconds column is the source of truth.");
            Assert.That(rows[0].SumFlexEndInSeconds, Is.EqualTo(7200));
        });
    }

    /// <summary>
    /// The display-side twin of the persisted defect (tenant 994, site 21445):
    /// a five-minute row whose <c>SumFlexEndInSeconds</c> still holds an older
    /// one-minute write's value — -290456 s (-80.68 h) against a real decimal
    /// balance of -3.97 h. The chain must return it cleared, and the following
    /// one-minute row must open on the DECIMAL, not on the residue.
    /// </summary>
    [Test]
    public void FiveMinuteRowWithStaleSeconds_IsClearedAndDoesNotPoisonTheBoundary()
    {
        var rows = new List<TimePlanningWorkingHoursModel>
        {
            FiveMinuteRow(1, flexHours: -7.58, sumFlexStart: 3.61,
                sumFlexStartInSeconds: -263168, sumFlexEndInSeconds: -290456),
            OneMinuteRow(2, flexInSeconds: 0)
        };

        _service.ApplyRunningFlexChain(rows, UnusedTimeline);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexEnd, Is.EqualTo(-3.97).Within(1e-9),
                "3.61 - 7.58 — the real closing balance.");
            Assert.That(rows[0].SumFlexStartInSeconds, Is.EqualTo(0));
            Assert.That(rows[0].SumFlexEndInSeconds, Is.EqualTo(0),
                "-290456 s was residue, not a balance.");

            Assert.That(rows[1].SumFlexStartInSeconds, Is.EqualTo(-14292),
                "The next row opens on -3.97 h, NOT on -80.68 h.");
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(-3.97).Within(0.001));
        });
    }

    // ------------------------------------------------------------------ //
    // 5. End to end: unmarked rows split by the site's effective date     //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The full production shape: legacy rows carrying NO write-time marker,
    /// with the boundary supplied by the site's recorded
    /// <c>UseOneMinuteIntervalsFrom</c> (2026-06-01). Days before it must stay
    /// on 5-minute rules; the flip day onwards runs in seconds.
    /// </summary>
    [Test]
    public void UnmarkedRows_SplitByTheSitesEffectiveDate()
    {
        var timeline = new OneMinuteModeTimeline(
            currentFlag: true,
            versionFlags: Array.Empty<(bool, DateTime)>(),
            effectiveFrom: new DateTime(2026, 6, 1, 14, 45, 0));

        var before = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 30),
            RegisteredUnderOneMinuteIntervals = null,
            FlexHours = 1.0,
            PaidOutFlex = "0",
            SumFlexStart = 1.0
        };
        var onTheFlipDay = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 6, 1),
            RegisteredUnderOneMinuteIntervals = null,
            FlexInSeconds = 61,
            PaidOutFlex = "0"
        };
        var rows = new List<TimePlanningWorkingHoursModel> { before, onTheFlipDay };

        _service.ApplyRunningFlexChain(rows, timeline);

        Assert.Multiple(() =>
        {
            Assert.That(before.SumFlexEnd, Is.EqualTo(2.0).Within(1e-9),
                "Pre-flip day recomputed under 5-minute rules.");
            Assert.That(before.SumFlexEndInSeconds, Is.EqualTo(0),
                "…and its seconds DTO field left untouched.");
            Assert.That(onTheFlipDay.SumFlexStartInSeconds, Is.EqualTo(7200),
                "The flip day opens on the pre-flip balance.");
            Assert.That(onTheFlipDay.SumFlexEndInSeconds, Is.EqualTo(7261));
        });
    }
}
