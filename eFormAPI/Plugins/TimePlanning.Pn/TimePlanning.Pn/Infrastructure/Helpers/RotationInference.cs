#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.PlanPattern;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Infers a worker's rotation from the plan their rows already carry, for the
/// one-off import that retires the Google Sheet (#5489). The rows mirror the
/// sheet, so no Google API call is needed.
///
/// The rule:
/// <list type="bullet">
/// <item>Only days on or after <c>windowStart</c> (a Monday) are read; week k
/// of the window is the k-th Monday-to-Sunday week from it.</item>
/// <item>A day is <b>unknown</b> when it has no row or carries an absence
/// (vacation, sickness, ... is not part of the rotation). Unknown days are
/// never a mismatch; they are skipped.</item>
/// <item>A known day's text is canonicalised (<see cref="PlannerShortForm.Canonicalise"/>),
/// so <c>8-16/0,5</c> equals <c>8:00-16:00/0.5</c> and empty equals "Fri".
/// Text that holds no shift at all makes the worker unparsable; a day
/// with plan hours but no shift text (an hours-only schedule) makes the
/// worker hoursOnly, since a pattern cannot carry hours without shifts.</item>
/// <item>P (1..8) fits when, for every rotation day (k mod P, weekday), all
/// known days agree, and each rotation day was seen at least twice, so the
/// repetition was actually observed. The smallest P that fits wins.</item>
/// <item>The anchor is <c>windowStart</c>, so week index 0 is the window's
/// first week.</item>
/// </list>
/// </summary>
public static class RotationInference
{
    public const string ReasonInconsistent = "inconsistent";
    public const string ReasonUnparsable = "unparsable";
    public const string ReasonInsufficientHistory = "insufficientHistory";
    public const string ReasonHoursOnly = "hoursOnly";

    /// <summary>Weeks of history read before the current week.</summary>
    public const int HistoryWeeks = 16;

    /// <summary>A rotation day must be seen this many times to count as repeating.</summary>
    private const int MinimumSightings = 2;

    /// <param name="PlanHours">The row's plan hours; hours with no shift text mean an hours-only schedule.</param>
    public readonly record struct Day(DateTime Date, string? PlanText, bool IsAbsence, double PlanHours = 0);

    public sealed record Result(bool Recognised, string? Reason, int RotationWeeks, DateTime AnchorDate,
        List<PlanPatternDayModel> Days);

    public static Result Infer(IEnumerable<Day> days, DateTime windowStart)
    {
        windowStart = windowStart.Date;
        var known = new List<(int Week, int DayOfWeek, string Text)>();
        foreach (var day in days)
        {
            if (day.Date.Date < windowStart || day.IsAbsence)
            {
                continue;
            }

            var text = PlannerShortForm.Canonicalise(day.PlanText);
            if (text == null)
            {
                return NotRecognised(ReasonUnparsable, windowStart);
            }

            // Plan hours with no shift: a pattern holds shifts only, so it
            // would read the day as off and zero the hours. Not a rotation.
            if (text.Length == 0 && day.PlanHours > 0)
            {
                return NotRecognised(ReasonHoursOnly, windowStart);
            }

            var offset = (day.Date.Date - windowStart).Days;
            known.Add((offset / 7, offset % 7, text));
        }

        var anyFitWithoutEvidence = false;
        for (var period = PlanPatternResolver.MinRotationWeeks; period <= PlanPatternResolver.MaxRotationWeeks; period++)
        {
            var cells = known
                .GroupBy(d => (WeekIndex: d.Week % period, d.DayOfWeek))
                .ToDictionary(g => g.Key, g => g.Select(d => d.Text).ToList());
            if (cells.Values.Any(texts => texts.Distinct().Count() > 1))
            {
                continue;
            }

            var complete = cells.Count == period * 7 && cells.Values.All(t => t.Count >= MinimumSightings);
            if (!complete)
            {
                anyFitWithoutEvidence = true;
                continue;
            }

            var patternDays = cells
                .OrderBy(c => c.Key.WeekIndex).ThenBy(c => c.Key.DayOfWeek)
                .Select(c => new PlanPatternDayModel
                {
                    WeekIndex = c.Key.WeekIndex,
                    DayOfWeek = c.Key.DayOfWeek,
                    PlanText = c.Value[0]
                })
                .ToList();
            return new Result(true, null, period, windowStart, patternDays);
        }

        return NotRecognised(
            known.Count == 0 || anyFitWithoutEvidence ? ReasonInsufficientHistory : ReasonInconsistent,
            windowStart);
    }

    private static Result NotRecognised(string reason, DateTime windowStart) =>
        new(false, reason, 0, windowStart, []);
}
