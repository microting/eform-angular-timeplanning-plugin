#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
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

    /// <summary>
    /// Sets the flag (the planner's settings and its import, #5489) and saves
    /// it. A change keeps the previous value as a version row, as the plugin
    /// settings page does through PluginDbOptions.
    /// </summary>
    public static async Task SetRetiredAsync(TimePlanningPnDbContext dbContext, bool retired, int userId)
    {
        await StageRetiredAsync(dbContext, retired, userId);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>As <see cref="SetRetiredAsync"/>, but leaves the save to the caller's transaction.</summary>
    public static async Task StageRetiredAsync(TimePlanningPnDbContext dbContext, bool retired, int userId)
    {
        var value = retired ? "true" : "false";
        var now = DateTime.UtcNow;
        var row = await dbContext.PluginConfigurationValues
            .FirstOrDefaultAsync(x => x.Name == SettingName);
        if (row == null)
        {
            dbContext.PluginConfigurationValues.Add(new PluginConfigurationValue
            {
                Name = SettingName,
                Value = value,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedByUserId = userId,
                UpdatedByUserId = userId
            });
            return;
        }

        if (row.Value == value)
        {
            return;
        }

        dbContext.PluginConfigurationValueVersions.Add(new PluginConfigurationValueVersion
        {
            Name = row.Name,
            Value = row.Value,
            Version = row.Version,
            CreatedAt = now,
            UpdatedAt = now,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        });
        row.Value = value;
        row.Version += 1;
        row.UpdatedAt = now;
        row.UpdatedByUserId = userId;
    }

    private static bool Parse(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed == "1" || (bool.TryParse(trimmed, out var retired) && retired);
    }
}
