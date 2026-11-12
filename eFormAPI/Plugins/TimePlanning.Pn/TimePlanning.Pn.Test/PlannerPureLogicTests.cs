using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489 planner: the short-form editor text and the import's rotation
/// inference. Pure functions, no database.
/// </summary>
[TestFixture]
public class PlannerPureLogicTests
{
    // ------------------------------------------------------------ short form

    [TestCase("7-15/0,5", "7:00-15:00/0.5")]
    [TestCase(" 7-15/0.5 ", "7:00-15:00/0.5")]
    [TestCase("7.30-15", "7:30-15:00")]
    [TestCase("6-10;14-18/0,25", "6:00-10:00;14:00-18:00/0.25")]
    [TestCase("8:00-16:00/0.5", "8:00-16:00/0.5")]
    [TestCase("7½-15", "7:30-15:00")]
    [TestCase("7,5-15/½", "7:30-15:00/0.5")]
    [TestCase("6-14/0:45", "6:00-14:00/0.75")]
    [TestCase("6 - 14 / .5", "6:00-14:00/0.5")]
    public void ShortForm_TimeText_IsNormalisedToCanonicalPlanText(string input, string expected)
    {
        var result = PlannerShortForm.Parse(input);

        Assert.That(result.Kind, Is.EqualTo(PlannerShortFormKind.Plan));
        Assert.That(result.PlanText, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ShortForm_Empty_IsClear(string? input)
    {
        var result = PlannerShortForm.Parse(input);

        Assert.That(result.Kind, Is.EqualTo(PlannerShortFormKind.Clear));
        Assert.That(result.PlanText, Is.EqualTo(""));
    }

    [TestCase("Fri")]
    [TestCase("fri")]
    [TestCase(" FRI ")]
    public void ShortForm_Fri_IsADayWithNoPlan(string input)
    {
        var result = PlannerShortForm.Parse(input);

        Assert.That(result.Kind, Is.EqualTo(PlannerShortFormKind.DayOff));
        Assert.That(result.PlanText, Is.EqualTo(""));
    }

    // A typo must not be half-saved: "7-15;x" would otherwise silently drop "x".
    [TestCase("hello")]
    [TestCase("7-15;x")]
    [TestCase("8")]
    [TestCase("7.7-15")]
    [TestCase("7-15 syg")]
    [TestCase("syg 7-15")]
    [TestCase("x7-15")]
    [TestCase("7-15x")]
    [TestCase("7-15/")]
    [TestCase("7-15/0,5 syg")]
    [TestCase("7-15/,")]
    [TestCase("٧-١٥")]
    public void ShortForm_TextThatIsNotWhollyAShift_IsInvalid(string input)
    {
        Assert.That(PlannerShortForm.Parse(input).Kind, Is.EqualTo(PlannerShortFormKind.Invalid));
    }

    // -------------------------------------------------------- rotation inference

    private static readonly DateTime WindowStart = new(2026, 7, 6); // a Monday

    private static string TextFor(int week, int dow) => $"{6 + (week % 8)}:00-{14 + dow % 3}:00";

    /// <summary>Rows for <paramref name="weeks"/> weeks that repeat every <paramref name="period"/> weeks.</summary>
    private static List<RotationInference.Day> Repeating(int period, int weeks, Func<int, int, string>? text = null)
    {
        text ??= TextFor;
        var rows = new List<RotationInference.Day>();
        for (var week = 0; week < weeks; week++)
        {
            for (var dow = 0; dow < 7; dow++)
            {
                rows.Add(new RotationInference.Day(WindowStart.AddDays(week * 7 + dow), text(week % period, dow), false));
            }
        }

        return rows;
    }

    [Test]
    public void Inference_FindsTheSmallestPeriod_ForEveryPeriodOneToEight()
    {
        for (var period = 1; period <= 8; period++)
        {
            var result = RotationInference.Infer(Repeating(period, 20), WindowStart);

            Assert.That(result.Recognised, Is.True, $"period {period}: {result.Reason}");
            Assert.That(result.RotationWeeks, Is.EqualTo(period), $"period {period}");
            Assert.That(result.AnchorDate, Is.EqualTo(WindowStart));
            Assert.That(result.Days, Has.Count.EqualTo(period * 7));
            for (var week = 0; week < period; week++)
            {
                for (var dow = 0; dow < 7; dow++)
                {
                    Assert.That(result.Days.Single(d => d.WeekIndex == week && d.DayOfWeek == dow).PlanText,
                        Is.EqualTo(TextFor(week, dow)), $"period {period} week {week} dow {dow}");
                }
            }
        }
    }

    [Test]
    public void Inference_TextsAreCanonicalised_SoSheetSpellingsMatch()
    {
        var rows = Repeating(1, 4, (_, _) => "8:00-16:00/0.5");
        rows[7] = rows[7] with { PlanText = "8-16/0,5" };

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.True, result.Reason);
        Assert.That(result.Days.All(d => d.PlanText == "8:00-16:00/0.5"), Is.True);
    }

    [Test]
    public void Inference_InconsistentHistory_IsNotRecognised()
    {
        var rows = Repeating(1, 20);
        rows[5 * 7 + 2] = rows[5 * 7 + 2] with { PlanText = "10:00-11:00" };
        rows[13 * 7 + 4] = rows[13 * 7 + 4] with { PlanText = "10:00-12:00" };

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.False);
        Assert.That(result.Reason, Is.EqualTo(RotationInference.ReasonInconsistent));
    }

    [Test]
    public void Inference_MissingRowsAndAbsenceDays_AreUnknownNotMismatches()
    {
        var rows = Repeating(2, 12)
            .Where(r => r.Date != WindowStart.AddDays(3 * 7 + 1)) // a missing row
            .Select(r => r.Date == WindowStart.AddDays(4 * 7 + 2)
                ? r with { PlanText = "", IsAbsence = true } // a vacation day
                : r)
            .ToList();

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.True, result.Reason);
        Assert.That(result.RotationWeeks, Is.EqualTo(2));
    }

    [Test]
    public void Inference_UnparsableText_IsNotRecognised()
    {
        var rows = Repeating(1, 6);
        rows[9] = rows[9] with { PlanText = "ring til mig" };

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.False);
        Assert.That(result.Reason, Is.EqualTo(RotationInference.ReasonUnparsable));
    }

    // An hours-only schedule (plan hours, no shift text) must never be read
    // as days off: applying that pattern would zero the worker's hours.
    [Test]
    public void Inference_HoursWithoutShifts_IsNotRecognised()
    {
        var rows = Repeating(1, 6, (_, _) => "").Select(r => r with { PlanHours = 7.5 }).ToList();

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.False);
        Assert.That(result.Reason, Is.EqualTo(RotationInference.ReasonHoursOnly));
        Assert.That(result.Days, Is.Empty);
    }

    [Test]
    public void Inference_OneHoursOnlyDay_AmongShifts_IsNotRecognised()
    {
        var rows = Repeating(1, 6, (_, _) => "7:00-15:00").Select(r => r with { PlanHours = 8 }).ToList();
        rows[10] = rows[10] with { PlanText = "" };

        Assert.That(RotationInference.Infer(rows, WindowStart).Reason, Is.EqualTo(RotationInference.ReasonHoursOnly));
    }

    [Test]
    public void Inference_EmptyAndFri_BothMeanNoPlan()
    {
        var rows = Repeating(1, 4, (_, dow) => dow >= 5 ? "" : "7:00-15:00");
        rows[12] = rows[12] with { PlanText = "Fri" }; // a Saturday

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.True, result.Reason);
        Assert.That(result.Days.Single(d => d.WeekIndex == 0 && d.DayOfWeek == 5).PlanText, Is.EqualTo(""));
    }

    // Every day of the rotation must have been seen at least twice: a single
    // sighting proves nothing about repetition.
    [Test]
    public void Inference_TooLittleHistory_IsNotRecognised()
    {
        var result = RotationInference.Infer(Repeating(1, 1), WindowStart);

        Assert.That(result.Recognised, Is.False);
        Assert.That(result.Reason, Is.EqualTo(RotationInference.ReasonInsufficientHistory));
    }

    [Test]
    public void Inference_NoRows_IsNotRecognised()
    {
        var result = RotationInference.Infer([], WindowStart);

        Assert.That(result.Recognised, Is.False);
        Assert.That(result.Reason, Is.EqualTo(RotationInference.ReasonInsufficientHistory));
    }

    [Test]
    public void Inference_RowsBeforeTheWindow_AreIgnored()
    {
        var rows = Repeating(1, 6);
        rows.Add(new RotationInference.Day(WindowStart.AddDays(-3), "1:00-2:00", false));

        var result = RotationInference.Infer(rows, WindowStart);

        Assert.That(result.Recognised, Is.True, result.Reason);
        Assert.That(result.RotationWeeks, Is.EqualTo(1));
    }
}
