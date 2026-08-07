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

    // Readable by any authenticated user: a non-admin needs the list to pick a
    // pay rule set for a site they are assigned to. Authoring stays admin-only
    // (see Create/Update/Delete below).
    [HttpGet]
    public async Task<OperationDataResult<PayRuleSetsListModel>> Index(
        [FromQuery] PayRuleSetsRequestModel requestModel)
    {
        return await _payRuleSetService.Index(requestModel);
    }

    // Readable by any authenticated user — same reasoning as Index: viewing a
    // pay rule set is needed to select one, changing it is not.
    [HttpGet("{id}")]
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
