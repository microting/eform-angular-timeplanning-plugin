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
        return await _payRuleSetService.Create(model);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Update(int id, [FromBody] PayRuleSetUpdateModel model)
    {
        return await _payRuleSetService.Update(id, model);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Delete(int id)
    {
        return await _payRuleSetService.Delete(id);
    }
}
