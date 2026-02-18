/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.PayRuleSet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.PayRuleSetService;

[Authorize]
[Route("api/time-planning-pn/pay-rule-sets")]
public class PayRuleSetController : Controller
{
    private readonly IPayRuleSetService _payRuleSetService;

    public PayRuleSetController(IPayRuleSetService payRuleSetService)
    {
        _payRuleSetService = payRuleSetService;
    }

    [HttpGet]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayRuleSetsListModel>> Index(
        [FromQuery] PayRuleSetsRequestModel requestModel)
    {
        return await _payRuleSetService.Index(requestModel);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayRuleSetModel>> Read(int id)
    {
        return await _payRuleSetService.Read(id);
    }

    [HttpPost]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Create([FromBody] PayRuleSetCreateModel model)
    {
        // Debug logging
        Console.WriteLine($"[PayRuleSetController.Create] Model is null: {model == null}");
        
        if (model != null)
        {
            Console.WriteLine($"[PayRuleSetController.Create] Model.Name: {model.Name}");
            Console.WriteLine($"[PayRuleSetController.Create] Model.PayDayRules count: {model.PayDayRules?.Count ?? 0}");
            if (model.PayDayRules != null)
            {
                foreach (var rule in model.PayDayRules)
                {
                    Console.WriteLine($"[PayRuleSetController.Create]   - DayCode: {rule.DayCode}, PayTierRules: {rule.PayTierRules?.Count ?? 0}");
                }
            }
        }
        
        if (!ModelState.IsValid)
        {
            Console.WriteLine("[PayRuleSetController.Create] ModelState is INVALID:");
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"  - {state.Key}: {error.ErrorMessage}");
                }
            }
        }
        
        return await _payRuleSetService.Create(model);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Update(int id, [FromBody] PayRuleSetUpdateModel model)
    {
        // Debug logging
        Console.WriteLine($"[PayRuleSetController.Update] Called with id: {id}");
        Console.WriteLine($"[PayRuleSetController.Update] Model is null: {model == null}");
        
        if (model != null)
        {
            Console.WriteLine($"[PayRuleSetController.Update] Model.Name: {model.Name}");
            Console.WriteLine($"[PayRuleSetController.Update] Model.PayDayRules count: {model.PayDayRules?.Count ?? 0}");
            if (model.PayDayRules != null)
            {
                foreach (var rule in model.PayDayRules)
                {
                    Console.WriteLine($"[PayRuleSetController.Update]   - DayCode: {rule.DayCode}, PayTierRules: {rule.PayTierRules?.Count ?? 0}");
                }
            }
        }
        
        if (!ModelState.IsValid)
        {
            Console.WriteLine("[PayRuleSetController.Update] ModelState is INVALID:");
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"  - {state.Key}: {error.ErrorMessage}");
                }
            }
        }
        
        return await _payRuleSetService.Update(id, model);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Delete(int id)
    {
        return await _payRuleSetService.Delete(id);
    }
}
