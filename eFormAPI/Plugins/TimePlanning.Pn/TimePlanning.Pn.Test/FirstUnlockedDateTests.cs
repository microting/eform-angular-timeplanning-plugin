using System;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class FirstUnlockedDateTests
{
    [Test]
    public void Day17_UsesPreviousMonth21st_PlusOne()
        => Assert.That(CorruptedPauseIdRepair.FirstUnlockedDate(new DateTime(2026, 6, 17)),
            Is.EqualTo(new DateTime(2026, 5, 22)));

    [Test]
    public void Day20_StillPreviousMonth()
        => Assert.That(CorruptedPauseIdRepair.FirstUnlockedDate(new DateTime(2026, 6, 20)),
            Is.EqualTo(new DateTime(2026, 5, 22)));

    [Test]
    public void Day21_SwitchesToCurrentMonth()
        => Assert.That(CorruptedPauseIdRepair.FirstUnlockedDate(new DateTime(2026, 6, 21)),
            Is.EqualTo(new DateTime(2026, 6, 22)));

    [Test]
    public void Day25_CurrentMonth()
        => Assert.That(CorruptedPauseIdRepair.FirstUnlockedDate(new DateTime(2026, 12, 25)),
            Is.EqualTo(new DateTime(2026, 12, 22)));

    [Test]
    public void EarlyJanuary_RollsToPreviousDecember()
        => Assert.That(CorruptedPauseIdRepair.FirstUnlockedDate(new DateTime(2026, 1, 10)),
            Is.EqualTo(new DateTime(2025, 12, 22)));
}
