using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test.Helpers;

[TestFixture]
public class PauseMinutesCalculatorTests
{
    // All timestamps are UTC (transport/storage rule).
    private static readonly DateTime Start = new(2026, 6, 16, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void SinglePause_Shift1_ThirtyMinutes_ReturnsId7()
    {
        var planning = new PlanRegistration
        {
            Pause1StartedAt = Start,
            Pause1StoppedAt = Start.AddMinutes(30),
        };

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 1), Is.EqualTo(7));
    }

    [Test]
    public void SinglePause_Shift2_ThirtyMinutes_ReturnsId7()
    {
        var planning = new PlanRegistration
        {
            Pause20StartedAt = Start,
            Pause20StoppedAt = Start.AddMinutes(30),
        };

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 2), Is.EqualTo(7));
    }

    [Test]
    public void MultiplePauses_Shift1_Accumulate_30Plus20_ReturnsId11()
    {
        var planning = new PlanRegistration
        {
            Pause1StartedAt = Start,
            Pause1StoppedAt = Start.AddMinutes(30),
            Pause10StartedAt = Start.AddHours(2),
            Pause10StoppedAt = Start.AddHours(2).AddMinutes(20),
        };

        // (30 + 20) / 5 + 1 = 11 — 5-min tick encoding, decoded as (id - 1) * 5
        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 1), Is.EqualTo(11));
    }

    [Test]
    public void MultiplePauses_Shift2_Accumulate_30Plus20_ReturnsId11()
    {
        var planning = new PlanRegistration
        {
            Pause20StartedAt = Start,
            Pause20StoppedAt = Start.AddMinutes(30),
            Pause21StartedAt = Start.AddHours(2),
            Pause21StoppedAt = Start.AddHours(2).AddMinutes(20),
        };

        // (30 + 20) / 5 + 1 = 11 — 5-min tick encoding, decoded as (id - 1) * 5
        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 2), Is.EqualTo(11));
    }

    [Test]
    public void LastSlot_Shift1_Pause102_IsIncluded()
    {
        // Only the final slot of shift 1 is set; a short/wrong slot list would miss it.
        var planning = new PlanRegistration
        {
            Pause102StartedAt = Start,
            Pause102StoppedAt = Start.AddMinutes(30),
        };

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 1), Is.EqualTo(7));
    }

    [Test]
    public void LastSlot_Shift2_Pause202_IsIncluded()
    {
        // Only the final slot of shift 2 is set; a short/wrong slot list would miss it.
        var planning = new PlanRegistration
        {
            Pause202StartedAt = Start,
            Pause202StoppedAt = Start.AddMinutes(30),
        };

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 2), Is.EqualTo(7));
    }

    [Test]
    public void NoPauses_Shift1_ReturnsZero()
    {
        Assert.That(PauseMinutesCalculator.DerivePauseId(new PlanRegistration(), 1), Is.EqualTo(0));
    }

    [Test]
    public void NoPauses_Shift2_ReturnsZero()
    {
        Assert.That(PauseMinutesCalculator.DerivePauseId(new PlanRegistration(), 2), Is.EqualTo(0));
    }

    [Test]
    public void Purity_Shift1_DoesNotMutateShift1PauseNumber()
    {
        var planning = new PlanRegistration
        {
            Shift1PauseNumber = 99,
            Pause1StartedAt = Start,
            Pause1StoppedAt = Start.AddMinutes(30),
        };

        PauseMinutesCalculator.DerivePauseId(planning, 1);

        Assert.That(planning.Shift1PauseNumber, Is.EqualTo(99));
    }

    [Test]
    public void Purity_Shift2_DoesNotMutateShift2PauseNumber()
    {
        var planning = new PlanRegistration
        {
            Shift2PauseNumber = 99,
            Pause20StartedAt = Start,
            Pause20StoppedAt = Start.AddMinutes(30),
        };

        PauseMinutesCalculator.DerivePauseId(planning, 2);

        Assert.That(planning.Shift2PauseNumber, Is.EqualTo(99));
    }

    [Test]
    public void InvalidShift_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PauseMinutesCalculator.DerivePauseId(new PlanRegistration(), 3));
    }

    // ---------------------------------------------------------------------
    // Canonical tick encoding regressions.
    //
    // A 5-minute pause tick id is minutes / 5 + 1, and every decoder in the
    // platform reads it back as (id - 1) * 5 (base FlexChain.ComputeShiftPauseSeconds
    // falls back to exactly that when a shift has no stamps). Id 0 means "no pause".
    // DerivePauseId used to return minutes / 5, one tick short, so a 30 min
    // pause was stored as id 6 and every reader saw 25 min.
    // ---------------------------------------------------------------------

    private static int DecodeTick(int pauseId) => pauseId > 0 ? (pauseId - 1) * 5 : 0;

    [TestCase(1, 30, 7)]
    [TestCase(1, 5, 2)]
    [TestCase(1, 60, 13)]
    [TestCase(2, 30, 7)]
    [TestCase(2, 45, 10)]
    public void CanonicalEncoding_PauseId_IsMinutesDividedByFivePlusOne(int shift, int minutes, int expectedId)
    {
        var planning = new PlanRegistration();
        if (shift == 1)
        {
            planning.Pause10StartedAt = Start;
            planning.Pause10StoppedAt = Start.AddMinutes(minutes);
        }
        else
        {
            planning.Pause20StartedAt = Start;
            planning.Pause20StoppedAt = Start.AddMinutes(minutes);
        }

        var id = PauseMinutesCalculator.DerivePauseId(planning, shift);

        Assert.That(id, Is.EqualTo(expectedId),
            $"A {minutes} min pause on shift {shift} must encode as tick id {expectedId} " +
            $"(minutes / 5 + 1), because every reader decodes (id - 1) * 5. Got id {id}, " +
            $"which decodes to {DecodeTick(id)} min.");
    }

    [Test]
    public void CanonicalEncoding_DerivedId_DecodesToTheSameMinutesFlexChainDeducts()
    {
        // 5-minute aligned stamps, so the 5-min floor in FlexChain changes nothing:
        // the derived id must decode to exactly the pause FlexChain deducts.
        var planning = new PlanRegistration
        {
            Pause1StartedAt = Start,
            Pause1StoppedAt = Start.AddMinutes(30),
            Pause10StartedAt = Start.AddHours(2),
            Pause10StoppedAt = Start.AddHours(2).AddMinutes(20),
        };
        var canonicalMinutes = FlexChain.ComputeShiftPauseSeconds(planning, 1, useOneMinuteIntervals: false) / 60;

        var id = PauseMinutesCalculator.DerivePauseId(planning, 1);

        Assert.That(DecodeTick(id), Is.EqualTo(canonicalMinutes),
            $"Pause1Id derived from the stamps must decode ((id - 1) * 5) to the {canonicalMinutes} min " +
            $"FlexChain deducts for the same stamps; got id {id} = {DecodeTick(id)} min.");
    }

    [Test]
    public void CanonicalSlotMapping_Pause2Slot_BelongsToShift2_NotShift1()
    {
        // FlexChain.EnumerateShiftPauseStampPairs: shift 1 = Pause1, Pause10..19,
        // Pause100..102; shift 2 = Pause2 (its primary), Pause20..29, Pause200..202.
        // DerivePauseId used to count the Pause2 slot towards shift 1 and leave it
        // out of shift 2, moving a pause on the second shift to the first.
        var planning = new PlanRegistration
        {
            Pause2StartedAt = Start,
            Pause2StoppedAt = Start.AddMinutes(30),
        };

        var shift1Id = PauseMinutesCalculator.DerivePauseId(planning, 1);
        var shift2Id = PauseMinutesCalculator.DerivePauseId(planning, 2);

        Assert.Multiple(() =>
        {
            Assert.That(shift1Id, Is.EqualTo(0),
                $"The Pause2 stamp is shift 2's primary pause (FlexChain slot mapping), so shift 1 " +
                $"must derive no pause (id 0). Got id {shift1Id} = {DecodeTick(shift1Id)} min.");
            Assert.That(shift2Id, Is.EqualTo(7),
                $"Shift 2 must count its primary Pause2 slot: 30 min = id 7. Got id {shift2Id} " +
                $"= {DecodeTick(shift2Id)} min.");
        });
    }
}
