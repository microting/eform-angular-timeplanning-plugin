/*
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

namespace TimePlanning.Pn.Services.BreakPolicyService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.BreakPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

/// <summary>
/// Service for managing break policies
/// </summary>
public class BreakPolicyService : IBreakPolicyService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<BreakPolicyService> _logger;

    public BreakPolicyService(
        TimePlanningPnDbContext dbContext,
        ILogger<BreakPolicyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OperationDataResult<BreakPoliciesListModel>> Index(BreakPoliciesRequestModel requestModel)
    {
        try
        {
            var query = _dbContext.BreakPolicies
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            var total = await query.CountAsync();

            var breakPolicies = await query
                .Skip(requestModel.Offset)
                .Take(requestModel.PageSize)
                .Select(bp => new BreakPolicySimpleModel
                {
                    Id = bp.Id,
                    Name = bp.Name,
                    Description = "" // BreakPolicy entity may not have Description field
                })
                .ToListAsync();

            return new OperationDataResult<BreakPoliciesListModel>(
                true,
                new BreakPoliciesListModel
                {
                    Total = total,
                    BreakPolicies = breakPolicies
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting break policies");
            return new OperationDataResult<BreakPoliciesListModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationDataResult<BreakPolicyModel>> Read(int id)
    {
        try
        {
            var breakPolicy = await _dbContext.BreakPolicies
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(bp => bp.Id == id);

            if (breakPolicy == null)
            {
                return new OperationDataResult<BreakPolicyModel>(
                    false,
                    "Break policy not found");
            }

            // Load break policy rules separately with all available fields
            var rules = await _dbContext.BreakPolicyRules
                .Where(r => r.BreakPolicyId == id && r.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var model = new BreakPolicyModel
            {
                Id = breakPolicy.Id,
                Name = breakPolicy.Name,
                Description = "", 
                BreakPolicyRules = rules.Select(r => new BreakPolicyRuleModel
                {
                    Id = r.Id,
                    BreakPolicyId = r.BreakPolicyId,
                    DayOfWeek = (int)r.DayOfWeek, // Cast enum to int
                    PaidBreakSeconds = 0, // TODO: Map actual field when schema is known
                    UnpaidBreakSeconds = 0 // TODO: Map actual field when schema is known
                }).ToList()
            };

            return new OperationDataResult<BreakPolicyModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading break policy {id}");
            return new OperationDataResult<BreakPolicyModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Create(BreakPolicyCreateModel model)
    {
        try
        {
            var breakPolicy = new BreakPolicy
            {
                Name = model.Name,
                // Description = model.Description, // BreakPolicy entity may not have Description field
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };

            await breakPolicy.Create(_dbContext);

            // Create nested BreakPolicyRules if provided
            if (model.BreakPolicyRules != null && model.BreakPolicyRules.Any())
            {
                foreach (var ruleModel in model.BreakPolicyRules)
                {
                    var rule = new BreakPolicyRule
                    {
                        BreakPolicyId = breakPolicy.Id,
                        DayOfWeek = (DayOfWeek)ruleModel.DayOfWeek,
                        PaidBreakMinutes = ruleModel.PaidBreakMinutes,
                        UnpaidBreakMinutes = ruleModel.UnpaidBreakMinutes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        WorkflowState = Constants.WorkflowStates.Created
                    };
                    await rule.Create(_dbContext);
                }
            }

            return new OperationResult(true, "Break policy created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating break policy");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Update(int id, BreakPolicyUpdateModel model)
    {
        try
        {
            var breakPolicy = await _dbContext.BreakPolicies
                .FirstOrDefaultAsync(bp => bp.Id == id && bp.WorkflowState != Constants.WorkflowStates.Removed);

            if (breakPolicy == null)
            {
                return new OperationResult(false, "Break policy not found");
            }

            breakPolicy.Name = model.Name;
            // breakPolicy.Description = model.Description; // BreakPolicy entity may not have Description field
            breakPolicy.UpdatedAt = DateTime.UtcNow;

            await breakPolicy.Update(_dbContext);

            // Handle nested BreakPolicyRules
            if (model.BreakPolicyRules != null)
            {
                // Get existing rules
                var existingRules = await _dbContext.BreakPolicyRules
                    .Where(r => r.BreakPolicyId == id && r.WorkflowState != Constants.WorkflowStates.Removed)
                    .ToListAsync();

                // Get list of rule IDs in the model (ones we want to keep/update)
                var modelRuleIds = model.BreakPolicyRules
                    .Where(r => r.Id.HasValue && r.Id.Value > 0)
                    .Select(r => r.Id.Value)
                    .ToList();

                // Delete rules that are not in the model
                foreach (var existingRule in existingRules)
                {
                    if (!modelRuleIds.Contains(existingRule.Id))
                    {
                        await existingRule.Delete(_dbContext);
                    }
                }

                // Update existing or create new rules
                foreach (var ruleModel in model.BreakPolicyRules)
                {
                    if (ruleModel.Id.HasValue && ruleModel.Id.Value > 0)
                    {
                        // Update existing rule
                        var existingRule = existingRules.FirstOrDefault(r => r.Id == ruleModel.Id.Value);
                        if (existingRule != null)
                        {
                            existingRule.DayOfWeek = (DayOfWeek)ruleModel.DayOfWeek;
                            existingRule.PaidBreakMinutes = ruleModel.PaidBreakMinutes;
                            existingRule.UnpaidBreakMinutes = ruleModel.UnpaidBreakMinutes;
                            existingRule.UpdatedAt = DateTime.UtcNow;
                            await existingRule.Update(_dbContext);
                        }
                    }
                    else
                    {
                        // Create new rule
                        var newRule = new BreakPolicyRule
                        {
                            BreakPolicyId = id,
                            DayOfWeek = (DayOfWeek)ruleModel.DayOfWeek,
                            PaidBreakMinutes = ruleModel.PaidBreakMinutes,
                            UnpaidBreakMinutes = ruleModel.UnpaidBreakMinutes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            WorkflowState = Constants.WorkflowStates.Created
                        };
                        await newRule.Create(_dbContext);
                    }
                }
            }

            return new OperationResult(true, "Break policy updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating break policy {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var breakPolicy = await _dbContext.BreakPolicies
                .FirstOrDefaultAsync(bp => bp.Id == id && bp.WorkflowState != Constants.WorkflowStates.Removed);

            if (breakPolicy == null)
            {
                return new OperationResult(false, "Break policy not found");
            }

            await breakPolicy.Delete(_dbContext);

            return new OperationResult(true, "Break policy deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting break policy {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }
}
