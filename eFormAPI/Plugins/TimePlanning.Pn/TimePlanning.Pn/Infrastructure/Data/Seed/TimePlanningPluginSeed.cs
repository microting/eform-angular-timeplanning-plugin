/*/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Infrastructure.Data.Seed;

using System;
using System.Linq;
using Data;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Extensions.Seed.CodeFirstLists;

public static class TimePlanningPluginSeed
{
    public static void SeedData(TimePlanningPnDbContext dbContext)
    {
        var seedData = new TimePlanningConfigurationSeedData();
        var configurationList = seedData.Data;
        foreach (var configurationItem in configurationList)
        {
            if (!dbContext.PluginConfigurationValues.Any(x => x.Name == configurationItem.Name))
            {
                var newConfigValue = new PluginConfigurationValue
                {
                    Name = configurationItem.Name,
                    Value = configurationItem.Value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Version = 1,
                    WorkflowState = Constants.WorkflowStates.Created,
                    CreatedByUserId = 1
                };
                dbContext.PluginConfigurationValues.Add(newConfigValue);
                dbContext.SaveChanges();
            }
        }

        // Seed plugin permissions
        var newPermissions = TimePlanningPermissionsSeedData.Data
            .Where(p => dbContext.PluginPermissions.All(x => x.ClaimName != p.ClaimName))
            .Select(p => new PluginPermission
            {
                PermissionName = p.PermissionName,
                ClaimName = p.ClaimName,
                CreatedAt = DateTime.UtcNow,
                Version = 1,
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedByUserId = 1
            });

        dbContext.PluginPermissions.AddRange(newPermissions);

        dbContext.SaveChanges();

        // loop over permissions in db and update the names if not correct
        foreach (var permission in dbContext.PluginPermissions.Where(x => x.ClaimName.StartsWith("time_planning")))
        {
            var seedPermission = TimePlanningPermissionsSeedData.Data.FirstOrDefault(x => x.ClaimName == permission.ClaimName);
            if (seedPermission != null && seedPermission.PermissionName != permission.PermissionName)
            {
                permission.PermissionName = seedPermission.PermissionName;
                permission.UpdatedAt = DateTime.UtcNow;
                permission.Version += 1;
                dbContext.PluginPermissions.Update(permission);
            }
        }

        // Match system messages by Id: the base package seeds ids 1-9 under other
        // names ("Day Off", "Time off", ...), so matching by Name would insert a
        // duplicate Id. Only rows the plugin created (same Name) are healed back to
        // the seed translations; rows that still equal their base package entry keep
        // their own texts, also for ids 2-4 whose Names coincide with the plugin's.
        var existingMessages = dbContext.Messages.ToDictionary(x => x.Id);
        var baseMessages = MessagesCodeFirst.GetList().ToDictionary(x => x.Id);
        foreach (var seedMessage in new TimePlanningSeedMessages().Data)
        {
            if (!existingMessages.TryGetValue(seedMessage.Id, out var existing))
            {
                dbContext.Messages.Add(seedMessage);
                continue;
            }

            var isUntouchedBaseRow = baseMessages.TryGetValue(existing.Id, out var baseMessage)
                && existing.Name == baseMessage.Name
                && HasSameTranslations(existing, baseMessage);
            if (isUntouchedBaseRow || existing.Name != seedMessage.Name)
            {
                continue;
            }

            // Tracked row: EF only writes it if a value actually changed.
            existing.DaName = seedMessage.DaName;
            existing.EnName = seedMessage.EnName;
            existing.DeName = seedMessage.DeName;
        }

        dbContext.SaveChanges();
    }

    private static bool HasSameTranslations(Message a, Message b) =>
        a.DaName == b.DaName && a.EnName == b.EnName && a.DeName == b.DeName;
}