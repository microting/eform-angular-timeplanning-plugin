/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.PayDayTypeRule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.PayDayTypeRuleService;

[Authorize]
[Route("api/time-planning-pn/pay-day-type-rules")]
public class PayDayTypeRuleController : Controller
{
    private readonly IPayDayTypeRuleService _service;

    public PayDayTypeRuleController(IPayDayTypeRuleService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayDayTypeRulesListModel>> Index(
        [FromQuery] PayDayTypeRulesRequestModel requestModel)
    {
        return await _service.Index(requestModel);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<PayDayTypeRuleModel>> Read(int id)
    {
        return await _service.Read(id);
    }

    [HttpPost]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Create([FromBody] PayDayTypeRuleCreateModel model)
    {
        return await _service.Create(model);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> Update(int id, [FromBody] PayDayTypeRuleUpdateModel model)
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
