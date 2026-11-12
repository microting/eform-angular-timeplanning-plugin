/*
The MIT License (MIT)

Copyright (c) 2007 - 2026 Microting A/S

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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using PushNotificationService;

/// <summary>
/// The absence-request rules shared by <see cref="AbsenceRequestService"/>
/// (approve/reject) and the planner's manager absence (#5489): who the caller
/// is, whether they manage a worker, and how an approved absence day is
/// written into the worker's row. Moved here unchanged from
/// AbsenceRequestService so both use the same code.
/// </summary>
internal static class AbsenceDecisionRules
{
    /// <summary>
    /// Resolves the authenticated caller's SDK site id (MicrotingUid) from
    /// the JWT. Returns 0 if the user has no worker/site record. Callers
    /// should treat 0 as "no resolvable caller".
    /// </summary>
    internal static async Task<int> ResolveCallerSdkSiteIdAsync(
        IUserService userService, BaseDbContext baseDbContext, IEFormCoreService coreService)
    {
        var currentUserAsync = await userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            return 0;
        }
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);

        var sdkCore = await coreService.GetCore();
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
    /// site, else null. The caller's active AssignedSite must be a manager,
    /// and only its own managing tags count.
    /// </summary>
    internal static async Task<int?> ResolveDecidingManagerAsync(int requesterSdkSitId,
        TimePlanningPnDbContext dbContext, IUserService userService, BaseDbContext baseDbContext,
        IEFormCoreService coreService)
    {
        var callerSdkSitId = await ResolveCallerSdkSiteIdAsync(userService, baseDbContext, coreService);
        if (callerSdkSitId == 0)
        {
            return null;
        }

        var managingTagIds = await GetActiveManagingTagIdsAsync(dbContext, callerSdkSitId);
        if (managingTagIds == null)
        {
            return null;
        }

        var sdkCore = await coreService.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
        var managesRequester = await sdkDbContext.SiteTags
            .AnyAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed
                           && x.Site.MicrotingUid == requesterSdkSitId
                           && managingTagIds.Contains((int)x.TagId!));
        return managesRequester ? callerSdkSitId : null;
    }

    /// <summary>
    /// The active managing tag ids of the site's latest active AssignedSite, or
    /// null when that row is missing or not a manager.
    /// </summary>
    internal static async Task<List<int>?> GetActiveManagingTagIdsAsync(
        TimePlanningPnDbContext dbContext, int callerSdkSitId)
    {
        var assignedSite = await dbContext.AssignedSites
            .Where(a => a.WorkflowState != Constants.WorkflowStates.Removed)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(a => a.SiteId == callerSdkSitId);
        if (assignedSite == null || !assignedSite.IsManager)
        {
            return null;
        }

        return await dbContext.AssignedSiteManagingTags
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed
                        && x.AssignedSiteId == assignedSite.Id)
            .Select(x => x.TagId)
            .ToListAsync();
    }

    /// <summary>
    /// Of <paramref name="workerSdkSitIds"/>, the sites a manager holding
    /// <paramref name="managingTagIds"/> manages: the same test
    /// <see cref="ResolveDecidingManagerAsync"/> makes per requester, for many
    /// workers in one query.
    /// </summary>
    internal static async Task<HashSet<int>> ManagedAmongAsync(MicrotingDbContext sdkDbContext,
        List<int> managingTagIds, List<int> workerSdkSitIds)
    {
        var managed = await sdkDbContext.SiteTags
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed
                        && x.Site.MicrotingUid != null
                        && workerSdkSitIds.Contains(x.Site.MicrotingUid.Value)
                        && managingTagIds.Contains((int)x.TagId!))
            .Select(x => x.Site.MicrotingUid!.Value)
            .Distinct()
            .ToListAsync();
        return managed.ToHashSet();
    }

    /// <summary>
    /// Writes one approved absence day into the worker's row (found or
    /// created): the absence flag of its message, MessageId and the approval
    /// metadata, then the pause recalculation. Plan hours and shifts are kept.
    /// </summary>
    internal static async Task ApplyAbsenceToPlanRegistrationAsync(TimePlanningPnDbContext dbContext,
        IUserService userService, ILogger logger, AbsenceRequest request, AbsenceRequestDay day,
        int managerSdkSitId)
    {
        try
        {
            // Find or create PlanRegistration for this worker and date
            var planRegistration = await dbContext.PlanRegistrations
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
                    CreatedByUserId = userService.UserId,
                    UpdatedByUserId = userService.UserId
                };
                await planRegistration.Create(dbContext);
            }

            // Load the message to determine absence type
            var message = await dbContext.Messages
                .FirstOrDefaultAsync(m => m.Id == day.MessageId);
            if (message != null)
            {
                ApplyAbsenceFromMessage(logger, planRegistration, message);
            }

            // Set absence audit fields if they exist
            planRegistration.MessageId = day.MessageId;
            planRegistration.UpdatedByUserId = userService.UserId;

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
                logger.LogWarning(ex, "Could not set audit fields on PlanRegistration");
            }

            await planRegistration.Update(dbContext);

            // Recalculate PlanRegistration using helper - only if AssignedSite exists
            var assignedSite = await dbContext.AssignedSites
                .Where(a => a.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.SiteId == planRegistration.SdkSitId);
            
            if (assignedSite != null)
            {
                try
                {
                    PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);
                    await planRegistration.Update(dbContext);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not calculate pause for PlanRegistration {Id}", planRegistration.Id);
                    // Continue without recalculation if it fails
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying absence to PlanRegistration for day {Date}", day.Date);
            throw;
        }
    }

    /// <summary>
    /// Fire-and-forget push telling the requester their absence request was
    /// approved (ApproveAsync and the planner's manager absence). Runs in its
    /// own DI scope because it outlives the request scope; a failure is logged.
    /// </summary>
    internal static void StartApprovedPush(IServiceScopeFactory serviceScopeFactory, ILogger logger,
        int requesterSdkSitId, int absenceRequestId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await SendToSiteInOwnScopeAsync(
                    serviceScopeFactory,
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
                logger.LogError(ex, "Error sending push notification for absence approval");
            }
        });
    }

    // Owns a short-lived DI scope per send: the fire-and-forget callers outlive
    // the request scope, so they must not use request-scoped services.
    internal static async Task SendToSiteInOwnScopeAsync(IServiceScopeFactory serviceScopeFactory,
        int targetSdkSitId, string title, string body, Dictionary<string, string> data)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        await pushService.SendToSiteAsync(targetSdkSitId, title, body, data);
    }

    private static void ApplyAbsenceFromMessage(ILogger logger, PlanRegistration pr, Message message)
    {
        if (!AbsenceMessageFlags.Apply(pr, message.Id))
        {
            logger.LogWarning(
                "Unknown absence message {MessageId} ({MessageName}); applying OtherAllowedAbsence",
                message.Id, message.Name);
        }
    }
}
