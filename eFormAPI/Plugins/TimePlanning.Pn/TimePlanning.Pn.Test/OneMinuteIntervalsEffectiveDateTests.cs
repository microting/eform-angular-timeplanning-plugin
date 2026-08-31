using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pure in-memory unit tests (no DbContext) for the one-minute-intervals
/// EFFECTIVE-DATE fix.
///
/// Background: <c>AssignedSite.UseOneMinuteIntervals</c> is a per-site boolean
/// with no effective-from date, so every flex recomputation re-derived a
/// worker's ENTIRE history under the site's CURRENT mode — switching it on
/// silently restated already-closed periods at one-minute precision.
/// <c>AssignedSite.UseOneMinuteIntervalsFrom</c> records when the flag took
/// effect; NULL means "nothing recorded" and preserves today's behaviour.
///
/// Covered here:
///  - <see cref="OneMinuteModeTimeline.ResolveByEffectiveDate"/> — the single
///    place the stored date becomes a verdict (before / on / after, date-only,
///    NULL falls through, flag-off short-circuit).
///  - <see cref="OneMinuteModeTimeline.WasOneMinuteAt"/> — the stored date wins
///    over the AssignedSiteVersions-derived timeline; a NULL date falls through
///    to that timeline unchanged.
///  - <see cref="OneMinuteModeTimeline.ResolveRowModeAsync"/> — the per-row
///    write-time marker outranks both.
///  - <see cref="PlanRegistrationHelper.SumFlexEndSecondsWithFallback"/> — the
///    reverse seed fallback (SumFlexEndInSeconds is 0 on ~97% of rows).
///  - <see cref="OneMinuteModeTimeline.StampEffectiveDateOnEnable"/> — the
///    false→true settings stamp and its no-clobber guard.
/// </summary>
[TestFixture]
public class OneMinuteIntervalsEffectiveDateTests
{
    private static readonly DateTime EffectiveFrom = new(2026, 6, 1, 14, 45, 0); // mid-day save
    private static readonly DateTime StampedAt = new(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------- //
    // 1. ResolveByEffectiveDate — the single resolution expression      //
    // ---------------------------------------------------------------- //

    [Test]
    public void EffectiveDate_Null_ReturnsNull_SoCallersFallThroughToTheTimeline()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(true, null, new DateTime(2026, 6, 1)),
                Is.Null, "Nothing recorded → the derived timeline must answer.");
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(false, null, new DateTime(2026, 6, 1)),
                Is.Null);
        });
    }

    [Test]
    public void EffectiveDate_Set_SplitsOnTheDate_DateOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(true, EffectiveFrom, new DateTime(2026, 5, 31)),
                Is.False, "The day BEFORE the effective date stays 5-minute.");
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(true, EffectiveFrom, new DateTime(2026, 6, 1)),
                Is.True,
                "A PlanRegistration.Date is a midnight anchor: an effective date saved at "
                + "14:45 still governs the WHOLE of that day (date-only comparison).");
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(true, EffectiveFrom, new DateTime(2026, 6, 2)),
                Is.True);
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(true, EffectiveFrom, new DateTime(2030, 1, 1)),
                Is.True);
        });
    }

    [Test]
    public void EffectiveDate_Set_ButFlagOff_IsFalseEverywhere()
    {
        // The flag is one-way in the settings path, but an ops/raw-SQL turn-off
        // must not resurrect one-minute mode from a stale recorded date.
        Assert.Multiple(() =>
        {
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(false, EffectiveFrom, new DateTime(2026, 5, 1)),
                Is.False);
            Assert.That(
                OneMinuteModeTimeline.ResolveByEffectiveDate(false, EffectiveFrom, new DateTime(2027, 1, 1)),
                Is.False);
        });
    }

    // ---------------------------------------------------------------- //
    // 2. Timeline precedence — stored date beats the derived trail      //
    // ---------------------------------------------------------------- //

    [Test]
    public void StoredEffectiveDate_OverridesTheDerivedVersionTimeline()
    {
        // The audit trail says the flag flipped on 2026-03-01, but ops recovered
        // the real transition and recorded 2026-06-01. The stored date wins.
        var timeline = new OneMinuteModeTimeline(
            true,
            new List<(bool, DateTime)>
            {
                (false, new DateTime(2026, 1, 1)),
                (true, new DateTime(2026, 3, 1))
            },
            EffectiveFrom);

        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 3, 15)), Is.False,
                "The derived trail would say true here; the recorded date says otherwise.");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 5, 31)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 6, 1)), Is.True);
        });
    }

    [Test]
    public void NullEffectiveDate_LeavesTheDerivedTimelineUntouched()
    {
        var timeline = new OneMinuteModeTimeline(
            true,
            new List<(bool, DateTime)>
            {
                (false, new DateTime(2026, 1, 1)),
                (true, new DateTime(2026, 3, 1))
            });

        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 2, 1)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 3, 1)), Is.True,
                "With nothing recorded the AssignedSiteVersions walk still governs.");
        });
    }

    // ---------------------------------------------------------------- //
    // 3. Per-row precedence — the write-time marker outranks both       //
    // ---------------------------------------------------------------- //

    // The dbContext argument is only touched when neither the marker nor the
    // recorded effective date can answer, so these cases can pass null for it.

    [Test]
    public async Task RowMarker_WinsOverTheEffectiveDate()
    {
        var site = new AssignedSite
        {
            UseOneMinuteIntervals = true,
            UseOneMinuteIntervalsFrom = EffectiveFrom
        };

        // Registered under one-minute mode on a date BEFORE the effective date
        // (e.g. an admin re-registered the day after the flip): the marker is
        // ground truth and must win.
        var markedOneMinute = new PlanRegistration
        {
            Date = new DateTime(2026, 1, 15),
            RegisteredUnderOneMinuteIntervals = true
        };
        // Registered under 5-minute mode on a date AFTER the effective date.
        var markedFiveMinute = new PlanRegistration
        {
            Date = new DateTime(2026, 9, 15),
            RegisteredUnderOneMinuteIntervals = false
        };

        Assert.That(
            await OneMinuteModeTimeline.ResolveRowModeAsync(null!, site, markedOneMinute),
            Is.True);
        Assert.That(
            await OneMinuteModeTimeline.ResolveRowModeAsync(null!, site, markedFiveMinute),
            Is.False);
    }

    [Test]
    public async Task UnmarkedRow_ResolvesFromTheEffectiveDate()
    {
        var site = new AssignedSite
        {
            UseOneMinuteIntervals = true,
            UseOneMinuteIntervalsFrom = EffectiveFrom
        };

        var before = new PlanRegistration { Date = new DateTime(2026, 5, 31) };
        var onTheDay = new PlanRegistration { Date = new DateTime(2026, 6, 1) };
        var after = new PlanRegistration { Date = new DateTime(2026, 7, 1) };

        Assert.That(await OneMinuteModeTimeline.ResolveRowModeAsync(null!, site, before), Is.False,
            "A closed pre-switch day must NOT be recomputed at one-minute precision.");
        Assert.That(await OneMinuteModeTimeline.ResolveRowModeAsync(null!, site, onTheDay), Is.True);
        Assert.That(await OneMinuteModeTimeline.ResolveRowModeAsync(null!, site, after), Is.True);
    }

    [Test]
    public async Task NoAssignedSite_ResolvesToFiveMinute()
    {
        var row = new PlanRegistration { Date = new DateTime(2026, 6, 1) };
        Assert.That(await OneMinuteModeTimeline.ResolveRowModeAsync(null!, null, row), Is.False);
    }

    // ---------------------------------------------------------------- //
    // 4. Reverse seed fallback                                          //
    // ---------------------------------------------------------------- //

    [Test]
    public void SeedFallback_NullPredecessor_IsZero()
    {
        Assert.That(PlanRegistrationHelper.SumFlexEndSecondsWithFallback(null), Is.EqualTo(0));
    }

    [Test]
    public void SeedFallback_PopulatedSecondsWin()
    {
        var pre = new PlanRegistration { SumFlexEndInSeconds = 7261, SumFlexEnd = 99 };
        Assert.That(PlanRegistrationHelper.SumFlexEndSecondsWithFallback(pre), Is.EqualTo(7261),
            "When the seconds column is populated it is the source of truth.");
    }

    [Test]
    public void SeedFallback_ZeroSeconds_FallsBackToTheDecimalBalance()
    {
        // Migration 20260108054344 added SumFlexEndInSeconds with defaultValue 0
        // and no backfill, so on ~97% of rows the real balance is only in the
        // decimal. Seeding from the raw column discards the whole balance —
        // which also fires on the FIRST post-switch row, whose predecessor is by
        // definition a pre-switch row that only ever had decimals written.
        Assert.Multiple(() =>
        {
            Assert.That(
                PlanRegistrationHelper.SumFlexEndSecondsWithFallback(
                    new PlanRegistration { SumFlexEndInSeconds = 0, SumFlexEnd = 12.5 }),
                Is.EqualTo(45000));
            Assert.That(
                PlanRegistrationHelper.SumFlexEndSecondsWithFallback(
                    new PlanRegistration { SumFlexEndInSeconds = 0, SumFlexEnd = -2.25 }),
                Is.EqualTo(-8100), "A negative carried balance survives the fallback.");
            Assert.That(
                PlanRegistrationHelper.SumFlexEndSecondsWithFallback(
                    new PlanRegistration { SumFlexEndInSeconds = 0, SumFlexEnd = 0 }),
                Is.EqualTo(0), "A genuine zero and an unbackfilled zero agree.");
        });
    }

    // ---------------------------------------------------------------- //
    // 5. The settings stamp                                             //
    // ---------------------------------------------------------------- //

    [Test]
    public void Stamp_FiresOnFalseToTrue()
    {
        var site = new AssignedSite { UseOneMinuteIntervals = false, UseOneMinuteIntervalsFrom = null };
        OneMinuteModeTimeline.StampEffectiveDateOnEnable(site, true, StampedAt);
        Assert.That(site.UseOneMinuteIntervalsFrom, Is.EqualTo(StampedAt));
    }

    [Test]
    public void Stamp_DoesNotFireWhenAlreadyTrue()
    {
        // UseOneMinuteIntervals is one-way (it is ORed with the incoming value),
        // so every later settings save re-submits true. Stamping again here
        // would move the effective date forward on every save.
        var site = new AssignedSite { UseOneMinuteIntervals = true, UseOneMinuteIntervalsFrom = null };
        OneMinuteModeTimeline.StampEffectiveDateOnEnable(site, true, StampedAt);
        Assert.That(site.UseOneMinuteIntervalsFrom, Is.Null);
    }

    [Test]
    public void Stamp_DoesNotOverwriteAnExistingDate()
    {
        // An ops script backfills recovered historical dates; a later settings
        // save must not clobber one with today's date.
        var backfilled = new DateTime(2025, 4, 2, 8, 0, 0, DateTimeKind.Utc);
        var site = new AssignedSite
        {
            UseOneMinuteIntervals = false,
            UseOneMinuteIntervalsFrom = backfilled
        };
        OneMinuteModeTimeline.StampEffectiveDateOnEnable(site, true, StampedAt);
        Assert.That(site.UseOneMinuteIntervalsFrom, Is.EqualTo(backfilled));
    }

    [Test]
    public void Stamp_DoesNotFireWhenIncomingIsFalse()
    {
        var site = new AssignedSite { UseOneMinuteIntervals = false, UseOneMinuteIntervalsFrom = null };
        OneMinuteModeTimeline.StampEffectiveDateOnEnable(site, false, StampedAt);
        Assert.That(site.UseOneMinuteIntervalsFrom, Is.Null);
    }
}
