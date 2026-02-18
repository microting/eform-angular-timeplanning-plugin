/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayRuleSetService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.PayRuleSet;
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
                .Where(pdr => pdr.Id > 0)
                .Select(pdr => pdr.Id)
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
                    
                    if (dayRuleModel.Id > 0)
                    {
                        // Update existing PayDayRule
                        payDayRule = existingPayDayRules.FirstOrDefault(pdr => pdr.Id == dayRuleModel.Id);
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
                        .Where(ptr => ptr.Id > 0)
                        .Select(ptr => ptr.Id)
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
                            if (tierRuleModel.Id > 0)
                            {
                                // Update existing PayTierRule
                                var existingTierRule = existingTierRules.FirstOrDefault(ptr => ptr.Id == tierRuleModel.Id);
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
