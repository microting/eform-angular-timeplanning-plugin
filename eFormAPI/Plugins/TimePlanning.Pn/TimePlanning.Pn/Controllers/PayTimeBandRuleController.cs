/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.PayTimeBandRule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.PayTimeBandRuleService;

[Authorize]
[Route("api/time-planning-pn/pay-time-band-rules")]
public class PayTimeBandRuleController : Controller
{
    private readonly IPayTimeBandRuleService _service;

    public PayTimeBandRuleController(IPayTimeBandRuleService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayTimeBandRulesListModel>> Index(
        [FromQuery] PayTimeBandRulesRequestModel requestModel)
    {
        return await _service.Index(requestModel);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayTimeBandRuleModel>> Read(int id)
    {
        return await _service.Read(id);
    }

    [HttpPost]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Create([FromBody] PayTimeBandRuleCreateModel model)
    {
        return await _service.Create(model);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Update(int id, [FromBody] PayTimeBandRuleUpdateModel model)
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
