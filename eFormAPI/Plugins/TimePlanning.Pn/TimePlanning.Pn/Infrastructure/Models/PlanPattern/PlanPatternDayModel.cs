#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.PlanPattern;

/// <summary>One day of a pattern as the planner sends it (#5489).</summary>
public class PlanPatternDayModel
{
    /// <summary>Week of the rotation, 0 .. RotationWeeks - 1.</summary>
    public int WeekIndex { get; set; }

    /// <summary>ISO day of week, 0 = Monday ... 6 = Sunday.</summary>
    public int DayOfWeek { get; set; }

    /// <summary>Canonical plan text, e.g. <c>7:00-15:00/0.5</c>; null or empty = no plan that day.</summary>
    public string? PlanText { get; set; }
}
