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
namespace TimePlanning.Pn.Services.ContentHandoverService;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.ContentHandover;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IContentHandoverService
{
    /// <summary>
    /// Offers the source day to a coworker. The source PlanRegistration must
    /// belong to the authenticated caller (resolved from the JWT).
    /// </summary>
    Task<OperationDataResult<List<ContentHandoverRequestModel>>> CreateAsync(int fromPlanRegistrationId, ContentHandoverRequestCreateModel model);
    Task<OperationDataResult<List<HandoverCoworkerModel>>> GetHandoverEligibleCoworkersAsync(DateTime date, List<int> shiftIndices);
    /// <summary>
    /// Accept, reject and cancel act as the authenticated caller, resolved
    /// from the JWT, never as a client-supplied site id: only the recipient
    /// may accept or reject, only the requester may cancel.
    /// </summary>
    Task<OperationResult> AcceptAsync(int requestId, ContentHandoverDecisionModel model);
    Task<OperationResult> RejectAsync(int requestId, ContentHandoverDecisionModel model);
    Task<OperationResult> CancelAsync(int requestId);
    /// <summary>
    /// Returns pending handover requests addressed to the currently
    /// authenticated caller's SDK site. The caller's site is resolved from
    /// the JWT, not from any client-supplied value, so a client cannot
    /// inspect another worker's inbox.
    /// </summary>
    Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetInboxAsync();
    Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetMineAsync();
    Task<OperationDataResult<List<HandoverCoworkerModel>>> GetHandoverEligibleCoworkersAsync(DateTime date);
    /// <summary>
    /// Returns ALL content handover requests matching the optional filters.
    /// Intended for admin/manager use — no user-scoping is applied.
    /// </summary>
    Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetAllAsync(
        string? status, string? fromDate, string? toDate, int? sdkSiteId,
        int page = 0, int pageSize = 100);
}
