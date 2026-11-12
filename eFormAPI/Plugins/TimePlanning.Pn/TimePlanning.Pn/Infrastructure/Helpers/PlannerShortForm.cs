#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microting.TimePlanningBase.Infrastructure.Helpers;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>What a planner cell's short form asks for (#5489).</summary>
public enum PlannerShortFormKind
{
    /// <summary>Empty text: "Ryd", remove the plan.</summary>
    Clear,

    /// <summary>"Fri": a day with no plan.</summary>
    DayOff,

    /// <summary>One or more shifts; <see cref="PlannerShortFormResult.PlanText"/> is canonical.</summary>
    Plan,

    /// <summary>Text that is not wholly shifts; nothing may be saved from it.</summary>
    Invalid
}

/// <param name="Kind">What the text asks for.</param>
/// <param name="PlanText">The canonical plan text to store for <see cref="PlannerShortFormKind.Plan"/>; "" for every other kind.</param>
public readonly record struct PlannerShortFormResult(PlannerShortFormKind Kind, string PlanText);

/// <summary>
/// Reads the planner editor's short form (#5489): <c>7-15/0,5</c>,
/// <c>6-10;14-18</c>, <c>Fri</c>, or nothing. Shifts are parsed and rendered
/// by the base <see cref="PlanTextParser"/>, so the stored text is the same
/// canonical <c>7:00-15:00/0.5</c> the pattern and the sheet import write.
/// Absence is not a short form: the editor sends a message id for it.
/// </summary>
public static class PlannerShortForm
{
    private const string DayOffText = "fri";

    /// <summary>
    /// A time as base PlanTextParser reads one: "7", "7.30", "7:30", "7,5",
    /// "7½". ASCII digits only: .NET's \d also matches other scripts' digits
    /// (e.g. "٧"), which the base then fails to int.Parse.
    /// </summary>
    private const string Time = @"[0-9]{1,2}(?:[:.,][0-9]{1,2}|½)?";

    // "0:30", or a non-empty decimal/fraction: "0,5", ".5", "½", "1¾" (the lookahead forbids an empty break).
    private const string Break = @"(?:[0-9]{1,2}:[0-9]{1,2}|(?=[0-9.,½¾])[0-9]*(?:[.,][0-9]+)?[½¾]?)";

    /// <summary>
    /// One (trimmed) segment that is wholly a shift: "start-end" with an
    /// optional "/break" and nothing else. The base parser's segment regex is
    /// not anchored (it is built to skip prose in sheet cells), so "7-15 syg"
    /// would otherwise be read as 7:00-15:00 and "syg" dropped.
    /// </summary>
    private static readonly Regex WholeSegment = new(
        $@"^{Time}\s*-\s*{Time}(?:\s*/\s*{Break})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static PlannerShortFormResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new PlannerShortFormResult(PlannerShortFormKind.Clear, "");
        }

        var trimmed = text.Trim();
        if (string.Equals(trimmed, DayOffText, StringComparison.OrdinalIgnoreCase))
        {
            return new PlannerShortFormResult(PlannerShortFormKind.DayOff, "");
        }

        // Every segment must be wholly a shift: the parser would silently drop
        // the ones that are not, or prose around one, and a manager's typo
        // must not be half-saved.
        var segments = trimmed.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Length > PlanTextParser.MaxShifts)
        {
            return new PlannerShortFormResult(PlannerShortFormKind.Invalid, "");
        }

        var shifts = new List<PlanTextParser.Shift?>();
        foreach (var segment in segments)
        {
            if (!WholeSegment.IsMatch(segment) || !PlanTextParser.TryParseSegment(segment, out var shift))
            {
                return new PlannerShortFormResult(PlannerShortFormKind.Invalid, "");
            }

            shifts.Add(shift);
        }

        return new PlannerShortFormResult(PlannerShortFormKind.Plan, PlanTextParser.Generate(shifts));
    }

    /// <summary>
    /// Lenient reading of a stored PlanText, for comparing days: "" for no
    /// plan (empty or "Fri"), the canonical text for shifts, null when the
    /// text holds something that is not a shift at all. Unlike
    /// <see cref="Parse"/>, prose next to a shift is tolerated, as the sheet
    /// import tolerates it.
    /// </summary>
    public static string? Canonicalise(string? planText)
    {
        if (string.IsNullOrWhiteSpace(planText)
            || string.Equals(planText.Trim(), DayOffText, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        var canonical = PlanTextParser.Generate(PlanTextParser.Parse(planText));
        return canonical.Length == 0 ? null : canonical;
    }
}
