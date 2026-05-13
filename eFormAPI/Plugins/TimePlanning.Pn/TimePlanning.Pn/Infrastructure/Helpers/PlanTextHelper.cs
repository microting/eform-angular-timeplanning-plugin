using System;
using System.Text.RegularExpressions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Infrastructure.Helpers;

public static class PlanTextHelper
{
    /// <summary>
    /// Parses a PlanText string and populates the shift fields on the given PlanRegistration.
    /// PlanText format: "HH:MM-HH:MM/break;HH:MM-HH:MM/break;..." (up to 5 shifts separated by ';').
    /// Each shift segment is "start-end" or "start-end/break".
    /// Times can use ':' or '.' as separator. Break is a decimal fraction of an hour.
    /// Also recalculates PlanHours from the parsed shifts.
    /// </summary>
    public static void ParsePlanText(PlanRegistration planRegistration)
    {
        if (string.IsNullOrEmpty(planRegistration.PlanText))
        {
            return;
        }

        var splitList = planRegistration.PlanText.Replace(",", ".").Split(';');

        for (int i = 0; i < splitList.Length && i < 5; i++)
        {
            var segment = splitList[i];
            ParseShiftSegment(segment, out var start, out var end, out var breakMinutes);
            SetShift(planRegistration, i + 1, start, end, breakMinutes);
        }

        RecalculatePlanHours(planRegistration);
    }

    /// <summary>
    /// Generates a PlanText string from the shift start/stop/break fields on a PlanRegistration.
    /// This is the reverse of ParsePlanText. The output format uses "HH:MM-HH:MM/break" for each
    /// shift, separated by ';'. Break is expressed as a decimal fraction of an hour.
    /// </summary>
    public static string GeneratePlanText(PlanRegistration planRegistration)
    {
        var segments = new System.Collections.Generic.List<string>();

        for (int i = 1; i <= 5; i++)
        {
            GetShift(planRegistration, i, out var start, out var end, out var breakMinutes);
            if (start == 0 && end == 0)
            {
                continue;
            }

            var startStr = MinutesToTimeString(start);
            var endStr = MinutesToTimeString(end);

            if (breakMinutes > 0)
            {
                var breakStr = MinutesToBreakString(breakMinutes);
                segments.Add($"{startStr}-{endStr}/{breakStr}");
            }
            else
            {
                segments.Add($"{startStr}-{endStr}");
            }
        }

        return string.Join(";", segments);
    }

    /// <summary>
    /// Recalculates PlanHours on a PlanRegistration from its shift fields.
    /// </summary>
    public static void RecalculatePlanHours(PlanRegistration planRegistration)
    {
        var calculatedPlanHoursInMinutes = 0;

        for (int i = 1; i <= 5; i++)
        {
            GetShift(planRegistration, i, out var start, out var end, out var breakMinutes);
            if (start != 0 && end != 0)
            {
                calculatedPlanHoursInMinutes += end - start - breakMinutes;
            }
        }

        if (calculatedPlanHoursInMinutes > 0)
        {
            planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
        }
    }

    /// <summary>
    /// Parses a single shift segment like "8:00-16:00/0.5" or "8.00-16.00".
    /// </summary>
    public static void ParseShiftSegment(string segment, out int startMinutes, out int endMinutes, out int breakMinutes)
    {
        startMinutes = 0;
        endMinutes = 0;
        breakMinutes = 0;

        if (string.IsNullOrEmpty(segment))
        {
            return;
        }

        var regexWithBreak = new Regex(@"(.*)-(.*)\/(.*)");
        var match = regexWithBreak.Match(segment);

        if (match.Captures.Count == 0)
        {
            // Try without break
            var regexNoBreak = new Regex(@"(.*)-(.*)");
            match = regexNoBreak.Match(segment);

            if (match.Captures.Count == 1)
            {
                startMinutes = ParseTimeToMinutes(match.Groups[1].Value);
                endMinutes = ParseTimeToMinutes(match.Groups[2].Value);

                if (match.Groups.Count == 4)
                {
                    var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                    breakMinutes = BreakTimeCalculator(breakPart);
                }
            }

            return;
        }

        if (match.Captures.Count == 1)
        {
            startMinutes = ParseTimeToMinutes(match.Groups[1].Value);
            endMinutes = ParseTimeToMinutes(match.Groups[2].Value);

            if (match.Groups.Count == 4)
            {
                var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                breakMinutes = BreakTimeCalculator(breakPart);
            }
        }
    }

    /// <summary>
    /// Parses a time string like "8:00", "8.30", "16:00" into total minutes since midnight.
    /// </summary>
    public static int ParseTimeToMinutes(string timePart)
    {
        var parts = timePart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
        var hours = int.Parse(parts[0]);
        var minutes = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return hours * 60 + minutes;
    }

    /// <summary>
    /// Converts total minutes since midnight to "H:MM" format.
    /// </summary>
    public static string MinutesToTimeString(int totalMinutes)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return $"{hours}:{minutes:D2}";
    }

    /// <summary>
    /// Converts break minutes to a decimal string representation used in PlanText.
    /// This is the reverse of BreakTimeCalculator.
    /// </summary>
    public static string MinutesToBreakString(int breakMinutes)
    {
        return breakMinutes switch
        {
            5 => "0.1",
            10 => "0.15",
            15 => "0.25",
            20 => "0.3",
            25 => "0.4",
            30 => "0.5",
            35 => "0.6",
            40 => "0.7",
            45 => "0.75",
            50 => "0.8",
            55 => "0.9",
            60 => "1",
            _ => "0"
        };
    }

    /// <summary>
    /// Converts a decimal break string to minutes. Mirrors GoogleSheetHelper.BreakTimeCalculator.
    /// </summary>
    public static int BreakTimeCalculator(string breakPart)
    {
        return breakPart switch
        {
            "0.1" => 5,
            ".1" => 5,
            "0.15" => 10,
            ".15" => 10,
            "0.25" => 15,
            ".25" => 15,
            "0.3" => 20,
            ".3" => 20,
            "0.4" => 25,
            ".4" => 25,
            "0.5" => 30,
            ".5" => 30,
            "0.6" => 35,
            ".6" => 35,
            "0.7" => 40,
            ".7" => 40,
            "0.75" => 45,
            ".75" => 45,
            "0.8" => 50,
            ".8" => 50,
            "0.9" => 55,
            ".9" => 55,
            "¾" => 45,
            "½" => 30,
            "1" => 60,
            _ => 0
        };
    }

    private static void SetShift(PlanRegistration reg, int shiftNumber, int start, int end, int breakMinutes)
    {
        switch (shiftNumber)
        {
            case 1:
                reg.PlannedStartOfShift1 = start;
                reg.PlannedEndOfShift1 = end;
                reg.PlannedBreakOfShift1 = breakMinutes;
                break;
            case 2:
                reg.PlannedStartOfShift2 = start;
                reg.PlannedEndOfShift2 = end;
                reg.PlannedBreakOfShift2 = breakMinutes;
                break;
            case 3:
                reg.PlannedStartOfShift3 = start;
                reg.PlannedEndOfShift3 = end;
                reg.PlannedBreakOfShift3 = breakMinutes;
                break;
            case 4:
                reg.PlannedStartOfShift4 = start;
                reg.PlannedEndOfShift4 = end;
                reg.PlannedBreakOfShift4 = breakMinutes;
                break;
            case 5:
                reg.PlannedStartOfShift5 = start;
                reg.PlannedEndOfShift5 = end;
                reg.PlannedBreakOfShift5 = breakMinutes;
                break;
        }
    }

    private static void GetShift(PlanRegistration reg, int shiftNumber, out int start, out int end, out int breakMinutes)
    {
        switch (shiftNumber)
        {
            case 1:
                start = reg.PlannedStartOfShift1;
                end = reg.PlannedEndOfShift1;
                breakMinutes = reg.PlannedBreakOfShift1;
                break;
            case 2:
                start = reg.PlannedStartOfShift2;
                end = reg.PlannedEndOfShift2;
                breakMinutes = reg.PlannedBreakOfShift2;
                break;
            case 3:
                start = reg.PlannedStartOfShift3;
                end = reg.PlannedEndOfShift3;
                breakMinutes = reg.PlannedBreakOfShift3;
                break;
            case 4:
                start = reg.PlannedStartOfShift4;
                end = reg.PlannedEndOfShift4;
                breakMinutes = reg.PlannedBreakOfShift4;
                break;
            case 5:
                start = reg.PlannedStartOfShift5;
                end = reg.PlannedEndOfShift5;
                breakMinutes = reg.PlannedBreakOfShift5;
                break;
            default:
                start = 0;
                end = 0;
                breakMinutes = 0;
                break;
        }
    }
}
