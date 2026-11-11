#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Reads <c>TimePlanningBaseSettings:GoogleSheetRetired</c> (#5489). Once a
/// customer has moved their plans into PlanPatterns the sheet is retired: it
/// is neither pulled nor pushed, and the startup seed stops putting workers
/// back on it.
///
/// Read straight from the database rather than through
/// IPluginDbOptions, so a value the planner just changed is seen at once.
/// A missing row or a value that is not a recognisable "true" reads as false,
/// which is how every existing installation behaves until someone retires
/// the sheet on purpose.
/// </summary>
public static class GoogleSheetRetirement
{
    public const string SettingName = "TimePlanningBaseSettings:GoogleSheetRetired";

    public static bool IsRetired(TimePlanningPnDbContext dbContext) =>
        Parse(dbContext.PluginConfigurationValues.AsNoTracking()
            .Where(x => x.Name == SettingName)
            .Select(x => x.Value)
            .FirstOrDefault());

    public static async Task<bool> IsRetiredAsync(TimePlanningPnDbContext dbContext) =>
        Parse(await dbContext.PluginConfigurationValues.AsNoTracking()
            .Where(x => x.Name == SettingName)
            .Select(x => x.Value)
            .FirstOrDefaultAsync());

    private static bool Parse(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed == "1" || (bool.TryParse(trimmed, out var retired) && retired);
    }
}
