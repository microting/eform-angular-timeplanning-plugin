#nullable enable
namespace TimePlanning.Pn.Infrastructure.Const;

/// <summary>
/// The planner's permission (#5489). Lives in the plugin rather than beside
/// the other claims in Microting.TimePlanningBase's TimePlanningClaims, so the
/// beta does not need a base release; the value follows the same
/// <c>time_planning_*</c> scheme, which the permission seed relies on.
/// </summary>
public static class TimePlanningPlannerClaims
{
    /// <summary>Gates the "Planlægning (beta)" menu entry, its route and every planner/... endpoint.</summary>
    public const string Planner = "time_planning_planner";
}
