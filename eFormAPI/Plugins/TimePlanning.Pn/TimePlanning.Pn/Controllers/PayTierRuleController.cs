/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.PayTierRule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.PayTierRuleService;

[Authorize]
[Route("api/time-planning-pn/pay-tier-rules")]
public class PayTierRuleController : Controller
{
    private readonly IPayTierRuleService _service;

    public PayTierRuleController(IPayTierRuleService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayTierRulesListModel>> Index(
        [FromQuery] PayTierRulesRequestModel requestModel)
    {
        return await _service.Index(requestModel);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayTierRuleModel>> Read(int id)
    {
        return await _service.Read(id);
    }

    [HttpPost]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Create([FromBody] PayTierRuleCreateModel model)
    {
        return await _service.Create(model);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Update(int id, [FromBody] PayTierRuleUpdateModel model)
    {
        return await _service.Update(id, model);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Delete(int id)
    {
        return await _service.Delete(id);
    }
}
