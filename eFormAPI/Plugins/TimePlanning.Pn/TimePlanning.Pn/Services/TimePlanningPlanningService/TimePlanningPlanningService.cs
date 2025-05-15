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

using System.Text.RegularExpressions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;

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
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    BaseDbContext baseDbContext,
    IEFormCoreService core)
    : ITimePlanningPlanningService
{
    public async Task<OperationDataResult<List<TimePlanningPlanningModel>>> Index(
        TimePlanningPlanningRequestModel model)
    {
        try
        {
            var sdkCore = await core.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var result = new List<TimePlanningPlanningModel>();
            var assignedSites =
                await dbContext.AssignedSites.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .ToListAsync().ConfigureAwait(false);
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
            foreach (var dbAssignedSite in assignedSites)
            {
                var site = await sdkDbContext.Sites
                    .Where(x => x.MicrotingUid == dbAssignedSite.SiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (site == null)
                {
                    continue;
                }
                var siteModel = new TimePlanningPlanningModel
                {
                    SiteId = dbAssignedSite.SiteId,
                    SiteName = site.Name,
                    PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
                };

                // do a lookup in the baseDbContext.Users where the concat string of FirstName and LastName toLowerCase() is equal to the site.Name toLowerCase()
                // if we find a user, we take the user.EmailSha256 and set the siteModel.AvatarUrl to the gravatar url with the sha256
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
                            await dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < missingDate
                                            && x.SdkSitId == dbAssignedSite.SiteId)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                        if (preTimePlanning != null)
                        {
                            newPlanRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            newPlanRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + newPlanRegistration.NettoHours -
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

                    await newPlanRegistration.Create(dbContext);
                }

                if (missingDates.Count > 0)
                {
                    planningsInPeriod = await dbContext.PlanRegistrations
                        .AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                        .Where(x => x.Date >= midnightOfDateFrom)
                        .Where(x => x.Date <= midnightOfDateTo)
                        .OrderBy(x => x.Date)
                        .ToListAsync().ConfigureAwait(false);
                }

                siteModel = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
                    planningsInPeriod,
                    siteModel,
                    dbContext,
                    dbAssignedSite,
                    logger,
                    site,
                    midnightOfDateFrom,
                    midnightOfDateTo);

                result.Add(siteModel);
            }

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
        TimePlanningPlanningRequestModel obj, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
    {
        var sdkCore = await core.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
        var currentUserAsync = await userService.GetCurrentUserAsync();
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);

        if (model != null)
        {
            currentUser.TimeRegistrationModel = model;
            currentUser.TimeRegistrationManufacturer = manufacturer;
            currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
            currentUser.TimeRegistrationOsVersion = osVersion;
            await baseDbContext.SaveChangesAsync();
        }

        var fullName = currentUser.FirstName.Trim() + " " + currentUser.LastName.Trim();
        var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x =>
            x.Name.Replace(" ", "") == fullName.Replace(" ", "") &&
            x.WorkflowState != Constants.WorkflowStates.Removed);

        if (site == null)
        {
            return new OperationDataResult<TimePlanningPlanningModel>(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        var dbAssignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == site.MicrotingUid);

        var datesInPeriod = new List<DateTime>();
        var midnightOfDateFrom = new DateTime(obj.DateFrom!.Value.Year, obj.DateFrom.Value.Month, obj.DateFrom.Value.Day, 0, 0, 0);
        var midnightOfDateTo = new DateTime(obj.DateTo!.Value.Year, obj.DateTo.Value.Month, obj.DateTo.Value.Day, 23, 59, 59);
        var todayMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
        var date = obj.DateFrom;
        while (date <= obj.DateTo)
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
                .OrderByDescending(x => x.Date)
                .ToListAsync().ConfigureAwait(false);
        }

        siteModel = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            dbContext,
            dbAssignedSite,
            logger,
            site,
            midnightOfDateFrom,
            midnightOfDateTo);

        siteModel.PlanningPrDayModels = obj.IsSortDsc
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

            planning.PlannedStartOfShift1 = model.PlannedStartOfShift1;
            planning.PlannedBreakOfShift1 = model.PlannedBreakOfShift1;
            planning.PlannedEndOfShift1 = model.PlannedEndOfShift1;
            planning.PlannedStartOfShift2 = model.PlannedStartOfShift2;
            planning.PlannedBreakOfShift2 = model.PlannedBreakOfShift2;
            planning.PlannedEndOfShift2 = model.PlannedEndOfShift2;
            planning.CommentOffice = model.CommentOffice;

            if (!planning.PlanChangedByAdmin)
            {
                var entry = dbContext.Entry(planning);
                planning.PlanChangedByAdmin = entry.State == EntityState.Modified;
            }

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? planning.Pause1Id;
                planning.Pause2Id = model.Pause2Id ?? planning.Pause2Id;
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
                // we need to calculate the pause id based on the start and stop times from all the pauses above
            }
            planning.Start1Id = model.Start1Id ?? 0;
            planning.Stop1Id = model.Stop1Id ?? 0;
            planning.Start2Id = model.Start2Id ?? 0;
            planning.Stop2Id = model.Stop2Id ?? 0;
            planning.MessageId = model.Message;
            planning.PaiedOutFlex = model.PaidOutFlex;

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

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planning.NettoHours = hours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

            planning.SumFlexStart = preTimePlanning.SumFlexEnd;
            planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHours -
                                  planning.PlanHours -
                                  planning.PaiedOutFlex;
            planning.Flex = planning.NettoHours - planning.PlanHours;
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

                planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                       planningAfterThisPlanning.NettoHours -
                                                       planningAfterThisPlanning.PlanHours -
                                                       planningAfterThisPlanning.PaiedOutFlex;
                planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
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
            var fullName = currentUser.FirstName.Trim() + " " + currentUser.LastName.Trim();
            var sdkSite = await sdkDbContext.Sites.SingleOrDefaultAsync(x =>
                x.Name.Replace(" ", "") == fullName.Replace(" ", "") &&
                x.WorkflowState != Constants.WorkflowStates.Removed);

            if (sdkSite == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("SiteNotFound"));
            }

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSite.MicrotingUid);

            if (assignedSite == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("AssignedSiteNotFound"));
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

                planning = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planning);
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

            if (assignedSite.UseOneMinuteIntervals)
            {
                planning.Start1StartedAt = model.Start1StartedAt;

                planning.Start1Id = planning.Start1StartedAt != null
                    ? planning.Start1StartedAt.Value.Hour * 12
                      + planning.Start1StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop1StoppedAt = model.Stop1StoppedAt;
                planning.Stop1Id = planning.Stop1StoppedAt != null
                    ? planning.Stop1StoppedAt.Value.Hour * 12
                      + planning.Stop1StoppedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Start2StartedAt = model.Start2StartedAt;
                planning.Start2Id = planning.Start2StartedAt != null
                    ? planning.Start2StartedAt.Value.Hour * 12
                      + planning.Start2StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop2StoppedAt = model.Stop2StoppedAt;
                planning.Stop2Id = planning.Stop2StoppedAt != null
                    ? planning.Stop2StoppedAt.Value.Hour * 12
                      + planning.Stop2StoppedAt.Value.Minute / 5 + 1
                    : 0;
            }

            planning.Start1Id = model.Start1Id ?? 0;
            planning.Stop1Id = model.Stop1Id ?? 0;
            planning.Start2Id = model.Start2Id ?? 0;
            planning.Stop2Id = model.Stop2Id ?? 0;
            planning.WorkerComment = model.WorkerComment;

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

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planning.NettoHours = hours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

            planning.SumFlexStart = preTimePlanning.SumFlexEnd;
            planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHours -
                                  planning.PlanHours -
                                  planning.PaiedOutFlex;
            planning.Flex = planning.NettoHours - planning.PlanHours;
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

                planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                       planningAfterThisPlanning.NettoHours -
                                                       planningAfterThisPlanning.PlanHours -
                                                       planningAfterThisPlanning.PaiedOutFlex;
                planningAfterThisPlanning.Flex =
                    planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
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
}