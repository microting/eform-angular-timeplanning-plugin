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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanningLocalizationService;

public class AbsenceRequestService : IAbsenceRequestService
{
    private readonly ILogger<AbsenceRequestService> _logger;
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly ITimePlanningLocalizationService _localizationService;
    private readonly IEFormCoreService _coreService;
    private readonly BaseDbContext _baseDbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // How long a decision in progress holds a request. Only matters when the
    // decider died mid-way and never released it; long enough for any approve.
    private static readonly TimeSpan DecisionClaimTimeout = TimeSpan.FromMinutes(10);

    public AbsenceRequestService(
        ILogger<AbsenceRequestService> logger,
        TimePlanningPnDbContext dbContext,
        IUserService userService,
        ITimePlanningLocalizationService localizationService,
        IEFormCoreService coreService,
        BaseDbContext baseDbContext,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _dbContext = dbContext;
        _userService = userService;
        _localizationService = localizationService;
        _coreService = coreService;
        _baseDbContext = baseDbContext;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Resolves the authenticated caller's SDK site id (MicrotingUid) from
    /// the JWT. Returns 0 if the user has no worker/site record. Callers
    /// should treat 0 as "no inbox visibility".
    /// </summary>
    private async Task<int> ResolveCallerSdkSiteIdAsync()
    {
        var currentUserAsync = await _userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            return 0;
        }
        var currentUser = _baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);

        var sdkCore = await _coreService.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();

        var worker = await sdkDbContext.Workers
            .Include(x => x.SiteWorkers)
            .ThenInclude(x => x.Site)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

        if (worker == null || worker.SiteWorkers.Count == 0)
        {
            return 0;
        }
        // Deterministically resolve the active site (excludes removed
        // SiteWorker/Site rows, ordered by SiteWorker.Id). All-removed -> 0.
        return worker.ResolveActiveSdkSiteId() ?? 0;
    }

    /// <summary>
    /// The authenticated caller's SDK site when it manages the requester's
    /// site, else null. The decider comes from the JWT, never from the body:
    /// a client-supplied manager id could name any manager.
    /// </summary>
    private async Task<int?> ResolveDecidingManagerAsync(int requesterSdkSitId)
    {
        var callerSdkSitId = await ResolveCallerSdkSiteIdAsync();
        if (callerSdkSitId == 0)
        {
            return null;
        }

        var managerSiteIds = await GetManagerSiteIdsForWorkerAsync(requesterSdkSitId);
        return managerSiteIds.Contains(callerSdkSitId) ? callerSdkSitId : null;
    }

    /// <summary>
    /// Atomically claims a Pending request for one decision by stamping
    /// DecidedAtUtc; the status stays Pending, so a failed decision leaves it
    /// Pending. The final status save overwrites the stamp; a failure releases
    /// it. Returns the stamp, or null when another decision holds or made it.
    /// </summary>
    private async Task<DateTime?> TryClaimPendingAsync(int absenceRequestId)
    {
        var now = DateTime.UtcNow;
        // Whole seconds: datetime(6) drops ticks, and the release matches the stamp exactly.
        var stamp = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var staleBefore = stamp - DecisionClaimTimeout;
        var claimed = await _dbContext.AbsenceRequests
            .Where(ar => ar.Id == absenceRequestId
                         && ar.Status == AbsenceRequestStatus.Pending
                         && (ar.DecidedAtUtc == null || ar.DecidedAtUtc < staleBefore))
            .ExecuteUpdateAsync(setters => setters.SetProperty(ar => ar.DecidedAtUtc, stamp));
        return claimed == 1 ? stamp : null;
    }

    /// <summary>
    /// Releases a claim whose decision failed, so the request can be decided
    /// again at once. A failed release only delays that until the claim times out.
    /// </summary>
    private async Task ReleaseClaimAsync(int absenceRequestId, DateTime? stamp)
    {
        if (stamp == null)
        {
            return;
        }

        try
        {
            await _dbContext.AbsenceRequests
                .Where(ar => ar.Id == absenceRequestId
                             && ar.Status == AbsenceRequestStatus.Pending
                             && ar.DecidedAtUtc == stamp)
                .ExecuteUpdateAsync(setters => setters.SetProperty(ar => ar.DecidedAtUtc, (DateTime?)null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not release the decision claim on absence request {RequestId}; it can be decided again after {Timeout}",
                absenceRequestId, DecisionClaimTimeout);
        }
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

            // A request touching a locked day could never be approved (Approve
            // refuses it), so it is refused here instead of left pending. Locked
            // days are a prefix of the calendar, so the range touches one
            // exactly when its first day is locked, and that day is the earliest
            // blocking one Approve would name too.
            var lockedThrough = await DayLockHelper.LockedThroughAsync(_dbContext, model.RequestedBySdkSitId);
            if (DayLockHelper.IsLocked(lockedThrough, dateFrom))
            {
                return new OperationDataResult<AbsenceRequestModel>(false, _localizationService.GetString(
                    await DayLockHelper.LockedMessageKeyAsync(_dbContext, model.RequestedBySdkSitId, dateFrom)));
            }

            // Overlap with a pending OR approved request is refused: approving a
            // second request over approved days would re-tag them with its own id,
            // and rejecting/cancelling it would then clear the earlier absence.
            var hasOverlap = await _dbContext.AbsenceRequests
                .AnyAsync(ar => ar.RequestedBySdkSitId == model.RequestedBySdkSitId
                                && (ar.Status == AbsenceRequestStatus.Pending
                                    || ar.Status == AbsenceRequestStatus.Approved)
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

            // Resolve the managers while the request scope is alive; the push itself
            // is fire-and-forget and runs in its own DI scope.
            var managerSiteIds = await GetManagerSiteIdsForWorkerAsync(model.RequestedBySdkSitId);
            var createdRequestId = createdRequest.Id;
            _ = Task.Run(async () =>
            {
                foreach (var managerSiteId in managerSiteIds)
                {
                    try
                    {
                        await SendToSiteInOwnScopeAsync(
                            managerSiteId,
                            "New absence request",
                            "A worker has requested time off",
                            new Dictionary<string, string>
                            {
                                { "type", "absence_created" },
                                { "absenceRequestId", createdRequestId.ToString() }
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending push notification for absence request creation");
                    }
                }
            });

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
        DateTime? claim = null;
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

            var managerSdkSitId = await ResolveDecidingManagerAsync(request.RequestedBySdkSitId);
            if (managerSdkSitId == null)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToApprove"));
            }

            // Checked before anything is persisted, so a locked day refuses the
            // whole request instead of failing part-way through the per-day
            // loop below. One boundary query for the worker. The earliest
            // locked day blocks; the message says what that day is.
            var lockedThrough = await DayLockHelper.LockedThroughAsync(_dbContext, request.RequestedBySdkSitId);
            var blockingDay = request.Days!
                .OrderBy(day => day.Date)
                .FirstOrDefault(day => DayLockHelper.IsLocked(lockedThrough, day.Date));
            if (blockingDay != null)
            {
                return new OperationResult(false, _localizationService.GetString(
                    await DayLockHelper.LockedMessageKeyAsync(
                        _dbContext, request.RequestedBySdkSitId, blockingDay.Date)));
            }

            claim = await TryClaimPendingAsync(absenceRequestId);
            if (claim == null)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestMustBePending"));
            }

            // Apply changes without an explicit transaction.
            // NOTE: Update() methods internally handle persistence, and using
            // an explicit BeginTransactionAsync here was causing a silent
            // rollback in the test environment (matches the workaround applied
            // in ContentHandoverService.AcceptAsync).
            // So the days are applied first and the request is marked Approved only
            // after every day is saved; a failure leaves it Pending, and re-approving
            // is idempotent (find-or-create, then overwrite). Don't touch the request
            // before the loop: each Update() saves the whole context.
            foreach (var day in request.Days!.OrderBy(d => d.Date))
            {
                await ApplyAbsenceToPlanRegistration(request, day, managerSdkSitId.Value);
            }

            request.Status = AbsenceRequestStatus.Approved;
            request.DecidedBySdkSitId = managerSdkSitId;
            request.DecidedAtUtc = DateTime.UtcNow;
            request.DecisionComment = model.DecisionComment;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            // R4: approval can create rows mid-history with an empty balance.
            // Carry the worker's balance from the earliest approved day to their
            // last row, the new rows included. Every day was checked unlocked
            // above. Tolerates a worker without an AssignedSite.
            if (request.Days!.Count > 0)
            {
                await CarryFlexForwardAsync(absenceRequestId, request.RequestedBySdkSitId,
                    request.Days.Min(d => d.Date), "approved");
            }

            // Fire-and-forget push to requester
            var requesterSdkSitId = request.RequestedBySdkSitId;
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendToSiteInOwnScopeAsync(
                        requesterSdkSitId,
                        "Absence request approved",
                        "Your absence request has been approved",
                        new Dictionary<string, string>
                        {
                            { "type", "absence_decided" },
                            { "action", "approved" },
                            { "absenceRequestId", absenceRequestId.ToString() }
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending push notification for absence approval");
                }
            });

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            // A refused save stays tracked and would rethrow on the next
            // SaveChanges on this context, so drop it with the rest.
            _dbContext.ChangeTracker.Clear();
            await ReleaseClaimAsync(absenceRequestId, claim);
            // Days already saved are re-applied on the next approve.
            _logger.LogError(ex,
                "Error approving absence request {RequestId}; the request is left Pending",
                absenceRequestId);
            return new OperationResult(false, _localizationService.GetString("ErrorApprovingAbsenceRequest"));
        }
    }

    public async Task<OperationResult> RejectAsync(int absenceRequestId, AbsenceRequestDecisionModel model)
    {
        DateTime? claim = null;
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

            var managerSdkSitId = await ResolveDecidingManagerAsync(request.RequestedBySdkSitId);
            if (managerSdkSitId == null)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToReject"));
            }

            claim = await TryClaimPendingAsync(absenceRequestId);
            if (claim == null)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestMustBePending"));
            }

            // Undo days left flagged by a failed approve before saving the status, so a failure here keeps it Pending.
            var walkFrom = await ClearDaysLeftFlaggedAsync(request);

            request.Status = AbsenceRequestStatus.Rejected;
            request.DecidedBySdkSitId = managerSdkSitId;
            request.DecidedAtUtc = DateTime.UtcNow;
            request.DecisionComment = model.DecisionComment;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            if (walkFrom.HasValue)
            {
                await CarryFlexForwardAsync(absenceRequestId, request.RequestedBySdkSitId, walkFrom.Value, "rejected");
            }

            // Fire-and-forget push to requester
            var requesterSdkSitId = request.RequestedBySdkSitId;
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendToSiteInOwnScopeAsync(
                        requesterSdkSitId,
                        "Absence request rejected",
                        "Your absence request has been rejected",
                        new Dictionary<string, string>
                        {
                            { "type", "absence_decided" },
                            { "action", "rejected" },
                            { "absenceRequestId", absenceRequestId.ToString() }
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending push notification for absence rejection");
                }
            });

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _dbContext.ChangeTracker.Clear();
            await ReleaseClaimAsync(absenceRequestId, claim);
            _logger.LogError(ex, "Error rejecting absence request {RequestId}", absenceRequestId);
            return new OperationResult(false, _localizationService.GetString("ErrorRejectingAbsenceRequest"));
        }
    }

    /// <summary>
    /// SDK site ids of the managers whose managing tags match one of the worker's
    /// site tags. Removed managing tags and removed AssignedSites are ignored.
    /// Returns an empty list (and logs) on failure: creating the request never
    /// fails because of the notification lookup, and a decision is refused.
    /// </summary>
    private async Task<List<int>> GetManagerSiteIdsForWorkerAsync(int workerSdkSitId)
    {
        try
        {
            var sdkCore = await _coreService.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var workerTagIds = await sdkDbContext.SiteTags
                .Where(x => x.Site.MicrotingUid == workerSdkSitId
                            && x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => (int)x.TagId!)
                .ToListAsync();

            return await _dbContext.AssignedSiteManagingTags
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed
                            && x.AssignedSite!.WorkflowState != Constants.WorkflowStates.Removed
                            && workerTagIds.Contains(x.TagId))
                .Select(x => x.AssignedSite!.SiteId)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving managers of worker {SdkSitId} for an absence request", workerSdkSitId);
            return [];
        }
    }

    // Owns a short-lived DI scope per send: the fire-and-forget callers outlive
    // the request scope, so they must not use request-scoped services.
    internal async Task SendToSiteInOwnScopeAsync(
        int targetSdkSitId, string title, string body, Dictionary<string, string> data)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        await pushService.SendToSiteAsync(targetSdkSitId, title, body, data);
    }

    public async Task<OperationResult> CancelAsync(int absenceRequestId)
    {
        DateTime? claim = null;
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

            // The requester is the authenticated caller, never a client-supplied id.
            var callerSdkSitId = await ResolveCallerSdkSiteIdAsync();
            if (callerSdkSitId == 0 || request.RequestedBySdkSitId != callerSdkSitId)
            {
                return new OperationResult(false, _localizationService.GetString("UnauthorizedToCancel"));
            }

            claim = await TryClaimPendingAsync(absenceRequestId);
            if (claim == null)
            {
                return new OperationResult(false, _localizationService.GetString("AbsenceRequestMustBePending"));
            }

            // Undo days left flagged by a failed approve before saving the status, so a failure here keeps it Pending.
            var walkFrom = await ClearDaysLeftFlaggedAsync(request);

            request.Status = AbsenceRequestStatus.Cancelled;
            request.DecidedAtUtc = DateTime.UtcNow;
            request.UpdatedByUserId = _userService.UserId;
            await request.Update(_dbContext);

            if (walkFrom.HasValue)
            {
                await CarryFlexForwardAsync(absenceRequestId, request.RequestedBySdkSitId, walkFrom.Value, "cancelled");
            }

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _dbContext.ChangeTracker.Clear();
            await ReleaseClaimAsync(absenceRequestId, claim);
            _logger.LogError(ex, "Error cancelling absence request {RequestId}", absenceRequestId);
            return new OperationResult(false, _localizationService.GetString("ErrorCancellingAbsenceRequest"));
        }
    }

    public async Task<OperationDataResult<List<AbsenceRequestModel>>> GetInboxAsync()
    {
        try
        {
            // Resolve caller's SDK site from the JWT instead of trusting a
            // client-supplied value. Returns empty inbox if unknown.
            var managerSdkSitId = await ResolveCallerSdkSiteIdAsync();
            if (managerSdkSitId == 0)
            {
                return new OperationDataResult<List<AbsenceRequestModel>>(true, new List<AbsenceRequestModel>());
            }

            // Check if the requesting user is a manager
            var assignedSite = await _dbContext.AssignedSites
                .Where(a => a.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.SiteId == managerSdkSitId);

            if (assignedSite == null || !assignedSite.IsManager)
            {
                return new OperationDataResult<List<AbsenceRequestModel>>(true, new List<AbsenceRequestModel>());
            }

            // Get the manager's managing tag IDs
            var managingTagIds = await _dbContext.AssignedSiteManagingTags
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.AssignedSiteId == assignedSite.Id)
                .Select(x => x.TagId)
                .ToListAsync();

            // Find all site IDs that share any of the manager's tags
            var sdkCore = await _coreService.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var managedSiteIds = await sdkDbContext.SiteTags
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => managingTagIds.Contains((int)x.TagId!))
                .Select(x => x.Site.MicrotingUid)
                .Distinct()
                .ToListAsync();

            var requests = await _dbContext.AbsenceRequests
                .Include(ar => ar.Days)
                .Where(ar => ar.Status == AbsenceRequestStatus.Pending)
                .Where(ar => managedSiteIds.Contains(ar.RequestedBySdkSitId))
                .OrderByDescending(ar => ar.RequestedAtUtc)
                .ToListAsync();

            var models = requests.Select(MapToModel).ToList();
            return new OperationDataResult<List<AbsenceRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting absence request inbox for authenticated caller");
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

    public async Task<OperationDataResult<List<AbsenceRequestModel>>> GetAllAsync(
        string? status, string? fromDate, string? toDate, int? sdkSiteId,
        int page = 0, int pageSize = 100)
    {
        try
        {
            var query = _dbContext.AbsenceRequests
                .Include(ar => ar.Days)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<AbsenceRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(ar => ar.Status == parsedStatus);
            }

            // Filter by date range
            if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var from))
            {
                var fromUtc = from.Date;
                query = query.Where(ar => ar.DateTo >= fromUtc);
            }

            if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var to))
            {
                var toUtc = to.Date;
                query = query.Where(ar => ar.DateFrom <= toUtc);
            }

            // Filter by sdkSiteId — matches RequestedBySdkSitId
            if (sdkSiteId.HasValue)
            {
                query = query.Where(ar => ar.RequestedBySdkSitId == sdkSiteId.Value);
            }

            var requests = await query
                .OrderByDescending(ar => ar.RequestedAtUtc)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Resolve worker names from SDK sites
            var allSiteIds = requests
                .Select(ar => ar.RequestedBySdkSitId)
                .Union(requests
                    .Where(ar => ar.DecidedBySdkSitId.HasValue)
                    .Select(ar => ar.DecidedBySdkSitId!.Value))
                .Distinct()
                .ToList();

            var siteNameLookup = new Dictionary<int, string>();
            if (allSiteIds.Count > 0)
            {
                var sdkCore = await _coreService.GetCore();
                var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
                siteNameLookup = await sdkDbContext.Sites
                    .Where(s => s.MicrotingUid.HasValue && allSiteIds.Contains(s.MicrotingUid.Value))
                    .Select(s => new { MicrotingUid = s.MicrotingUid!.Value, s.Name })
                    .ToDictionaryAsync(s => s.MicrotingUid, s => s.Name ?? string.Empty);
            }

            var models = requests.Select(ar =>
            {
                var model = MapToModel(ar);
                model.RequestedByWorkerName = siteNameLookup.GetValueOrDefault(ar.RequestedBySdkSitId);
                if (ar.DecidedBySdkSitId.HasValue)
                {
                    model.DecidedByWorkerName = siteNameLookup.GetValueOrDefault(ar.DecidedBySdkSitId.Value);
                }
                return model;
            }).ToList();

            return new OperationDataResult<List<AbsenceRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all absence requests");
            return new OperationDataResult<List<AbsenceRequestModel>>(false,
                _localizationService.GetString("ErrorGettingAbsenceRequests"));
        }
    }

    private async Task ApplyAbsenceToPlanRegistration(AbsenceRequest request, AbsenceRequestDay day,
        int managerSdkSitId)
    {
        try
        {
            // Find or create PlanRegistration for this worker and date
            var planRegistration = await _dbContext.PlanRegistrations
                .FirstOrDefaultAsync(pr => pr.SdkSitId == request.RequestedBySdkSitId
                                           && pr.Date == day.Date
                                           && pr.WorkflowState != Constants.WorkflowStates.Removed);

            if (planRegistration == null)
            {
                // Create new PlanRegistration with minimal defaults
                planRegistration = new PlanRegistration
                {
                    Date = day.Date,
                    SdkSitId = request.RequestedBySdkSitId,
                    PlanHours = 0,
                    PlanHoursInSeconds = 0,
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
            var message = await _dbContext.Messages
                .FirstOrDefaultAsync(m => m.Id == day.MessageId);
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
                    absenceApprovedByProp.SetValue(planRegistration, managerSdkSitId);
                }

                var absenceMessageIdProp = prType.GetProperty("AbsenceMessageId");
                if (absenceMessageIdProp != null && absenceMessageIdProp.CanWrite)
                {
                    absenceMessageIdProp.SetValue(planRegistration, day.MessageId);
                }
            }
            catch (Exception ex)
            {
                // Log but ignore if audit fields don't exist
                _logger.LogWarning(ex, "Could not set audit fields on PlanRegistration");
            }

            await planRegistration.Update(_dbContext);

            // Recalculate PlanRegistration using helper - only if AssignedSite exists
            var assignedSite = await _dbContext.AssignedSites
                .Where(a => a.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.SiteId == planRegistration.SdkSitId);
            
            if (assignedSite != null)
            {
                try
                {
                    PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);
                    await planRegistration.Update(_dbContext);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate pause for PlanRegistration {Id}", planRegistration.Id);
                    // Continue without recalculation if it fails
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying absence to PlanRegistration for day {Date}", day.Date);
            throw;
        }
    }

    /// <summary>
    /// Carries the worker's flex balance forward from <paramref name="from"/>.
    /// Runs after the request is saved, so a failed walk must not turn the
    /// decision into an error: log it for manual reconciliation and carry on
    /// (same as the handover walk). Tolerates a worker without an AssignedSite.
    /// </summary>
    private async Task CarryFlexForwardAsync(int absenceRequestId, int sdkSitId, DateTime from, string verb)
    {
        try
        {
            var assignedSite = await _dbContext.AssignedSites
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSitId);
            var changed = await FlexChainRecompute.RunForwardAsync(_dbContext, assignedSite, sdkSitId, from);
            _logger.LogInformation(
                "[Absence] Request {RequestId} {Verb}: flex carried for sdkSitId={SdkSitId} from {From:yyyy-MM-dd}, {Changed} row(s) changed",
                absenceRequestId, verb, sdkSitId, from, changed);
        }
        catch (Exception walkEx)
        {
            // Drop the failed walk's pending row changes so no later
            // SaveChanges on this context can flush a half-carried chain.
            // Nothing after the walk uses a tracked entity: the push uses ids.
            _dbContext.ChangeTracker.Clear();
            _logger.LogError(walkEx,
                "Absence request {Id} {Verb} and saved, but carrying the flex balance forward FAILED for worker {SdkSitId} from {From:yyyy-MM-dd} — MANUAL RECONCILIATION required (re-save the day or re-run the walk)",
                absenceRequestId, verb, sdkSitId, from);
        }
    }

    /// <summary>
    /// Undoes what <see cref="ApplyAbsenceToPlanRegistration"/> set on the
    /// unlocked days a failed approve left tagged with this request: the absence
    /// flags, MessageId and the approval metadata are reset, plan hours and
    /// shifts are untouched. Returns the earliest cleared date for the flex walk, or null.
    /// The walk itself runs after the status is saved: its catch clears the
    /// change tracker, which would silently drop an unsaved status change.
    /// </summary>
    private async Task<DateTime?> ClearDaysLeftFlaggedAsync(AbsenceRequest request)
    {
        var tagged = await _dbContext.PlanRegistrations
            .Where(pr => pr.AbsenceRequestId == request.Id
                         && pr.SdkSitId == request.RequestedBySdkSitId
                         && pr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        if (tagged.Count == 0)
        {
            return null;
        }

        var lockedThrough = await DayLockHelper.LockedThroughAsync(_dbContext, request.RequestedBySdkSitId);
        DateTime? earliest = null;
        foreach (var pr in tagged.OrderBy(pr => pr.Date))
        {
            if (DayLockHelper.IsLocked(lockedThrough, pr.Date))
            {
                _logger.LogError(
                    "Absence request {RequestId} is not approved, but day {Date:yyyy-MM-dd} of worker {SdkSitId} is locked and stays flagged for it — MANUAL RECONCILIATION required (payroll counts the absence)",
                    request.Id, pr.Date, request.RequestedBySdkSitId);
                continue;
            }

            pr.OnVacation = false;
            pr.Sick = false;
            pr.OtherAllowedAbsence = false;
            pr.AbsenceWithoutPermission = false;
            pr.MessageId = null;
            pr.AbsenceRequestId = null;
            pr.AbsenceApprovedAtUtc = null;
            pr.AbsenceApprovedBySdkSitId = null;
            pr.AbsenceMessageId = null;
            pr.UpdatedByUserId = _userService.UserId;
            await pr.Update(_dbContext);
            earliest ??= pr.Date;
        }

        return earliest;
    }

    private void ApplyAbsenceFromMessage(PlanRegistration pr, Message message)
    {
        if (!AbsenceMessageFlags.Apply(pr, message.Id))
        {
            _logger.LogWarning(
                "Unknown absence message {MessageId} ({MessageName}); applying OtherAllowedAbsence",
                message.Id, message.Name);
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
            // A Pending row's DecidedAtUtc is only a decision claim, not a decision.
            DecidedAtUtc = request.Status == AbsenceRequestStatus.Pending ? null : request.DecidedAtUtc,
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
