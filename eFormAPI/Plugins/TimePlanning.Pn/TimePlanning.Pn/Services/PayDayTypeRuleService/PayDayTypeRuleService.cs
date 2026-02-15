/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayDayTypeRuleService;

using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.PayDayTypeRule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

public class PayDayTypeRuleService : IPayDayTypeRuleService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PayDayTypeRuleService> _logger;

    public PayDayTypeRuleService(
        TimePlanningPnDbContext dbContext,
        ILogger<PayDayTypeRuleService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OperationDataResult<PayDayTypeRulesListModel>> Index(PayDayTypeRulesRequestModel requestModel)
    {
        try
        {
            var query = _dbContext.PayDayTypeRules
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            if (requestModel.PayRuleSetId.HasValue)
            {
                query = query.Where(x => x.PayRuleSetId == requestModel.PayRuleSetId.Value);
            }

            var total = await query.CountAsync();

            var rules = await query
                .Skip(requestModel.Offset)
                .Take(requestModel.PageSize)
                .Select(r => new PayDayTypeRuleSimpleModel
                {
                    Id = r.Id,
                    DayType = r.DayType.ToString()
                })
                .ToListAsync();

            return new OperationDataResult<PayDayTypeRulesListModel>(
                true,
                new PayDayTypeRulesListModel
                {
                    Total = total,
                    PayDayTypeRules = rules
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay day type rules");
            return new OperationDataResult<PayDayTypeRulesListModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationDataResult<PayDayTypeRuleModel>> Read(int id)
    {
        try
        {
            var rule = await _dbContext.PayDayTypeRules
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rule == null)
            {
                return new OperationDataResult<PayDayTypeRuleModel>(
                    false,
                    "Pay day type rule not found");
            }

            var model = new PayDayTypeRuleModel
            {
                Id = rule.Id,
                PayRuleSetId = rule.PayRuleSetId,
                DayType = rule.DayType.ToString()
            };

            return new OperationDataResult<PayDayTypeRuleModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading pay day type rule {id}");
            return new OperationDataResult<PayDayTypeRuleModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Create(PayDayTypeRuleCreateModel model)
    {
        try
        {
            // Parse DayType enum
            if (!Enum.TryParse<DayType>(model.DayType, out var dayType))
            {
                return new OperationResult(false, "Invalid day type");
            }

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = model.PayRuleSetId,
                DayType = dayType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };

            await rule.Create(_dbContext);

            return new OperationResult(true, "Pay day type rule created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay day type rule");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Update(int id, PayDayTypeRuleUpdateModel model)
    {
        try
        {
            var rule = await _dbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == id && r.WorkflowState != Constants.WorkflowStates.Removed);

            if (rule == null)
            {
                return new OperationResult(false, "Pay day type rule not found");
            }

            // Parse DayType enum
            if (!Enum.TryParse<DayType>(model.DayType, out var dayType))
            {
                return new OperationResult(false, "Invalid day type");
            }

            rule.DayType = dayType;
            rule.UpdatedAt = DateTime.UtcNow;

            await rule.Update(_dbContext);

            return new OperationResult(true, "Pay day type rule updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating pay day type rule {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var rule = await _dbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == id && r.WorkflowState != Constants.WorkflowStates.Removed);

            if (rule == null)
            {
                return new OperationResult(false, "Pay day type rule not found");
            }

            await rule.Delete(_dbContext);

            return new OperationResult(true, "Pay day type rule deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting pay day type rule {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }
}
