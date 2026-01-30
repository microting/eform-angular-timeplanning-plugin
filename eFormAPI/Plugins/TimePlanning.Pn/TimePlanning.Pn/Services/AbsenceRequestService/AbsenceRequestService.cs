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
namespace TimePlanning.Pn.Services.AbsenceRequestService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.AbsenceRequest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanningLocalizationService;

public class AbsenceRequestService : IAbsenceRequestService
{
    private readonly ILogger<AbsenceRequestService> _logger;
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly ITimePlanningLocalizationService _localizationService;

    public AbsenceRequestService(
        ILogger<AbsenceRequestService> logger,
        TimePlanningPnDbContext dbContext,
        IUserService userService,
        ITimePlanningLocalizationService localizationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _userService = userService;
        _localizationService = localizationService;
    }

    public async Task<OperationDataResult<AbsenceRequestModel>> CreateAsync(AbsenceRequestCreateModel model)
    {
        try
        {
            // Normalize dates to date-only (midnight)
            var dateFrom = model.DateFrom.Date;
            var dateTo = model.DateTo.Date;

            // Validate date range
            if (dateTo < dateFrom)
            {
                return new OperationDataResult<AbsenceRequestModel>(false,
                    _localizationService.GetString("DateToMustBeGreaterThanOrEqualToDateFrom"));
            }

            // Check for overlapping pending requests for the same worker
            var hasOverlap = await _dbContext.AbsenceRequests
                .AnyAsync(ar => ar.RequestedBySdkSitId == model.RequestedBySdkSitId
                                && ar.Status == AbsenceRequestStatus.Pending
                                && ar.DateFrom <= dateTo
                                && ar.DateTo >= dateFrom);

            if (hasOverlap)
            {
                return new OperationDataResult<AbsenceRequestModel>(false,
                    _localizationService.GetString("OverlappingAbsenceRequestExists"));
            }

            // Create absence request
            var absenceRequest = new AbsenceRequest
            {
                RequestedBySdkSitId = model.RequestedBySdkSitId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Status = AbsenceRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow,
                RequestComment = model.RequestComment,
                CreatedByUserId = _userService.UserId,
                UpdatedByUserId = _userService.UserId
            };

            await absenceRequest.Create(_dbContext);

            // Create absence request days for each date in range
            var currentDate = dateFrom;
            while (currentDate <= dateTo)
            {
                var absenceDay = new AbsenceRequestDay
                {
                    AbsenceRequestId = absenceRequest.Id,
                    Date = currentDate,
                    MessageId = model.MessageId,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId
                };
                await absenceDay.Create(_dbContext);
                currentDate = currentDate.AddDays(1);
            }

            // Load the created request with days for response
            var createdRequest = await _dbContext.AbsenceRequests
                .Include(ar => ar.Days)
                .FirstAsync(ar => ar.Id == absenceRequest.Id);

            var resultModel = MapToModel(createdRequest);
            return new OperationDataResult<AbsenceRequestModel>(true, resultModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating absence request");
            return new OperationDataResult<AbsenceRequestModel>(false,
                _localizationService.GetString("ErrorCreatingAbsenceRequest"));
        }
    }

    public async Task<OperationResult> ApproveAsync(int absenceRequestId, AbsenceRequestDecisionModel model)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Load request with days
            var request = await _dbContext.AbsenceRequests
                .Include(ar => ar.Days)
                .FirstOrDefaultAsync(ar => ar.Id == absenceRequestId);

            if (request == null)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestNotFound"));
            }

            if (request.Status != AbsenceRequestStatus.Pending)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestMustBePending"));
            }

            // Update request status
            request.Status = AbsenceRequestStatus.Approved;
            request.DecidedBySdkSitId = model.ManagerSdkSitId;
            request.DecidedAtUtc = DateTime.UtcNow;
            request.DecisionComment = model.DecisionComment;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            // Apply absence to each day's PlanRegistration
            foreach (var day in request.Days!)
            {
                await ApplyAbsenceToPlanRegistration(request, day);
            }

            await transaction.CommitAsync();
            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error approving absence request {RequestId}", absenceRequestId);
            return new OperationResult(false, _localizationService.GetString("ErrorApprovingAbsenceRequest"));
        }
    }

    public async Task<OperationResult> RejectAsync(int absenceRequestId, AbsenceRequestDecisionModel model)
    {
        try
        {
            var request = await _dbContext.AbsenceRequests
                .FirstOrDefaultAsync(ar => ar.Id == absenceRequestId);

            if (request == null)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestNotFound"));
            }

            if (request.Status != AbsenceRequestStatus.Pending)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestMustBePending"));
            }

            request.Status = AbsenceRequestStatus.Rejected;
            request.DecidedBySdkSitId = model.ManagerSdkSitId;
            request.DecidedAtUtc = DateTime.UtcNow;
            request.DecisionComment = model.DecisionComment;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting absence request {RequestId}", absenceRequestId);
            return new OperationResult(false, _localizationService.GetString("ErrorRejectingAbsenceRequest"));
        }
    }

    public async Task<OperationResult> CancelAsync(int absenceRequestId, int requestedBySdkSitId)
    {
        try
        {
            var request = await _dbContext.AbsenceRequests
                .FirstOrDefaultAsync(ar => ar.Id == absenceRequestId);

            if (request == null)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestNotFound"));
            }

            if (request.Status != AbsenceRequestStatus.Pending)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestMustBePending"));
            }

            if (request.RequestedBySdkSitId != requestedBySdkSitId)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToCancel"));
            }

            request.Status = AbsenceRequestStatus.Cancelled;
            request.DecidedAtUtc = DateTime.UtcNow;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling absence request {RequestId}", absenceRequestId);
            return new OperationResult(false, _localizationService.GetString("ErrorCancellingAbsenceRequest"));
        }
    }

    public async Task<OperationDataResult<List<AbsenceRequestModel>>> GetInboxAsync(int managerSdkSitId)
    {
        try
        {
            var requests = await _dbContext.AbsenceRequests
                .Include(ar => ar.Days)
                .Where(ar => ar.Status == AbsenceRequestStatus.Pending)
                .OrderByDescending(ar => ar.RequestedAtUtc)
                .ToListAsync();

            var models = requests.Select(MapToModel).ToList();
            return new OperationDataResult<List<AbsenceRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting absence request inbox for manager {ManagerId}", managerSdkSitId);
            return new OperationDataResult<List<AbsenceRequestModel>>(false,
                _localizationService.GetString("ErrorGettingAbsenceRequests"));
        }
    }

    public async Task<OperationDataResult<List<AbsenceRequestModel>>> GetMineAsync(int requestedBySdkSitId)
    {
        try
        {
            var requests = await _dbContext.AbsenceRequests
                .Include(ar => ar.Days)
                .Where(ar => ar.RequestedBySdkSitId == requestedBySdkSitId)
                .OrderByDescending(ar => ar.RequestedAtUtc)
                .ToListAsync();

            var models = requests.Select(MapToModel).ToList();
            return new OperationDataResult<List<AbsenceRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting absence requests for worker {WorkerId}", requestedBySdkSitId);
            return new OperationDataResult<List<AbsenceRequestModel>>(false,
                _localizationService.GetString("ErrorGettingAbsenceRequests"));
        }
    }

    private async Task ApplyAbsenceToPlanRegistration(AbsenceRequest request, AbsenceRequestDay day)
    {
        // Find or create PlanRegistration for this worker and date
        var planRegistration = await _dbContext.PlanRegistrations
            .FirstOrDefaultAsync(pr => pr.SdkSitId == request.RequestedBySdkSitId
                                       && pr.Date == day.Date);

        if (planRegistration == null)
        {
            // Create new PlanRegistration with minimal defaults
            planRegistration = new PlanRegistration
            {
                Date = day.Date,
                SdkSitId = request.RequestedBySdkSitId,
                PlanHours = 0,
                NettoHours = 0,
                PaiedOutFlex = 0,
                Pause1Id = 0,
                Pause2Id = 0,
                Pause3Id = 0,
                Pause4Id = 0,
                Pause5Id = 0,
                Start1Id = 0,
                Stop1Id = 0,
                Start2Id = 0,
                Stop2Id = 0,
                Start3Id = 0,
                Stop3Id = 0,
                Start4Id = 0,
                Stop4Id = 0,
                Start5Id = 0,
                Stop5Id = 0,
                CreatedByUserId = _userService.UserId,
                UpdatedByUserId = _userService.UserId
            };
            await planRegistration.Create(_dbContext);
        }

        // Load the message to determine absence type
        var message = await _dbContext.Messages.FindAsync(day.MessageId);
        if (message != null)
        {
            ApplyAbsenceFromMessage(planRegistration, message);
        }

        // Set absence audit fields if they exist
        planRegistration.MessageId = day.MessageId;
        planRegistration.UpdatedByUserId = _userService.UserId;

        // Try to set audit fields if they exist on the entity
        try
        {
            var prType = planRegistration.GetType();
            var absenceRequestIdProp = prType.GetProperty("AbsenceRequestId");
            if (absenceRequestIdProp != null && absenceRequestIdProp.CanWrite)
            {
                absenceRequestIdProp.SetValue(planRegistration, request.Id);
            }

            var absenceApprovedAtProp = prType.GetProperty("AbsenceApprovedAtUtc");
            if (absenceApprovedAtProp != null && absenceApprovedAtProp.CanWrite)
            {
                absenceApprovedAtProp.SetValue(planRegistration, DateTime.UtcNow);
            }

            var absenceApprovedByProp = prType.GetProperty("AbsenceApprovedBySdkSitId");
            if (absenceApprovedByProp != null && absenceApprovedByProp.CanWrite)
            {
                absenceApprovedByProp.SetValue(planRegistration, request.DecidedBySdkSitId);
            }

            var absenceMessageIdProp = prType.GetProperty("AbsenceMessageId");
            if (absenceMessageIdProp != null && absenceMessageIdProp.CanWrite)
            {
                absenceMessageIdProp.SetValue(planRegistration, day.MessageId);
            }
        }
        catch
        {
            // Ignore if audit fields don't exist
        }

        await planRegistration.Update(_dbContext);

        // Recalculate PlanRegistration using helper
        var assignedSite = await _dbContext.AssignedSites
            .FirstOrDefaultAsync(a => a.SiteId == planRegistration.SdkSitId);
        
        if (assignedSite != null)
        {
            PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);
            await planRegistration.Update(_dbContext);
        }
    }

    private void ApplyAbsenceFromMessage(PlanRegistration pr, Message message)
    {
        // Clear all absence flags first
        pr.OnVacation = false;
        pr.Sick = false;
        pr.OtherAllowedAbsence = false;
        pr.AbsenceWithoutPermission = false;

        // Map message to absence flag based on message name
        switch (message.Name)
        {
            case "Vacation":
            case "VacationDayOff":
            case "TimeOff":
                pr.OnVacation = true;
                break;
            case "Sick":
            case "Children1stSick":
            case "Children2ndSick":
                pr.Sick = true;
                break;
            case "DayOff":
            case "Course":
            case "LeaveOfAbsence":
            case "Maternity":
            case "Holiday":
                pr.OtherAllowedAbsence = true;
                break;
            default:
                // For unknown message types, use OtherAllowedAbsence
                pr.OtherAllowedAbsence = true;
                break;
        }
    }

    private AbsenceRequestModel MapToModel(AbsenceRequest request)
    {
        return new AbsenceRequestModel
        {
            Id = request.Id,
            RequestedBySdkSitId = request.RequestedBySdkSitId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Status = request.Status.ToString(),
            RequestedAtUtc = request.RequestedAtUtc,
            DecidedAtUtc = request.DecidedAtUtc,
            DecidedBySdkSitId = request.DecidedBySdkSitId,
            RequestComment = request.RequestComment,
            DecisionComment = request.DecisionComment,
            Days = request.Days?.Select(d => new AbsenceRequestDayModel
            {
                Date = d.Date,
                MessageId = d.MessageId
            }).ToList() ?? []
        };
    }
}
