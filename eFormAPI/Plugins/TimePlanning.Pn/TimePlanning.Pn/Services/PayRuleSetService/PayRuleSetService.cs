/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayRuleSetService;

using System;
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

            var model = new PayRuleSetModel
            {
                Id = payRuleSet.Id,
                Name = payRuleSet.Name,
                PayDayRules = payDayRules.Select(pdr => new PayDayRuleModel
                {
                    Id = pdr.Id,
                    PayRuleSetId = pdr.PayRuleSetId,
                    DayCode = pdr.DayCode
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
