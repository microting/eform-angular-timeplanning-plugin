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
using Infrastructure.Models.AbsenceRequest;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.AbsenceRequestService;

[Route("api/time-planning-pn/absence-requests")]
public class AbsenceRequestsController(IAbsenceRequestService absenceRequestService) : Controller
{
    private readonly IAbsenceRequestService _absenceRequestService = absenceRequestService;

    [HttpPost]
    public async Task<OperationDataResult<AbsenceRequestModel>> Create([FromBody] AbsenceRequestCreateModel model)
    {
        return await _absenceRequestService.CreateAsync(model);
    }

    [HttpPost]
    [Route("{id}/approve")]
    public async Task<OperationResult> Approve(int id, [FromBody] AbsenceRequestDecisionModel model)
    {
        return await _absenceRequestService.ApproveAsync(id, model);
    }

    [HttpPost]
    [Route("{id}/reject")]
    public async Task<OperationResult> Reject(int id, [FromBody] AbsenceRequestDecisionModel model)
    {
        return await _absenceRequestService.RejectAsync(id, model);
    }

    [HttpPost]
    [Route("{id}/cancel")]
    public async Task<OperationResult> Cancel(int id, [FromBody] int requestedBySdkSitId)
    {
        return await _absenceRequestService.CancelAsync(id, requestedBySdkSitId);
    }

    [HttpGet]
    [Route("inbox")]
    public async Task<OperationDataResult<List<AbsenceRequestModel>>> GetInbox(int managerSdkSitId)
    {
        return await _absenceRequestService.GetInboxAsync(managerSdkSitId);
    }

    [HttpGet]
    [Route("mine")]
    public async Task<OperationDataResult<List<AbsenceRequestModel>>> GetMine(int requestedBySdkSitId)
    {
        return await _absenceRequestService.GetMineAsync(requestedBySdkSitId);
    }
}
