/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayRuleSetService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.PayDayTypeRule;
using Infrastructure.Models.PayRuleSet;
using Infrastructure.Models.PayTimeBandRule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

public class PayRuleSetService : IPayRuleSetService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PayRuleSetService> _logger;

    public PayRuleSetService(
        TimePlanningPnDbContext dbContext,
        ILogger<PayRuleSetService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OperationDataResult<PayRuleSetsListModel>> Index(PayRuleSetsRequestModel requestModel)
    {
        try
        {
            var query = _dbContext.PayRuleSets
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            var total = await query.CountAsync();

            var payRuleSets = await query
                .Skip(requestModel.Offset)
                .Take(requestModel.PageSize)
                .Select(prs => new PayRuleSetSimpleModel
                {
                    Id = prs.Id,
                    Name = prs.Name
                })
                .ToListAsync();

            return new OperationDataResult<PayRuleSetsListModel>(
                true,
                new PayRuleSetsListModel
                {
                    Total = total,
                    PayRuleSets = payRuleSets
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay rule sets");
            return new OperationDataResult<PayRuleSetsListModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationDataResult<PayRuleSetModel>> Read(int id)
    {
        try
        {
            var payRuleSet = await _dbContext.PayRuleSets
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(prs => prs.Id == id);

            if (payRuleSet == null)
            {
                return new OperationDataResult<PayRuleSetModel>(
                    false,
                    "Pay rule set not found");
            }

            // Load PayDayRules separately
            var payDayRules = await _dbContext.PayDayRules
                .Where(r => r.PayRuleSetId == id && r.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            // Load PayTierRules for each PayDayRule
            var payDayRuleIds = payDayRules.Select(pdr => pdr.Id).ToList();
            var payTierRules = await _dbContext.PayTierRules
                .Where(ptr => payDayRuleIds.Contains(ptr.PayDayRuleId) && ptr.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var model = new PayRuleSetModel
            {
                Id = payRuleSet.Id,
                Name = payRuleSet.Name,
                PayDayRules = payDayRules.Select(pdr => new PayDayRuleModel
                {
                    Id = pdr.Id,
                    PayRuleSetId = pdr.PayRuleSetId,
                    DayCode = pdr.DayCode,
                    PayTierRules = payTierRules
                        .Where(ptr => ptr.PayDayRuleId == pdr.Id)
                        .Select(ptr => new Infrastructure.Models.PayTierRule.PayTierRuleModel
                        {
                            Id = ptr.Id,
                            PayDayRuleId = ptr.PayDayRuleId,
                            Order = ptr.Order,
                            UpToSeconds = ptr.UpToSeconds,
                            PayCode = ptr.PayCode
                        })
                        .ToList()
                }).ToList()
            };

            // Load PayDayTypeRules
            var payDayTypeRules = await _dbContext.PayDayTypeRules
                .Where(r => r.PayRuleSetId == id && r.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            // Load TimeBandRules for each PayDayTypeRule
            var payDayTypeRuleIds = payDayTypeRules.Select(pdtr => pdtr.Id).ToList();
            var payTimeBandRules = await _dbContext.PayTimeBandRules
                .Where(ptbr => payDayTypeRuleIds.Contains(ptbr.PayDayTypeRuleId) && ptbr.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            model.PayDayTypeRules = payDayTypeRules.Select(pdtr => new PayDayTypeRuleModel
            {
                Id = pdtr.Id,
                PayRuleSetId = pdtr.PayRuleSetId,
                DayType = pdtr.DayType.ToString(),
                DefaultPayCode = pdtr.DefaultPayCode,
                Priority = pdtr.Priority,
                TimeBandRules = payTimeBandRules
                    .Where(ptbr => ptbr.PayDayTypeRuleId == pdtr.Id)
                    .Select(ptbr => new PayTimeBandRuleModel
                    {
                        Id = ptbr.Id,
                        PayDayTypeRuleId = ptbr.PayDayTypeRuleId,
                        StartSecondOfDay = ptbr.StartSecondOfDay,
                        EndSecondOfDay = ptbr.EndSecondOfDay,
                        PayCode = ptbr.PayCode,
                        Priority = ptbr.Priority
                    })
                    .ToList()
            }).ToList();

            return new OperationDataResult<PayRuleSetModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading pay rule set {id}");
            return new OperationDataResult<PayRuleSetModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Create(PayRuleSetCreateModel model)
    {
        try
        {
            var payRuleSet = new PayRuleSet
            {
                Name = model.Name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };

            await payRuleSet.Create(_dbContext);

            // Create PayDayRules and their PayTierRules
            if (model.PayDayRules != null && model.PayDayRules.Any())
            {
                foreach (var dayRuleModel in model.PayDayRules)
                {
                    var payDayRule = new PayDayRule
                    {
                        PayRuleSetId = payRuleSet.Id,
                        DayCode = dayRuleModel.DayCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        WorkflowState = Constants.WorkflowStates.Created
                    };

                    await payDayRule.Create(_dbContext);

                    // Create PayTierRules for this PayDayRule
                    if (dayRuleModel.PayTierRules != null && dayRuleModel.PayTierRules.Any())
                    {
                        foreach (var tierRuleModel in dayRuleModel.PayTierRules)
                        {
                            var payTierRule = new PayTierRule
                            {
                                PayDayRuleId = payDayRule.Id,
                                Order = tierRuleModel.Order,
                                UpToSeconds = tierRuleModel.UpToSeconds,
                                PayCode = tierRuleModel.PayCode,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                WorkflowState = Constants.WorkflowStates.Created
                            };

                            await payTierRule.Create(_dbContext);
                        }
                    }
                }
            }

            // Create PayDayTypeRules and their TimeBandRules
            if (model.PayDayTypeRules != null && model.PayDayTypeRules.Any())
            {
                foreach (var dayTypeRuleModel in model.PayDayTypeRules)
                {
                    var payDayTypeRule = new PayDayTypeRule
                    {
                        PayRuleSetId = payRuleSet.Id,
                        DayType = Enum.Parse<DayType>(dayTypeRuleModel.DayType),
                        DefaultPayCode = dayTypeRuleModel.DefaultPayCode,
                        Priority = dayTypeRuleModel.Priority,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        WorkflowState = Constants.WorkflowStates.Created
                    };

                    await payDayTypeRule.Create(_dbContext);

                    // Create TimeBandRules for this PayDayTypeRule
                    if (dayTypeRuleModel.TimeBandRules != null && dayTypeRuleModel.TimeBandRules.Any())
                    {
                        foreach (var timeBandRuleModel in dayTypeRuleModel.TimeBandRules)
                        {
                            var payTimeBandRule = new PayTimeBandRule
                            {
                                PayDayTypeRuleId = payDayTypeRule.Id,
                                StartSecondOfDay = timeBandRuleModel.StartSecondOfDay,
                                EndSecondOfDay = timeBandRuleModel.EndSecondOfDay,
                                PayCode = timeBandRuleModel.PayCode,
                                Priority = timeBandRuleModel.Priority,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                WorkflowState = Constants.WorkflowStates.Created
                            };

                            await payTimeBandRule.Create(_dbContext);
                        }
                    }
                }
            }

            return new OperationResult(true, "Pay rule set created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay rule set");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Update(int id, PayRuleSetUpdateModel model)
    {
        try
        {
            var payRuleSet = await _dbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Id == id && prs.WorkflowState != Constants.WorkflowStates.Removed);

            if (payRuleSet == null)
            {
                return new OperationResult(false, "Pay rule set not found");
            }

            payRuleSet.Name = model.Name;
            payRuleSet.UpdatedAt = DateTime.UtcNow;

            await payRuleSet.Update(_dbContext);

            // Get existing PayDayRules
            var existingPayDayRules = await _dbContext.PayDayRules
                .Where(pdr => pdr.PayRuleSetId == id && pdr.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var existingPayDayRuleIds = existingPayDayRules.Select(pdr => pdr.Id).ToList();

            // Get existing PayTierRules for these PayDayRules
            var existingPayTierRules = await _dbContext.PayTierRules
                .Where(ptr => existingPayDayRuleIds.Contains(ptr.PayDayRuleId) && ptr.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            // Process PayDayRules from model
            var modelPayDayRuleIds = model.PayDayRules?
                .Where(pdr => pdr.Id.HasValue && pdr.Id.Value > 0)
                .Select(pdr => pdr.Id.Value)
                .ToList() ?? new List<int>();

            // Delete PayDayRules that are not in the model
            foreach (var existingRule in existingPayDayRules.Where(er => !modelPayDayRuleIds.Contains(er.Id)))
            {
                // Delete associated PayTierRules first
                var tierRulesToDelete = existingPayTierRules.Where(ptr => ptr.PayDayRuleId == existingRule.Id);
                foreach (var tierRule in tierRulesToDelete)
                {
                    await tierRule.Delete(_dbContext);
                }

                await existingRule.Delete(_dbContext);
            }

            // Update or Create PayDayRules
            if (model.PayDayRules != null && model.PayDayRules.Any())
            {
                foreach (var dayRuleModel in model.PayDayRules)
                {
                    PayDayRule payDayRule;

                    if (dayRuleModel.Id.HasValue && dayRuleModel.Id.Value > 0)
                    {
                        // Update existing PayDayRule
                        payDayRule = existingPayDayRules.FirstOrDefault(pdr => pdr.Id == dayRuleModel.Id.Value);
                        if (payDayRule != null)
                        {
                            payDayRule.DayCode = dayRuleModel.DayCode;
                            payDayRule.UpdatedAt = DateTime.UtcNow;
                            await payDayRule.Update(_dbContext);
                        }
                        else
                        {
                            // PayDayRule with this ID not found, skip
                            continue;
                        }
                    }
                    else
                    {
                        // Create new PayDayRule
                        payDayRule = new PayDayRule
                        {
                            PayRuleSetId = payRuleSet.Id,
                            DayCode = dayRuleModel.DayCode,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            WorkflowState = Constants.WorkflowStates.Created
                        };
                        await payDayRule.Create(_dbContext);
                    }

                    // Now handle PayTierRules for this PayDayRule
                    var existingTierRules = existingPayTierRules
                        .Where(ptr => ptr.PayDayRuleId == payDayRule.Id)
                        .ToList();

                    var modelTierRuleIds = dayRuleModel.PayTierRules?
                        .Where(ptr => ptr.Id.HasValue && ptr.Id.Value > 0)
                        .Select(ptr => ptr.Id.Value)
                        .ToList() ?? new List<int>();

                    // Delete PayTierRules not in the model
                    foreach (var existingTierRule in existingTierRules.Where(etr => !modelTierRuleIds.Contains(etr.Id)))
                    {
                        await existingTierRule.Delete(_dbContext);
                    }

                    // Update or Create PayTierRules
                    if (dayRuleModel.PayTierRules != null && dayRuleModel.PayTierRules.Any())
                    {
                        foreach (var tierRuleModel in dayRuleModel.PayTierRules)
                        {
                            if (tierRuleModel.Id.HasValue && tierRuleModel.Id.Value > 0)
                            {
                                // Update existing PayTierRule
                                var existingTierRule = existingTierRules.FirstOrDefault(ptr => ptr.Id == tierRuleModel.Id.Value);
                                if (existingTierRule != null)
                                {
                                    existingTierRule.Order = tierRuleModel.Order;
                                    existingTierRule.UpToSeconds = tierRuleModel.UpToSeconds;
                                    existingTierRule.PayCode = tierRuleModel.PayCode;
                                    existingTierRule.UpdatedAt = DateTime.UtcNow;
                                    await existingTierRule.Update(_dbContext);
                                }
                            }
                            else
                            {
                                // Create new PayTierRule
                                var payTierRule = new PayTierRule
                                {
                                    PayDayRuleId = payDayRule.Id,
                                    Order = tierRuleModel.Order,
                                    UpToSeconds = tierRuleModel.UpToSeconds,
                                    PayCode = tierRuleModel.PayCode,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow,
                                    WorkflowState = Constants.WorkflowStates.Created
                                };
                                await payTierRule.Create(_dbContext);
                            }
                        }
                    }
                }
            }

            // Handle PayDayTypeRules
            var existingPayDayTypeRules = await _dbContext.PayDayTypeRules
                .Where(pdtr => pdtr.PayRuleSetId == id && pdtr.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var existingPayDayTypeRuleIds = existingPayDayTypeRules.Select(pdtr => pdtr.Id).ToList();

            // Get existing TimeBandRules for these PayDayTypeRules
            var existingTimeBandRules = await _dbContext.PayTimeBandRules
                .Where(ptbr => existingPayDayTypeRuleIds.Contains(ptbr.PayDayTypeRuleId) && ptbr.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var modelDayTypeRuleIds = model.PayDayTypeRules?
                .Where(pdtr => pdtr.Id.HasValue && pdtr.Id.Value > 0)
                .Select(pdtr => pdtr.Id.Value)
                .ToList() ?? new List<int>();

            // Delete PayDayTypeRules that are not in the model
            foreach (var existingDayTypeRule in existingPayDayTypeRules.Where(er => !modelDayTypeRuleIds.Contains(er.Id)))
            {
                // Delete associated TimeBandRules first
                var bandRulesToDelete = existingTimeBandRules.Where(ptbr => ptbr.PayDayTypeRuleId == existingDayTypeRule.Id);
                foreach (var bandRule in bandRulesToDelete)
                {
                    await bandRule.Delete(_dbContext);
                }

                await existingDayTypeRule.Delete(_dbContext);
            }

            // Update or Create PayDayTypeRules
            if (model.PayDayTypeRules != null && model.PayDayTypeRules.Any())
            {
                foreach (var dayTypeRuleModel in model.PayDayTypeRules)
                {
                    PayDayTypeRule payDayTypeRule;

                    if (dayTypeRuleModel.Id.HasValue && dayTypeRuleModel.Id.Value > 0)
                    {
                        // Update existing PayDayTypeRule
                        payDayTypeRule = existingPayDayTypeRules.FirstOrDefault(pdtr => pdtr.Id == dayTypeRuleModel.Id.Value);
                        if (payDayTypeRule != null)
                        {
                            payDayTypeRule.DayType = Enum.Parse<DayType>(dayTypeRuleModel.DayType);
                            payDayTypeRule.DefaultPayCode = dayTypeRuleModel.DefaultPayCode;
                            payDayTypeRule.Priority = dayTypeRuleModel.Priority;
                            payDayTypeRule.UpdatedAt = DateTime.UtcNow;
                            await payDayTypeRule.Update(_dbContext);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // Create new PayDayTypeRule
                        payDayTypeRule = new PayDayTypeRule
                        {
                            PayRuleSetId = payRuleSet.Id,
                            DayType = Enum.Parse<DayType>(dayTypeRuleModel.DayType),
                            DefaultPayCode = dayTypeRuleModel.DefaultPayCode,
                            Priority = dayTypeRuleModel.Priority,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            WorkflowState = Constants.WorkflowStates.Created
                        };
                        await payDayTypeRule.Create(_dbContext);
                    }

                    // Now handle TimeBandRules for this PayDayTypeRule
                    var existingBandRules = existingTimeBandRules
                        .Where(ptbr => ptbr.PayDayTypeRuleId == payDayTypeRule.Id)
                        .ToList();

                    var modelBandRuleIds = dayTypeRuleModel.TimeBandRules?
                        .Where(ptbr => ptbr.Id.HasValue && ptbr.Id.Value > 0)
                        .Select(ptbr => ptbr.Id.Value)
                        .ToList() ?? new List<int>();

                    // Delete TimeBandRules not in the model
                    foreach (var existingBandRule in existingBandRules.Where(ebr => !modelBandRuleIds.Contains(ebr.Id)))
                    {
                        await existingBandRule.Delete(_dbContext);
                    }

                    // Update or Create TimeBandRules
                    if (dayTypeRuleModel.TimeBandRules != null && dayTypeRuleModel.TimeBandRules.Any())
                    {
                        foreach (var timeBandRuleModel in dayTypeRuleModel.TimeBandRules)
                        {
                            if (timeBandRuleModel.Id.HasValue && timeBandRuleModel.Id.Value > 0)
                            {
                                // Update existing TimeBandRule
                                var existingBandRule = existingBandRules.FirstOrDefault(ebr => ebr.Id == timeBandRuleModel.Id.Value);
                                if (existingBandRule != null)
                                {
                                    existingBandRule.StartSecondOfDay = timeBandRuleModel.StartSecondOfDay;
                                    existingBandRule.EndSecondOfDay = timeBandRuleModel.EndSecondOfDay;
                                    existingBandRule.PayCode = timeBandRuleModel.PayCode;
                                    existingBandRule.Priority = timeBandRuleModel.Priority;
                                    existingBandRule.UpdatedAt = DateTime.UtcNow;
                                    await existingBandRule.Update(_dbContext);
                                }
                            }
                            else
                            {
                                // Create new TimeBandRule
                                var payTimeBandRule = new PayTimeBandRule
                                {
                                    PayDayTypeRuleId = payDayTypeRule.Id,
                                    StartSecondOfDay = timeBandRuleModel.StartSecondOfDay,
                                    EndSecondOfDay = timeBandRuleModel.EndSecondOfDay,
                                    PayCode = timeBandRuleModel.PayCode,
                                    Priority = timeBandRuleModel.Priority,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow,
                                    WorkflowState = Constants.WorkflowStates.Created
                                };
                                await payTimeBandRule.Create(_dbContext);
                            }
                        }
                    }
                }
            }

            return new OperationResult(true, "Pay rule set updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating pay rule set {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var payRuleSet = await _dbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Id == id && prs.WorkflowState != Constants.WorkflowStates.Removed);

            if (payRuleSet == null)
            {
                return new OperationResult(false, "Pay rule set not found");
            }

            await payRuleSet.Delete(_dbContext);

            return new OperationResult(true, "Pay rule set deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting pay rule set {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }
}