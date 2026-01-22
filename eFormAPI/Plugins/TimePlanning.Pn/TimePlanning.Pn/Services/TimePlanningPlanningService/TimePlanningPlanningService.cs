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
                    .Where(x => x.Resigned == model.ShowResignedSites)
                    .ToListAsync().ConfigureAwait(false);

            if (model.SiteId != 0 && model.SiteId != null)
            {
                assignedSites = assignedSites.Where(x => x.SiteId == model.SiteId).ToList();
            }

            foreach (var assignedSite in assignedSites)
            {
                Console.WriteLine($"Resigned site: {assignedSite.SiteId}, Resigned at: {assignedSite.ResignedAtDate}");
            }

            var midnightOfDateFrom = new DateTime(model.DateFrom!.Value.Year, model.DateFrom.Value.Month, model.DateFrom.Value.Day, 0, 0, 0);
            var midnightOfDateTo = new DateTime(model.DateTo!.Value.Year, model.DateTo.Value.Month, model.DateTo.Value.Day, 23, 59, 59);
            var todayMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            var datesInPeriod = new List<DateTime>();
            var date = model.DateFrom;
            while (date <= model.DateTo)
            {
                datesInPeriod.Add(date.Value);
                date = date.Value.AddDays(1);
            }

            var usersList = await baseDbContext.Users
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
            var sitesList = await sdkDbContext.Sites
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
                    PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
                };

                // do a lookup in the baseDbContext.Users where the concat string of FirstName and LastName toLowerCase() is equal to the site.Name toLowerCase()
                // if we find a user, we take the user.EmailSha256 and set the siteModel.AvatarUrl to the gravatar url with the sha256
                var user = usersList.FirstOrDefault(x => (x.FirstName + " " + x.LastName).Replace(" ", "").ToLower() ==
                                                site.Name.Replace(" ", "").ToLower());
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
                        siteModel.SoftwareVersionIsValid = int.Parse(user.TimeRegistrationSoftwareVersion.Replace(".", "")) >= 3114;
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

        var site = worker.SiteWorkers.First().Site;

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
        var date = model.DateFrom;
        while (date <= model.DateTo)
        {
            datesInPeriod.Add(date.Value);
            date = date.Value.AddDays(1);
        }

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = (int)site.MicrotingUid!,
            SiteName = site.Name,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };

        try
        {
            siteModel.SoftwareVersionIsValid = currentUser.TimeRegistrationSoftwareVersion != null &&
                                               int.Parse(currentUser.TimeRegistrationSoftwareVersion.Replace(".", "")) >= 3114;
        }
        catch (Exception)
        {
            // If the version format is invalid, we assume it's not valid
            siteModel.SoftwareVersionIsValid = false;
        }

        var user = await baseDbContext.Users
            .Where(x => (x.FirstName + " " + x.LastName).Replace(" ", "").ToLower() == site.Name.Replace(" ", "").ToLower())
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

        siteModel = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            dbContextHelper.GetDbContext(),
            dbAssignedSite,
            logger,
            site,
            midnightOfDateFrom,
            midnightOfDateTo,
            options);

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

            planning.PlannedStartOfShift1 = model.PlannedStartOfShift1;
            planning.PlannedBreakOfShift1 = model.PlannedBreakOfShift1;
            planning.PlannedEndOfShift1 = model.PlannedEndOfShift1;
            planning.PlannedStartOfShift2 = model.PlannedStartOfShift2;
            planning.PlannedBreakOfShift2 = model.PlannedBreakOfShift2;
            planning.PlannedEndOfShift2 = model.PlannedEndOfShift2;
            planning.CommentOffice = model.CommentOffice;
            planning.NettoHoursOverride = model.NettoHoursOverride;
            planning.NettoHoursOverrideActive = model.NettoHoursOverrideActive;

            if (!planning.PlanChangedByAdmin)
            {
                var entry = dbContext.Entry(planning);
                planning.PlanChangedByAdmin = entry.State == EntityState.Modified;
            }

            planning.UpdatedByUserId = userService.UserId;

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? planning.Pause1Id;
                planning.Pause2Id = model.Pause2Id ?? planning.Pause2Id;
                planning.Pause3Id = model.Pause3Id ?? planning.Pause3Id;
                planning.Pause4Id = model.Pause4Id ?? planning.Pause4Id;
                planning.Pause5Id = model.Pause5Id ?? planning.Pause5Id;
            }
            else
            {
                planning.Shift1PauseNumber = 0;
                planning.Pause1StartedAt = model.Pause1StartedAt;
                planning.Pause1StoppedAt = model.Pause1StoppedAt;
                if (planning.Pause1StartedAt != null && planning.Pause1StoppedAt != null)
                {
                    planning.Shift1PauseNumber = (int)((DateTime)planning.Pause1StoppedAt - (DateTime)planning.Pause1StartedAt).TotalMinutes;
                }
                planning.Pause2StartedAt = model.Pause2StartedAt;
                planning.Pause2StoppedAt = model.Pause2StoppedAt;
                if (planning.Pause2StartedAt != null && planning.Pause2StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause2StoppedAt - (DateTime)planning.Pause2StartedAt).TotalMinutes;
                }
                planning.Pause10StartedAt = model.Pause10StartedAt;
                planning.Pause10StoppedAt = model.Pause10StoppedAt;
                if (planning.Pause10StartedAt != null && planning.Pause10StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause10StoppedAt - (DateTime)planning.Pause10StartedAt).TotalMinutes;
                }

                planning.Pause11StartedAt = model.Pause11StartedAt;
                planning.Pause11StoppedAt = model.Pause11StoppedAt;
                if (planning.Pause11StartedAt != null && planning.Pause11StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause11StoppedAt - (DateTime)planning.Pause11StartedAt).TotalMinutes;
                }
                planning.Pause12StartedAt = model.Pause12StartedAt;
                planning.Pause12StoppedAt = model.Pause12StoppedAt;
                if (planning.Pause12StartedAt != null && planning.Pause12StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause12StoppedAt - (DateTime)planning.Pause12StartedAt).TotalMinutes;
                }
                planning.Pause13StartedAt = model.Pause13StartedAt;
                planning.Pause13StoppedAt = model.Pause13StoppedAt;
                if (planning.Pause13StartedAt != null && planning.Pause13StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause13StoppedAt - (DateTime)planning.Pause13StartedAt).TotalMinutes;
                }
                planning.Pause14StartedAt = model.Pause14StartedAt;
                planning.Pause14StoppedAt = model.Pause14StoppedAt;
                if (planning.Pause14StartedAt != null && planning.Pause14StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause14StoppedAt - (DateTime)planning.Pause14StartedAt).TotalMinutes;
                }
                planning.Pause15StartedAt = model.Pause15StartedAt;
                planning.Pause15StoppedAt = model.Pause15StoppedAt;
                if (planning.Pause15StartedAt != null && planning.Pause15StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause15StoppedAt - (DateTime)planning.Pause15StartedAt).TotalMinutes;
                }
                planning.Pause16StartedAt = model.Pause16StartedAt;
                planning.Pause16StoppedAt = model.Pause16StoppedAt;
                if (planning.Pause16StartedAt != null && planning.Pause16StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause16StoppedAt - (DateTime)planning.Pause16StartedAt).TotalMinutes;
                }
                planning.Pause17StartedAt = model.Pause17StartedAt;
                planning.Pause17StoppedAt = model.Pause17StoppedAt;
                if (planning.Pause17StartedAt != null && planning.Pause17StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause17StoppedAt - (DateTime)planning.Pause17StartedAt).TotalMinutes;
                }
                planning.Pause18StartedAt = model.Pause18StartedAt;
                planning.Pause18StoppedAt = model.Pause18StoppedAt;
                if (planning.Pause18StartedAt != null && planning.Pause18StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause18StoppedAt - (DateTime)planning.Pause18StartedAt).TotalMinutes;
                }
                planning.Pause19StartedAt = model.Pause19StartedAt;
                planning.Pause19StoppedAt = model.Pause19StoppedAt;
                if (planning.Pause19StartedAt != null && planning.Pause19StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause19StoppedAt - (DateTime)planning.Pause19StartedAt).TotalMinutes;
                }
                planning.Pause100StartedAt = model.Pause100StartedAt;
                planning.Pause100StoppedAt = model.Pause100StoppedAt;
                if (planning.Pause100StartedAt != null && planning.Pause100StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause100StoppedAt - (DateTime)planning.Pause100StartedAt).TotalMinutes;
                }
                planning.Pause101StartedAt = model.Pause101StartedAt;
                planning.Pause101StoppedAt = model.Pause101StoppedAt;
                if (planning.Pause101StartedAt != null && planning.Pause101StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause101StoppedAt - (DateTime)planning.Pause101StartedAt).TotalMinutes;
                }
                planning.Pause102StartedAt = model.Pause102StartedAt;
                planning.Pause102StoppedAt = model.Pause102StoppedAt;
                if (planning.Pause102StartedAt != null && planning.Pause102StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause102StoppedAt - (DateTime)planning.Pause102StartedAt).TotalMinutes;
                }

                planning.Pause1Id = planning.Shift1PauseNumber / 5;

                planning.Shift2PauseNumber = 0;
                planning.Pause20StartedAt = model.Pause20StartedAt;
                planning.Pause20StoppedAt = model.Pause20StoppedAt;
                if (planning.Pause20StartedAt != null && planning.Pause20StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause20StoppedAt - (DateTime)planning.Pause20StartedAt).TotalMinutes;
                }
                planning.Pause21StartedAt = model.Pause21StartedAt;
                planning.Pause21StoppedAt = model.Pause21StoppedAt;
                if (planning.Pause21StartedAt != null && planning.Pause21StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause21StoppedAt - (DateTime)planning.Pause21StartedAt).TotalMinutes;
                }
                planning.Pause22StartedAt = model.Pause22StartedAt;
                planning.Pause22StoppedAt = model.Pause22StoppedAt;
                if (planning.Pause22StartedAt != null && planning.Pause22StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause22StoppedAt - (DateTime)planning.Pause22StartedAt).TotalMinutes;
                }
                planning.Pause23StartedAt = model.Pause23StartedAt;
                planning.Pause23StoppedAt = model.Pause23StoppedAt;
                if (planning.Pause23StartedAt != null && planning.Pause23StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause23StoppedAt - (DateTime)planning.Pause23StartedAt).TotalMinutes;
                }
                planning.Pause24StartedAt = model.Pause24StartedAt;
                planning.Pause24StoppedAt = model.Pause24StoppedAt;
                if (planning.Pause24StartedAt != null && planning.Pause24StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause24StoppedAt - (DateTime)planning.Pause24StartedAt).TotalMinutes;
                }
                planning.Pause25StartedAt = model.Pause25StartedAt;
                planning.Pause25StoppedAt = model.Pause25StoppedAt;
                if (planning.Pause25StartedAt != null && planning.Pause25StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause25StoppedAt - (DateTime)planning.Pause25StartedAt).TotalMinutes;
                }
                planning.Pause26StartedAt = model.Pause26StartedAt;
                planning.Pause26StoppedAt = model.Pause26StoppedAt;
                if (planning.Pause26StartedAt != null && planning.Pause26StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause26StoppedAt - (DateTime)planning.Pause26StartedAt).TotalMinutes;
                }
                planning.Pause27StartedAt = model.Pause27StartedAt;
                planning.Pause27StoppedAt = model.Pause27StoppedAt;
                if (planning.Pause27StartedAt != null && planning.Pause27StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause27StoppedAt - (DateTime)planning.Pause27StartedAt).TotalMinutes;
                }
                planning.Pause28StartedAt = model.Pause28StartedAt;
                planning.Pause28StoppedAt = model.Pause28StoppedAt;
                if (planning.Pause28StartedAt != null && planning.Pause28StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause28StoppedAt - (DateTime)planning.Pause28StartedAt).TotalMinutes;
                }
                planning.Pause29StartedAt = model.Pause29StartedAt;
                planning.Pause29StoppedAt = model.Pause29StoppedAt;
                if (planning.Pause29StartedAt != null && planning.Pause29StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause29StoppedAt - (DateTime)planning.Pause29StartedAt).TotalMinutes;
                }
                planning.Pause200StartedAt = model.Pause200StartedAt;
                planning.Pause200StoppedAt = model.Pause200StoppedAt;
                if (planning.Pause200StartedAt != null && planning.Pause200StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause200StoppedAt - (DateTime)planning.Pause200StartedAt).TotalMinutes;
                }
                planning.Pause201StartedAt = model.Pause201StartedAt;
                planning.Pause201StoppedAt = model.Pause201StoppedAt;
                if (planning.Pause201StartedAt != null && planning.Pause201StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause201StoppedAt - (DateTime)planning.Pause201StartedAt).TotalMinutes;
                }
                planning.Pause202StartedAt = model.Pause202StartedAt;
                planning.Pause202StoppedAt = model.Pause202StoppedAt;
                if (planning.Pause202StartedAt != null && planning.Pause202StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause202StoppedAt - (DateTime)planning.Pause202StartedAt).TotalMinutes;
                }
                planning.Pause2Id = planning.Shift2PauseNumber / 5;

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
                planning.Pause1Id = 0;
            }
            if (model.Start1Id == null)
            {
                planning.Start1StartedAt = null;
            }

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

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            if (planning.Stop1Id >= planning.Start1Id && planning.Stop1Id != 0)
            {
                nettoMinutes = planning.Stop1Id - planning.Start1Id;
                nettoMinutes -= planning.Pause1Id > 0 ? planning.Pause1Id - 1 : 0;
            }

            if (planning.Stop2Id >= planning.Start2Id && planning.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop2Id - planning.Start2Id;
                nettoMinutes -= planning.Pause2Id > 0 ? planning.Pause2Id - 1 : 0;
            }

            if (planning.Stop3Id >= planning.Start3Id && planning.Stop3Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop3Id - planning.Start3Id;
                nettoMinutes -= planning.Pause3Id > 0 ? planning.Pause3Id - 1 : 0;
            }

            if (planning.Stop4Id >= planning.Start4Id && planning.Stop4Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop4Id - planning.Start4Id;
                nettoMinutes -= planning.Pause4Id > 0 ? planning.Pause4Id - 1 : 0;
            }

            if (planning.Stop5Id >= planning.Start5Id && planning.Stop5Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop5Id - planning.Start5Id;
                nettoMinutes -= planning.Pause5Id > 0 ? planning.Pause5Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planning.NettoHours = hours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync() ?? new PlanRegistration
                {
                    SumFlexEnd = 0
                };

            planning.SumFlexStart = preTimePlanning.SumFlexEnd;
            if (planning.NettoHoursOverrideActive)
            {
                planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHoursOverride -
                                      planning.PlanHours -
                                      planning.PaiedOutFlex;
                planning.Flex = planning.NettoHoursOverride - planning.PlanHours;
            } else
            {
                planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHours -
                                      planning.PlanHours -
                                      planning.PaiedOutFlex;
                planning.Flex = planning.NettoHours - planning.PlanHours;
            }

            // Ensure timestamps are populated from IDs for accurate time tracking calculation
            EnsureTimestampsFromIds(planning);

            // Compute time tracking fields (seconds-based calculation)
            PlanRegistrationHelper.ComputeTimeTrackingFields(planning);

            await planning.Update(dbContext).ConfigureAwait(false);

            var planningsAfterThisPlanning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == planning.SdkSitId)
                .Where(x => x.Date > planning.Date)
                .Where(x => x.Date <= planning.Date.AddDays(7))
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

                if (preTimePlanningAfterThisPlanning != null)
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
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
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

            var mcrotingUid = worker.SiteWorkers.First().Site.MicrotingUid;

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
                .Where(x => x.Id == model.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (planning == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("PlanningNotFound"));
            }

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? planning.Pause1Id;
                planning.Pause2Id = model.Pause2Id ?? planning.Pause2Id;
                planning.Pause3Id = model.Pause3Id ?? planning.Pause3Id;
                planning.Pause4Id = model.Pause4Id ?? planning.Pause4Id;
                planning.Pause5Id = model.Pause5Id ?? planning.Pause5Id;
            }
            else
            {
                planning.Shift1PauseNumber = 0;
                planning.Pause1StartedAt = model.Pause1StartedAt;
                planning.Pause1StoppedAt = model.Pause1StoppedAt;
                if (planning.Pause1StartedAt != null && planning.Pause1StoppedAt != null)
                {
                    planning.Shift1PauseNumber =
                        (int)((DateTime)planning.Pause1StoppedAt - (DateTime)planning.Pause1StartedAt).TotalMinutes;
                }

                planning.Pause2StartedAt = model.Pause2StartedAt;
                planning.Pause2StoppedAt = model.Pause2StoppedAt;
                if (planning.Pause2StartedAt != null && planning.Pause2StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause2StoppedAt - (DateTime)planning.Pause2StartedAt).TotalMinutes;
                }

                planning.Pause10StartedAt = model.Pause10StartedAt;
                planning.Pause10StoppedAt = model.Pause10StoppedAt;
                if (planning.Pause10StartedAt != null && planning.Pause10StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause10StoppedAt - (DateTime)planning.Pause10StartedAt).TotalMinutes;
                }

                planning.Pause11StartedAt = model.Pause11StartedAt;
                planning.Pause11StoppedAt = model.Pause11StoppedAt;
                if (planning.Pause11StartedAt != null && planning.Pause11StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause11StoppedAt - (DateTime)planning.Pause11StartedAt).TotalMinutes;
                }

                planning.Pause12StartedAt = model.Pause12StartedAt;
                planning.Pause12StoppedAt = model.Pause12StoppedAt;
                if (planning.Pause12StartedAt != null && planning.Pause12StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause12StoppedAt - (DateTime)planning.Pause12StartedAt).TotalMinutes;
                }

                planning.Pause13StartedAt = model.Pause13StartedAt;
                planning.Pause13StoppedAt = model.Pause13StoppedAt;
                if (planning.Pause13StartedAt != null && planning.Pause13StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause13StoppedAt - (DateTime)planning.Pause13StartedAt).TotalMinutes;
                }

                planning.Pause14StartedAt = model.Pause14StartedAt;
                planning.Pause14StoppedAt = model.Pause14StoppedAt;
                if (planning.Pause14StartedAt != null && planning.Pause14StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause14StoppedAt - (DateTime)planning.Pause14StartedAt).TotalMinutes;
                }

                planning.Pause15StartedAt = model.Pause15StartedAt;
                planning.Pause15StoppedAt = model.Pause15StoppedAt;
                if (planning.Pause15StartedAt != null && planning.Pause15StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause15StoppedAt - (DateTime)planning.Pause15StartedAt).TotalMinutes;
                }

                planning.Pause16StartedAt = model.Pause16StartedAt;
                planning.Pause16StoppedAt = model.Pause16StoppedAt;
                if (planning.Pause16StartedAt != null && planning.Pause16StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause16StoppedAt - (DateTime)planning.Pause16StartedAt).TotalMinutes;
                }

                planning.Pause17StartedAt = model.Pause17StartedAt;
                planning.Pause17StoppedAt = model.Pause17StoppedAt;
                if (planning.Pause17StartedAt != null && planning.Pause17StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause17StoppedAt - (DateTime)planning.Pause17StartedAt).TotalMinutes;
                }

                planning.Pause18StartedAt = model.Pause18StartedAt;
                planning.Pause18StoppedAt = model.Pause18StoppedAt;
                if (planning.Pause18StartedAt != null && planning.Pause18StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause18StoppedAt - (DateTime)planning.Pause18StartedAt).TotalMinutes;
                }

                planning.Pause19StartedAt = model.Pause19StartedAt;
                planning.Pause19StoppedAt = model.Pause19StoppedAt;
                if (planning.Pause19StartedAt != null && planning.Pause19StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause19StoppedAt - (DateTime)planning.Pause19StartedAt).TotalMinutes;
                }

                planning.Pause100StartedAt = model.Pause100StartedAt;
                planning.Pause100StoppedAt = model.Pause100StoppedAt;
                if (planning.Pause100StartedAt != null && planning.Pause100StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause100StoppedAt - (DateTime)planning.Pause100StartedAt).TotalMinutes;
                }

                planning.Pause101StartedAt = model.Pause101StartedAt;
                planning.Pause101StoppedAt = model.Pause101StoppedAt;
                if (planning.Pause101StartedAt != null && planning.Pause101StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause101StoppedAt - (DateTime)planning.Pause101StartedAt).TotalMinutes;
                }

                planning.Pause102StartedAt = model.Pause102StartedAt;
                planning.Pause102StoppedAt = model.Pause102StoppedAt;
                if (planning.Pause102StartedAt != null && planning.Pause102StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause102StoppedAt - (DateTime)planning.Pause102StartedAt).TotalMinutes;
                }

                planning.Pause1Id = planning.Shift1PauseNumber / 5;

                planning.Shift2PauseNumber = 0;
                planning.Pause20StartedAt = model.Pause20StartedAt;
                planning.Pause20StoppedAt = model.Pause20StoppedAt;
                if (planning.Pause20StartedAt != null && planning.Pause20StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause20StoppedAt - (DateTime)planning.Pause20StartedAt).TotalMinutes;
                }

                planning.Pause21StartedAt = model.Pause21StartedAt;
                planning.Pause21StoppedAt = model.Pause21StoppedAt;
                if (planning.Pause21StartedAt != null && planning.Pause21StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause21StoppedAt - (DateTime)planning.Pause21StartedAt).TotalMinutes;
                }

                planning.Pause22StartedAt = model.Pause22StartedAt;
                planning.Pause22StoppedAt = model.Pause22StoppedAt;
                if (planning.Pause22StartedAt != null && planning.Pause22StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause22StoppedAt - (DateTime)planning.Pause22StartedAt).TotalMinutes;
                }

                planning.Pause23StartedAt = model.Pause23StartedAt;
                planning.Pause23StoppedAt = model.Pause23StoppedAt;
                if (planning.Pause23StartedAt != null && planning.Pause23StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause23StoppedAt - (DateTime)planning.Pause23StartedAt).TotalMinutes;
                }

                planning.Pause24StartedAt = model.Pause24StartedAt;
                planning.Pause24StoppedAt = model.Pause24StoppedAt;
                if (planning.Pause24StartedAt != null && planning.Pause24StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause24StoppedAt - (DateTime)planning.Pause24StartedAt).TotalMinutes;
                }

                planning.Pause25StartedAt = model.Pause25StartedAt;
                planning.Pause25StoppedAt = model.Pause25StoppedAt;
                if (planning.Pause25StartedAt != null && planning.Pause25StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause25StoppedAt - (DateTime)planning.Pause25StartedAt).TotalMinutes;
                }

                planning.Pause26StartedAt = model.Pause26StartedAt;
                planning.Pause26StoppedAt = model.Pause26StoppedAt;
                if (planning.Pause26StartedAt != null && planning.Pause26StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause26StoppedAt - (DateTime)planning.Pause26StartedAt).TotalMinutes;
                }

                planning.Pause27StartedAt = model.Pause27StartedAt;
                planning.Pause27StoppedAt = model.Pause27StoppedAt;
                if (planning.Pause27StartedAt != null && planning.Pause27StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause27StoppedAt - (DateTime)planning.Pause27StartedAt).TotalMinutes;
                }

                planning.Pause28StartedAt = model.Pause28StartedAt;
                planning.Pause28StoppedAt = model.Pause28StoppedAt;
                if (planning.Pause28StartedAt != null && planning.Pause28StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause28StoppedAt - (DateTime)planning.Pause28StartedAt).TotalMinutes;
                }

                planning.Pause29StartedAt = model.Pause29StartedAt;
                planning.Pause29StoppedAt = model.Pause29StoppedAt;
                if (planning.Pause29StartedAt != null && planning.Pause29StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause29StoppedAt - (DateTime)planning.Pause29StartedAt).TotalMinutes;
                }

                planning.Pause200StartedAt = model.Pause200StartedAt;
                planning.Pause200StoppedAt = model.Pause200StoppedAt;
                if (planning.Pause200StartedAt != null && planning.Pause200StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause200StoppedAt - (DateTime)planning.Pause200StartedAt).TotalMinutes;
                }

                planning.Pause201StartedAt = model.Pause201StartedAt;
                planning.Pause201StoppedAt = model.Pause201StoppedAt;
                if (planning.Pause201StartedAt != null && planning.Pause201StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause201StoppedAt - (DateTime)planning.Pause201StartedAt).TotalMinutes;
                }

                planning.Pause202StartedAt = model.Pause202StartedAt;
                planning.Pause202StoppedAt = model.Pause202StoppedAt;
                if (planning.Pause202StartedAt != null && planning.Pause202StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause202StoppedAt - (DateTime)planning.Pause202StartedAt).TotalMinutes;
                }

                planning.Pause2Id = planning.Shift2PauseNumber / 5;
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
                planning.Start1Id = planning.Start1StartedAt != null
                    ? planning.Start1StartedAt.Value.Hour * 12
                      + planning.Start1StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop1Id = planning.Stop1StoppedAt != null
                    ? planning.Stop1StoppedAt.Value.Hour * 12
                      + planning.Stop1StoppedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Start2Id = planning.Start2StartedAt != null
                    ? planning.Start2StartedAt.Value.Hour * 12
                      + planning.Start2StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop2Id = planning.Stop2StoppedAt != null
                    ? planning.Stop2StoppedAt.Value.Hour * 12
                      + planning.Stop2StoppedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Start3Id = planning.Start3StartedAt != null
                    ? planning.Start3StartedAt.Value.Hour * 12
                      + planning.Start3StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop3Id = planning.Stop3StoppedAt != null
                    ? planning.Stop3StoppedAt.Value.Hour * 12
                      + planning.Stop3StoppedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Start4Id = planning.Start4StartedAt != null
                    ? planning.Start4StartedAt.Value.Hour * 12
                      + planning.Start4StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop4Id = planning.Stop4StoppedAt != null
                    ? planning.Stop4StoppedAt.Value.Hour * 12
                      + planning.Stop4StoppedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Start5Id = planning.Start5StartedAt != null
                    ? planning.Start5StartedAt.Value.Hour * 12
                      + planning.Start5StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop5Id = planning.Stop5StoppedAt != null
                    ? planning.Stop5StoppedAt.Value.Hour * 12
                      + planning.Stop5StoppedAt.Value.Minute / 5 + 1
                    : 0;
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
            planning.WorkerComment = model.WorkerComment;

            planning = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planning);

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            if (planning.Stop1Id >= planning.Start1Id && planning.Stop1Id != 0)
            {
                nettoMinutes = planning.Stop1Id - planning.Start1Id;
                nettoMinutes -= planning.Pause1Id > 0 ? planning.Pause1Id - 1 : 0;
            }

            if (planning.Stop2Id >= planning.Start2Id && planning.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop2Id - planning.Start2Id;
                nettoMinutes -= planning.Pause2Id > 0 ? planning.Pause2Id - 1 : 0;
            }

            if (planning.Stop3Id >= planning.Start3Id && planning.Stop3Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop3Id - planning.Start3Id;
                nettoMinutes -= planning.Pause3Id > 0 ? planning.Pause3Id - 1 : 0;
            }

            if (planning.Stop4Id >= planning.Start4Id && planning.Stop4Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop4Id - planning.Start4Id;
                nettoMinutes -= planning.Pause4Id > 0 ? planning.Pause4Id - 1 : 0;
            }

            if (planning.Stop5Id >= planning.Start5Id && planning.Stop5Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop5Id - planning.Start5Id;
                nettoMinutes -= planning.Pause5Id > 0 ? planning.Pause5Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planning.NettoHours = hours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync() ?? new PlanRegistration
                {
                    SumFlexEnd = 0
                };

            planning.SumFlexStart = preTimePlanning.SumFlexEnd;
            planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHours -
                                  planning.PlanHours -
                                  planning.PaiedOutFlex;
            planning.Flex = planning.NettoHours - planning.PlanHours;

            // Ensure timestamps are populated from IDs for accurate time tracking calculation
            EnsureTimestampsFromIds(planning);

            // Compute time tracking fields (seconds-based calculation)
            PlanRegistrationHelper.ComputeTimeTrackingFields(planning);

            await planning.Update(dbContext).ConfigureAwait(false);

            var planningsAfterThisPlanning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == planning.SdkSitId)
                .Where(x => x.Date > planning.Date)
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

                if (preTimePlanningAfterThisPlanning != null)
                {
                    planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                    planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                           planningAfterThisPlanning.NettoHours -
                                                           planningAfterThisPlanning.PlanHours -
                                                           planningAfterThisPlanning.PaiedOutFlex;
                    planningAfterThisPlanning.Flex =
                        planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                }
                else
                {
                    // No previous planning found, start from 0
                    planningAfterThisPlanning.SumFlexStart = 0;
                    planningAfterThisPlanning.SumFlexEnd = planningAfterThisPlanning.NettoHours -
                                                           planningAfterThisPlanning.PlanHours -
                                                           planningAfterThisPlanning.PaiedOutFlex;
                    planningAfterThisPlanning.Flex =
                        planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
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
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
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
    private void EnsureTimestampsFromIds(PlanRegistration planning)
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

        // Convert Pause IDs to timestamps if timestamps are missing
        if (planning.Pause1StartedAt == null && planning.Pause1StoppedAt == null && planning.Pause1Id > 0)
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

        if (planning.Pause2StartedAt == null && planning.Pause2StoppedAt == null && planning.Pause2Id > 0)
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

        if (planning.Pause3StartedAt == null && planning.Pause3StoppedAt == null && planning.Pause3Id > 0)
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

        if (planning.Pause4StartedAt == null && planning.Pause4StoppedAt == null && planning.Pause4Id > 0)
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

        if (planning.Pause5StartedAt == null && planning.Pause5StoppedAt == null && planning.Pause5Id > 0)
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

    public async Task<OperationDataResult<PlanRegistrationVersionHistoryModel>> GetVersionHistory(int planRegistrationId)
    {
        try
        {
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

            // Get the assigned site to check GPS and Snapshot settings
            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.SiteId == planRegistration.SdkSitId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            var gpsEnabled = assignedSite?.GpsEnabled ?? false;
            var snapshotEnabled = assignedSite?.SnapshotEnabled ?? false;

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

                var changes = CompareVersions(currentVersion, previousVersion);
                var lastChange = changes.Last();

                // Get GPS coordinates for this version
                if (gpsEnabled && changes.Any())
                {
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
                            ToValue = snapshot.PictureHash,
                            FieldType = "snapshot",
                            PictureHash = snapshot.PictureHash,
                            RegistrationType = snapshot.RegistrationType
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

    private List<FieldChange> CompareVersions(PlanRegistrationVersion current, PlanRegistrationVersion? previous)
    {
        var changes = new List<FieldChange>();

        // Compare all relevant fields
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