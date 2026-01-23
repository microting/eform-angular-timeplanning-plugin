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

#nullable enable
namespace TimePlanning.Pn.Controllers;

using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.Planning;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.TimePlanningPlanningService;

[Route("api/time-planning-pn/plannings")]
public class TimePlanningPlanningController(ITimePlanningPlanningService planningService) : Controller
{
    private readonly ITimePlanningPlanningService _planningService = planningService;

    [HttpPost]
    [Route("index")]
    public async Task<OperationDataResult<List<TimePlanningPlanningModel>>> Index(
        [FromBody] TimePlanningPlanningRequestModel model)
    {
        return await _planningService.Index(model);
    }

    [HttpPut]
    [Route("{id}")]
    public async Task<OperationResult> Update(int id, [FromBody] TimePlanningPlanningPrDayModel model)
    {
        return await _planningService.Update(id, model);
    }

    [HttpPut]
    [Route("update-by-current-user")]
    public async Task<IActionResult> UpdateByCurrentUserNam([FromBody] TimePlanningPlanningPrDayModel model)
    {
        var result = await _planningService.UpdateByCurrentUserNam(model);
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message == "AssignedSiteNotFound")
        {
            return NotFound(new OperationResult(false, "Assigned site not found."));
        }
        return BadRequest(new OperationResult(false, result.Message));
    }

    [HttpGet]
    [Route("get-by-user")]
    public async Task<OperationDataResult<TimePlanningPlanningModel>> IndexByCurrentUserNam(TimePlanningPlanningRequestModel model, string? softwareVersion, string? deviceModel, string? manufacturer, string? osVersion)
    {
        return await _planningService.IndexByCurrentUserName(model, softwareVersion, deviceModel, manufacturer, osVersion);
    }

    [HttpGet]
    [Route("{planRegistrationId}/version-history")]
    public async Task<OperationDataResult<PlanRegistrationVersionHistoryModel>> GetVersionHistory(int planRegistrationId)
    {
        return await _planningService.GetVersionHistory(planRegistrationId);
    }
}