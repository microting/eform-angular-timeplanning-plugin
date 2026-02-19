/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayTimeBandRuleService;

using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.PayTimeBandRule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

public class PayTimeBandRuleService : IPayTimeBandRuleService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PayTimeBandRuleService> _logger;

    public PayTimeBandRuleService(
        TimePlanningPnDbContext dbContext,
        ILogger<PayTimeBandRuleService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OperationDataResult<PayTimeBandRulesListModel>> Index(PayTimeBandRulesRequestModel requestModel)
    {
        try
        {
            var query = _dbContext.PayTimeBandRules
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            if (requestModel.PayDayTypeRuleId.HasValue)
            {
                query = query.Where(x => x.PayDayTypeRuleId == requestModel.PayDayTypeRuleId.Value);
            }

            var total = await query.CountAsync();

            var rules = await query
                .OrderBy(r => r.StartSecondOfDay) // Order by start time
                .Skip(requestModel.Offset)
                .Take(requestModel.PageSize)
                .Select(r => new PayTimeBandRuleSimpleModel
                {
                    Id = r.Id,
                    StartSecondOfDay = r.StartSecondOfDay,
                    EndSecondOfDay = r.EndSecondOfDay,
                    PayCode = r.PayCode
                })
                .ToListAsync();

            return new OperationDataResult<PayTimeBandRulesListModel>(
                true,
                new PayTimeBandRulesListModel
                {
                    Total = total,
                    PayTimeBandRules = rules
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay time band rules");
            return new OperationDataResult<PayTimeBandRulesListModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationDataResult<PayTimeBandRuleModel>> Read(int id)
    {
        try
        {
            var rule = await _dbContext.PayTimeBandRules
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rule == null)
            {
                return new OperationDataResult<PayTimeBandRuleModel>(
                    false,
                    "Pay time band rule not found");
            }

            var model = new PayTimeBandRuleModel
            {
                Id = rule.Id,
                PayDayTypeRuleId = rule.PayDayTypeRuleId,
                StartSecondOfDay = rule.StartSecondOfDay,
                EndSecondOfDay = rule.EndSecondOfDay,
                PayCode = rule.PayCode
            };

            return new OperationDataResult<PayTimeBandRuleModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading pay time band rule {id}");
            return new OperationDataResult<PayTimeBandRuleModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Create(PayTimeBandRuleCreateModel model)
    {
        try
        {
            var rule = new PayTimeBandRule
            {
                PayDayTypeRuleId = model.PayDayTypeRuleId,
                StartSecondOfDay = model.StartSecondOfDay,
                EndSecondOfDay = model.EndSecondOfDay,
                PayCode = model.PayCode,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };

            await rule.Create(_dbContext);

            return new OperationResult(true, "Pay time band rule created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay time band rule");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Update(int id, PayTimeBandRuleUpdateModel model)
    {
        try
        {
            var rule = await _dbContext.PayTimeBandRules
                .FirstOrDefaultAsync(r => r.Id == id && r.WorkflowState != Constants.WorkflowStates.Removed);

            if (rule == null)
            {
                return new OperationResult(false, "Pay time band rule not found");
            }

            rule.StartSecondOfDay = model.StartSecondOfDay;
            rule.EndSecondOfDay = model.EndSecondOfDay;
            rule.PayCode = model.PayCode;
            rule.UpdatedAt = DateTime.UtcNow;

            await rule.Update(_dbContext);

            return new OperationResult(true, "Pay time band rule updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating pay time band rule {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var rule = await _dbContext.PayTimeBandRules
                .FirstOrDefaultAsync(r => r.Id == id && r.WorkflowState != Constants.WorkflowStates.Removed);

            if (rule == null)
            {
                return new OperationResult(false, "Pay time band rule not found");
            }

            await rule.Delete(_dbContext);

            return new OperationResult(true, "Pay time band rule deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting pay time band rule {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }
}
