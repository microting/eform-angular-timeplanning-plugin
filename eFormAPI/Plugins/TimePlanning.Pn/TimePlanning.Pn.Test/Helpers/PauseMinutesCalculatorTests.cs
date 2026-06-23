using System;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test.Helpers;

[TestFixture]
public class PauseMinutesCalculatorTests
{
    // All timestamps are UTC (transport/storage rule).
    private static readonly DateTime Start = new(2026, 6, 16, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void SinglePause_Shift1_ThirtyMinutes_ReturnsId6()
    {
        var planning = new PlanRegistration
        {
            Pause1StartedAt = Start,
            Pause1StoppedAt = Start.AddMinutes(30),
        };

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 1), Is.EqualTo(6));
    }

    [Test]
    public void SinglePause_Shift2_ThirtyMinutes_ReturnsId6()
    {
        var planning = new PlanRegistration
        {
            Pause20StartedAt = Start,
            Pause20StoppedAt = Start.AddMinutes(30),
        };

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 2), Is.EqualTo(6));
    }

    [Test]
    public void MultiplePauses_Shift1_Accumulate_30Plus20_ReturnsId10()
    {
        var planning = new PlanRegistration
        {
            Pause1StartedAt = Start,
            Pause1StoppedAt = Start.AddMinutes(30),
            Pause10StartedAt = Start.AddHours(2),
            Pause10StoppedAt = Start.AddHours(2).AddMinutes(20),
        };

        // (30 + 20) / 5 = 10
        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 1), Is.EqualTo(10));
    }

    [Test]
    public void MultiplePauses_Shift2_Accumulate_30Plus20_ReturnsId10()
    {
        var planning = new PlanRegistration
        {
            Pause20StartedAt = Start,
            Pause20StoppedAt = Start.AddMinutes(30),
            Pause21StartedAt = Start.AddHours(2),
            Pause21StoppedAt = Start.AddHours(2).AddMinutes(20),
        };

        // (30 + 20) / 5 = 10
        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 2), Is.EqualTo(10));
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

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 1), Is.EqualTo(6));
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

        Assert.That(PauseMinutesCalculator.DerivePauseId(planning, 2), Is.EqualTo(6));
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
}
