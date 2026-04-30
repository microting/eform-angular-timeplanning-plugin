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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.ContentHandover;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanningLocalizationService;

public class ContentHandoverService : IContentHandoverService
{
    private readonly ILogger<ContentHandoverService> _logger;
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly ITimePlanningLocalizationService _localizationService;
    private readonly IEFormCoreService _core;
    private readonly BaseDbContext _baseDbContext;
    private readonly IPushNotificationService _pushNotificationService;

    public ContentHandoverService(
        ILogger<ContentHandoverService> logger,
        TimePlanningPnDbContext dbContext,
        IUserService userService,
        ITimePlanningLocalizationService localizationService,
        IEFormCoreService core,
        BaseDbContext baseDbContext,
        IPushNotificationService pushNotificationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _userService = userService;
        _localizationService = localizationService;
        _core = core;
        _baseDbContext = baseDbContext;
        _pushNotificationService = pushNotificationService;
    }

    // Danish-locale, case-insensitive comparer used to sort the handover-eligible
    // coworker list. Exposed publicly so the regression test can pin the
    // canonical da-DK collation order (Æ/Ø/Å come AFTER Z, in that order).
    public static readonly StringComparer HandoverCoworkerNameComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), ignoreCase: true);

    public Task<OperationDataResult<List<HandoverCoworkerModel>>> GetHandoverEligibleCoworkersAsync(DateTime date)
    {
        return GetHandoverEligibleCoworkersAsync(date, new List<int>());
    }

    public async Task<OperationDataResult<List<HandoverCoworkerModel>>> GetHandoverEligibleCoworkersAsync(DateTime date, List<int> shiftIndices)
    {
        try
        {
            var sdkCore = await _core.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();

            var currentUserAsync = await _userService.GetCurrentUserAsync();
            var currentUser = _baseDbContext.Users
                .Single(x => x.Id == currentUserAsync.Id);

            var worker = await sdkDbContext.Workers
                .Include(x => x.SiteWorkers)
                .ThenInclude(x => x.Site)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

            if (worker == null)
            {
                SentrySdk.CaptureMessage($"Worker with email {currentUser.Email} not found");
                return new OperationDataResult<List<HandoverCoworkerModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            }

            var callerSite = worker.SiteWorkers.First().Site;

            var callerTagIds = await sdkDbContext.SiteTags
                .Where(x => x.SiteId == callerSite.Id
                            && x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => (int)x.TagId!)
                .ToListAsync();

            if (callerTagIds.Count == 0)
            {
                return new OperationDataResult<List<HandoverCoworkerModel>>(true, new List<HandoverCoworkerModel>());
            }

            var candidateRaw = await sdkDbContext.SiteTags
                .Include(x => x.Site)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed
                            && x.SiteId != callerSite.Id
                            && callerTagIds.Contains((int)x.TagId!))
                .Select(x => new { x.Site.MicrotingUid, SiteName = x.Site.Name })
                .Distinct()
                .ToListAsync();

            var candidateSites = candidateRaw
                .Where(x => x.MicrotingUid.HasValue)
                .Select(x => new { MicrotingUid = x.MicrotingUid!.Value, x.SiteName })
                .GroupBy(x => x.MicrotingUid)
                .Select(g => g.First())
                .ToList();

            if (candidateSites.Count == 0)
            {
                return new OperationDataResult<List<HandoverCoworkerModel>>(true, new List<HandoverCoworkerModel>());
            }

            var candidateMicrotingUids = candidateSites.Select(x => x.MicrotingUid).ToList();

            var activeAssignedSiteIds = await _dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed
                            && x.Resigned != true
                            && candidateMicrotingUids.Contains(x.SiteId))
                .Select(x => x.SiteId)
                .ToListAsync();

            var activeCandidates = candidateSites
                .Where(x => activeAssignedSiteIds.Contains(x.MicrotingUid))
                .ToList();

            if (activeCandidates.Count == 0)
            {
                return new OperationDataResult<List<HandoverCoworkerModel>>(true, new List<HandoverCoworkerModel>());
            }

            var targetDate = date.Date;
            var activeCandidateSiteIds = activeCandidates.Select(x => x.MicrotingUid).ToList();

            var planRegistrations = await _dbContext.PlanRegistrations
                .Where(pr => activeCandidateSiteIds.Contains(pr.SdkSitId) && pr.Date == targetDate)
                .ToListAsync();

            // Resolve the caller's PlanRegistration on the same date so we can
            // read the actual time windows of the sender's shifts that are
            // candidates for handover. Without this, we can't tell whether
            // a receiver's existing shift would overlap.
            int? callerSdkSitId = callerSite.MicrotingUid;
            PlanRegistration? callerPr = null;
            if (callerSdkSitId.HasValue)
            {
                callerPr = await _dbContext.PlanRegistrations
                    .FirstOrDefaultAsync(pr => pr.SdkSitId == callerSdkSitId.Value && pr.Date == targetDate);
            }

            // Build the list of (start, end) windows for each requested sender
            // shift index. Empty/zero windows are dropped — they cannot overlap
            // anything and cannot be handed over.
            var senderShiftWindows = new List<(int start, int end)>();
            if (shiftIndices != null && shiftIndices.Count > 0 && callerPr != null)
            {
                foreach (var n in shiftIndices.Distinct())
                {
                    var s = GetShift(callerPr, n);
                    if (s.start != 0 || s.end != 0)
                    {
                        senderShiftWindows.Add((s.start, s.end));
                    }
                }
            }

            bool IsCandidateEligible(PlanRegistration? pr)
            {
                if (shiftIndices == null || shiftIndices.Count == 0)
                {
                    // Legacy behavior: no shift-level filter.
                    return true;
                }

                if (pr == null)
                {
                    // No PlanRegistration on that date: all 5 slots implicitly
                    // free, so as long as the sender shifts themselves don't
                    // mutually overlap (caller responsibility) we're good.
                    return true;
                }

                // Count free slots on the receiver. The merge-into-first-free
                // semantic needs at least one free slot per sender shift.
                var freeSlots = 0;
                for (var i = 1; i <= 5; i++)
                {
                    if (IsShiftEmpty(pr, i)) freeSlots++;
                }

                if (senderShiftWindows.Count == 0)
                {
                    // Sender has no actual content for the requested indices —
                    // fall back to the legacy "at least one free slot" check
                    // so we don't over-restrict the picker.
                    return freeSlots >= shiftIndices.Distinct().Count();
                }

                if (freeSlots < senderShiftWindows.Count) return false;

                // No sender shift may overlap any existing receiver shift.
                foreach (var w in senderShiftWindows)
                {
                    if (OverlapsAnyShift(pr, w.start, w.end)) return false;
                }
                return true;
            }

            var result = activeCandidates
                .Select(c =>
                {
                    var pr = planRegistrations.FirstOrDefault(p => p.SdkSitId == c.MicrotingUid);
                    return new
                    {
                        Eligible = IsCandidateEligible(pr),
                        Model = new HandoverCoworkerModel
                        {
                            SdkSiteId = c.MicrotingUid,
                            SiteName = c.SiteName ?? string.Empty,
                            PlanRegistrationId = pr?.Id ?? 0
                        }
                    };
                })
                .Where(x => x.Eligible)
                .Select(x => x.Model)
                .OrderBy(x => x.SiteName, HandoverCoworkerNameComparer)
                .ToList();

            return new OperationDataResult<List<HandoverCoworkerModel>>(true, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting handover eligible coworkers");
            return new OperationDataResult<List<HandoverCoworkerModel>>(false,
                _localizationService.GetString("ErrorGettingHandoverRequests"));
        }
    }

    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> CreateAsync(
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
                return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                    _localizationService.GetString("SourcePlanRegistrationNotFound"));
            }

            // Find target PlanRegistration
            var toPR = await _dbContext.PlanRegistrations
                .FirstOrDefaultAsync(pr => pr.SdkSitId == model.ToSdkSitId
                                           && pr.Date == fromPR.Date);

            if (toPR == null)
            {
                return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                    _localizationService.GetString("TargetPlanRegistrationNotFound"));
            }

            // Validate different workers
            if (fromPR.SdkSitId == toPR.SdkSitId)
            {
                return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                    _localizationService.GetString("CannotHandoverToSameWorker"));
            }

            var shiftIndices = model.ShiftIndices ?? new List<int>();

            // Load existing pending requests scoped to (target, date). We'll
            // use these to enforce per-shift duplicate rules as well as the
            // full-day vs partial interactions.
            var existingPending = await _dbContext.PlanRegistrationContentHandoverRequests
                .Where(r => r.ToSdkSitId == model.ToSdkSitId
                            && r.Date == fromPR.Date
                            && r.Status == HandoverRequestStatus.Pending)
                .ToListAsync();

            if (shiftIndices.Count == 0)
            {
                // Legacy full-day create.
                // Validate source has content
                var hasPlanHours = fromPR.PlanHoursInSeconds > 0;
                var hasPlanText = !string.IsNullOrWhiteSpace(fromPR.PlanText);
                if (!hasPlanHours && !hasPlanText)
                {
                    return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                        _localizationService.GetString("SourcePlanRegistrationHasNoContent"));
                }

                if (existingPending.Count > 0)
                {
                    return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                        _localizationService.GetString("PendingHandoverRequestAlreadyExists"));
                }

                var request = new PlanRegistrationContentHandoverRequest
                {
                    FromSdkSitId = fromPR.SdkSitId,
                    ToSdkSitId = model.ToSdkSitId,
                    Date = fromPR.Date,
                    FromPlanRegistrationId = fromPR.Id,
                    ToPlanRegistrationId = toPR.Id,
                    Status = HandoverRequestStatus.Pending,
                    RequestedAtUtc = DateTime.UtcNow,
                    ShiftIndex = null,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId
                };
                await request.Create(_dbContext);

                FireCreatePush(model.ToSdkSitId, new List<int> { request.Id }, 1, fromPR.Date);

                return new OperationDataResult<List<ContentHandoverRequestModel>>(
                    true, new List<ContentHandoverRequestModel> { MapToModel(request, fromPR) });
            }

            // Partial (per-shift) create. Transactional all-or-nothing.
            // Validate each requested shift index first.
            var distinctShifts = shiftIndices.Distinct().ToList();
            var errors = new List<string>();
            foreach (var n in distinctShifts)
            {
                if (n < 1 || n > 5)
                {
                    errors.Add($"Invalid shift index {n}");
                    continue;
                }

                var sourceEnd = GetPlannedEndOfShift(fromPR, n);
                if (sourceEnd <= 0)
                {
                    errors.Add($"Shift {n}: source has no planned content");
                    continue;
                }

                var targetEnd = GetPlannedEndOfShift(toPR, n);
                if (targetEnd != 0)
                {
                    errors.Add($"Shift {n}: target slot already has content");
                    continue;
                }

                // Duplicate-pending: same shift already pending to this target blocks.
                if (existingPending.Any(r => r.ShiftIndex == n))
                {
                    errors.Add($"Shift {n}: a pending handover already exists for this shift");
                    continue;
                }

                // Pending full-day (ShiftIndex == null) also blocks partial.
                if (existingPending.Any(r => r.ShiftIndex == null))
                {
                    errors.Add($"Shift {n}: a pending full-day handover exists for this date");
                    continue;
                }
            }

            // Non-empty partial also blocked by a full-day pending — covered above.

            if (errors.Count > 0)
            {
                return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                    string.Join("; ", errors));
            }

            // All per-shift validation passed above as a pre-flight pass — we never
            // persist a partial batch because every shift is validated before any
            // Create() is called. No explicit transaction needed: an EF-level
            // failure mid-loop would still leave prior rows unwritten if we used
            // SaveChanges once at the end, but PnBase.Create() calls SaveChanges
            // per entity. That's a pre-existing trade-off kept deliberately (matches
            // the rest of this service); the validation pre-flight is the real
            // correctness gate.
            var created = new List<PlanRegistrationContentHandoverRequest>();
            foreach (var n in distinctShifts)
            {
                var request = new PlanRegistrationContentHandoverRequest
                {
                    FromSdkSitId = fromPR.SdkSitId,
                    ToSdkSitId = model.ToSdkSitId,
                    Date = fromPR.Date,
                    FromPlanRegistrationId = fromPR.Id,
                    ToPlanRegistrationId = toPR.Id,
                    Status = HandoverRequestStatus.Pending,
                    RequestedAtUtc = DateTime.UtcNow,
                    ShiftIndex = n,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId
                };
                await request.Create(_dbContext);
                created.Add(request);
            }

            FireCreatePush(model.ToSdkSitId, created.Select(r => r.Id).ToList(), created.Count, fromPR.Date);

            return new OperationDataResult<List<ContentHandoverRequestModel>>(
                true, created.Select(r => MapToModel(r, fromPR)).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content handover request");
            return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                _localizationService.GetString("ErrorCreatingHandoverRequest"));
        }
    }

    private void FireCreatePush(int toSdkSitId, List<int> requestIds, int shiftCount, DateTime date)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var title = "New handover request";
                var body = shiftCount > 1
                    ? $"A coworker wants to hand over {shiftCount} shifts on {date:yyyy-MM-dd}"
                    : "A coworker wants to hand over content to you";
                // Dual-key scheme: Accept/Reject pushes use the singular
                // "handoverRequestId" and Flutter's FCM handler keys off that
                // name. Old handlers will open the first request in the batch
                // (better than nothing); new handlers can read the comma-joined
                // "handoverRequestIds" for the full batch.
                var primaryId = requestIds.Count > 0 ? requestIds[0].ToString() : "";
                await _pushNotificationService.SendToSiteAsync(
                    toSdkSitId,
                    title,
                    body,
                    new Dictionary<string, string>
                    {
                        { "type", "handover_created" },
                        { "handoverRequestId", primaryId },
                        { "handoverRequestIds", string.Join(",", requestIds) },
                        { "shiftCount", shiftCount.ToString() }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification for handover creation");
            }
        });
    }

    private static int GetPlannedEndOfShift(PlanRegistration pr, int n)
    {
        return n switch
        {
            1 => pr.PlannedEndOfShift1,
            2 => pr.PlannedEndOfShift2,
            3 => pr.PlannedEndOfShift3,
            4 => pr.PlannedEndOfShift4,
            5 => pr.PlannedEndOfShift5,
            _ => 0
        };
    }

    private static int GetPlannedStartOfShift(PlanRegistration pr, int n)
    {
        return n switch
        {
            1 => pr.PlannedStartOfShift1,
            2 => pr.PlannedStartOfShift2,
            3 => pr.PlannedStartOfShift3,
            4 => pr.PlannedStartOfShift4,
            5 => pr.PlannedStartOfShift5,
            _ => 0
        };
    }

    public async Task<OperationResult> AcceptAsync(
        int requestId, 
        int currentSdkSitId, 
        ContentHandoverDecisionModel model)
    {
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

            // Validate receiver is empty for the relevant scope.
            // A target that only carries a message (e.g. vacation / MessageId)
            // but has no actual shift content is eligible — the message is
            // preserved and the shift data is written alongside it.
            if (request.ShiftIndex == null)
            {
                var targetHasShiftContent =
                    (toPR.PlannedStartOfShift1 != 0 && toPR.PlannedEndOfShift1 != 0) ||
                    (toPR.PlannedStartOfShift2 != 0 && toPR.PlannedEndOfShift2 != 0) ||
                    (toPR.PlannedStartOfShift3 != 0 && toPR.PlannedEndOfShift3 != 0) ||
                    (toPR.PlannedStartOfShift4 != 0 && toPR.PlannedEndOfShift4 != 0) ||
                    (toPR.PlannedStartOfShift5 != 0 && toPR.PlannedEndOfShift5 != 0);
                var targetHasContent = targetHasShiftContent ||
                                       (!string.IsNullOrWhiteSpace(toPR.PlanText) && toPR.MessageId == null);
                if (targetHasContent)
                {
                    return new OperationResult(false,
                        _localizationService.GetString("TargetPlanRegistrationMustBeEmpty"));
                }
            }
            else
            {
                // Partial-shift accept: the sender's shift n is merged into the
                // receiver's first free slot rather than positionally overwriting
                // slot n. Reject only when (a) the receiver has no free slot at
                // all, or (b) the candidate shift's time window overlaps an
                // existing shift on the receiver.
                var (cStart, cEnd, _) = GetShift(fromPR, request.ShiftIndex.Value);
                if (FindFirstFreeSlot(toPR) == 0)
                {
                    return new OperationResult(false,
                        _localizationService.GetString("ReceiverHasNoFreeShiftSlot"));
                }
                if (OverlapsAnyShift(toPR, cStart, cEnd))
                {
                    return new OperationResult(false,
                        _localizationService.GetString("ShiftOverlapsExistingShift"));
                }
            }

            // Apply changes without explicit transaction
            // NOTE: Update() methods internally handle persistence, 
            // and using an explicit transaction was causing issues in the test environment
            try
            {
                // Move content from source to target (full day or shift-scoped)
                MoveContent(fromPR, toPR, request.ShiftIndex);

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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not set audit fields");
                    // Continue - audit field failure should not prevent handover
                }

                // Recalculate pause / break fields.
                var fromAssignedSite = await _dbContext.AssignedSites
                    .FirstOrDefaultAsync(a => a.SiteId == fromPR.SdkSitId);
                var toAssignedSite = await _dbContext.AssignedSites
                    .FirstOrDefaultAsync(a => a.SiteId == toPR.SdkSitId);

                if (request.ShiftIndex.HasValue)
                {
                    // Recalculate PlanHours / PlanHoursInSeconds from the five
                    // planned shift slots on BOTH registrations so the totals
                    // reflect the moved shift data. Only needed for partial-shift
                    // handovers; full-day MoveContent copies PlanHours directly.
                    PlanRegistrationHelper.RecalculatePlanHoursFromShifts(fromPR);
                    PlanRegistrationHelper.RecalculatePlanHoursFromShifts(toPR);

                    if (fromAssignedSite == null || toAssignedSite == null)
                    {
                        _dbContext.ChangeTracker.Clear();
                        return new OperationResult(false,
                            $"Cannot accept partial handover: AssignedSite missing for " +
                            (fromAssignedSite == null ? "source" : "target") + " worker");
                    }
                    try
                    {
                        PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(fromAssignedSite, fromPR);
                        PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(toAssignedSite, toPR);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Recalc failed during partial handover accept for request {RequestId}", requestId);
                        _dbContext.ChangeTracker.Clear();
                        return new OperationResult(false,
                            _localizationService.GetString("ErrorAcceptingHandoverRequest"));
                    }
                }
                else
                {
                    // Full-day path: best-effort pause recalc.
                    try
                    {
                        if (fromAssignedSite != null)
                        {
                            PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(fromAssignedSite, fromPR);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not recalculate source PlanRegistration {Id} after handover", fromPR.Id);
                    }
                    try
                    {
                        if (toAssignedSite != null)
                        {
                            PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(toAssignedSite, toPR);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not recalculate target PlanRegistration {Id} after handover", toPR.Id);
                    }
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

                // Fire-and-forget push to sender
                var fromSdkSitId = request.FromSdkSitId;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _pushNotificationService.SendToSiteAsync(
                            fromSdkSitId,
                            "Handover accepted",
                            "Your content handover request has been accepted",
                            new Dictionary<string, string>
                            {
                                { "type", "handover_decided" },
                                { "action", "accepted" },
                                { "handoverRequestId", requestId.ToString() }
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending push notification for handover acceptance");
                    }
                });

                return new OperationResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting handover request {RequestId}", requestId);
                throw;
            }
        }
        catch (Exception ex)
        {
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

            // Fire-and-forget push to sender
            var fromSdkSitId = request.FromSdkSitId;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _pushNotificationService.SendToSiteAsync(
                        fromSdkSitId,
                        "Handover rejected",
                        "Your content handover request has been rejected",
                        new Dictionary<string, string>
                        {
                            { "type", "handover_decided" },
                            { "action", "rejected" },
                            { "handoverRequestId", requestId.ToString() }
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending push notification for handover rejection");
                }
            });

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

    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetInboxAsync()
    {
        try
        {
            // Resolve the caller's SDK site from the JWT — client-supplied
            // ids are intentionally ignored to prevent inbox-peeking.
            var toSdkSitId = await ResolveCallerSdkSiteIdAsync();
            if (toSdkSitId == 0)
            {
                return new OperationDataResult<List<ContentHandoverRequestModel>>(true, new List<ContentHandoverRequestModel>());
            }

            var requests = await _dbContext.PlanRegistrationContentHandoverRequests
                .Where(r => r.ToSdkSitId == toSdkSitId && r.Status == HandoverRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAtUtc)
                .ToListAsync();

            // Resolve worker names from SDK sites
            var allSiteIds = requests
                .SelectMany(r => new[] { r.FromSdkSitId, r.ToSdkSitId })
                .Distinct()
                .ToList();

            var siteNameLookup = new Dictionary<int, string>();
            if (allSiteIds.Count > 0)
            {
                var sdkCore = await _core.GetCore();
                var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
                siteNameLookup = await sdkDbContext.Sites
                    .Where(s => s.MicrotingUid.HasValue && allSiteIds.Contains(s.MicrotingUid.Value))
                    .Select(s => new { MicrotingUid = s.MicrotingUid!.Value, s.Name })
                    .ToDictionaryAsync(s => s.MicrotingUid, s => s.Name ?? string.Empty);
            }

            var prIds = requests.Select(r => r.FromPlanRegistrationId).Distinct().ToList();
            var planRegistrations = await _dbContext.PlanRegistrations
                .Where(p => prIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var models = requests.Select(r =>
            {
                planRegistrations.TryGetValue(r.FromPlanRegistrationId, out var fromPR);
                var model = MapToModel(r, fromPR);
                model.FromWorkerName = siteNameLookup.GetValueOrDefault(r.FromSdkSitId);
                model.ToWorkerName = siteNameLookup.GetValueOrDefault(r.ToSdkSitId);
                return model;
            }).ToList();
            return new OperationDataResult<List<ContentHandoverRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting handover inbox for authenticated caller");
            return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                _localizationService.GetString("ErrorGettingHandoverRequests"));
        }
    }

    /// <summary>
    /// Resolves the authenticated caller's SDK site id (MicrotingUid) from
    /// the JWT. Returns 0 if the caller has no worker/site record. Callers
    /// should treat 0 as "no inbox visibility".
    /// </summary>
    private async Task<int> ResolveCallerSdkSiteIdAsync()
    {
        var currentUserAsync = await _userService.GetCurrentUserAsync();
        var currentUser = _baseDbContext.Users.Single(x => x.Id == currentUserAsync.Id);

        var sdkCore = await _core.GetCore();
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
        return worker.SiteWorkers.First().Site.MicrotingUid ?? 0;
    }

    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetMineAsync(int fromSdkSitId)
    {
        try
        {
            var requests = await _dbContext.PlanRegistrationContentHandoverRequests
                .Where(r => r.FromSdkSitId == fromSdkSitId)
                .OrderByDescending(r => r.RequestedAtUtc)
                .ToListAsync();

            // Resolve worker names from SDK sites
            var allSiteIds = requests
                .SelectMany(r => new[] { r.FromSdkSitId, r.ToSdkSitId })
                .Distinct()
                .ToList();

            var siteNameLookup = new Dictionary<int, string>();
            if (allSiteIds.Count > 0)
            {
                var sdkCore = await _core.GetCore();
                var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
                siteNameLookup = await sdkDbContext.Sites
                    .Where(s => s.MicrotingUid.HasValue && allSiteIds.Contains(s.MicrotingUid.Value))
                    .Select(s => new { MicrotingUid = s.MicrotingUid!.Value, s.Name })
                    .ToDictionaryAsync(s => s.MicrotingUid, s => s.Name ?? string.Empty);
            }

            var prIds = requests.Select(r => r.FromPlanRegistrationId).Distinct().ToList();
            var planRegistrations = await _dbContext.PlanRegistrations
                .Where(p => prIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var models = requests.Select(r =>
            {
                planRegistrations.TryGetValue(r.FromPlanRegistrationId, out var fromPR);
                var model = MapToModel(r, fromPR);
                model.FromWorkerName = siteNameLookup.GetValueOrDefault(r.FromSdkSitId);
                model.ToWorkerName = siteNameLookup.GetValueOrDefault(r.ToSdkSitId);
                return model;
            }).ToList();
            return new OperationDataResult<List<ContentHandoverRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting handover requests for worker {WorkerId}", fromSdkSitId);
            return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                _localizationService.GetString("ErrorGettingHandoverRequests"));
        }
    }

    public async Task<OperationDataResult<List<ContentHandoverRequestModel>>> GetAllAsync(
        string? status, string? fromDate, string? toDate, int? sdkSiteId,
        int page = 0, int pageSize = 100)
    {
        try
        {
            var query = _dbContext.PlanRegistrationContentHandoverRequests
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<HandoverRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(r => r.Status == parsedStatus);
            }

            // Filter by date range (request Date field)
            if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var from))
            {
                var fromUtc = from.Date;
                query = query.Where(r => r.Date >= fromUtc);
            }

            if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var to))
            {
                var toUtc = to.Date;
                query = query.Where(r => r.Date <= toUtc);
            }

            // Filter by sdkSiteId — matches EITHER FromSdkSitId OR ToSdkSitId
            if (sdkSiteId.HasValue)
            {
                var siteId = sdkSiteId.Value;
                query = query.Where(r => r.FromSdkSitId == siteId || r.ToSdkSitId == siteId);
            }

            var requests = await query
                .OrderByDescending(r => r.RequestedAtUtc)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Resolve worker names from SDK sites
            var allSiteIds = requests
                .SelectMany(r => new[] { r.FromSdkSitId, r.ToSdkSitId })
                .Distinct()
                .ToList();

            var siteNameLookup = new Dictionary<int, string>();
            if (allSiteIds.Count > 0)
            {
                var sdkCore = await _core.GetCore();
                var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
                siteNameLookup = await sdkDbContext.Sites
                    .Where(s => s.MicrotingUid.HasValue && allSiteIds.Contains(s.MicrotingUid.Value))
                    .Select(s => new { MicrotingUid = s.MicrotingUid!.Value, s.Name })
                    .ToDictionaryAsync(s => s.MicrotingUid, s => s.Name ?? string.Empty);
            }

            var prIds = requests.Select(r => r.FromPlanRegistrationId).Distinct().ToList();
            var planRegistrations = await _dbContext.PlanRegistrations
                .Where(p => prIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var models = requests.Select(r =>
            {
                planRegistrations.TryGetValue(r.FromPlanRegistrationId, out var fromPR);
                var model = MapToModel(r, fromPR);
                model.FromWorkerName = siteNameLookup.GetValueOrDefault(r.FromSdkSitId);
                model.ToWorkerName = siteNameLookup.GetValueOrDefault(r.ToSdkSitId);
                return model;
            }).ToList();

            return new OperationDataResult<List<ContentHandoverRequestModel>>(true, models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all handover requests");
            return new OperationDataResult<List<ContentHandoverRequestModel>>(false,
                _localizationService.GetString("ErrorGettingHandoverRequests"));
        }
    }

    private void MoveContent(PlanRegistration source, PlanRegistration target, int? shiftIndex = null)
    {
        if (shiftIndex.HasValue)
        {
            MoveShift(source, target, shiftIndex.Value);
            return;
        }

        // Move planning fields from source to target
        target.PlanText = source.PlanText;
        target.PlanHours = source.PlanHours;
        target.PlanHoursInSeconds = source.PlanHoursInSeconds;

        // Move all five planned shift slots from source to target
        target.PlannedStartOfShift1 = source.PlannedStartOfShift1;
        target.PlannedEndOfShift1 = source.PlannedEndOfShift1;
        target.PlannedBreakOfShift1 = source.PlannedBreakOfShift1;
        target.PlannedStartOfShift2 = source.PlannedStartOfShift2;
        target.PlannedEndOfShift2 = source.PlannedEndOfShift2;
        target.PlannedBreakOfShift2 = source.PlannedBreakOfShift2;
        target.PlannedStartOfShift3 = source.PlannedStartOfShift3;
        target.PlannedEndOfShift3 = source.PlannedEndOfShift3;
        target.PlannedBreakOfShift3 = source.PlannedBreakOfShift3;
        target.PlannedStartOfShift4 = source.PlannedStartOfShift4;
        target.PlannedEndOfShift4 = source.PlannedEndOfShift4;
        target.PlannedBreakOfShift4 = source.PlannedBreakOfShift4;
        target.PlannedStartOfShift5 = source.PlannedStartOfShift5;
        target.PlannedEndOfShift5 = source.PlannedEndOfShift5;
        target.PlannedBreakOfShift5 = source.PlannedBreakOfShift5;
        target.IsDoubleShift = source.IsDoubleShift;

        // Clear the moved fields on source
        source.PlanText = null;
        source.PlanHours = 0;
        source.PlanHoursInSeconds = 0;

        source.PlannedStartOfShift1 = 0;
        source.PlannedEndOfShift1 = 0;
        source.PlannedBreakOfShift1 = 0;
        source.PlannedStartOfShift2 = 0;
        source.PlannedEndOfShift2 = 0;
        source.PlannedBreakOfShift2 = 0;
        source.PlannedStartOfShift3 = 0;
        source.PlannedEndOfShift3 = 0;
        source.PlannedBreakOfShift3 = 0;
        source.PlannedStartOfShift4 = 0;
        source.PlannedEndOfShift4 = 0;
        source.PlannedBreakOfShift4 = 0;
        source.PlannedStartOfShift5 = 0;
        source.PlannedEndOfShift5 = 0;
        source.PlannedBreakOfShift5 = 0;
        source.IsDoubleShift = false;
    }

    private static void MoveShift(PlanRegistration source, PlanRegistration target, int n)
    {
        var (start, end, breakLen) = GetShift(source, n);
        var freeSlot = FindFirstFreeSlot(target);
        if (freeSlot == 0)
        {
            // Caller (AcceptAsync partial guard) is expected to have validated
            // free-slot availability. Defensive no-op so we never silently
            // overwrite an existing shift on the receiver.
            return;
        }
        SetShift(target, freeSlot, start, end, breakLen);
        SetShift(source, n, 0, 0, 0);
        SortShiftsByStart(target);

        // Do NOT touch PlanText on partial. PlanHours/PlanHoursInSeconds/IsDoubleShift
        // will be recomputed by PlanRegistrationHelper on BOTH rows in the caller.
    }

    // Returns the (start, end, breakLen) tuple for slot n on the row, or (0, 0, 0) if empty.
    private static (int start, int end, int breakLen) GetShift(PlanRegistration row, int n)
    {
        return n switch
        {
            1 => (row.PlannedStartOfShift1, row.PlannedEndOfShift1, row.PlannedBreakOfShift1),
            2 => (row.PlannedStartOfShift2, row.PlannedEndOfShift2, row.PlannedBreakOfShift2),
            3 => (row.PlannedStartOfShift3, row.PlannedEndOfShift3, row.PlannedBreakOfShift3),
            4 => (row.PlannedStartOfShift4, row.PlannedEndOfShift4, row.PlannedBreakOfShift4),
            5 => (row.PlannedStartOfShift5, row.PlannedEndOfShift5, row.PlannedBreakOfShift5),
            _ => (0, 0, 0),
        };
    }

    private static void SetShift(PlanRegistration row, int n, int start, int end, int breakLen)
    {
        switch (n)
        {
            case 1: row.PlannedStartOfShift1 = start; row.PlannedEndOfShift1 = end; row.PlannedBreakOfShift1 = breakLen; break;
            case 2: row.PlannedStartOfShift2 = start; row.PlannedEndOfShift2 = end; row.PlannedBreakOfShift2 = breakLen; break;
            case 3: row.PlannedStartOfShift3 = start; row.PlannedEndOfShift3 = end; row.PlannedBreakOfShift3 = breakLen; break;
            case 4: row.PlannedStartOfShift4 = start; row.PlannedEndOfShift4 = end; row.PlannedBreakOfShift4 = breakLen; break;
            case 5: row.PlannedStartOfShift5 = start; row.PlannedEndOfShift5 = end; row.PlannedBreakOfShift5 = breakLen; break;
        }
    }

    // True when slot n is empty (both Start and End == 0).
    private static bool IsShiftEmpty(PlanRegistration row, int n)
    {
        var s = GetShift(row, n);
        return s.start == 0 && s.end == 0;
    }

    private static int FindFirstFreeSlot(PlanRegistration row)
    {
        for (var i = 1; i <= 5; i++)
        {
            if (IsShiftEmpty(row, i)) return i;
        }
        return 0;
    }

    // Time-window overlap test on minute-since-midnight ints. Adjacent (a.End == b.Start) is NOT overlap.
    private static bool ShiftsOverlap(int aStart, int aEnd, int bStart, int bEnd)
    {
        return aStart < bEnd && bStart < aEnd;
    }

    // Returns true if the candidate (start..end) overlaps any non-empty shift on row,
    // optionally ignoring slot `ignoreSlot` (used when checking after a clear on source).
    private static bool OverlapsAnyShift(PlanRegistration row, int candidateStart, int candidateEnd, int ignoreSlot = 0)
    {
        if (candidateStart == 0 && candidateEnd == 0) return false;
        for (var i = 1; i <= 5; i++)
        {
            if (i == ignoreSlot) continue;
            var s = GetShift(row, i);
            if (s.start == 0 && s.end == 0) continue;
            if (ShiftsOverlap(candidateStart, candidateEnd, s.start, s.end)) return true;
        }
        return false;
    }

    // Sorts all 5 slots on row by start time, putting empty slots last.
    private static void SortShiftsByStart(PlanRegistration row)
    {
        var shifts = new List<(int start, int end, int breakLen)>();
        for (var i = 1; i <= 5; i++)
        {
            var s = GetShift(row, i);
            if (s.start != 0 || s.end != 0) shifts.Add(s);
        }
        shifts.Sort((a, b) => a.start.CompareTo(b.start));
        for (var i = 1; i <= 5; i++)
        {
            if (i - 1 < shifts.Count)
            {
                SetShift(row, i, shifts[i - 1].start, shifts[i - 1].end, shifts[i - 1].breakLen);
            }
            else
            {
                SetShift(row, i, 0, 0, 0);
            }
        }
    }

    private ContentHandoverRequestModel MapToModel(
        PlanRegistrationContentHandoverRequest request,
        PlanRegistration? fromPlanRegistration = null)
    {
        int? shiftStartTime = null;
        int? shiftEndTime = null;

        if (fromPlanRegistration != null && request.ShiftIndex is > 0)
        {
            shiftStartTime = GetPlannedStartOfShift(fromPlanRegistration, request.ShiftIndex.Value);
            shiftEndTime = GetPlannedEndOfShift(fromPlanRegistration, request.ShiftIndex.Value);
        }

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
            DecisionComment = request.DecisionComment,
            ShiftIndex = request.ShiftIndex,
            ShiftStartTime = shiftStartTime,
            ShiftEndTime = shiftEndTime
        };
    }
}
