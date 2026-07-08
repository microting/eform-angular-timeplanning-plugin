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
using System.Text.RegularExpressions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Settings;

namespace TimePlanning.Pn.Services.TimePlanningPlanningService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanningLocalizationService;

public class TimePlanningPlanningService(
    ILogger<TimePlanningPlanningService> logger,
    IPluginDbOptions<TimePlanningBaseSettings> options,
    TimePlanningPnDbContext dbContext,
    ITimePlanningDbContextHelper dbContextHelper,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    BaseDbContext baseDbContext,
    IEFormCoreService core)
    : ITimePlanningPlanningService
{
    private const string GoogleMapsUrlTemplate = "https://www.google.com/maps?q={0},{1}";

    public async Task<OperationDataResult<List<TimePlanningPlanningModel>>> Index(
        TimePlanningPlanningRequestModel model)
    {
        try
        {
            var sdkCore = await core.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var result = new List<TimePlanningPlanningModel>();
            var assignedSites =
                await dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .ToListAsync().ConfigureAwait(false);

            var currentUserAsync = await userService.GetCurrentUserAsync();
            if (currentUserAsync == null)
            {
                return new OperationDataResult<List<TimePlanningPlanningModel>>(false,
                    localizationService.GetString("UserNotFound"), null!);
            }
            var currentUser = baseDbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Single(x => x.Id == currentUserAsync.Id);

            var isAdmin = currentUser.UserRoles
                .Any(x => x.Role.Name == "admin");
            if (!isAdmin)
            {
                var userSecurityGroups = baseDbContext.SecurityGroupUsers
                    .Include(x => x.SecurityGroup)
                    .Where(x => x.EformUserId == currentUser.Id)
                    .ToList();
                var eFormAdminsGroup = userSecurityGroups
                    .Any(x => x.SecurityGroup.Name == "eForm admins");
                isAdmin = eFormAdminsGroup;
                if (!isAdmin)
                {
                    var isEformUsersGroup = userSecurityGroups
                        .Any(x => x.SecurityGroup.Name == "eForm users");
                    var isKunTidGroup = userSecurityGroups
                        .Any(x => x.SecurityGroup.Name == "Kun tid");
                    if (isEformUsersGroup && !isKunTidGroup)
                    {
                        // Fallback: when no user in the system is configured as a manager,
                        // grant "eForm users" members the admin-for-visibility view on this
                        // endpoint so the planning dashboard isn't empty in that degenerate
                        // state. Users also in "Kun tid" (time-registration device users with
                        // WebAccess) are explicitly excluded and stay restricted to own site.
                        var anyManagerExists = await dbContext.AssignedSites
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .AnyAsync(x => x.IsManager)
                            .ConfigureAwait(false);
                        if (!anyManagerExists)
                        {
                            isAdmin = true;
                        }
                    }
                }
            }

            if (!isAdmin)
            {
                var worker = await sdkDbContext.Workers
                    .Include(x => x.SiteWorkers)
                    .ThenInclude(x => x.Site)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

                if (worker == null)
                {
                    SentrySdk.CaptureMessage($"Worker with email {currentUser.Email} not found");
                    return new OperationDataResult<List<TimePlanningPlanningModel>>(
                        false,
                        localizationService.GetString("ErrorWhileObtainingPlannings"));
                }

                // Deterministically resolve the active site (excludes removed
                // SiteWorker/Site rows). No active site -> same error path as a
                // missing worker (previously NRE'd on empty SiteWorkers).
                var site = worker!.ResolveActiveSite();
                if (site == null)
                {
                    SentrySdk.CaptureMessage($"No active site for worker with email {currentUser.Email}");
                    return new OperationDataResult<List<TimePlanningPlanningModel>>(
                        false,
                        localizationService.GetString("ErrorWhileObtainingPlannings"));
                }

                var assignedSite = assignedSites
                    .FirstOrDefault(x => x.SiteId == site.MicrotingUid);
                if (assignedSite == null || !assignedSite.IsManager)
                {
                    model.SiteId = site.MicrotingUid;
                } else if (assignedSite.IsManager)
                {
                    var assignedSiteTags = await dbContext.AssignedSiteManagingTags
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.AssignedSiteId == assignedSite.Id)
                        .Select(x => x.TagId)
                        .ToListAsync();
                    var assignedSiteIdsWithTags = await sdkDbContext.SiteTags
                        .Include(x => x.Site)
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => assignedSiteTags.Contains((int)x.TagId!))
                        .Select(x => x.Site.MicrotingUid)
                        .Distinct()
                        .ToListAsync();
                    assignedSites = assignedSites
                        .Where(x => assignedSiteIdsWithTags.Contains(x.SiteId))
                        .ToList();
                    // Only re-add the manager's own AssignedSite if the manager-tag
                    // filter dropped it. When the manager is in their own managed
                    // tag (SiteTag joins the manager's own SiteId to that TagId),
                    // it is already present and a blind Add() produced two rows
                    // for the manager on the planning page.
                    if (assignedSites.All(x => x.Id != assignedSite.Id))
                    {
                        assignedSites.Add(assignedSite);
                    }
                }
            }

            // Defensive dedup: guarantee no duplicate AssignedSite rows reach the
            // planning grid even if upstream filters left some in. The grid keys
            // off SiteId per row, so dedup by Id (primary key) is enough here.
            assignedSites = assignedSites
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            assignedSites = model.ShowResignedSites ? assignedSites.Where(x => x.Resigned).ToList() : assignedSites.Where(x => !x.Resigned).ToList();

            if (model.SiteId != 0 && model.SiteId != null)
            {
                assignedSites = assignedSites.Where(x => x.SiteId == model.SiteId).ToList();
            }

            // Filter by tags if provided
            if (model.TagIds != null && model.TagIds.Count > 0)
            {
                var sdkSitesWithAnyOfTags = await sdkDbContext.SiteTags
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => model.TagIds.Contains((int)x.TagId))
                    .Include(x => x.Site)
                    .Select(x => x.Site.MicrotingUid)
                    .Distinct()
                    .ToListAsync();

                assignedSites = assignedSites
                .Where(x => sdkSitesWithAnyOfTags.Contains(x.SiteId))
                .ToList();
            }

            foreach (var assignedSite in assignedSites)
            {
                Console.WriteLine($"Resigned site: {assignedSite.SiteId}, Resigned at: {assignedSite.ResignedAtDate}");
            }

            var midnightOfDateFrom = new DateTime(model.DateFrom!.Value.Year, model.DateFrom.Value.Month, model.DateFrom.Value.Day, 0, 0, 0);
            var midnightOfDateTo = new DateTime(model.DateTo!.Value.Year, model.DateTo.Value.Month, model.DateTo.Value.Day, 23, 59, 59);
            var todayMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            var datesInPeriod = new List<DateTime>();
            var date = midnightOfDateFrom;
            while (date <= midnightOfDateTo)
            {
                datesInPeriod.Add(date);
                date = date.AddDays(1);
            }

            var usersList = await baseDbContext.Users
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
            var sitesList = await sdkDbContext.Sites
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
            var siteWorkersList = await sdkDbContext.SiteWorkers
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
            var workersList = await sdkDbContext.Workers
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);

            var tasks = assignedSites.Select(async dbAssignedSite =>
            {
                var innerDbContext =
                    dbContextHelper.GetDbContext();
                var site = sitesList
                    .FirstOrDefault(x => x.MicrotingUid == dbAssignedSite.SiteId);

                if (site == null)
                {
                    logger.LogWarning($"Site with ID {dbAssignedSite.SiteId} not found in Sites list.");
                    return null; // Skip this site if not found
                }

                var siteModel = new TimePlanningPlanningModel
                {
                    SiteId = dbAssignedSite.SiteId,
                    SiteName = site.Name,
                    // Phase 4: per-row mirror of the assigned-site flag drives the
                    // web admin's HH:mm vs HH:mm:ss display path in the plannings table.
                    UseOneMinuteIntervals = dbAssignedSite.UseOneMinuteIntervals,
                    PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
                };

                var siteWorker = siteWorkersList.FirstOrDefault(x => x.SiteId == site.Id);
                var worker = siteWorker != null ? workersList.FirstOrDefault(x => x.Id == siteWorker.WorkerId) : null;
                var workerEmail = (worker?.Email ?? "").Trim().ToLower();
                var user = string.IsNullOrEmpty(workerEmail) ? null
                    : usersList.FirstOrDefault(x => (x.Email ?? "").Trim().ToLower() == workerEmail);
                if (user != null)
                {
                    siteModel.AvatarUrl = user.ProfilePictureSnapshot != null
                        ? $"api/images/login-page-images?fileName={user.ProfilePictureSnapshot}"
                        : $"https://www.gravatar.com/avatar/{user.EmailSha256}?s=32&d=identicon";
                    siteModel.SoftwareVersion = user.TimeRegistrationSoftwareVersion;
                    siteModel.DeviceModel = user.TimeRegistrationModel;
                    siteModel.DeviceManufacturer = user.TimeRegistrationManufacturer;
                    try
                    {
                        siteModel.SoftwareVersionIsValid = Version.TryParse(user.TimeRegistrationSoftwareVersion, out var v1) && v1 >= new Version(4,0,26);
                    }
                    catch (Exception)
                    {
                        // If the version format is invalid, we assume it's not valid
                        siteModel.SoftwareVersionIsValid = false;
                    }
                }

                var planningsInPeriod = await innerDbContext.PlanRegistrations
                    .AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                    .Where(x => x.Date >= midnightOfDateFrom)
                    .Where(x => x.Date <= midnightOfDateTo)
                    .Select(x => new PlanRegistration
                    {
                        Id = x.Id,
                        Date = x.Date,
                        })
                    .OrderBy(x => x.Date)
                    .ToListAsync().ConfigureAwait(false);

                var datesInPlannings = planningsInPeriod.Select(x => x.Date).ToList();
                var missingDates = new List<DateTime>();

                foreach (var dateTime in datesInPeriod)
                {
                    if (!datesInPlannings.Contains(dateTime))
                    {
                        missingDates.Add(dateTime);
                    }
                }

                foreach (var missingDate in missingDates)
                {
                    var newPlanRegistration = new PlanRegistration
                    {
                        Date = missingDate,
                        SdkSitId = dbAssignedSite.SiteId,
                        CreatedByUserId = userService.UserId,
                        UpdatedByUserId = userService.UserId
                    };

                    if (missingDate.Date <= todayMidnight)
                    {
                        var preTimePlanning =
                            await innerDbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < missingDate
                                            && x.SdkSitId == dbAssignedSite.SiteId)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                        if (preTimePlanning != null)
                        {
                            newPlanRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            newPlanRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd - newPlanRegistration.NettoHours -
                                newPlanRegistration.PlanHours -
                                newPlanRegistration.PaiedOutFlex;
                            newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                        }
                        else
                        {
                            newPlanRegistration.SumFlexEnd =
                                newPlanRegistration.NettoHours - newPlanRegistration.PlanHours -
                                newPlanRegistration.PaiedOutFlex;
                            newPlanRegistration.SumFlexStart = 0;
                            newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                        }
                    }

                    await newPlanRegistration.Create(innerDbContext);
                }

                if (missingDates.Count > 0)
                {
                    planningsInPeriod = await innerDbContext.PlanRegistrations
                        .AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                        .Where(x => x.Date >= midnightOfDateFrom)
                        .Where(x => x.Date <= midnightOfDateTo)
                        .Select(x => new PlanRegistration
                        {
                            Id = x.Id,
                            Date = x.Date,
                        })
                        .OrderBy(x => x.Date)
                        .ToListAsync().ConfigureAwait(false);
                }

                siteModel = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
                    planningsInPeriod,
                    siteModel,
                    innerDbContext,
                    dbAssignedSite,
                    logger,
                    site,
                    midnightOfDateFrom,
                    midnightOfDateTo,
                    options);

                result.Add(siteModel);
            // }
            return siteModel;
            }).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            result = result.OrderBy(x => Regex.Replace(x.SiteName, @"\d", ""))
                .ThenBy(x => x.SiteName)
                .ToList();

            return new OperationDataResult<List<TimePlanningPlanningModel>>(
                true,
                result);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<TimePlanningPlanningModel>>(
                false,
                localizationService.GetString("ErrorWhileObtainingPlannings"));
        }
    }

    public async Task<OperationDataResult<TimePlanningPlanningModel>> IndexByCurrentUserName(
        TimePlanningPlanningRequestModel model, string? softwareVersion, string? deviceModel, string? manufacturer, string? osVersion)
    {
        var sdkCore = await core.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
        var currentUserAsync = await userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            return new OperationDataResult<TimePlanningPlanningModel>(false,
                localizationService.GetString("UserNotFound"), null!);
        }
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);

        if (deviceModel != null)
        {
            currentUser.TimeRegistrationModel = deviceModel;
            currentUser.TimeRegistrationManufacturer = manufacturer;
            currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
            currentUser.TimeRegistrationOsVersion = osVersion;
            await baseDbContext.SaveChangesAsync();
        }

        var worker = await sdkDbContext.Workers
            .Include(x => x.SiteWorkers)
            .ThenInclude(x => x.Site)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

        if (worker == null)
        {
            SentrySdk.CaptureMessage($"Worker with email {currentUser.Email} not found");
            return new OperationDataResult<TimePlanningPlanningModel>(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        // Deterministically resolve the active site (excludes removed
        // SiteWorker/Site rows). No active site -> not found (mirrors the
        // worker == null path above; previously NRE'd on empty SiteWorkers).
        var site = worker.ResolveActiveSite();
        if (site == null)
        {
            SentrySdk.CaptureMessage($"No active site for worker with email {currentUser.Email}");
            return new OperationDataResult<TimePlanningPlanningModel>(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        var dbAssignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == site.MicrotingUid);

        if (dbAssignedSite == null)
        {
            return new OperationDataResult<TimePlanningPlanningModel>(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        var datesInPeriod = new List<DateTime>();
        var midnightOfDateFrom = new DateTime(model.DateFrom!.Value.Year, model.DateFrom.Value.Month, model.DateFrom.Value.Day, 0, 0, 0);
        var midnightOfDateTo = new DateTime(model.DateTo!.Value.Year, model.DateTo.Value.Month, model.DateTo.Value.Day, 23, 59, 59);
        var todayMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
        var midnightOfDateToForLoop = new DateTime(model.DateTo!.Value.Year, model.DateTo.Value.Month, model.DateTo.Value.Day, 0, 0, 0);
        var date = midnightOfDateFrom;
        while (date <= midnightOfDateToForLoop)
        {
            datesInPeriod.Add(date);
            date = date.AddDays(1);
        }

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = (int)site.MicrotingUid!,
            SiteName = site.Name,
            // Phase 4: per-row mirror of the assigned-site flag drives the
            // web admin's HH:mm vs HH:mm:ss display path in the plannings table.
            UseOneMinuteIntervals = dbAssignedSite.UseOneMinuteIntervals,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };

        try
        {
            siteModel.SoftwareVersionIsValid = Version.TryParse(currentUser.TimeRegistrationSoftwareVersion, out var v2) && v2 >= new Version(4,0,26);
        }
        catch (Exception)
        {
            // If the version format is invalid, we assume it's not valid
            siteModel.SoftwareVersionIsValid = false;
        }

        var workerEmail2 = (worker.Email ?? "").Trim().ToLower();
        var user = string.IsNullOrEmpty(workerEmail2) ? null : await baseDbContext.Users
            .Where(x => x.Email.ToLower() == workerEmail2)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (user != null)
        {
            siteModel.AvatarUrl = user.ProfilePictureSnapshot != null
                ? $"api/images/login-page-images?fileName={user.ProfilePictureSnapshot}"
                : $"https://www.gravatar.com/avatar/{user.EmailSha256}?s=32&d=identicon";
        }

        var planningsInPeriod = await dbContext.PlanRegistrations
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
            .Where(x => x.Date >= midnightOfDateFrom)
            .Where(x => x.Date <= midnightOfDateTo)
            .Select(x => new PlanRegistration
            {
                Id = x.Id,
                Date = x.Date,
            })
            .OrderByDescending(x => x.Date)
            .ToListAsync().ConfigureAwait(false);

        var datesInPlannings = planningsInPeriod.Select(x => x.Date).ToList();
        var missingDates = new List<DateTime>();

        foreach (var dateTime in datesInPeriod)
        {
            if (!datesInPlannings.Contains(dateTime))
            {
                missingDates.Add(dateTime);
            }
        }

        foreach (var missingDate in missingDates)
        {
            var newPlanRegistration = new PlanRegistration
            {
                Date = missingDate,
                SdkSitId = dbAssignedSite.SiteId,
                CreatedByUserId = userService.UserId,
                UpdatedByUserId = userService.UserId
            };

            if (missingDate.Date <= todayMidnight)
            {
                var preTimePlanning =
                    await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < missingDate
                                    && x.SdkSitId == dbAssignedSite.SiteId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                if (preTimePlanning != null)
                {
                    newPlanRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                    newPlanRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + newPlanRegistration.NettoHours -
                                                     newPlanRegistration.PlanHours -
                                                     newPlanRegistration.PaiedOutFlex;
                    newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                }
                else
                {
                    newPlanRegistration.SumFlexEnd = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours -
                                                     newPlanRegistration.PaiedOutFlex;
                    newPlanRegistration.SumFlexStart = 0;
                    newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                }
            }

            await newPlanRegistration.Create(dbContext);
        }

        if (missingDates.Count > 0)
        {
            planningsInPeriod = await dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                .Where(x => x.Date >= midnightOfDateFrom)
                .Where(x => x.Date <= midnightOfDateTo)
                .Select(x => new PlanRegistration
                {
                    Id = x.Id,
                    Date = x.Date,
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync().ConfigureAwait(false);
        }

        // Resolve the site's default language so message ids can be turned into
        // localized labels. LanguageCode is stored as e.g. "da" / "en-US" /
        // "de-DE"; normalize to the 2-letter prefix the label switch expects.
        var siteLanguage = await sdkDbContext.Languages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == site.LanguageId);
        var messageLanguage = siteLanguage?.LanguageCode?.Split('-')[0].ToLowerInvariant();

        siteModel = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            dbContextHelper.GetDbContext(),
            dbAssignedSite,
            logger,
            site,
            midnightOfDateFrom,
            midnightOfDateTo,
            options,
            messageLanguage);

        siteModel.PlanningPrDayModels = model.IsSortDsc
            ? siteModel.PlanningPrDayModels.OrderByDescending(x => x.Date).ToList()
            : siteModel.PlanningPrDayModels.OrderBy(x => x.Date).ToList();

        return new OperationDataResult<TimePlanningPlanningModel>(
            true,
            siteModel);

    }

    public async Task<OperationResult> Update(int id, TimePlanningPlanningPrDayModel model)
    {
        try
        {
            // Phase 2/5 fix (PR #1545 b1m round 5): the dashboard fires a PUT
            // with only off-grid actual stamps and no planning fields. When
            // model-binding falls through (malformed/empty body), [FromBody]
            // hands us a null model and the very next line — model.Planned-
            // StartOfShift1 — NRE'd inside the catch block as a generic
            // "ErrorWhileUpdatingPlanning" 200/{success:false}. Returning a
            // structured failure instead lets the front-end surface a real
            // validation error rather than silently retrying the save.
            if (model == null)
            {
                return new OperationResult(
                    false,
                    localizationService.GetString("ErrorWhileUpdatingPlanning"));
            }

            var currentUserAsync = await userService.GetCurrentUserAsync();
            var planning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefault(x => x.Id == id);

            if (planning == null)
            {
                return new OperationResult(
                    false,
                    localizationService.GetString("PlanningNotFound"));
            }

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstAsync(x => x.SiteId == planning.SdkSitId);

            if (assignedSite.Resigned)
            {
                return new OperationResult(
                    false,
                    localizationService.GetString("PlanningCannotBeEditedForResignedSite"));
            }

            // Snapshot each shift's PRE-EDIT legacy coarse pause tick (Pause{N}Id)
            // BEFORE the model is applied, so the pause-override inference can
            // change-detect an admin pause edit (Approach C) by comparing the
            // submitted Break{N}Shift against this tick — without locking an
            // override when only start/stop changed (16288-shape rows), and
            // idempotently for non-5-multiple overrides on unrelated re-saves.
            var preEditShownTicks = CaptureCurrentShiftShownTicks(planning);
            // Shifts whose override was set by the flag-ON exact-minute path; the
            // inference must not overwrite them (FIX 4 — exact-minute wins).
            var exactHandledShifts = new HashSet<int>();

            planning.PlannedStartOfShift1 = model.PlannedStartOfShift1;
            planning.PlannedBreakOfShift1 = model.PlannedBreakOfShift1;
            planning.PlannedEndOfShift1 = model.PlannedEndOfShift1;
            planning.PlannedStartOfShift2 = model.PlannedStartOfShift2;
            planning.PlannedBreakOfShift2 = model.PlannedBreakOfShift2;
            planning.PlannedEndOfShift2 = model.PlannedEndOfShift2;
            planning.PlannedStartOfShift3 = model.PlannedStartOfShift3;
            planning.PlannedBreakOfShift3 = model.PlannedBreakOfShift3;
            planning.PlannedEndOfShift3 = model.PlannedEndOfShift3;
            planning.PlannedStartOfShift4 = model.PlannedStartOfShift4;
            planning.PlannedBreakOfShift4 = model.PlannedBreakOfShift4;
            planning.PlannedEndOfShift4 = model.PlannedEndOfShift4;
            planning.PlannedStartOfShift5 = model.PlannedStartOfShift5;
            planning.PlannedBreakOfShift5 = model.PlannedBreakOfShift5;
            planning.PlannedEndOfShift5 = model.PlannedEndOfShift5;
            planning.PlanText = PlanTextHelper.GeneratePlanText(planning);
            planning.CommentOffice = model.CommentOffice;
            planning.NettoHoursOverride = model.NettoHoursOverride;
            planning.NettoHoursOverrideActive = model.NettoHoursOverrideActive;

            planning = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planning);

            if (!planning.PlanChangedByAdmin)
            {
                var entry = dbContext.Entry(planning);
                planning.PlanChangedByAdmin = entry.State == EntityState.Modified;
            }

            planning.UpdatedByUserId = userService.UserId;

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? 0;
                planning.Pause2Id = model.Pause2Id ?? 0;
                planning.Pause3Id = model.Pause3Id ?? 0;
                planning.Pause4Id = model.Pause4Id ?? 0;
                planning.Pause5Id = model.Pause5Id ?? 0;
            }
            else
            {
                planning.Pause1StartedAt = model.Pause1StartedAt;
                planning.Pause1StoppedAt = model.Pause1StoppedAt;
                planning.Pause2StartedAt = model.Pause2StartedAt;
                planning.Pause2StoppedAt = model.Pause2StoppedAt;
                planning.Pause10StartedAt = model.Pause10StartedAt;
                planning.Pause10StoppedAt = model.Pause10StoppedAt;
                planning.Pause11StartedAt = model.Pause11StartedAt;
                planning.Pause11StoppedAt = model.Pause11StoppedAt;
                planning.Pause12StartedAt = model.Pause12StartedAt;
                planning.Pause12StoppedAt = model.Pause12StoppedAt;
                planning.Pause13StartedAt = model.Pause13StartedAt;
                planning.Pause13StoppedAt = model.Pause13StoppedAt;
                planning.Pause14StartedAt = model.Pause14StartedAt;
                planning.Pause14StoppedAt = model.Pause14StoppedAt;
                planning.Pause15StartedAt = model.Pause15StartedAt;
                planning.Pause15StoppedAt = model.Pause15StoppedAt;
                planning.Pause16StartedAt = model.Pause16StartedAt;
                planning.Pause16StoppedAt = model.Pause16StoppedAt;
                planning.Pause17StartedAt = model.Pause17StartedAt;
                planning.Pause17StoppedAt = model.Pause17StoppedAt;
                planning.Pause18StartedAt = model.Pause18StartedAt;
                planning.Pause18StoppedAt = model.Pause18StoppedAt;
                planning.Pause19StartedAt = model.Pause19StartedAt;
                planning.Pause19StoppedAt = model.Pause19StoppedAt;
                planning.Pause100StartedAt = model.Pause100StartedAt;
                planning.Pause100StoppedAt = model.Pause100StoppedAt;
                planning.Pause101StartedAt = model.Pause101StartedAt;
                planning.Pause101StoppedAt = model.Pause101StoppedAt;
                planning.Pause102StartedAt = model.Pause102StartedAt;
                planning.Pause102StoppedAt = model.Pause102StoppedAt;

                planning.Pause1Id = PauseMinutesCalculator.DerivePauseId(planning, 1);

                planning.Pause20StartedAt = model.Pause20StartedAt;
                planning.Pause20StoppedAt = model.Pause20StoppedAt;
                planning.Pause21StartedAt = model.Pause21StartedAt;
                planning.Pause21StoppedAt = model.Pause21StoppedAt;
                planning.Pause22StartedAt = model.Pause22StartedAt;
                planning.Pause22StoppedAt = model.Pause22StoppedAt;
                planning.Pause23StartedAt = model.Pause23StartedAt;
                planning.Pause23StoppedAt = model.Pause23StoppedAt;
                planning.Pause24StartedAt = model.Pause24StartedAt;
                planning.Pause24StoppedAt = model.Pause24StoppedAt;
                planning.Pause25StartedAt = model.Pause25StartedAt;
                planning.Pause25StoppedAt = model.Pause25StoppedAt;
                planning.Pause26StartedAt = model.Pause26StartedAt;
                planning.Pause26StoppedAt = model.Pause26StoppedAt;
                planning.Pause27StartedAt = model.Pause27StartedAt;
                planning.Pause27StoppedAt = model.Pause27StoppedAt;
                planning.Pause28StartedAt = model.Pause28StartedAt;
                planning.Pause28StoppedAt = model.Pause28StoppedAt;
                planning.Pause29StartedAt = model.Pause29StartedAt;
                planning.Pause29StoppedAt = model.Pause29StoppedAt;
                planning.Pause200StartedAt = model.Pause200StartedAt;
                planning.Pause200StoppedAt = model.Pause200StoppedAt;
                planning.Pause201StartedAt = model.Pause201StartedAt;
                planning.Pause201StoppedAt = model.Pause201StoppedAt;
                planning.Pause202StartedAt = model.Pause202StartedAt;
                planning.Pause202StoppedAt = model.Pause202StoppedAt;

                planning.Pause2Id = PauseMinutesCalculator.DerivePauseId(planning, 2);

                planning.Pause3StartedAt = model.Pause3StartedAt;
                planning.Pause3StoppedAt = model.Pause3StoppedAt;
                planning.Pause3Id = model.Pause3Id ?? 0;

                planning.Pause4StartedAt = model.Pause4StartedAt;
                planning.Pause4StoppedAt = model.Pause4StoppedAt;
                planning.Pause4Id = model.Pause4Id ?? 0;

                planning.Pause5StartedAt = model.Pause5StartedAt;
                planning.Pause5StoppedAt = model.Pause5StoppedAt;
                planning.Pause5Id = model.Pause5Id ?? 0;
            }
            planning.Start1Id = model.Start1Id ?? 0;
            planning.Stop1Id = model.Stop1Id ?? 0;
            planning.Start2Id = model.Start2Id ?? 0;
            planning.Stop2Id = model.Stop2Id ?? 0;
            planning.Start3Id = model.Start3Id ?? 0;
            planning.Stop3Id = model.Stop3Id ?? 0;
            planning.Start4Id = model.Start4Id ?? 0;
            planning.Stop4Id = model.Stop4Id ?? 0;
            planning.Start5Id = model.Start5Id ?? 0;
            planning.Stop5Id = model.Stop5Id ?? 0;
            planning.MessageId = model.Message;
            planning.PaiedOutFlex = model.PaidOutFlex;

            planning.Start1StartedAt = model.Start1StartedAt;
            planning.Stop1StoppedAt = model.Stop1StoppedAt;
            planning.Start2StartedAt = model.Start2StartedAt;
            planning.Stop2StoppedAt = model.Stop2StoppedAt;
            planning.Start3StartedAt = model.Start3StartedAt;
            planning.Stop3StoppedAt = model.Stop3StoppedAt;
            planning.Start4StartedAt = model.Start4StartedAt;
            planning.Stop4StoppedAt = model.Stop4StoppedAt;
            planning.Start5StartedAt = model.Start5StartedAt;
            planning.Stop5StoppedAt = model.Stop5StoppedAt;

            if (model.Stop2Id == null)
            {
                planning.Stop2StoppedAt = null;
                planning.Pause2StartedAt = null;
                planning.Pause2StoppedAt = null;
                planning.Pause20StartedAt = null;
                planning.Pause20StoppedAt = null;
                planning.Pause21StartedAt = null;
                planning.Pause21StoppedAt = null;
                planning.Pause22StartedAt = null;
                planning.Pause22StoppedAt = null;
                planning.Pause23StartedAt = null;
                planning.Pause23StoppedAt = null;
                planning.Pause24StartedAt = null;
                planning.Pause24StoppedAt = null;
                planning.Pause25StartedAt = null;
                planning.Pause25StoppedAt = null;
                planning.Pause26StartedAt = null;
                planning.Pause26StoppedAt = null;
                planning.Pause27StartedAt = null;
                planning.Pause27StoppedAt = null;
                planning.Pause28StartedAt = null;
                planning.Pause28StoppedAt = null;
                planning.Pause29StartedAt = null;
                planning.Pause29StoppedAt = null;
                planning.Pause200StartedAt = null;
                planning.Pause200StoppedAt = null;
                planning.Pause201StartedAt = null;
                planning.Pause201StoppedAt = null;
                planning.Pause202StartedAt = null;
                planning.Pause202StoppedAt = null;
                planning.Shift2PauseNumber = 0;
                planning.Pause2Id = 0;
            }

            if (model.Start2Id == null)
            {
                planning.Start2StartedAt = null;
            }

            if (model.Stop1Id == null)
            {
                planning.Stop1StoppedAt = null;
                planning.Pause1StartedAt = null;
                planning.Pause1StoppedAt = null;
                planning.Pause10StartedAt = null;
                planning.Pause10StoppedAt = null;
                planning.Pause11StartedAt = null;
                planning.Pause11StoppedAt = null;
                planning.Pause12StartedAt = null;
                planning.Pause12StoppedAt = null;
                planning.Pause13StartedAt = null;
                planning.Pause13StoppedAt = null;
                planning.Pause14StartedAt = null;
                planning.Pause14StoppedAt = null;
                planning.Pause15StartedAt = null;
                planning.Pause15StoppedAt = null;
                planning.Pause16StartedAt = null;
                planning.Pause16StoppedAt = null;
                planning.Pause17StartedAt = null;
                planning.Pause17StoppedAt = null;
                planning.Pause18StartedAt = null;
                planning.Pause18StoppedAt = null;
                planning.Pause19StartedAt = null;
                planning.Pause19StoppedAt = null;
                planning.Pause100StartedAt = null;
                planning.Pause100StoppedAt = null;
                planning.Pause101StartedAt = null;
                planning.Pause101StoppedAt = null;
                planning.Pause102StartedAt = null;
                planning.Pause102StoppedAt = null;
                planning.Shift1PauseNumber = 0;
                planning.Pause1Id = 0;
            }
            if (model.Start1Id == null)
            {
                planning.Start1StartedAt = null;
            }

            if (assignedSite.UseOneMinuteIntervals)
            {
                var exactPauses = new[]
                {
                    (1, model.Pause1ExactMinutes),
                    (2, model.Pause2ExactMinutes),
                    (3, model.Pause3ExactMinutes),
                    (4, model.Pause4ExactMinutes),
                    (5, model.Pause5ExactMinutes),
                };
                foreach (var (shift, minutes) in exactPauses)
                {
                    if (minutes.HasValue)
                    {
                        // Approach C: an exact-minute pause edit on a one-minute
                        // site sets the per-shift override instead of clearing +
                        // synthesizing the recorded pause timestamps. The worker's
                        // recorded Pause*StartedAt/StoppedAt are preserved as
                        // documentation; the override is the authoritative total.
                        PlanRegistrationHelper.SetShiftPauseOverrideMinutes(planning, shift, minutes.Value);
                        // FIX 4: mark this shift handled so the later inference
                        // (ApplyInferredPauseOverrides) does not overwrite the
                        // exact-minute value with a coarse Break{N}Shift-derived one.
                        exactHandledShifts.Add(shift);
                    }
                }

                var exactStarts = new[]
                {
                    (1, model.Start1ExactMinutes),
                    (2, model.Start2ExactMinutes),
                    (3, model.Start3ExactMinutes),
                    (4, model.Start4ExactMinutes),
                    (5, model.Start5ExactMinutes),
                };
                foreach (var (shift, minutes) in exactStarts)
                {
                    if (minutes.HasValue)
                    {
                        ApplyExactMinuteStart(planning, shift, minutes.Value);
                    }
                }

                var exactStops = new[]
                {
                    (1, model.Stop1ExactMinutes),
                    (2, model.Stop2ExactMinutes),
                    (3, model.Stop3ExactMinutes),
                    (4, model.Stop4ExactMinutes),
                    (5, model.Stop5ExactMinutes),
                };
                foreach (var (shift, minutes) in exactStops)
                {
                    if (minutes.HasValue)
                    {
                        ApplyExactMinuteStop(planning, shift, minutes.Value);
                    }
                }

                // Re-derive legacy 5-min-tick Start*/Stop* Ids from the just-written
                // exact-minute timestamps. Mirrors UpdateByCurrentUserNam under flag-on:
                // without this, the unconditional model-supplied IDs written above stay
                // 5-min-quantized while the *StartedAt/*StoppedAt are off-grid.
                DeriveLegacyShiftIdsFromTimestamps(planning);
            }

            // Write-time mode marker: this admin edit rewrites the Start/Stop
            // registrations, so the row is (re-)registered under the site's
            // CURRENT input mode — an exact-minute edit on a one-minute site
            // renders exactly forever, even when the row's date predates the
            // site's flip to one-minute intervals.
            planning.RegisteredUnderOneMinuteIntervals = assignedSite.UseOneMinuteIntervals;

            if (!assignedSite.UseOnlyPlanHours)
            {
                double minutesPlanned = 0;
                if (planning.PlannedStartOfShift1 != 0 && planning.PlannedEndOfShift1 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift1 - planning.PlannedStartOfShift1 - planning.PlannedBreakOfShift1;
                }
                if (planning.PlannedStartOfShift2 != 0 && planning.PlannedEndOfShift2 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift2 - planning.PlannedStartOfShift2 - planning.PlannedBreakOfShift2;
                }
                if (planning.PlannedStartOfShift3 != 0 && planning.PlannedEndOfShift3 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift3 - planning.PlannedStartOfShift3 - planning.PlannedBreakOfShift3;
                }
                if (planning.PlannedStartOfShift4 != 0 && planning.PlannedEndOfShift4 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift4 - planning.PlannedStartOfShift4 - planning.PlannedBreakOfShift4;
                }
                if (planning.PlannedStartOfShift5 != 0 && planning.PlannedEndOfShift5 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift5 - planning.PlannedStartOfShift5 - planning.PlannedBreakOfShift5;
                }
                if (planning.MessageId == null)
                {
                    planning.PlanHours = minutesPlanned != 0 ? minutesPlanned / 60 : model.PlanHours;
                }
                else
                {
                    planning.PlanHours = model.PlanHours;
                }
            } else {
                planning.PlanHours = model.PlanHours;
            }

            // Approach C: infer the per-shift pause override from the submitted
            // Break{N}Shift (change-detected against the pre-edit Pause{N}Id) or
            // honor an explicit web clear/value signal. Exact-minute shifts already
            // set above are skipped (FIX 4). Never touches the recorded
            // Pause*StartedAt/StoppedAt timestamps.
            ApplyInferredPauseOverrides(planning, model, preEditShownTicks, exactHandledShifts);

            double nettoMinutes = ComputePlanningNettoMinutes(planning, assignedSite.UseOneMinuteIntervals);

            double hours = nettoMinutes / 60;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

            // Phase 2: when UseOneMinuteIntervals is on, recompute NettoHours
            // from DateTime deltas (precise to the second) and write
            // *InSeconds columns as the source of truth; back-derive the
            // legacy double hour fields. Flag-off path stays byte-identical.
            if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
            {
                PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                    planning,
                    preTimePlanning?.SumFlexEndInSeconds ?? 0,
                    preTimePlanning != null);
            }
            else
            {
                planning.NettoHours = hours;
                var preSumFlexEnd = preTimePlanning?.SumFlexEnd ?? 0;
                planning.SumFlexStart = preSumFlexEnd;
                if (planning.NettoHoursOverrideActive)
                {
                    planning.SumFlexEnd = preSumFlexEnd + planning.NettoHoursOverride -
                                          planning.PlanHours -
                                          planning.PaiedOutFlex;
                    planning.Flex = planning.NettoHoursOverride - planning.PlanHours;
                }
                else
                {
                    planning.SumFlexEnd = preSumFlexEnd + planning.NettoHours -
                                          planning.PlanHours -
                                          planning.PaiedOutFlex;
                    planning.Flex = planning.NettoHours - planning.PlanHours;
                }
            }

            // Ensure timestamps are populated from IDs for accurate time tracking calculation
            EnsureTimestampsFromIds(planning);

            // Compute time tracking fields (seconds-based calculation)
            PlanRegistrationHelper.ComputeTimeTrackingFields(planning);
            // Phase 2/5 fix: GetCurrentUserAsync() does FirstOrDefaultAsync against
            // the base AspNetUsers table and can return null (cross-tenant token,
            // expired claim, or any code path where UserId resolves to 0). The
            // unguarded `currentUserAsync.Id` access then NRE'd here and the
            // catch block swallowed it as a generic "ErrorWhileUpdatingPlanning"
            // 200/{success:false}, which is exactly the symptom PR #1545's b1m
            // playwright variant tripped over (the dashboard waits for the
            // /index POST that the frontend only fires after success=true).
            // Fall back to userService.UserId so the audit field still gets
            // a non-null int (matches the legacy flag-off path that already
            // assigns userService.UserId at the top of Update).
            planning.UpdatedByUserId = currentUserAsync?.Id ?? userService.UserId;

            await planning.Update(dbContext).ConfigureAwait(false);
            var todayDateMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);

            var planningsAfterThisPlanning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == planning.SdkSitId)
                .Where(x => x.Date > planning.Date)
                .Where(x => x.Date < todayDateMidnight.AddDays(1))
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var planningAfterThisPlanning in planningsAfterThisPlanning)
            {
                var preTimePlanningAfterThisPlanning =
                    await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < planningAfterThisPlanning.Date
                                    && x.SdkSitId == planningAfterThisPlanning.SdkSitId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                // Phase 2: when UseOneMinuteIntervals is on, replay the
                // SumFlex chain through subsequent days using *InSeconds as
                // the source of truth so accumulated rounding does not drift.
                if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
                {
                    PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                        planningAfterThisPlanning,
                        preTimePlanningAfterThisPlanning?.SumFlexEndInSeconds ?? 0,
                        preTimePlanningAfterThisPlanning != null);
                }
                else if (preTimePlanningAfterThisPlanning != null)
                {
                    planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                    if (planningAfterThisPlanning.NettoHoursOverrideActive)
                    {
                        planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                               planningAfterThisPlanning.NettoHoursOverride -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHoursOverride - planningAfterThisPlanning.PlanHours;
                    }
                    else
                    {
                        planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                               planningAfterThisPlanning.NettoHours -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                    }
                }
                else
                {
                    // No previous planning found, start from 0
                    if (planningAfterThisPlanning.NettoHoursOverrideActive)
                    {
                        planningAfterThisPlanning.SumFlexEnd = planningAfterThisPlanning.NettoHoursOverride -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHoursOverride - planningAfterThisPlanning.PlanHours;
                    }
                    else
                    {
                        planningAfterThisPlanning.SumFlexEnd = planningAfterThisPlanning.NettoHours -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                    }
                    planningAfterThisPlanning.SumFlexStart = 0;
                }

                await planningAfterThisPlanning.Update(dbContext).ConfigureAwait(false);
            }

            return new OperationResult(
                true,
                localizationService.GetString("SuccessfullyUpdatedPlanning"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            // Phase 2/5 fix: log the full exception (message + stack trace +
            // inner exceptions) at Error level so future failures of this
            // catch can be diagnosed from CI stdout.  The previous code only
            // logged e.Message at Error and e.StackTrace at Trace, and the
            // default min-level filters out Trace, which is why PR #1545's
            // b1m run produced a bare "Object reference not set..." with no
            // stack frame to point at the offending line.
            logger.LogError(e, "TimePlanningPlanningService.Update failed");
            return new OperationResult(
                false,
                localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }

    public async Task<OperationResult> UpdateByCurrentUserNam(
        TimePlanningPlanningPrDayModel model)
    {
        try
        {
            var sdkCore = await core.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var currentUserAsync = await userService.GetCurrentUserAsync();
            if (currentUserAsync == null)
            {
                // Phase 2/5 fix: GetCurrentUserAsync() can return null when
                // UserId resolves to 0 (no claim, expired token, etc.).  Without
                // this guard the next line NRE'd on currentUserAsync.Id and the
                // catch block swallowed it as a generic
                // "ErrorWhileUpdatingPlanning" 200/{success:false}.
                return new OperationResult(false, "Current user not found");
            }
            var currentUser = baseDbContext.Users
                .Single(x => x.Id == currentUserAsync.Id);
            var worker = await sdkDbContext.Workers
                .Include(x => x.SiteWorkers)
                .ThenInclude(x => x.Site)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

            if (worker == null)
            {
                SentrySdk.CaptureMessage($"Worker with email {currentUser.Email} not found");
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("SiteNotFound"));
            }

            // Deterministically resolve the active site (excludes removed
            // SiteWorker/Site rows). No active site -> not found (mirrors the
            // worker == null path above; previously NRE'd on empty SiteWorkers).
            var site = worker.ResolveActiveSite();
            if (site == null)
            {
                SentrySdk.CaptureMessage($"No active site for worker with email {currentUser.Email}");
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("SiteNotFound"));
            }
            var mcrotingUid = site.MicrotingUid;

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == mcrotingUid);

            if (assignedSite == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    "AssignedSiteNotFound");
            }

            if (assignedSite.Resigned)
            {
                return new OperationResult(
                    false,
                    localizationService.GetString("PlanningCannotBeEditedForResignedSite"));
            }

            var planning = await dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == model.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (planning == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("PlanningNotFound"));
            }

            // Snapshot each shift's PRE-EDIT EFFECTIVE SHOWN coarse tick (override →
            // (override/5)+1, else Pause{N}Id) BEFORE the model is applied, so the
            // pause-override inference can change-detect a manual pause edit
            // (Approach C) by comparing the submitted Break{N}Shift against this
            // tick — without locking an override when only start/stop changed
            // (16288-shape rows), and idempotently for non-5-multiple overrides.
            var preEditShownTicks = CaptureCurrentShiftShownTicks(planning);
            // UpdateByCurrentUserNam has no flag-ON exact-minute pause loop, so no
            // shift is pre-handled; pass an empty set for the FIX 4 contract.
            var exactHandledShifts = new HashSet<int>();

            planning.PlannedStartOfShift1 = model.PlannedStartOfShift1;
            planning.PlannedBreakOfShift1 = model.PlannedBreakOfShift1;
            planning.PlannedEndOfShift1   = model.PlannedEndOfShift1;
            planning.PlannedStartOfShift2 = model.PlannedStartOfShift2;
            planning.PlannedBreakOfShift2 = model.PlannedBreakOfShift2;
            planning.PlannedEndOfShift2   = model.PlannedEndOfShift2;
            planning.PlannedStartOfShift3 = model.PlannedStartOfShift3;
            planning.PlannedBreakOfShift3 = model.PlannedBreakOfShift3;
            planning.PlannedEndOfShift3   = model.PlannedEndOfShift3;
            planning.PlannedStartOfShift4 = model.PlannedStartOfShift4;
            planning.PlannedBreakOfShift4 = model.PlannedBreakOfShift4;
            planning.PlannedEndOfShift4   = model.PlannedEndOfShift4;
            planning.PlannedStartOfShift5 = model.PlannedStartOfShift5;
            planning.PlannedBreakOfShift5 = model.PlannedBreakOfShift5;
            planning.PlannedEndOfShift5   = model.PlannedEndOfShift5;
            planning.PlanText = PlanTextHelper.GeneratePlanText(planning);

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? 0;
                planning.Pause2Id = model.Pause2Id ?? 0;
                planning.Pause3Id = model.Pause3Id ?? 0;
                planning.Pause4Id = model.Pause4Id ?? 0;
                planning.Pause5Id = model.Pause5Id ?? 0;
            }
            else
            {
                planning.Pause1StartedAt = model.Pause1StartedAt;
                planning.Pause1StoppedAt = model.Pause1StoppedAt;
                planning.Pause2StartedAt = model.Pause2StartedAt;
                planning.Pause2StoppedAt = model.Pause2StoppedAt;
                planning.Pause10StartedAt = model.Pause10StartedAt;
                planning.Pause10StoppedAt = model.Pause10StoppedAt;
                planning.Pause11StartedAt = model.Pause11StartedAt;
                planning.Pause11StoppedAt = model.Pause11StoppedAt;
                planning.Pause12StartedAt = model.Pause12StartedAt;
                planning.Pause12StoppedAt = model.Pause12StoppedAt;
                planning.Pause13StartedAt = model.Pause13StartedAt;
                planning.Pause13StoppedAt = model.Pause13StoppedAt;
                planning.Pause14StartedAt = model.Pause14StartedAt;
                planning.Pause14StoppedAt = model.Pause14StoppedAt;
                planning.Pause15StartedAt = model.Pause15StartedAt;
                planning.Pause15StoppedAt = model.Pause15StoppedAt;
                planning.Pause16StartedAt = model.Pause16StartedAt;
                planning.Pause16StoppedAt = model.Pause16StoppedAt;
                planning.Pause17StartedAt = model.Pause17StartedAt;
                planning.Pause17StoppedAt = model.Pause17StoppedAt;
                planning.Pause18StartedAt = model.Pause18StartedAt;
                planning.Pause18StoppedAt = model.Pause18StoppedAt;
                planning.Pause19StartedAt = model.Pause19StartedAt;
                planning.Pause19StoppedAt = model.Pause19StoppedAt;
                planning.Pause100StartedAt = model.Pause100StartedAt;
                planning.Pause100StoppedAt = model.Pause100StoppedAt;
                planning.Pause101StartedAt = model.Pause101StartedAt;
                planning.Pause101StoppedAt = model.Pause101StoppedAt;
                planning.Pause102StartedAt = model.Pause102StartedAt;
                planning.Pause102StoppedAt = model.Pause102StoppedAt;

                planning.Pause1Id = PauseMinutesCalculator.DerivePauseId(planning, 1);

                planning.Pause20StartedAt = model.Pause20StartedAt;
                planning.Pause20StoppedAt = model.Pause20StoppedAt;
                planning.Pause21StartedAt = model.Pause21StartedAt;
                planning.Pause21StoppedAt = model.Pause21StoppedAt;
                planning.Pause22StartedAt = model.Pause22StartedAt;
                planning.Pause22StoppedAt = model.Pause22StoppedAt;
                planning.Pause23StartedAt = model.Pause23StartedAt;
                planning.Pause23StoppedAt = model.Pause23StoppedAt;
                planning.Pause24StartedAt = model.Pause24StartedAt;
                planning.Pause24StoppedAt = model.Pause24StoppedAt;
                planning.Pause25StartedAt = model.Pause25StartedAt;
                planning.Pause25StoppedAt = model.Pause25StoppedAt;
                planning.Pause26StartedAt = model.Pause26StartedAt;
                planning.Pause26StoppedAt = model.Pause26StoppedAt;
                planning.Pause27StartedAt = model.Pause27StartedAt;
                planning.Pause27StoppedAt = model.Pause27StoppedAt;
                planning.Pause28StartedAt = model.Pause28StartedAt;
                planning.Pause28StoppedAt = model.Pause28StoppedAt;
                planning.Pause29StartedAt = model.Pause29StartedAt;
                planning.Pause29StoppedAt = model.Pause29StoppedAt;
                planning.Pause200StartedAt = model.Pause200StartedAt;
                planning.Pause200StoppedAt = model.Pause200StoppedAt;
                planning.Pause201StartedAt = model.Pause201StartedAt;
                planning.Pause201StoppedAt = model.Pause201StoppedAt;
                planning.Pause202StartedAt = model.Pause202StartedAt;
                planning.Pause202StoppedAt = model.Pause202StoppedAt;

                planning.Pause2Id = PauseMinutesCalculator.DerivePauseId(planning, 2);

                planning.Pause3StartedAt = model.Pause3StartedAt;
                planning.Pause3StoppedAt = model.Pause3StoppedAt;
                planning.Pause3Id = model.Pause3Id ?? 0;

                planning.Pause4StartedAt = model.Pause4StartedAt;
                planning.Pause4StoppedAt = model.Pause4StoppedAt;
                planning.Pause4Id = model.Pause4Id ?? 0;

                planning.Pause5StartedAt = model.Pause5StartedAt;
                planning.Pause5StoppedAt = model.Pause5StoppedAt;
                planning.Pause5Id = model.Pause5Id ?? 0;
                // we need to calculate the pause id based on the start and stop times from all the pauses above
            }

            planning.Start1StartedAt = model.Start1StartedAt;
            planning.Stop1StoppedAt = model.Stop1StoppedAt;
            planning.Start2StartedAt = model.Start2StartedAt;
            planning.Stop2StoppedAt = model.Stop2StoppedAt;
            planning.Start3StartedAt = model.Start3StartedAt;
            planning.Stop3StoppedAt = model.Stop3StoppedAt;
            planning.Start4StartedAt = model.Start4StartedAt;
            planning.Stop4StoppedAt = model.Stop4StoppedAt;
            planning.Start5StartedAt = model.Start5StartedAt;
            planning.Stop5StoppedAt = model.Stop5StoppedAt;

            if (assignedSite.UseOneMinuteIntervals)
            {
                DeriveLegacyShiftIdsFromTimestamps(planning);
            }
            else
            {
                // Flag-off: legacy behaviour — copy IDs straight from the model.
                // Under UseOneMinuteIntervals the IDs above were derived from
                // timestamps and must NOT be clobbered by the (typically 0/null)
                // model-supplied IDs from the worker-punchclock gRPC path.
                planning.Start1Id = model.Start1Id ?? 0;
                planning.Stop1Id = model.Stop1Id ?? 0;
                planning.Start2Id = model.Start2Id ?? 0;
                planning.Stop2Id = model.Stop2Id ?? 0;
                planning.Start3Id = model.Start3Id ?? 0;
                planning.Stop3Id = model.Stop3Id ?? 0;
                planning.Start4Id = model.Start4Id ?? 0;
                planning.Stop4Id = model.Stop4Id ?? 0;
                planning.Start5Id = model.Start5Id ?? 0;
                planning.Stop5Id = model.Stop5Id ?? 0;
            }

            // Write-time mode marker: this save rewrites the Start/Stop
            // registrations, so the row is (re-)registered under the site's
            // CURRENT input mode.
            planning.RegisteredUnderOneMinuteIntervals = assignedSite.UseOneMinuteIntervals;

            planning.WorkerComment = model.WorkerComment;

            planning = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planning);

            // Approach C: infer the per-shift pause override from the submitted
            // Break{N}Shift (change-detected against the pre-edit Pause{N}Id) or
            // honor an explicit web clear/value signal. Never touches the recorded
            // Pause*StartedAt/StoppedAt timestamps.
            ApplyInferredPauseOverrides(planning, model, preEditShownTicks, exactHandledShifts);

            double nettoMinutes = ComputePlanningNettoMinutes(planning, assignedSite.UseOneMinuteIntervals);

            double hours = nettoMinutes / 60;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

            // Phase 2: when UseOneMinuteIntervals is on, recompute NettoHours
            // from DateTime deltas (precise to the second) and write
            // *InSeconds columns as the source of truth; back-derive the
            // legacy double hour fields. Flag-off path stays byte-identical.
            if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
            {
                PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                    planning,
                    preTimePlanning?.SumFlexEndInSeconds ?? 0,
                    preTimePlanning != null);
            }
            else
            {
                planning.NettoHours = hours;
                var preSumFlexEnd = preTimePlanning?.SumFlexEnd ?? 0;
                planning.SumFlexStart = preSumFlexEnd;
                planning.SumFlexEnd = preSumFlexEnd + planning.NettoHours -
                                      planning.PlanHours -
                                      planning.PaiedOutFlex;
                planning.Flex = planning.NettoHours - planning.PlanHours;
            }

            // Ensure timestamps are populated from IDs for accurate time tracking calculation
            EnsureTimestampsFromIds(planning);

            // Compute time tracking fields (seconds-based calculation)
            PlanRegistrationHelper.ComputeTimeTrackingFields(planning);
            planning.UpdatedByUserId = currentUser.Id;

            await planning.Update(dbContext).ConfigureAwait(false);
            var todayDateMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);

            var planningsAfterThisPlanning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == planning.SdkSitId)
                .Where(x => x.Date > planning.Date)
                .Where(x => x.Date < todayDateMidnight.AddDays(1))
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var planningAfterThisPlanning in planningsAfterThisPlanning)
            {
                var preTimePlanningAfterThisPlanning =
                    await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < planningAfterThisPlanning.Date
                                    && x.SdkSitId == planningAfterThisPlanning.SdkSitId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                // Phase 2: when UseOneMinuteIntervals is on, replay the
                // SumFlex chain through subsequent days using *InSeconds as
                // the source of truth so accumulated rounding does not drift.
                if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
                {
                    PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                        planningAfterThisPlanning,
                        preTimePlanningAfterThisPlanning?.SumFlexEndInSeconds ?? 0,
                        preTimePlanningAfterThisPlanning != null);
                }
                else if (preTimePlanningAfterThisPlanning != null)
                {
                    planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                    if (planningAfterThisPlanning.NettoHoursOverrideActive)
                    {
                        planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                               planningAfterThisPlanning.NettoHoursOverride -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHoursOverride - planningAfterThisPlanning.PlanHours;
                    }
                    else
                    {
                        planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                               planningAfterThisPlanning.NettoHours -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                    }
                }
                else
                {
                    // No previous planning found, start from 0
                    if (planningAfterThisPlanning.NettoHoursOverrideActive)
                    {
                        planningAfterThisPlanning.SumFlexEnd = planningAfterThisPlanning.NettoHoursOverride -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHoursOverride - planningAfterThisPlanning.PlanHours;
                    }
                    else
                    {
                        planningAfterThisPlanning.SumFlexEnd = planningAfterThisPlanning.NettoHours -
                                                               planningAfterThisPlanning.PlanHours -
                                                               planningAfterThisPlanning.PaiedOutFlex;
                        planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                    }
                    planningAfterThisPlanning.SumFlexStart = 0;
                }

                await planningAfterThisPlanning.Update(dbContext).ConfigureAwait(false);
            }

            return new OperationResult(
                true,
                localizationService.GetString("SuccessfullyUpdatedPlanning"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            // See the matching note at the bottom of Update() — log the full
            // exception so the catch leaves a usable stack trace in stdout
            // for CI diagnosis.
            logger.LogError(e, "TimePlanningPlanningService.UpdateByCurrentUserNam failed");
            return new OperationResult(
                false,
                localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }

    /// <summary>
    /// Ensures timestamp fields are populated from ID fields when timestamps are missing.
    /// This allows ComputeTimeTrackingFields to work with both ID-based and timestamp-based data.
    /// IDs use 5-minute intervals, so timestamps are rounded to nearest 5 minutes.
    /// </summary>
    internal static void EnsureTimestampsFromIds(PlanRegistration planning)
    {
        var midnight = new DateTime(planning.Date.Year, planning.Date.Month, planning.Date.Day, 0, 0, 0);

        // Convert Start/Stop IDs to timestamps if timestamps are missing
        if (planning.Start1StartedAt == null && planning.Start1Id > 0)
        {
            planning.Start1StartedAt = midnight.AddMinutes((planning.Start1Id - 1) * 5);
        }
        if (planning.Stop1StoppedAt == null && planning.Stop1Id > 0)
        {
            planning.Stop1StoppedAt = midnight.AddMinutes((planning.Stop1Id - 1) * 5);
        }

        if (planning.Start2StartedAt == null && planning.Start2Id > 0)
        {
            planning.Start2StartedAt = midnight.AddMinutes((planning.Start2Id - 1) * 5);
        }
        if (planning.Stop2StoppedAt == null && planning.Stop2Id > 0)
        {
            planning.Stop2StoppedAt = midnight.AddMinutes((planning.Stop2Id - 1) * 5);
        }

        if (planning.Start3StartedAt == null && planning.Start3Id > 0)
        {
            planning.Start3StartedAt = midnight.AddMinutes((planning.Start3Id - 1) * 5);
        }
        if (planning.Stop3StoppedAt == null && planning.Stop3Id > 0)
        {
            planning.Stop3StoppedAt = midnight.AddMinutes((planning.Stop3Id - 1) * 5);
        }

        if (planning.Start4StartedAt == null && planning.Start4Id > 0)
        {
            planning.Start4StartedAt = midnight.AddMinutes((planning.Start4Id - 1) * 5);
        }
        if (planning.Stop4StoppedAt == null && planning.Stop4Id > 0)
        {
            planning.Stop4StoppedAt = midnight.AddMinutes((planning.Stop4Id - 1) * 5);
        }

        if (planning.Start5StartedAt == null && planning.Start5Id > 0)
        {
            planning.Start5StartedAt = midnight.AddMinutes((planning.Start5Id - 1) * 5);
        }
        if (planning.Stop5StoppedAt == null && planning.Stop5Id > 0)
        {
            planning.Stop5StoppedAt = midnight.AddMinutes((planning.Stop5Id - 1) * 5);
        }

        // Convert Pause IDs to timestamps if timestamps are missing.
        // FIX 3: never fabricate documentation timestamps for a shift carrying a
        // pause override — the override (not the stale Pause{N}Id) drives the
        // total, and the observation columns must reflect only real recorded
        // pauses, not a synthesized pair derived from the coarse legacy field.
        if (planning.Pause1OverrideMinutes == null
            && planning.Pause1StartedAt == null && planning.Pause1StoppedAt == null && planning.Pause1Id > 0)
        {
            // Assume pause starts after start and lasts for the specified duration
            // We'll place it at the midpoint of the shift for simplicity
            var pauseDurationMinutes = (planning.Pause1Id - 1) * 5;
            if (planning.Start1StartedAt != null && planning.Stop1StoppedAt != null)
            {
                var shiftMidpoint = planning.Start1StartedAt.Value.AddMinutes(
                    (planning.Stop1StoppedAt.Value - planning.Start1StartedAt.Value).TotalMinutes / 2);
                planning.Pause1StartedAt = shiftMidpoint;
                planning.Pause1StoppedAt = shiftMidpoint.AddMinutes(pauseDurationMinutes);
            }
        }

        if (planning.Pause2OverrideMinutes == null
            && planning.Pause2StartedAt == null && planning.Pause2StoppedAt == null && planning.Pause2Id > 0)
        {
            var pauseDurationMinutes = (planning.Pause2Id - 1) * 5;
            if (planning.Start2StartedAt != null && planning.Stop2StoppedAt != null)
            {
                var shiftMidpoint = planning.Start2StartedAt.Value.AddMinutes(
                    (planning.Stop2StoppedAt.Value - planning.Start2StartedAt.Value).TotalMinutes / 2);
                planning.Pause2StartedAt = shiftMidpoint;
                planning.Pause2StoppedAt = shiftMidpoint.AddMinutes(pauseDurationMinutes);
            }
        }

        if (planning.Pause3OverrideMinutes == null
            && planning.Pause3StartedAt == null && planning.Pause3StoppedAt == null && planning.Pause3Id > 0)
        {
            var pauseDurationMinutes = (planning.Pause3Id - 1) * 5;
            if (planning.Start3StartedAt != null && planning.Stop3StoppedAt != null)
            {
                var shiftMidpoint = planning.Start3StartedAt.Value.AddMinutes(
                    (planning.Stop3StoppedAt.Value - planning.Start3StartedAt.Value).TotalMinutes / 2);
                planning.Pause3StartedAt = shiftMidpoint;
                planning.Pause3StoppedAt = shiftMidpoint.AddMinutes(pauseDurationMinutes);
            }
        }

        if (planning.Pause4OverrideMinutes == null
            && planning.Pause4StartedAt == null && planning.Pause4StoppedAt == null && planning.Pause4Id > 0)
        {
            var pauseDurationMinutes = (planning.Pause4Id - 1) * 5;
            if (planning.Start4StartedAt != null && planning.Stop4StoppedAt != null)
            {
                var shiftMidpoint = planning.Start4StartedAt.Value.AddMinutes(
                    (planning.Stop4StoppedAt.Value - planning.Start4StartedAt.Value).TotalMinutes / 2);
                planning.Pause4StartedAt = shiftMidpoint;
                planning.Pause4StoppedAt = shiftMidpoint.AddMinutes(pauseDurationMinutes);
            }
        }

        if (planning.Pause5OverrideMinutes == null
            && planning.Pause5StartedAt == null && planning.Pause5StoppedAt == null && planning.Pause5Id > 0)
        {
            var pauseDurationMinutes = (planning.Pause5Id - 1) * 5;
            if (planning.Start5StartedAt != null && planning.Stop5StoppedAt != null)
            {
                var shiftMidpoint = planning.Start5StartedAt.Value.AddMinutes(
                    (planning.Stop5StoppedAt.Value - planning.Start5StartedAt.Value).TotalMinutes / 2);
                planning.Pause5StartedAt = shiftMidpoint;
                planning.Pause5StoppedAt = shiftMidpoint.AddMinutes(pauseDurationMinutes);
            }
        }
    }

    /// <summary>
    /// Writes Start{N}StartedAt as planning.Date.Date + minutes-of-day.
    /// Under UseOneMinuteIntervals=true this is the authoritative store
    /// for the admin-edit actual shift start (the legacy Start{N}Id
    /// column remains a 5-minute-quantized fallback).
    /// </summary>
    private static void ApplyExactMinuteStart(PlanRegistration planning, int shift, int minutes)
    {
        var anchor = planning.Date.Date + TimeSpan.FromMinutes(minutes);
        switch (shift)
        {
            case 1: planning.Start1StartedAt = anchor; break;
            case 2: planning.Start2StartedAt = anchor; break;
            case 3: planning.Start3StartedAt = anchor; break;
            case 4: planning.Start4StartedAt = anchor; break;
            case 5: planning.Start5StartedAt = anchor; break;
        }
    }

    /// <summary>
    /// Writes Stop{N}StoppedAt as planning.Date.Date + minutes-of-day, advancing
    /// by one day when the stop minute is at or before the matching shift's start
    /// (cross-midnight). Anchored to the matching Start{N}StartedAt's date when set.
    /// </summary>
    private static void ApplyExactMinuteStop(PlanRegistration planning, int shift, int minutes)
    {
        DateTime? startStamp = shift switch
        {
            1 => planning.Start1StartedAt,
            2 => planning.Start2StartedAt,
            3 => planning.Start3StartedAt,
            4 => planning.Start4StartedAt,
            5 => planning.Start5StartedAt,
            _ => null,
        };
        var baseDate = planning.Date.Date;
        if (startStamp.HasValue)
        {
            var startMinutes = (int)(startStamp.Value - startStamp.Value.Date).TotalMinutes;
            if (minutes <= startMinutes)
            {
                baseDate = baseDate.AddDays(1);
            }
        }
        var anchor = baseDate + TimeSpan.FromMinutes(minutes);
        switch (shift)
        {
            case 1: planning.Stop1StoppedAt = anchor; break;
            case 2: planning.Stop2StoppedAt = anchor; break;
            case 3: planning.Stop3StoppedAt = anchor; break;
            case 4: planning.Stop4StoppedAt = anchor; break;
            case 5: planning.Stop5StoppedAt = anchor; break;
        }
    }

    private static void DeriveLegacyShiftIdsFromTimestamps(PlanRegistration planning)
    {
        static int TickId(DateTime? ts) =>
            ts.HasValue ? ts.Value.Hour * 12 + ts.Value.Minute / 5 + 1 : 0;

        planning.Start1Id = TickId(planning.Start1StartedAt);
        planning.Stop1Id  = TickId(planning.Stop1StoppedAt);
        planning.Start2Id = TickId(planning.Start2StartedAt);
        planning.Stop2Id  = TickId(planning.Stop2StoppedAt);
        planning.Start3Id = TickId(planning.Start3StartedAt);
        planning.Stop3Id  = TickId(planning.Stop3StoppedAt);
        planning.Start4Id = TickId(planning.Start4StartedAt);
        planning.Stop4Id  = TickId(planning.Stop4StoppedAt);
        planning.Start5Id = TickId(planning.Start5StartedAt);
        planning.Stop5Id  = TickId(planning.Stop5StoppedAt);
    }

    /// <summary>
    /// Capture each shift's PRE-EDIT EFFECTIVE shown coarse tick from the persisted
    /// entity BEFORE the model is applied. This MUST mirror exactly what the read
    /// path (<see cref="PlanRegistrationHelper.ProjectPauseOverridesOntoDto"/>)
    /// emits as Break{N}Shift, because that is the value the client round-trips:
    ///   • override set    → (Pause{N}OverrideMinutes / 5) + 1
    ///   • no override     → raw Pause{N}Id
    /// <see cref="ApplyInferredPauseOverrides"/> change-detects an admin/manual
    /// pause edit by comparing model.Break{N}Shift against this shown tick. Using
    /// the SHOWN tick (not the raw Pause{N}Id) is what makes a re-save IDEMPOTENT
    /// for a non-5-multiple override (e.g. 33 → served tick 7): an unrelated save
    /// round-trips Break{N}Shift = 7 == shown tick, so inference leaves the exact
    /// override untouched instead of rounding it down to (7-1)*5 = 30. It also
    /// avoids spuriously locking an override when only start/stop changed on a row
    /// whose slot-sum is not a 5-minute multiple (16288 shape).
    /// </summary>
    internal static int[] CaptureCurrentShiftShownTicks(PlanRegistration planning)
    {
        var ticks = new int[6]; // index 0 unused; shifts are 1..5
        for (var shift = 1; shift <= 5; shift++)
        {
            var overrideMinutes = PlanRegistrationHelper.GetShiftPauseOverrideMinutes(planning, shift);
            ticks[shift] = overrideMinutes.HasValue
                ? (overrideMinutes.Value / 5) + 1
                : GetShiftPauseId(planning, shift);
        }
        return ticks;
    }

    private static int GetShiftPauseId(PlanRegistration planning, int shift) => shift switch
    {
        1 => planning.Pause1Id,
        2 => planning.Pause2Id,
        3 => planning.Pause3Id,
        4 => planning.Pause4Id,
        5 => planning.Pause5Id,
        _ => 0
    };

    /// <summary>
    /// Change-detected, NON-destructive pause override inference (Approach C).
    ///
    /// Precedence per shift:
    ///  1. EXPLICIT web signal (FIX 2): when the dialog marks a shift as explicitly
    ///     specified (<see cref="TimePlanningPlanningPrDayModel.ClearPauseOverrides"/>
    ///     or the per-shift Pause{N}OverrideMinutesSpecified flag), honor it
    ///     directly — a value sets the override, an explicit clear reverts to null
    ///     (compute-from-slots) — and SKIP inference for that shift.
    ///  2. EXACT-minute path (FIX 4): a shift already handled by the flag-ON
    ///     exact-minute loop is in <paramref name="handledShifts"/>; its override
    ///     must win, so inference skips it.
    ///  3. INFERENCE: derive the submitted coarse tick from Break{N}Shift and
    ///     compare it against the PRE-EDIT EFFECTIVE SHOWN tick
    ///     (<paramref name="preEditShownTicks"/>) — the same value the read path
    ///     round-trips (override → (override/5)+1, else raw Pause{N}Id). Only when
    ///     they DIFFER does the user-changed-the-pause signal fire and set the
    ///     override to (Break{N}Shift - 1) * 5 minutes (Break{N}Shift == 0 →
    ///     override 0). When equal, the override is left UNTOUCHED so (a) editing
    ///     only start/stop never spuriously locks one and (b) an unrelated re-save
    ///     of a row with a non-5-multiple override (e.g. 33, shown as tick 7) is
    ///     IDEMPOTENT — the exact override survives instead of rounding to 30. A
    ///     genuine re-edit (Break{N}Shift differs from the shown tick) still updates
    ///     the override.
    ///
    /// This never touches any Pause*StartedAt/StoppedAt timestamps — the worker's
    /// recorded pauses are preserved as documentation.
    /// </summary>
    internal static void ApplyInferredPauseOverrides(
        PlanRegistration planning,
        TimePlanningPlanningPrDayModel model,
        int[] preEditShownTicks,
        ISet<int> handledShifts)
    {
        for (var shift = 1; shift <= 5; shift++)
        {
            // (1) Explicit web signal wins (set value or explicit clear → null).
            if (model.ClearPauseOverrides || GetModelOverrideSpecified(model, shift))
            {
                PlanRegistrationHelper.SetShiftPauseOverrideMinutes(
                    planning, shift, model.ClearPauseOverrides ? null : GetModelPauseOverrideMinutes(model, shift));
                continue;
            }

            // (2) Exact-minute path already set this shift's override; do not clobber.
            if (handledShifts.Contains(shift))
            {
                continue;
            }

            // (3) Inference: compare the submitted coarse tick against the pre-edit
            //     EFFECTIVE SHOWN tick (the same value the read path round-trips).
            //     Equal → the user did not touch the pause → leave the override
            //     as-is (idempotent: a non-5-multiple override survives a re-save).
            var breakShift = GetModelBreakShift(model, shift);
            if (breakShift == preEditShownTicks[shift])
            {
                continue;
            }

            // Changed: Break{N}Shift > 0 → (Break{N}Shift - 1) * 5 minutes;
            // Break{N}Shift == 0 → empty pause → 0 minutes.
            var submittedMinutes = breakShift > 0 ? (breakShift - 1) * 5 : 0;
            PlanRegistrationHelper.SetShiftPauseOverrideMinutes(planning, shift, submittedMinutes);
        }
    }

    private static int GetModelBreakShift(TimePlanningPlanningPrDayModel model, int shift) => shift switch
    {
        1 => model.Break1Shift,
        2 => model.Break2Shift,
        3 => model.Break3Shift,
        4 => model.Break4Shift,
        5 => model.Break5Shift,
        _ => 0
    };

    private static int? GetModelPauseOverrideMinutes(TimePlanningPlanningPrDayModel model, int shift) => shift switch
    {
        1 => model.Pause1OverrideMinutes,
        2 => model.Pause2OverrideMinutes,
        3 => model.Pause3OverrideMinutes,
        4 => model.Pause4OverrideMinutes,
        5 => model.Pause5OverrideMinutes,
        _ => null
    };

    private static bool GetModelOverrideSpecified(TimePlanningPlanningPrDayModel model, int shift) => shift switch
    {
        1 => model.Pause1OverrideMinutesSpecified,
        2 => model.Pause2OverrideMinutesSpecified,
        3 => model.Pause3OverrideMinutesSpecified,
        4 => model.Pause4OverrideMinutesSpecified,
        5 => model.Pause5OverrideMinutesSpecified,
        _ => false
    };

    /// <summary>
    /// Computes total netto minutes (raw minutes; caller divides by 60 for hours)
    /// across all 5 shifts. Under flag-on, when both Start{N}StartedAt and
    /// Stop{N}StoppedAt are set, uses (Stop - Start).TotalMinutes minus the
    /// timestamp-derived pause minutes for that shift. Otherwise falls back to
    /// the legacy ((Stop{N}Id - Start{N}Id) - max(Pause{N}Id - 1, 0)) * 5 math.
    /// </summary>
    private static double ComputePlanningNettoMinutes(PlanRegistration planning, bool useOneMinuteIntervals)
    {
        const int multiplier = 5;
        double total = 0;
        for (var shift = 1; shift <= 5; shift++)
        {
            var (startedAt, stoppedAt, pauseStarted, pauseStopped, startId, stopId, pauseId) = GetShiftTimings(planning, shift);

            // Admin/manual pause override wins: when set, it is the authoritative
            // total pause MINUTES for the shift, replacing both the one-minute
            // timestamp delta and the legacy (Pause{N}Id-1)*5 tick deduction.
            var overrideMinutes = PlanRegistrationHelper.GetShiftPauseOverrideMinutes(planning, shift);

            if (useOneMinuteIntervals && startedAt.HasValue && stoppedAt.HasValue && stoppedAt.Value > startedAt.Value)
            {
                var shiftMinutes = (stoppedAt.Value - startedAt.Value).TotalMinutes;
                double pauseMinutes;
                if (overrideMinutes.HasValue)
                {
                    pauseMinutes = overrideMinutes.Value;
                }
                else if (pauseStarted.HasValue && pauseStopped.HasValue && pauseStopped.Value > pauseStarted.Value)
                {
                    pauseMinutes = (pauseStopped.Value - pauseStarted.Value).TotalMinutes;
                }
                else
                {
                    pauseMinutes = 0;
                }
                total += shiftMinutes - pauseMinutes;
            }
            else
            {
                if (stopId >= startId && stopId != 0)
                {
                    double sm = (stopId - startId) * (double)multiplier;
                    sm -= overrideMinutes ?? (pauseId > 0 ? (pauseId - 1) * multiplier : 0);
                    total += sm;
                }
            }
        }
        return total;
    }

    private static (DateTime? StartedAt, DateTime? StoppedAt, DateTime? PauseStarted, DateTime? PauseStopped, int StartId, int StopId, int PauseId)
        GetShiftTimings(PlanRegistration planning, int shift) => shift switch
    {
        1 => (planning.Start1StartedAt, planning.Stop1StoppedAt, planning.Pause1StartedAt, planning.Pause1StoppedAt, planning.Start1Id, planning.Stop1Id, planning.Pause1Id),
        2 => (planning.Start2StartedAt, planning.Stop2StoppedAt, planning.Pause2StartedAt, planning.Pause2StoppedAt, planning.Start2Id, planning.Stop2Id, planning.Pause2Id),
        3 => (planning.Start3StartedAt, planning.Stop3StoppedAt, planning.Pause3StartedAt, planning.Pause3StoppedAt, planning.Start3Id, planning.Stop3Id, planning.Pause3Id),
        4 => (planning.Start4StartedAt, planning.Stop4StoppedAt, planning.Pause4StartedAt, planning.Pause4StoppedAt, planning.Start4Id, planning.Stop4Id, planning.Pause4Id),
        5 => (planning.Start5StartedAt, planning.Stop5StoppedAt, planning.Pause5StartedAt, planning.Pause5StoppedAt, planning.Start5Id, planning.Stop5Id, planning.Pause5Id),
        _ => (null, null, null, null, 0, 0, 0),
    };

    public async Task<OperationDataResult<PlanRegistrationVersionHistoryModel>> GetVersionHistory(int planRegistrationId)
    {
        try
        {
            var currentUserAsync = await userService.GetCurrentUserAsync();
            // Get the plan registration to find the associated site
            var planRegistration = await dbContext.PlanRegistrations
                .Where(x => x.Id == planRegistrationId)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (planRegistration == null)
            {
                return new OperationDataResult<PlanRegistrationVersionHistoryModel>(
                    false,
                    localizationService.GetString("PlanRegistrationNotFound"));
            }

            // GPS/Snapshot always come from the TimePlanning GLOBAL (per-customer) settings,
            // never from the per-site AssignedSite column, matching the serve paths in
            // TimeSettingService.
            var gpsEnabled = options.Value.GpsEnabled == "1";
            var snapshotEnabled = options.Value.SnapshotEnabled == "1";

            // Get all versions for this plan registration, ordered by version descending (newest first)
            var versions = await dbContext.PlanRegistrationVersions
                .Where(x => x.PlanRegistrationId == planRegistrationId)
                .OrderByDescending(x => x.Version)
                .ToListAsync().ConfigureAwait(false);

            var result = new PlanRegistrationVersionHistoryModel
            {
                PlanRegistrationId = planRegistrationId,
                GpsEnabled = gpsEnabled,
                SnapshotEnabled = snapshotEnabled,
                Versions = []
            };

            // Compare each version with the previous one
            for (int i = 0; i < versions.Count; i++)
            {
                var currentVersion = versions[i];
                var previousVersion = i < versions.Count - 1 ? versions[i + 1] : null;

                var changes = CompareVersions(currentVersion, previousVersion, currentUserAsync?.Id ?? 0);

                // Get GPS coordinates for this version
                if (gpsEnabled && changes.Any())
                {
                    var lastChange = changes.Last();
                    var gpsCoordinates = await dbContext.GpsCoordinates
                        .Where(x => x.PlanRegistrationId == planRegistrationId)
                        .Where(x => x.RegistrationType == lastChange.FieldName)
                        .ToListAsync().ConfigureAwait(false);

                    foreach (var gps in gpsCoordinates)
                    {
                        changes.Add(new FieldChange
                        {
                            FieldName = "GPS",
                            FromValue = "",
                            ToValue = string.Format(GoogleMapsUrlTemplate, gps.Latitude, gps.Longitude),
                            FieldType = "gps",
                            Latitude = gps.Latitude,
                            Longitude = gps.Longitude,
                            RegistrationType = gps.RegistrationType
                        });
                    }
                }

                // Get picture snapshots for this version
                if (snapshotEnabled && changes.Any())
                {
                    var lastChange = changes.Last();
                    var snapshots = await dbContext.PictureSnapshots
                        .Where(x => x.PlanRegistrationId == planRegistrationId)
                        .Where(x => x.RegistrationType == lastChange.FieldName)
                        .ToListAsync().ConfigureAwait(false);

                    foreach (var snapshot in snapshots)
                    {
                        changes.Add(new FieldChange
                        {
                            FieldName = "Snapshot",
                            FromValue = "",
                            ToValue = $"{snapshot.FileName}.{snapshot.FileExtension}",
                            FieldType = "snapshot",
                            PictureHash = snapshot.PictureHash,
                            RegistrationType = snapshot.RegistrationType,
                        });
                    }
                }

                if (changes.Any())
                {
                    result.Versions.Add(new PlanRegistrationVersionModel
                    {
                        Version = currentVersion.Version,
                        UpdatedAt = currentVersion.UpdatedAt ?? DateTime.UtcNow,
                        UpdatedByUserId = currentVersion.UpdatedByUserId,
                        UpdatedByUserName = currentVersion.UpdatedByUserId == 0 ? "System"
                            : await userService.GetFullNameUserByUserIdAsync(currentVersion.UpdatedByUserId).ConfigureAwait(false),
                        Changes = changes
                    });
                }
            }

            return new OperationDataResult<PlanRegistrationVersionHistoryModel>(true, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting version history for PlanRegistration {PlanRegistrationId}", planRegistrationId);
            return new OperationDataResult<PlanRegistrationVersionHistoryModel>(
                false,
                localizationService.GetString("ErrorWhileGettingVersionHistory"));
        }
    }

    private List<FieldChange> CompareVersions(PlanRegistrationVersion current, PlanRegistrationVersion? previous, int currentUserId)
    {
        var changes = new List<FieldChange>();

        // Compare all relevant fields
        CompareBoolField(changes, "PlanChangedByAdmin", previous?.PlanChangedByAdmin, current.PlanChangedByAdmin);
        CompareField(changes, "PlanText", previous?.PlanText, current.PlanText);
        CompareField(changes, "PlanHours", previous?.PlanHours.ToString(), current.PlanHours.ToString());
        CompareField(changes, "NettoHours", previous?.NettoHours.ToString(), current.NettoHours.ToString());
        CompareField(changes, "Flex", previous?.Flex.ToString(), current.Flex.ToString());
        CompareField(changes, "SumFlexStart", previous?.SumFlexStart.ToString(), current.SumFlexStart.ToString());
        CompareField(changes, "SumFlexEnd", previous?.SumFlexEnd.ToString(), current.SumFlexEnd.ToString());
        CompareField(changes, "PaiedOutFlex", previous?.PaiedOutFlex.ToString(), current.PaiedOutFlex.ToString());
        CompareField(changes, "NettoHoursOverride", previous?.NettoHoursOverride.ToString(), current.NettoHoursOverride.ToString());
        CompareField(changes, "NettoHoursOverrideActive", previous?.NettoHoursOverrideActive.ToString(), current.NettoHoursOverrideActive.ToString());

        // Comments
        CompareField(changes, "CommentOffice", previous?.CommentOffice, current.CommentOffice);
        CompareField(changes, "WorkerComment", previous?.WorkerComment, current.WorkerComment);

        // Absence flags
        CompareBoolField(changes, "OnVacation", previous?.OnVacation, current.OnVacation);
        CompareBoolField(changes, "Sick", previous?.Sick, current.Sick);
        CompareBoolField(changes, "OtherAllowedAbsence", previous?.OtherAllowedAbsence, current.OtherAllowedAbsence);
        CompareBoolField(changes, "AbsenceWithoutPermission", previous?.AbsenceWithoutPermission, current.AbsenceWithoutPermission);

        // All planned shifts and pauses
        CompareField(changes, "PlannedStartOfShift1", previous?.PlannedStartOfShift1.ToString(), current.PlannedStartOfShift1.ToString());
        CompareField(changes, "PlannedEndOfShift1", previous?.PlannedEndOfShift1.ToString(), current.PlannedEndOfShift1.ToString());
        CompareField(changes, "PlannedStartOfShift2", previous?.PlannedStartOfShift2.ToString(), current.PlannedStartOfShift2.ToString());
        CompareField(changes, "PlannedEndOfShift2", previous?.PlannedEndOfShift2.ToString(), current.PlannedEndOfShift2.ToString());
        CompareField(changes, "PlannedStartOfShift3", previous?.PlannedStartOfShift3.ToString(), current.PlannedStartOfShift3.ToString());
        CompareField(changes, "PlannedEndOfShift3", previous?.PlannedEndOfShift3.ToString(), current.PlannedEndOfShift3.ToString());
        CompareField(changes, "PlannedStartOfShift4", previous?.PlannedStartOfShift4.ToString(), current.PlannedStartOfShift4.ToString());
        CompareField(changes, "PlannedEndOfShift4", previous?.PlannedEndOfShift4.ToString(), current.PlannedEndOfShift4.ToString());
        CompareField(changes, "PlannedStartOfShift5", previous?.PlannedStartOfShift5.ToString(), current.PlannedStartOfShift5.ToString());
        CompareField(changes, "PlannedEndOfShift5", previous?.PlannedEndOfShift5.ToString(), current.PlannedEndOfShift5.ToString());
        CompareField(changes, "PlannedBreakOfShift1", previous?.PlannedBreakOfShift1.ToString(), current.PlannedBreakOfShift1.ToString());
        CompareField(changes, "PlannedBreakOfShift2", previous?.PlannedBreakOfShift2.ToString(), current.PlannedBreakOfShift2.ToString());
        CompareField(changes, "PlannedBreakOfShift3", previous?.PlannedBreakOfShift3.ToString(), current.PlannedBreakOfShift3.ToString());
        CompareField(changes, "PlannedBreakOfShift4", previous?.PlannedBreakOfShift4.ToString(), current.PlannedBreakOfShift4.ToString());
        CompareField(changes, "PlannedBreakOfShift5", previous?.PlannedBreakOfShift5.ToString(), current.PlannedBreakOfShift5.ToString());

        if (currentUserId == 1)
        {
            CompareField(changes, "Start1Id", previous?.Start1Id.ToString(), current.Start1Id.ToString());
            CompareField(changes, "Stop1Id", previous?.Stop1Id.ToString(), current.Stop1Id.ToString());
            CompareField(changes, "Start2Id", previous?.Start2Id.ToString(), current.Start2Id.ToString());
            CompareField(changes, "Stop2Id", previous?.Stop2Id.ToString(), current.Stop2Id.ToString());
            CompareField(changes, "Start3Id", previous?.Start3Id.ToString(), current.Start3Id.ToString());
            CompareField(changes, "Stop3Id", previous?.Stop3Id.ToString(), current.Stop3Id.ToString());
            CompareField(changes, "Start4Id", previous?.Start4Id.ToString(), current.Start4Id.ToString());
            CompareField(changes, "Stop4Id", previous?.Stop4Id.ToString(), current.Stop4Id.ToString());
            CompareField(changes, "Start5Id", previous?.Start5Id.ToString(), current.Start5Id.ToString());
            CompareField(changes, "Stop5Id", previous?.Stop5Id.ToString(), current.Stop5Id.ToString());
            CompareField(changes, "Pause1Id", previous?.Pause1Id.ToString(), current.Pause1Id.ToString());
            CompareField(changes, "Pause2Id", previous?.Pause2Id.ToString(), current.Pause2Id.ToString());
            CompareField(changes, "Pause3Id", previous?.Pause3Id.ToString(), current.Pause3Id.ToString());
            CompareField(changes, "Pause4Id", previous?.Pause4Id.ToString(), current.Pause4Id.ToString());
            CompareField(changes, "Pause5Id", previous?.Pause5Id.ToString(), current.Pause5Id.ToString());
            CompareField(changes, "AbsenceHours", previous?.AbsenceHours.ToString(), current.AbsenceHours.ToString());
            CompareField(changes, "EffectiveNetHours", previous?.EffectiveNetHours.ToString(), current.EffectiveNetHours.ToString());
            CompareDateTimeField(changes, "FirstWorkStartUtc", previous?.FirstWorkStartUtc, current.FirstWorkStartUtc);
            CompareField(changes, "HolidayHours", previous?.HolidayHours.ToString(), current.HolidayHours.ToString());
            CompareBoolField(changes, "IsSaturday", previous?.IsSaturday, current.IsSaturday);
            CompareBoolField(changes, "IsSunday", previous?.IsSunday, current.IsSunday);
            CompareDateTimeField(changes, "LastWorkEndUtc", previous?.LastWorkEndUtc, current.LastWorkEndUtc);
            CompareField(changes, "NightHours", previous?.NightHours.ToString(), current.NightHours.ToString());
            CompareField(changes, "NormalHours", previous?.NormalHours.ToString(), current.NormalHours.ToString());
            CompareField(changes, "OvertimeHours", previous?.OvertimeHours.ToString(), current.OvertimeHours.ToString());
            CompareBoolField(changes, "RuleEngineCalculated", previous?.RuleEngineCalculated, current.RuleEngineCalculated);
            CompareDateTimeField(changes, "RuleEngineCalculatedAt", previous?.RuleEngineCalculatedAt, current.RuleEngineCalculatedAt);
            CompareField(changes, "WeekendHours", previous?.WeekendHours.ToString(), current.WeekendHours.ToString());
            CompareField(changes, "AbsenceHoursInSeconds", previous?.AbsenceHoursInSeconds.ToString(), current.AbsenceHoursInSeconds.ToString());
            CompareField(changes, "EffectiveNetHoursInSeconds", previous?.EffectiveNetHoursInSeconds.ToString(), current.EffectiveNetHoursInSeconds.ToString());
            CompareField(changes, "FlexInSeconds", previous?.FlexInSeconds.ToString(), current.FlexInSeconds.ToString());
            CompareField(changes, "HolidayHoursInSeconds", previous?.HolidayHoursInSeconds.ToString(), current.HolidayHoursInSeconds.ToString());
            CompareField(changes, "NettoHoursOverrideInSeconds", previous?.NettoHoursOverrideInSeconds.ToString(), current.NettoHoursOverrideInSeconds.ToString());
            CompareField(changes, "NightHoursInSeconds", previous?.NightHoursInSeconds.ToString(), current.NightHoursInSeconds.ToString());
            CompareField(changes, "NormalHoursInSeconds", previous?.NormalHoursInSeconds.ToString(), current.NormalHoursInSeconds.ToString());
            CompareField(changes, "OvertimeHoursInSeconds", previous?.OvertimeHoursInSeconds.ToString(), current.OvertimeHoursInSeconds.ToString());
            CompareField(changes, "PaiedOutFlexInSeconds", previous?.PaiedOutFlexInSeconds.ToString(), current.PaiedOutFlexInSeconds.ToString());
            CompareField(changes, "PlanHoursInSeconds", previous?.PlanHoursInSeconds.ToString(), current.PlanHoursInSeconds.ToString());
            CompareField(changes, "SumFlexEndInSeconds", previous?.SumFlexEndInSeconds.ToString(), current.SumFlexEndInSeconds.ToString());
            CompareField(changes, "WeekendHoursInSeconds", previous?.WeekendHoursInSeconds.ToString(), current.WeekendHoursInSeconds.ToString());
            CompareBoolField(changes, "Reconciled", previous?.Reconciled, current.Reconciled);
            CompareDateTimeField(changes, "ReconciledAt", previous?.ReconciledAt, current.ReconciledAt);
            CompareBoolField(changes, "TransferredToPayroll", previous?.TransferredToPayroll, current.TransferredToPayroll);
            CompareDateTimeField(changes, "TransferredToPayrollAt", previous?.TransferredToPayrollAt, current.TransferredToPayrollAt);
            CompareDateTimeField(changes, "ContentHandedOverAtUtc", previous?.ContentHandedOverAtUtc, current.ContentHandedOverAtUtc);
            CompareField(changes, "ContentHandoverFromSdkSitId", previous?.ContentHandoverFromSdkSitId.ToString(), current.ContentHandoverFromSdkSitId.ToString());
            CompareField(changes, "ContentHandoverRequestId", previous?.ContentHandoverRequestId.ToString(), current.ContentHandoverRequestId.ToString());
            CompareField(changes, "ContentHandoverToSdkSitId", previous?.ContentHandoverToSdkSitId.ToString(), current.ContentHandoverToSdkSitId.ToString());
            CompareDateTimeField(changes, "AbsenceApprovedAtUtc", previous?.AbsenceApprovedAtUtc, current.AbsenceApprovedAtUtc);
            CompareField(changes, "AbsenceApprovedBySdkSitId", previous?.AbsenceApprovedBySdkSitId.ToString(), current.AbsenceApprovedBySdkSitId.ToString());
            CompareField(changes, "AbsenceMessageId", previous?.AbsenceMessageId.ToString(), current.AbsenceMessageId.ToString());
            CompareField(changes, "AbsenceRequestId", previous?.AbsenceRequestId.ToString(), current.AbsenceRequestId.ToString());
        }

        // Shift 1
        CompareDateTimeField(changes, "Start1StartedAt", previous?.Start1StartedAt, current.Start1StartedAt);
        CompareDateTimeField(changes, "Stop1StoppedAt", previous?.Stop1StoppedAt, current.Stop1StoppedAt);
        CompareDateTimeField(changes, "Pause1StartedAt", previous?.Pause1StartedAt, current.Pause1StartedAt);
        CompareDateTimeField(changes, "Pause1StoppedAt", previous?.Pause1StoppedAt, current.Pause1StoppedAt);

        // Shift 2
        CompareDateTimeField(changes, "Start2StartedAt", previous?.Start2StartedAt, current.Start2StartedAt);
        CompareDateTimeField(changes, "Stop2StoppedAt", previous?.Stop2StoppedAt, current.Stop2StoppedAt);
        CompareDateTimeField(changes, "Pause2StartedAt", previous?.Pause2StartedAt, current.Pause2StartedAt);
        CompareDateTimeField(changes, "Pause2StoppedAt", previous?.Pause2StoppedAt, current.Pause2StoppedAt);

        // Shift 3
        CompareDateTimeField(changes, "Start3StartedAt", previous?.Start3StartedAt, current.Start3StartedAt);
        CompareDateTimeField(changes, "Stop3StoppedAt", previous?.Stop3StoppedAt, current.Stop3StoppedAt);
        CompareDateTimeField(changes, "Pause3StartedAt", previous?.Pause3StartedAt, current.Pause3StartedAt);
        CompareDateTimeField(changes, "Pause3StoppedAt", previous?.Pause3StoppedAt, current.Pause3StoppedAt);

        // Shift 4
        CompareDateTimeField(changes, "Start4StartedAt", previous?.Start4StartedAt, current.Start4StartedAt);
        CompareDateTimeField(changes, "Stop4StoppedAt", previous?.Stop4StoppedAt, current.Stop4StoppedAt);
        CompareDateTimeField(changes, "Pause4StartedAt", previous?.Pause4StartedAt, current.Pause4StartedAt);
        CompareDateTimeField(changes, "Pause4StoppedAt", previous?.Pause4StoppedAt, current.Pause4StoppedAt);

        // Shift 5
        CompareDateTimeField(changes, "Start5StartedAt", previous?.Start5StartedAt, current.Start5StartedAt);
        CompareDateTimeField(changes, "Stop5StoppedAt", previous?.Stop5StoppedAt, current.Stop5StoppedAt);
        CompareDateTimeField(changes, "Pause5StartedAt", previous?.Pause5StartedAt, current.Pause5StartedAt);
        CompareDateTimeField(changes, "Pause5StoppedAt", previous?.Pause5StoppedAt, current.Pause5StoppedAt);

        return changes;
    }

    private void CompareField(List<FieldChange> changes, string fieldName, string? previousValue, string? currentValue)
    {
        previousValue ??= "";
        currentValue ??= "";

        if (previousValue != currentValue)
        {
            changes.Add(new FieldChange
            {
                FieldName = fieldName,
                FromValue = previousValue,
                ToValue = currentValue,
                FieldType = "standard"
            });
        }
    }

    private void CompareDateTimeField(List<FieldChange> changes, string fieldName, DateTime? previousValue, DateTime? currentValue)
    {
        var prevStr = previousValue?.ToString("yyyy-MM-dd HH:mm:ss.ffffff") ?? "";
        var currStr = currentValue?.ToString("yyyy-MM-dd HH:mm:ss.ffffff") ?? "";

        if (prevStr != currStr)
        {
            changes.Add(new FieldChange
            {
                FieldName = fieldName,
                FromValue = prevStr,
                ToValue = currStr,
                FieldType = "standard"
            });
        }
    }

    private void CompareBoolField(List<FieldChange> changes, string fieldName, bool? previousValue, bool? currentValue)
    {
        var prevBool = previousValue ?? false;
        var currBool = currentValue ?? false;

        if (prevBool != currBool)
        {
            changes.Add(new FieldChange
            {
                FieldName = fieldName,
                FromValue = prevBool.ToString(),
                ToValue = currBool.ToString(),
                FieldType = "standard"
            });
        }
    }
}