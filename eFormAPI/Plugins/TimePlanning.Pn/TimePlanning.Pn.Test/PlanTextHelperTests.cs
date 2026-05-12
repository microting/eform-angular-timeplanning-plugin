using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanTextHelperTests
{
    // ──────────────────────────────────────────────
    //  BreakTimeCalculator tests
    // ──────────────────────────────────────────────

    [TestCase("0.1", 5)]
    [TestCase(".1", 5)]
    [TestCase("0.15", 10)]
    [TestCase(".15", 10)]
    [TestCase("0.25", 15)]
    [TestCase(".25", 15)]
    [TestCase("0.3", 20)]
    [TestCase(".3", 20)]
    [TestCase("0.4", 25)]
    [TestCase(".4", 25)]
    [TestCase("0.5", 30)]
    [TestCase(".5", 30)]
    [TestCase("0.6", 35)]
    [TestCase(".6", 35)]
    [TestCase("0.7", 40)]
    [TestCase(".7", 40)]
    [TestCase("0.75", 45)]
    [TestCase(".75", 45)]
    [TestCase("0.8", 50)]
    [TestCase(".8", 50)]
    [TestCase("0.9", 55)]
    [TestCase(".9", 55)]
    [TestCase("¾", 45)]
    [TestCase("½", 30)]
    [TestCase("1", 60)]
    [TestCase("unknown", 0)]
    [TestCase("", 0)]
    public void BreakTimeCalculator_ReturnsCorrectMinutes(string input, int expectedMinutes)
    {
        Assert.That(PlanTextHelper.BreakTimeCalculator(input), Is.EqualTo(expectedMinutes));
    }

    // ──────────────────────────────────────────────
    //  MinutesToBreakString tests (reverse of BreakTimeCalculator)
    // ──────────────────────────────────────────────

    [TestCase(5, "0.1")]
    [TestCase(10, "0.15")]
    [TestCase(15, "0.25")]
    [TestCase(20, "0.3")]
    [TestCase(25, "0.4")]
    [TestCase(30, "0.5")]
    [TestCase(35, "0.6")]
    [TestCase(40, "0.7")]
    [TestCase(45, "0.75")]
    [TestCase(50, "0.8")]
    [TestCase(55, "0.9")]
    [TestCase(60, "1")]
    [TestCase(0, "0")]
    [TestCase(7, "0")]  // not in the mapping
    public void MinutesToBreakString_ReturnsCorrectString(int minutes, string expected)
    {
        Assert.That(PlanTextHelper.MinutesToBreakString(minutes), Is.EqualTo(expected));
    }

    // ──────────────────────────────────────────────
    //  MinutesToBreakString round-trip with BreakTimeCalculator
    // ──────────────────────────────────────────────

    [TestCase(5)]
    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    [TestCase(25)]
    [TestCase(30)]
    [TestCase(35)]
    [TestCase(40)]
    [TestCase(45)]
    [TestCase(50)]
    [TestCase(55)]
    [TestCase(60)]
    public void BreakRoundTrip_MinutesToStringToMinutes(int minutes)
    {
        var str = PlanTextHelper.MinutesToBreakString(minutes);
        var result = PlanTextHelper.BreakTimeCalculator(str);
        Assert.That(result, Is.EqualTo(minutes));
    }

    // ──────────────────────────────────────────────
    //  ParseTimeToMinutes tests
    // ──────────────────────────────────────────────

    [TestCase("8:00", 480)]
    [TestCase("8.00", 480)]
    [TestCase("16:00", 960)]
    [TestCase("8:30", 510)]
    [TestCase("8.30", 510)]
    [TestCase("0:00", 0)]
    [TestCase("12:45", 765)]
    public void ParseTimeToMinutes_ReturnsCorrectValue(string input, int expected)
    {
        Assert.That(PlanTextHelper.ParseTimeToMinutes(input), Is.EqualTo(expected));
    }

    // ──────────────────────────────────────────────
    //  MinutesToTimeString tests
    // ──────────────────────────────────────────────

    [TestCase(480, "8:00")]
    [TestCase(960, "16:00")]
    [TestCase(510, "8:30")]
    [TestCase(0, "0:00")]
    [TestCase(765, "12:45")]
    public void MinutesToTimeString_ReturnsCorrectValue(int minutes, string expected)
    {
        Assert.That(PlanTextHelper.MinutesToTimeString(minutes), Is.EqualTo(expected));
    }

    // ──────────────────────────────────────────────
    //  ParsePlanText tests – single shift
    // ──────────────────────────────────────────────

    [Test]
    public void ParsePlanText_SingleShiftNoBreak()
    {
        var reg = new PlanRegistration { PlanText = "8:00-16:00" };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(480));
        Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(960));
        Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(0));
        Assert.That(reg.PlanHours, Is.EqualTo(8.0));
    }

    [Test]
    public void ParsePlanText_SingleShiftWithBreak()
    {
        var reg = new PlanRegistration { PlanText = "8:00-16:00/0.5" };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(480));
        Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(960));
        Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(30));
        Assert.That(reg.PlanHours, Is.EqualTo(7.5));
    }

    [Test]
    public void ParsePlanText_SingleShiftDotSeparator()
    {
        var reg = new PlanRegistration { PlanText = "8.00-16.00/0.5" };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(480));
        Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(960));
        Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(30));
    }

    // ──────────────────────────────────────────────
    //  ParsePlanText tests – multiple shifts
    // ──────────────────────────────────────────────

    [Test]
    public void ParsePlanText_TwoShifts()
    {
        var reg = new PlanRegistration { PlanText = "8:00-12:00/0.25;13:00-17:00/0.5" };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(480));
        Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(720));
        Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(15));

        Assert.That(reg.PlannedStartOfShift2, Is.EqualTo(780));
        Assert.That(reg.PlannedEndOfShift2, Is.EqualTo(1020));
        Assert.That(reg.PlannedBreakOfShift2, Is.EqualTo(30));

        // Total: (720-480-15) + (1020-780-30) = 225 + 210 = 435 min = 7.25 hours
        Assert.That(reg.PlanHours, Is.EqualTo(435.0 / 60.0));
    }

    [Test]
    public void ParsePlanText_FiveShifts()
    {
        var reg = new PlanRegistration
        {
            PlanText = "6:00-8:00;9:00-11:00;12:00-14:00;15:00-17:00;18:00-20:00"
        };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(360));
        Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(480));
        Assert.That(reg.PlannedStartOfShift2, Is.EqualTo(540));
        Assert.That(reg.PlannedEndOfShift2, Is.EqualTo(660));
        Assert.That(reg.PlannedStartOfShift3, Is.EqualTo(720));
        Assert.That(reg.PlannedEndOfShift3, Is.EqualTo(840));
        Assert.That(reg.PlannedStartOfShift4, Is.EqualTo(900));
        Assert.That(reg.PlannedEndOfShift4, Is.EqualTo(1020));
        Assert.That(reg.PlannedStartOfShift5, Is.EqualTo(1080));
        Assert.That(reg.PlannedEndOfShift5, Is.EqualTo(1200));
        Assert.That(reg.PlanHours, Is.EqualTo(10.0));
    }

    [Test]
    public void ParsePlanText_EmptyString_NoChange()
    {
        var reg = new PlanRegistration { PlanText = "" };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(0));
        Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(0));
    }

    [Test]
    public void ParsePlanText_NullString_NoChange()
    {
        var reg = new PlanRegistration { PlanText = null };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────
    //  GeneratePlanText tests
    // ──────────────────────────────────────────────

    [Test]
    public void GeneratePlanText_SingleShiftNoBreak()
    {
        var reg = new PlanRegistration
        {
            PlannedStartOfShift1 = 480,
            PlannedEndOfShift1 = 960,
            PlannedBreakOfShift1 = 0
        };
        var result = PlanTextHelper.GeneratePlanText(reg);
        Assert.That(result, Is.EqualTo("8:00-16:00"));
    }

    [Test]
    public void GeneratePlanText_SingleShiftWithBreak()
    {
        var reg = new PlanRegistration
        {
            PlannedStartOfShift1 = 480,
            PlannedEndOfShift1 = 960,
            PlannedBreakOfShift1 = 30
        };
        var result = PlanTextHelper.GeneratePlanText(reg);
        Assert.That(result, Is.EqualTo("8:00-16:00/0.5"));
    }

    [Test]
    public void GeneratePlanText_TwoShifts()
    {
        var reg = new PlanRegistration
        {
            PlannedStartOfShift1 = 480,
            PlannedEndOfShift1 = 720,
            PlannedBreakOfShift1 = 15,
            PlannedStartOfShift2 = 780,
            PlannedEndOfShift2 = 1020,
            PlannedBreakOfShift2 = 30
        };
        var result = PlanTextHelper.GeneratePlanText(reg);
        Assert.That(result, Is.EqualTo("8:00-12:00/0.25;13:00-17:00/0.5"));
    }

    [Test]
    public void GeneratePlanText_NoShifts_ReturnsEmpty()
    {
        var reg = new PlanRegistration();
        var result = PlanTextHelper.GeneratePlanText(reg);
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void GeneratePlanText_FiveShifts()
    {
        var reg = new PlanRegistration
        {
            PlannedStartOfShift1 = 360, PlannedEndOfShift1 = 480,
            PlannedStartOfShift2 = 540, PlannedEndOfShift2 = 660,
            PlannedStartOfShift3 = 720, PlannedEndOfShift3 = 840,
            PlannedStartOfShift4 = 900, PlannedEndOfShift4 = 1020,
            PlannedStartOfShift5 = 1080, PlannedEndOfShift5 = 1200
        };
        var result = PlanTextHelper.GeneratePlanText(reg);
        Assert.That(result, Is.EqualTo("6:00-8:00;9:00-11:00;12:00-14:00;15:00-17:00;18:00-20:00"));
    }

    // ──────────────────────────────────────────────
    //  Round-trip tests: Parse → Generate → Parse should yield same shift values
    // ──────────────────────────────────────────────

    [TestCase("8:00-16:00/0.5")]
    [TestCase("8:00-12:00/0.25;13:00-17:00/0.5")]
    [TestCase("6:00-8:00;9:00-11:00;12:00-14:00;15:00-17:00;18:00-20:00")]
    [TestCase("7:30-15:30/0.75")]
    [TestCase("8:00-16:00")]
    public void RoundTrip_ParseThenGenerate_ProducesBitwiseSameShifts(string planText)
    {
        // Parse the original text
        var reg1 = new PlanRegistration { PlanText = planText };
        PlanTextHelper.ParsePlanText(reg1);

        // Generate text from the parsed shifts
        var generatedText = PlanTextHelper.GeneratePlanText(reg1);

        // Parse the generated text
        var reg2 = new PlanRegistration { PlanText = generatedText };
        PlanTextHelper.ParsePlanText(reg2);

        // Assert all shift fields are bitwise identical
        Assert.That(reg2.PlannedStartOfShift1, Is.EqualTo(reg1.PlannedStartOfShift1));
        Assert.That(reg2.PlannedEndOfShift1, Is.EqualTo(reg1.PlannedEndOfShift1));
        Assert.That(reg2.PlannedBreakOfShift1, Is.EqualTo(reg1.PlannedBreakOfShift1));
        Assert.That(reg2.PlannedStartOfShift2, Is.EqualTo(reg1.PlannedStartOfShift2));
        Assert.That(reg2.PlannedEndOfShift2, Is.EqualTo(reg1.PlannedEndOfShift2));
        Assert.That(reg2.PlannedBreakOfShift2, Is.EqualTo(reg1.PlannedBreakOfShift2));
        Assert.That(reg2.PlannedStartOfShift3, Is.EqualTo(reg1.PlannedStartOfShift3));
        Assert.That(reg2.PlannedEndOfShift3, Is.EqualTo(reg1.PlannedEndOfShift3));
        Assert.That(reg2.PlannedBreakOfShift3, Is.EqualTo(reg1.PlannedBreakOfShift3));
        Assert.That(reg2.PlannedStartOfShift4, Is.EqualTo(reg1.PlannedStartOfShift4));
        Assert.That(reg2.PlannedEndOfShift4, Is.EqualTo(reg1.PlannedEndOfShift4));
        Assert.That(reg2.PlannedBreakOfShift4, Is.EqualTo(reg1.PlannedBreakOfShift4));
        Assert.That(reg2.PlannedStartOfShift5, Is.EqualTo(reg1.PlannedStartOfShift5));
        Assert.That(reg2.PlannedEndOfShift5, Is.EqualTo(reg1.PlannedEndOfShift5));
        Assert.That(reg2.PlannedBreakOfShift5, Is.EqualTo(reg1.PlannedBreakOfShift5));
        Assert.That(reg2.PlanHours, Is.EqualTo(reg1.PlanHours));
    }

    [Test]
    public void RoundTrip_Generate_Then_Parse_Matches()
    {
        var reg1 = new PlanRegistration
        {
            PlannedStartOfShift1 = 480,
            PlannedEndOfShift1 = 960,
            PlannedBreakOfShift1 = 30,
            PlannedStartOfShift2 = 1020,
            PlannedEndOfShift2 = 1200,
            PlannedBreakOfShift2 = 15,
        };

        var text = PlanTextHelper.GeneratePlanText(reg1);
        var reg2 = new PlanRegistration { PlanText = text };
        PlanTextHelper.ParsePlanText(reg2);

        Assert.That(reg2.PlannedStartOfShift1, Is.EqualTo(reg1.PlannedStartOfShift1));
        Assert.That(reg2.PlannedEndOfShift1, Is.EqualTo(reg1.PlannedEndOfShift1));
        Assert.That(reg2.PlannedBreakOfShift1, Is.EqualTo(reg1.PlannedBreakOfShift1));
        Assert.That(reg2.PlannedStartOfShift2, Is.EqualTo(reg1.PlannedStartOfShift2));
        Assert.That(reg2.PlannedEndOfShift2, Is.EqualTo(reg1.PlannedEndOfShift2));
        Assert.That(reg2.PlannedBreakOfShift2, Is.EqualTo(reg1.PlannedBreakOfShift2));
    }

    // ──────────────────────────────────────────────
    //  Edge cases for break values with comma
    // ──────────────────────────────────────────────

    [Test]
    public void ParsePlanText_CommaInBreak()
    {
        // PlanText uses comma as decimal separator - should be handled
        var reg = new PlanRegistration { PlanText = "8:00-16:00/0,5" };
        PlanTextHelper.ParsePlanText(reg);

        Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(30));
    }

    // ──────────────────────────────────────────────
    //  MinutesToBreakString with unmapped break value
    // ──────────────────────────────────────────────

    [Test]
    public void GeneratePlanText_UnmappedBreak_OmitsBreak()
    {
        // 7 minutes doesn't map to a known break string -> "0"
        // The generated text should include /0 which means 0 break
        var reg = new PlanRegistration
        {
            PlannedStartOfShift1 = 480,
            PlannedEndOfShift1 = 960,
            PlannedBreakOfShift1 = 7
        };
        var result = PlanTextHelper.GeneratePlanText(reg);
        // Since MinutesToBreakString(7) = "0", and breakMinutes > 0, it will be "8:00-16:00/0"
        Assert.That(result, Is.EqualTo("8:00-16:00/0"));
    }
}
