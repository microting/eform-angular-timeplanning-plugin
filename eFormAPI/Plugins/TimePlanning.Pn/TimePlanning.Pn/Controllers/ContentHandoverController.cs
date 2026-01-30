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
using Infrastructure.Models.ContentHandover;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.ContentHandoverService;

[Route("api/time-planning-pn")]
public class ContentHandoverController(IContentHandoverService contentHandoverService) : Controller
{
    private readonly IContentHandoverService _contentHandoverService = contentHandoverService;

    [HttpPost]
    [Route("plan-registrations/{id}/handover-requests")]
    public async Task<OperationDataResult<ContentHandoverRequestModel>> Create(
        int id, 
        [FromBody] ContentHandoverRequestCreateModel model)
    {
        return await _contentHandoverService.CreateAsync(id, model);
    }

    [HttpPost]
    [Route("handover-requests/{id}/accept")]
    public async Task<OperationResult> Accept(int id, int currentSdkSitId, [FromBody] ContentHandoverDecisionModel model)
    {
        return await _contentHandoverService.AcceptAsync(id, currentSdkSitId, model);
    }

    [HttpPost]
    [Route("handover-requests/{id}/reject")]
    public async Task<OperationResult> Reject(int id, int currentSdkSitId, [FromBody] ContentHandoverDecisionModel model)
    {
        return await _contentHandoverService.RejectAsync(id, currentSdkSitId, model);
    }

    [HttpPost]
    [Route("handover-requests/{id}/cancel")]
    public async Task<OperationResult> Cancel(int id, int currentSdkSitId)
    {
        return await _contentHandoverService.CancelAsync(id, currentSdkSitId);
    }

    [HttpGet]
    [Route("handover-requests/inbox")]
    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetInbox(int toSdkSitId)
    {
        return await _contentHandoverService.GetInboxAsync(toSdkSitId);
    }

    [HttpGet]
    [Route("handover-requests/mine")]
    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetMine(int fromSdkSitId)
    {
        return await _contentHandoverService.GetMineAsync(fromSdkSitId);
    }
}
