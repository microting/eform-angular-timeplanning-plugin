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
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.ContentHandover;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanningLocalizationService;

public class ContentHandoverService : IContentHandoverService
{
    private readonly ILogger<ContentHandoverService> _logger;
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly ITimePlanningLocalizationService _localizationService;

    public ContentHandoverService(
        ILogger<ContentHandoverService> logger,
        TimePlanningPnDbContext dbContext,
        IUserService userService,
        ITimePlanningLocalizationService localizationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _userService = userService;
        _localizationService = localizationService;
    }

    public async Task<OperationDataResult<ContentHandoverRequestModel>> CreateAsync(
        int fromPlanRegistrationId, 
        ContentHandoverRequestCreateModel model)
    {
        try
        {
            // Load source PlanRegistration
            var fromPR = await _dbContext.PlanRegistrations
                .FirstOrDefaultAsync(pr => pr.Id == fromPlanRegistrationId);

            if (fromPR == null)
            {
                return new OperationDataResult<ContentHandoverRequestModel>(false,
                    _localizationService.GetString("SourcePlanRegistrationNotFound"));
            }

            // Validate source has content
            var hasPlanHours = fromPR.PlanHoursInSeconds > 0;
            var hasPlanText = !string.IsNullOrWhiteSpace(fromPR.PlanText);
            if (!hasPlanHours && !hasPlanText)
            {
                return new OperationDataResult<ContentHandoverRequestModel>(false,
                    _localizationService.GetString("SourcePlanRegistrationHasNoContent"));
            }

            // Find target PlanRegistration
            var toPR = await _dbContext.PlanRegistrations
                .FirstOrDefaultAsync(pr => pr.SdkSitId == model.ToSdkSitId
                                           && pr.Date == fromPR.Date);

            if (toPR == null)
            {
                return new OperationDataResult<ContentHandoverRequestModel>(false,
                    _localizationService.GetString("TargetPlanRegistrationNotFound"));
            }

            // Validate different workers
            if (fromPR.SdkSitId == toPR.SdkSitId)
            {
                return new OperationDataResult<ContentHandoverRequestModel>(false,
                    _localizationService.GetString("CannotHandoverToSameWorker"));
            }

            // Check for existing pending request for same target and date
            var hasPendingRequest = await _dbContext.PlanRegistrationContentHandoverRequests
                .AnyAsync(r => r.ToSdkSitId == model.ToSdkSitId
                               && r.Date == fromPR.Date
                               && r.Status == HandoverRequestStatus.Pending);

            if (hasPendingRequest)
            {
                return new OperationDataResult<ContentHandoverRequestModel>(false,
                    _localizationService.GetString("PendingHandoverRequestAlreadyExists"));
            }

            // Create handover request
            var request = new PlanRegistrationContentHandoverRequest
            {
                FromSdkSitId = fromPR.SdkSitId,
                ToSdkSitId = model.ToSdkSitId,
                Date = fromPR.Date,
                FromPlanRegistrationId = fromPR.Id,
                ToPlanRegistrationId = toPR.Id,
                Status = HandoverRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow,
                CreatedByUserId = _userService.UserId,
                UpdatedByUserId = _userService.UserId
            };

            await request.Create(_dbContext);

            var resultModel = MapToModel(request);
            return new OperationDataResult<ContentHandoverRequestModel>(true, resultModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content handover request");
            return new OperationDataResult<ContentHandoverRequestModel>(false,
                _localizationService.GetString("ErrorCreatingHandoverRequest"));
        }
    }

    public async Task<OperationResult> AcceptAsync(
        int requestId, 
        int currentSdkSitId, 
        ContentHandoverDecisionModel model)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Load request
            var request = await _dbContext.PlanRegistrationContentHandoverRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                return new OperationResult(false, _localizationService.GetString("HandoverRequestNotFound"));
            }

            if (request.Status != HandoverRequestStatus.Pending)
            {
                return new OperationResult(false, _localizationService.GetString("HandoverRequestMustBePending"));
            }

            if (request.ToSdkSitId != currentSdkSitId)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToAccept"));
            }

            // Load source and target PlanRegistrations
            var fromPR = await _dbContext.PlanRegistrations
                .FirstOrDefaultAsync(pr => pr.Id == request.FromPlanRegistrationId);
            var toPR = await _dbContext.PlanRegistrations
                .FirstOrDefaultAsync(pr => pr.Id == request.ToPlanRegistrationId);

            if (fromPR == null || toPR == null)
            {
                return new OperationResult(false, _localizationService.GetString("PlanRegistrationsNotFound"));
            }

            // Validate receiver is empty
            var targetHasContent = toPR.PlanHoursInSeconds > 0 || !string.IsNullOrWhiteSpace(toPR.PlanText);
            if (targetHasContent)
            {
                return new OperationResult(false,
                    _localizationService.GetString("TargetPlanRegistrationMustBeEmpty"));
            }

            // Move content from source to target
            MoveContent(fromPR, toPR);

            // Set audit fields if they exist
            try
            {
                var prType = typeof(PlanRegistration);
                var contentHandoverFromProp = prType.GetProperty("ContentHandoverFromSdkSitId");
                if (contentHandoverFromProp != null && contentHandoverFromProp.CanWrite)
                {
                    contentHandoverFromProp.SetValue(toPR, request.FromSdkSitId);
                }

                var contentHandoverToProp = prType.GetProperty("ContentHandoverToSdkSitId");
                if (contentHandoverToProp != null && contentHandoverToProp.CanWrite)
                {
                    contentHandoverToProp.SetValue(fromPR, request.ToSdkSitId);
                }

                var contentHandoverRequestIdPropFrom = prType.GetProperty("ContentHandoverRequestId");
                if (contentHandoverRequestIdPropFrom != null && contentHandoverRequestIdPropFrom.CanWrite)
                {
                    contentHandoverRequestIdPropFrom.SetValue(fromPR, request.Id);
                    contentHandoverRequestIdPropFrom.SetValue(toPR, request.Id);
                }

                var contentHandedOverAtProp = prType.GetProperty("ContentHandedOverAtUtc");
                if (contentHandedOverAtProp != null && contentHandedOverAtProp.CanWrite)
                {
                    contentHandedOverAtProp.SetValue(fromPR, DateTime.UtcNow);
                    contentHandedOverAtProp.SetValue(toPR, DateTime.UtcNow);
                }
            }
            catch
            {
                // Ignore if audit fields don't exist
            }

            fromPR.UpdatedByUserId = _userService.UserId;
            toPR.UpdatedByUserId = _userService.UserId;
            await fromPR.Update(_dbContext);
            await toPR.Update(_dbContext);

            // Update request status
            request.Status = HandoverRequestStatus.Accepted;
            request.RespondedAtUtc = DateTime.UtcNow;
            request.DecisionComment = model.DecisionComment;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            // Recalculate both PlanRegistrations
            var fromAssignedSite = await _dbContext.AssignedSites
                .FirstOrDefaultAsync(a => a.SiteId == fromPR.SdkSitId);
            if (fromAssignedSite != null)
            {
                PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(fromAssignedSite, fromPR);
                await fromPR.Update(_dbContext);
            }

            var toAssignedSite = await _dbContext.AssignedSites
                .FirstOrDefaultAsync(a => a.SiteId == toPR.SdkSitId);
            if (toAssignedSite != null)
            {
                PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(toAssignedSite, toPR);
                await toPR.Update(_dbContext);
            }

            await transaction.CommitAsync();
            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error accepting handover request {RequestId}", requestId);
            return new OperationResult(false, _localizationService.GetString("ErrorAcceptingHandoverRequest"));
        }
    }

    public async Task<OperationResult> RejectAsync(
        int requestId, 
        int currentSdkSitId, 
        ContentHandoverDecisionModel model)
    {
        try
        {
            var request = await _dbContext.PlanRegistrationContentHandoverRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                return new OperationResult(false, _localizationService.GetString("HandoverRequestNotFound"));
            }

            if (request.Status != HandoverRequestStatus.Pending)
            {
                return new OperationResult(false, _localizationService.GetString("HandoverRequestMustBePending"));
            }

            if (request.ToSdkSitId != currentSdkSitId)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToReject"));
            }

            request.Status = HandoverRequestStatus.Rejected;
            request.RespondedAtUtc = DateTime.UtcNow;
            request.DecisionComment = model.DecisionComment;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting handover request {RequestId}", requestId);
            return new OperationResult(false, _localizationService.GetString("ErrorRejectingHandoverRequest"));
        }
    }

    public async Task<OperationResult> CancelAsync(int requestId, int currentSdkSitId)
    {
        try
        {
            var request = await _dbContext.PlanRegistrationContentHandoverRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                return new OperationResult(false, _localizationService.GetString("HandoverRequestNotFound"));
            }

            if (request.Status != HandoverRequestStatus.Pending)
            {
                return new OperationResult(false, _localizationService.GetString("HandoverRequestMustBePending"));
            }

            if (request.FromSdkSitId != currentSdkSitId)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToCancel"));
            }

            request.Status = HandoverRequestStatus.Cancelled;
            request.RespondedAtUtc = DateTime.UtcNow;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling handover request {RequestId}", requestId);
            return new OperationResult(false, _localizationService.GetString("ErrorCancellingHandoverRequest"));
        }
    }

    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetInboxAsync(int toSdkSitId)
    {
        try
        {
            var requests = await _dbContext.PlanRegistrationContentHandoverRequests
                .Where(r => r.ToSdkSitId == toSdkSitId && r.Status == HandoverRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAtUtc)
                .ToListAsync();

            var models = requests.Select(MapToModel).ToList();
            return new OperationDataResult<List<ContentHandoverRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting handover inbox for worker {WorkerId}", toSdkSitId);
            return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                _localizationService.GetString("ErrorGettingHandoverRequests"));
        }
    }

    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetMineAsync(int fromSdkSitId)
    {
        try
        {
            var requests = await _dbContext.PlanRegistrationContentHandoverRequests
                .Where(r => r.FromSdkSitId == fromSdkSitId)
                .OrderByDescending(r => r.RequestedAtUtc)
                .ToListAsync();

            var models = requests.Select(MapToModel).ToList();
            return new OperationDataResult<List<ContentHandoverRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting handover requests for worker {WorkerId}", fromSdkSitId);
            return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                _localizationService.GetString("ErrorGettingHandoverRequests"));
        }
    }

    private void MoveContent(PlanRegistration source, PlanRegistration target)
    {
        // Move planning fields from source to target
        target.PlanText = source.PlanText;
        target.PlanHours = source.PlanHours;
        target.PlanHoursInSeconds = source.PlanHoursInSeconds;
        
        // Try to move planned shift fields if they exist
        try
        {
            var prType = source.GetType();
            
            for (int i = 1; i <= 5; i++)
            {
                var startProp = prType.GetProperty($"PlannedStartOfShift{i}");
                if (startProp != null && startProp.CanRead && startProp.CanWrite)
                {
                    var value = startProp.GetValue(source);
                    startProp.SetValue(target, value);
                }
                
                var endProp = prType.GetProperty($"PlannedEndOfShift{i}");
                if (endProp != null && endProp.CanRead && endProp.CanWrite)
                {
                    var value = endProp.GetValue(source);
                    endProp.SetValue(target, value);
                }
                
                var breakProp = prType.GetProperty($"PlannedBreakOfShift{i}");
                if (breakProp != null && breakProp.CanRead && breakProp.CanWrite)
                {
                    var value = breakProp.GetValue(source);
                    breakProp.SetValue(target, value);
                }
            }
            
            var isDoubleShiftProp = prType.GetProperty("IsDoubleShift");
            if (isDoubleShiftProp != null && isDoubleShiftProp.CanRead && isDoubleShiftProp.CanWrite)
            {
                var value = isDoubleShiftProp.GetValue(source);
                isDoubleShiftProp.SetValue(target, value);
            }
        }
        catch
        {
            // Ignore if properties don't exist
        }

        // Clear the moved fields on source
        source.PlanText = null;
        source.PlanHours = 0;
        source.PlanHoursInSeconds = 0;
        
        // Try to clear planned shift fields if they exist
        try
        {
            var prType = source.GetType();
            var plannedStartOfShift1Prop = prType.GetProperty("PlannedStartOfShift1");
            if (plannedStartOfShift1Prop != null && plannedStartOfShift1Prop.CanWrite)
            {
                plannedStartOfShift1Prop.SetValue(source, null);
            }
            
            var plannedEndOfShift1Prop = prType.GetProperty("PlannedEndOfShift1");
            if (plannedEndOfShift1Prop != null && plannedEndOfShift1Prop.CanWrite)
            {
                plannedEndOfShift1Prop.SetValue(source, null);
            }
            
            var plannedBreakOfShift1Prop = prType.GetProperty("PlannedBreakOfShift1");
            if (plannedBreakOfShift1Prop != null && plannedBreakOfShift1Prop.CanWrite)
            {
                plannedBreakOfShift1Prop.SetValue(source, null);
            }
            
            // Repeat for shifts 2-5
            for (int i = 2; i <= 5; i++)
            {
                var startProp = prType.GetProperty($"PlannedStartOfShift{i}");
                if (startProp != null && startProp.CanWrite)
                {
                    startProp.SetValue(source, null);
                }
                
                var endProp = prType.GetProperty($"PlannedEndOfShift{i}");
                if (endProp != null && endProp.CanWrite)
                {
                    endProp.SetValue(source, null);
                }
                
                var breakProp = prType.GetProperty($"PlannedBreakOfShift{i}");
                if (breakProp != null && breakProp.CanWrite)
                {
                    breakProp.SetValue(source, null);
                }
            }
            
            var isDoubleShiftProp = prType.GetProperty("IsDoubleShift");
            if (isDoubleShiftProp != null && isDoubleShiftProp.CanWrite)
            {
                isDoubleShiftProp.SetValue(source, false);
            }
        }
        catch
        {
            // Ignore if properties don't exist or can't be set
        }
    }

    private ContentHandoverRequestModel MapToModel(PlanRegistrationContentHandoverRequest request)
    {
        return new ContentHandoverRequestModel
        {
            Id = request.Id,
            FromSdkSitId = request.FromSdkSitId,
            ToSdkSitId = request.ToSdkSitId,
            Date = request.Date,
            FromPlanRegistrationId = request.FromPlanRegistrationId,
            ToPlanRegistrationId = request.ToPlanRegistrationId,
            Status = request.Status.ToString(),
            RequestedAtUtc = request.RequestedAtUtc,
            RespondedAtUtc = request.RespondedAtUtc,
            RequestComment = null, // Entity doesn't have RequestComment
            DecisionComment = request.DecisionComment
        };
    }
}
