using System;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PauseIdCorrectionTests
{
    private static readonly DateTime Start = new(2026, 6, 16, 9, 0, 0);

    [Test]
    public void Corrupt_AbsoluteTick_IsCorrectedToDuration()
    {
        // 28-minute real pause, but id=116 (absolute 09:35 tick) decodes to 575 min.
        var stop = Start.AddMinutes(28);
        Assert.That(PauseIdCorrection.CorrectedPauseId(Start, stop, 116), Is.EqualTo(6));
    }

    [Test]
    public void CorrectValue_ReturnsNull()
    {
        // 30-min pause, id=7 = (30/5)+1 is already correct.
        var stop = Start.AddMinutes(30);
        Assert.That(PauseIdCorrection.CorrectedPauseId(Start, stop, 7), Is.Null);
    }

    [Test]
    public void OffByOne_IsLeftAlone()
    {
        // 30-min pause, id=6 = 30/5 (missing +1) understates -> not corrected.
        var stop = Start.AddMinutes(30);
        Assert.That(PauseIdCorrection.CorrectedPauseId(Start, stop, 6), Is.Null);
    }

    [Test]
    public void ZeroOrMissing_ReturnsNull()
    {
        var stop = Start.AddMinutes(30);
        Assert.That(PauseIdCorrection.CorrectedPauseId(Start, stop, 0), Is.Null);
        Assert.That(PauseIdCorrection.CorrectedPauseId(null, stop, 116), Is.Null);
        Assert.That(PauseIdCorrection.CorrectedPauseId(Start, null, 116), Is.Null);
        Assert.That(PauseIdCorrection.CorrectedPauseId(Start, Start, 116), Is.Null);
    }
}
