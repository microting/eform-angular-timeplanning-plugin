/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayTierRuleService;

using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.PayTierRule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

public class PayTierRuleService : IPayTierRuleService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PayTierRuleService> _logger;
    private readonly ITimePlanningLocalizationService _localizationService;

    public PayTierRuleService(
        TimePlanningPnDbContext dbContext,
        ILogger<PayTierRuleService> logger,
        ITimePlanningLocalizationService localizationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _localizationService = localizationService;
    }

    /// <summary>
    /// A pay tier rule belongs to a PayDayRule, which belongs to a PayRuleSet.
    /// Locked overenskomst presets are read-only, and the guard on
    /// PayRuleSetService only covers the rule set row itself — without this
    /// check a locked preset could be rewritten one child row at a time.
    /// </summary>
    private async Task<bool> OwningPayRuleSetIsLocked(int payDayRuleId)
    {
        var payRuleSetName = await _dbContext.PayDayRules
            .Where(pdr => pdr.Id == payDayRuleId)
            .Select(pdr => pdr.PayRuleSet.Name)
            .FirstOrDefaultAsync();

        return payRuleSetName != null && PayRuleSetLock.IsLockedPresetName(payRuleSetName);
    }

    public async Task<OperationDataResult<PayTierRulesListModel>> Index(PayTierRulesRequestModel requestModel)
    {
        try
        {
            var query = _dbContext.PayTierRules
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            if (requestModel.PayDayRuleId.HasValue)
            {
                query = query.Where(x => x.PayDayRuleId == requestModel.PayDayRuleId.Value);
            }

            var total = await query.CountAsync();

            var rules = await query
                .OrderBy(r => r.Order) // Order by tier order
                .Skip(requestModel.Offset)
                .Take(requestModel.PageSize)
                .Select(r => new PayTierRuleSimpleModel
                {
                    Id = r.Id,
                    Order = r.Order,
                    UpToSeconds = r.UpToSeconds,
                    PayCode = r.PayCode
                })
                .ToListAsync();

            return new OperationDataResult<PayTierRulesListModel>(
                true,
                new PayTierRulesListModel
                {
                    Total = total,
                    PayTierRules = rules
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay tier rules");
            return new OperationDataResult<PayTierRulesListModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationDataResult<PayTierRuleModel>> Read(int id)
    {
        try
        {
            var rule = await _dbContext.PayTierRules
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rule == null)
            {
                return new OperationDataResult<PayTierRuleModel>(
                    false,
                    "Pay tier rule not found");
            }

            var model = new PayTierRuleModel
            {
                Id = rule.Id,
                PayDayRuleId = rule.PayDayRuleId,
                Order = rule.Order,
                UpToSeconds = rule.UpToSeconds,
                PayCode = rule.PayCode
            };

            return new OperationDataResult<PayTierRuleModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading pay tier rule {id}");
            return new OperationDataResult<PayTierRuleModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Create(PayTierRuleCreateModel model)
    {
        try
        {
            if (await OwningPayRuleSetIsLocked(model.PayDayRuleId))
            {
                return new OperationResult(false, _localizationService.GetString("CannotEditLockedPreset"));
            }

            var rule = new PayTierRule
            {
                PayDayRuleId = model.PayDayRuleId,
                Order = model.Order,
                UpToSeconds = model.UpToSeconds,
                PayCode = model.PayCode,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };

            await rule.Create(_dbContext);

            return new OperationResult(true, "Pay tier rule created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay tier rule");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Update(int id, PayTierRuleUpdateModel model)
    {
        try
        {
            var rule = await _dbContext.PayTierRules
                .FirstOrDefaultAsync(r => r.Id == id && r.WorkflowState != Constants.WorkflowStates.Removed);

            if (rule == null)
            {
                return new OperationResult(false, "Pay tier rule not found");
            }

            if (await OwningPayRuleSetIsLocked(rule.PayDayRuleId))
            {
                return new OperationResult(false, _localizationService.GetString("CannotEditLockedPreset"));
            }

            rule.Order = model.Order;
            rule.UpToSeconds = model.UpToSeconds;
            rule.PayCode = model.PayCode;
            rule.UpdatedAt = DateTime.UtcNow;

            await rule.Update(_dbContext);

            return new OperationResult(true, "Pay tier rule updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating pay tier rule {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var rule = await _dbContext.PayTierRules
                .FirstOrDefaultAsync(r => r.Id == id && r.WorkflowState != Constants.WorkflowStates.Removed);

            if (rule == null)
            {
                return new OperationResult(false, "Pay tier rule not found");
            }

            if (await OwningPayRuleSetIsLocked(rule.PayDayRuleId))
            {
                return new OperationResult(false, _localizationService.GetString("CannotEditLockedPreset"));
            }

            await rule.Delete(_dbContext);

            return new OperationResult(true, "Pay tier rule deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting pay tier rule {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }
}
