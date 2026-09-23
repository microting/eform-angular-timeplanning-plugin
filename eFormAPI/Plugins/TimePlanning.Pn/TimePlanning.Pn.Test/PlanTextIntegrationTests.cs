using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using NUnit.Framework;
using TimePlanning.Pn.Services.ContentHandoverService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// The plugin's side of the shared PlanText parser.
///
/// <see cref="PlanTextParser"/> and <see cref="PlanRegistrationPlanText"/> are
/// exhaustively tested in Microting.TimePlanningBase, so this fixture does not
/// re-test the grammar. It covers the assumptions this plugin's call sites make
/// about them:
///
///   * GoogleSheetHelper and PlanRegistrationHelper assign PlanHours from the
///     sheet's hours column and then parse. Parsing must not overwrite that
///     figure for a row whose text is not a shift, because PlanHours feeds
///     SumFlexEnd and a zero there drifts every following day.
///   * TimePlanningPlanningService regenerates PlanText from the columns, so
///     what Generate writes has to read back as the same shift.
///   * ContentHandoverService's PlanText surgery has to round-trip a break
///     above one hour, which the plugin's own table used to map to zero.
///
/// No database is needed; the handover helpers are internal to the plugin and
/// visible here through InternalsVisibleTo.
/// </summary>
[TestFixture]
public class PlanTextIntegrationTests
{
    /// <summary>Parses <paramref name="planText"/> into a fresh registration.</summary>
    private static PlanRegistration Parsed(string planText, double planHours = 0)
    {
        var registration = new PlanRegistration { PlanText = planText, PlanHours = planHours };
        PlanRegistrationPlanText.ParseInto(registration);
        return registration;
    }

    // ──────────────────────────────────────────────
    //  What the sheet call sites rely on
    // ──────────────────────────────────────────────

    /// <summary>
    /// GoogleSheetHelper assigns PlanHours from the hours column immediately
    /// before parsing (see its parsedPlanHours assignment), and
    /// PlanRegistrationHelper re-parses on every period load for a sheet site.
    /// A cell holding an absence marker, a note, or a bare number of hours must
    /// therefore come through with the hours column intact.
    /// </summary>
    [TestCase("Ferie")]
    [TestCase("Helligdag")]
    [TestCase("Fri")]
    [TestCase("8")]
    [TestCase("7,4")]
    [TestCase("0-0")]
    [TestCase("")]
    [TestCase(null)]
    public void ParseInto_TextThatIsNotAShift_LeavesTheHoursColumnAlone(string planText)
    {
        var reg = Parsed(planText, planHours: 7.4);

        Assert.Multiple(() =>
        {
            Assert.That(reg.PlannedStartOfShift1, Is.Zero, "the shift columns are still cleared");
            Assert.That(reg.PlanHours, Is.EqualTo(7.4), "the hours column is the authority for this row");
        });
    }

    [Test]
    public void ParseInto_ShiftText_OverridesTheHoursColumn()
    {
        var reg = Parsed("8:00-16:00/0.5", planHours: 99);

        Assert.Multiple(() =>
        {
            Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(8 * 60));
            Assert.That(reg.PlannedEndOfShift1, Is.EqualTo(16 * 60));
            Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(30));
            Assert.That(reg.PlanHours, Is.EqualTo(7.5));
        });
    }

    /// <summary>
    /// The plugin's own break table stopped at "1" and mapped anything longer
    /// to zero, so an hour and a half of break was paid as worked time.
    /// </summary>
    [Test]
    public void ParseInto_BreakAboveOneHour_IsNotDropped()
    {
        var reg = Parsed("8:00-16:00/1.5");

        Assert.Multiple(() =>
        {
            Assert.That(reg.PlannedBreakOfShift1, Is.EqualTo(90));
            Assert.That(reg.PlanHours, Is.EqualTo(6.5));
        });
    }

    [Test]
    public void ParseInto_FillsAllFiveSlots()
    {
        var reg = Parsed("6:00-8:00/0.5;9:00-11:00/0.5;12:00-14:00/1;15:00-17:00/1.5;18:00-20:00/0.25");

        Assert.Multiple(() =>
        {
            Assert.That(reg.PlannedStartOfShift3, Is.EqualTo(12 * 60));
            Assert.That(reg.PlannedBreakOfShift4, Is.EqualTo(90));
            Assert.That(reg.PlannedStartOfShift5, Is.EqualTo(18 * 60));
            Assert.That(reg.PlannedBreakOfShift5, Is.EqualTo(15));
        });
    }

    [Test]
    public void ParseInto_ClearsSlotsTheTextNoLongerMentions()
    {
        var reg = Parsed("6:00-8:00;9:00-11:00;12:00-14:00;15:00-17:00;18:00-20:00");
        Assert.That(reg.PlannedStartOfShift5, Is.EqualTo(18 * 60), "precondition");

        reg.PlanText = "6:00-8:00";
        PlanRegistrationPlanText.ParseInto(reg);

        Assert.Multiple(() =>
        {
            Assert.That(reg.PlannedStartOfShift1, Is.EqualTo(6 * 60));
            Assert.That(reg.PlannedStartOfShift2, Is.Zero);
            Assert.That(reg.PlannedStartOfShift5, Is.Zero);
        });
    }

    // ──────────────────────────────────────────────
    //  What TimePlanningPlanningService relies on
    // ──────────────────────────────────────────────

    /// <summary>
    /// An admin edit regenerates PlanText from the columns. The old generator
    /// wrote "/0" for any break above an hour, so a 90-minute break came back
    /// as no break at all on the next parse.
    /// </summary>
    [Test]
    public void Generate_ThenParseInto_RoundTripsEveryShift()
    {
        var edited = Parsed("8:00-12:00/0.25;13:00-18:00/1.5");

        var reparsed = Parsed(PlanRegistrationPlanText.Generate(edited));

        Assert.Multiple(() =>
        {
            Assert.That(reparsed.PlannedStartOfShift2, Is.EqualTo(edited.PlannedStartOfShift2));
            Assert.That(reparsed.PlannedEndOfShift2, Is.EqualTo(edited.PlannedEndOfShift2));
            Assert.That(reparsed.PlannedBreakOfShift2, Is.EqualTo(90));
            Assert.That(reparsed.PlanHours, Is.EqualTo(edited.PlanHours));
        });
    }

    // ──────────────────────────────────────────────
    //  ContentHandoverService PlanText surgery
    // ──────────────────────────────────────────────

    [Test]
    public void Handover_LiftsSegmentFromSender_AndInsertsItBeforeTheFirstLaterSegment()
    {
        var (senderText, lifted) = ContentHandoverService.TryRemoveSegmentByStartEnd(
            "8:00-12:00/0.5;13:00-18:00/1.5", 13 * 60, 18 * 60);

        var receiverText = ContentHandoverService.InsertSegmentSorted("19:00-21:00;6:00-7:00", lifted!);

        Assert.Multiple(() =>
        {
            Assert.That(senderText, Is.EqualTo("8:00-12:00/0.5"));
            Assert.That(lifted, Is.EqualTo("13:00-18:00/1.5"), "the segment is lifted verbatim");
            // Insertion is positional, before the first segment that starts later.
            Assert.That(receiverText, Is.EqualTo("13:00-18:00/1.5;19:00-21:00;6:00-7:00"));
        });

        var receiver = Parsed(receiverText);
        Assert.That(receiver.PlannedBreakOfShift1, Is.EqualTo(90), "the 90-minute break survives the move");
    }

    [Test]
    public void Handover_NoMatchingSegment_LeavesPlanTextUntouched()
    {
        var (planText, removed) =
            ContentHandoverService.TryRemoveSegmentByStartEnd("8:00-12:00/0.5", 13 * 60, 18 * 60);

        Assert.Multiple(() =>
        {
            Assert.That(planText, Is.EqualTo("8:00-12:00/0.5"));
            Assert.That(removed, Is.Null);
        });
    }

    /// <summary>
    /// When the sender has no matching segment to lift verbatim, the segment is
    /// formatted from the shift columns instead. Whatever that emits has to
    /// parse back to the same three numbers, which the old canonical-hours
    /// table managed for neither a break over an hour nor one off the
    /// five-minute grid.
    /// </summary>
    [TestCase(90)]
    [TestCase(45)]
    [TestCase(5)]
    [TestCase(0)]
    public void Handover_FallbackSegment_RoundTripsBreak(int breakMinutes)
    {
        var segment = ContentHandoverService.FormatShiftSegmentForFallback(8 * 60, 16 * 60, breakMinutes);

        Assert.That(PlanTextParser.TryParseSegment(segment, out var shift), Is.True, segment);
        Assert.Multiple(() =>
        {
            Assert.That(shift.StartMinutes, Is.EqualTo(8 * 60));
            Assert.That(shift.EndMinutes, Is.EqualTo(16 * 60));
            Assert.That(shift.BreakMinutes, Is.EqualTo(breakMinutes));
        });
    }

    /// <summary>
    /// Handover text and grid text are produced by different code paths and
    /// used to disagree on the shape of a time. They must not.
    /// </summary>
    [Test]
    public void Handover_FallbackSegment_MatchesWhatGenerateWouldWrite()
    {
        var fallback = ContentHandoverService.FormatShiftSegmentForFallback(8 * 60, 16 * 60, 90);

        Assert.That(fallback, Is.EqualTo("8:00-16:00/1.5"));
    }
}
