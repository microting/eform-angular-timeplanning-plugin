using System;
using System.Collections.Generic;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pure in-memory unit tests (no DB) for <see cref="OneMinuteModeTimeline"/>,
/// the mode-at-registration resolver built from AssignedSiteVersions audit
/// rows. Exercises every documented edge: no version rows (fallback to the
/// current flag), flag already true in the earliest version (born-true),
/// a single false→true flip, multiple toggles, mid-day saves governing the
/// whole day (date-only comparison), same-day double toggles where the
/// last save wins, and the divergence correction (trail flipped outside the
/// audited path → current flag wins from the last audited save date).
/// </summary>
[TestFixture]
public class OneMinuteModeTimelineTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 9, 30, 0);
    private static readonly DateTime FlipDay = new(2026, 6, 1, 14, 45, 0); // mid-day save

    private static OneMinuteModeTimeline Timeline(
        bool fallback, params (bool Flag, DateTime SavedAt)[] versions)
        => new(fallback, new List<(bool, DateTime)>(versions));

    [Test]
    public void NoVersionRows_FallsBackToCurrentFlag()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Timeline(true).WasOneMinuteAt(new DateTime(2020, 1, 1)), Is.True);
            Assert.That(Timeline(true).WasOneMinuteAt(new DateTime(2030, 1, 1)), Is.True);
            Assert.That(Timeline(false).WasOneMinuteAt(new DateTime(2026, 6, 1)), Is.False);
        });
    }

    [Test]
    public void BornTrue_TrueFromTheBeginningOfTime()
    {
        var timeline = Timeline(true, (true, Origin));
        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(DateTime.MinValue), Is.True);
            Assert.That(timeline.WasOneMinuteAt(Origin.AddYears(-10)), Is.True,
                "Dates before the first version row take the earliest version's value.");
            Assert.That(timeline.WasOneMinuteAt(Origin.AddYears(10)), Is.True);
        });
    }

    /// <summary>
    /// Divergence correction: the audit trail ends on false but the entity's
    /// CURRENT flag is true — the flip happened outside the audited path
    /// (raw-SQL ops change, or a seed dump predating the column; the l1m CI
    /// shard is exactly this shape). The current flag must take over from the
    /// LAST audited save date; audited history before it is preserved.
    /// </summary>
    [Test]
    public void TrailDivergesFromCurrentFlag_CurrentFlagWinsFromLastAuditedSave()
    {
        var lastSave = new DateTime(2026, 3, 10, 9, 0, 0);
        var timeline = Timeline(true, (false, Origin), (false, lastSave));
        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 2, 1)), Is.False,
                "Audited pre-divergence history stays authoritative.");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 3, 10)), Is.True,
                "From the last audited save date the CURRENT flag governs.");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 7, 1)), Is.True);
        });
    }

    [Test]
    public void SingleFlip_FalseThenTrue_SplitsOnTheSaveDate()
    {
        var timeline = Timeline(true, (false, Origin), (true, FlipDay));
        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(Origin.AddYears(-1)), Is.False,
                "Dates before the earliest version take the earliest value.");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 5, 31)), Is.False,
                "The day before the flip stays 5-minute.");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 6, 1)), Is.True,
                "A mid-day (14:45) flip governs the WHOLE flip day (date-only rule).");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 6, 2)), Is.True);
        });
    }

    [Test]
    public void ConsecutiveDuplicateVersions_ProduceNoChangePoints()
    {
        // Every AssignedSite edit writes a version row; most don't touch the
        // flag. Duplicates must not create change points.
        var timeline = Timeline(true,
            (false, Origin),
            (false, Origin.AddMonths(1)),
            (false, Origin.AddMonths(2)),
            (true, FlipDay),
            (true, FlipDay.AddDays(3)));
        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(Origin.AddMonths(2)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(FlipDay), Is.True);
            Assert.That(timeline.WasOneMinuteAt(FlipDay.AddDays(10)), Is.True);
        });
    }

    [Test]
    public void MultiToggle_IntervalWalk()
    {
        var backDay = new DateTime(2026, 8, 15, 8, 0, 0);
        var reFlipDay = new DateTime(2026, 11, 3, 16, 20, 0);
        var timeline = Timeline(true,
            (false, Origin), (true, FlipDay), (false, backDay), (true, reFlipDay));
        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 3, 1)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 7, 1)), Is.True);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 8, 15)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 10, 1)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 11, 3)), Is.True);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2027, 1, 1)), Is.True);
        });
    }

    [Test]
    public void SameDayDoubleToggle_LastSaveWins()
    {
        // Current flag false — consistent with the trail's last save, so the
        // divergence correction stays inert and pure save-order rules apply.
        var timeline = Timeline(false,
            (false, Origin),
            (true, new DateTime(2026, 6, 1, 10, 0, 0)),
            (false, new DateTime(2026, 6, 1, 11, 0, 0)));
        Assert.Multiple(() =>
        {
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 6, 1)), Is.False,
                "Two toggles saved the same day: the LAST save governs that day.");
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 6, 2)), Is.False);
            Assert.That(timeline.WasOneMinuteAt(new DateTime(2026, 5, 31)), Is.False);
        });
    }
}
