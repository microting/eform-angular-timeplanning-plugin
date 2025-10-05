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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using DocumentFormat.OpenXml;
using Microsoft.AspNetCore.Http;
using TimePlanning.Pn.Resources;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microting.eForm.Infrastructure.Data.Entities;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;

namespace TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.Settings;
using Infrastructure.Models.WorkingHours.Index;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using TimePlanningLocalizationService;

/// <summary>
/// TimePlanningWorkingHoursService
/// </summary>
public class TimePlanningWorkingHoursService(
    ILogger<TimePlanningWorkingHoursService> logger,
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    BaseDbContext baseDbContext,
    IPluginDbOptions<TimePlanningBaseSettings> options,
    IEFormCoreService coreHelper)
    : ITimePlanningWorkingHoursService
{
    public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(
        TimePlanningWorkingHoursRequestModel model)
    {
        try
        {
            model.DateFrom = new DateTime(model.DateFrom.Year, model.DateFrom.Month, model.DateFrom.Day, 0, 0, 0);
            model.DateTo = new DateTime(model.DateTo.Year, model.DateTo.Month, model.DateTo.Day, 0, 0, 0);
            var core = await coreHelper.GetCore();
            await using var sdkDbContext = core.DbContextHelper.GetDbContext();
            var maxDaysEditable = options.Value.MaxDaysEditable;
            var language = await userService.GetCurrentUserLanguage();
            var ci = new CultureInfo(language.LanguageCode);
            List<(DateTime, string)> tupleValueList = new();
            var site = await sdkDbContext.Sites
                .AsNoTracking()
                .Where(x => x.MicrotingUid == model.SiteId)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .FirstAsync();

            var timePlanningRequest = dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == model.SiteId);

            // two dates may be displayed instead of one if the same date is selected.
            if (model.DateFrom == model.DateTo)
            {
                timePlanningRequest = timePlanningRequest
                    .Where(x => x.Date == model.DateFrom);
            }
            else
            {
                timePlanningRequest = timePlanningRequest
                    .Where(x => x.Date >= model.DateFrom && x.Date <= model.DateTo);
            }

            var dateTime = DateTime.Now;
            var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

            var timePlannings = await timePlanningRequest
                .Select(x => new TimePlanningWorkingHoursModel
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    WorkerName = site.Name,
                    WeekDay = (int)x.Date.DayOfWeek,
                    Date = x.Date,
                    PlanText = x.PlanText,
                    PlanHours = x.PlanHours,
                    Shift1Start = x.Start1Id,
                    Shift1Stop = x.Stop1Id,
                    Shift1Pause = x.Pause1Id,
                    Shift2Start = x.Start2Id,
                    Shift2Stop = x.Stop2Id,
                    Shift2Pause = x.Pause2Id,
                    Shift3Start = x.Start3Id,
                    Shift3Stop = x.Stop3Id,
                    Shift3Pause = x.Pause3Id,
                    Shift4Start = x.Start4Id,
                    Shift4Stop = x.Stop4Id,
                    Shift4Pause = x.Pause4Id,
                    Shift5Start = x.Start5Id,
                    Shift5Stop = x.Stop5Id,
                    Shift5Pause = x.Pause5Id,
                    NettoHours = Math.Round(x.NettoHours, 2),
                    FlexHours = Math.Round(x.Flex, 2),
                    SumFlexStart = Math.Round(x.SumFlexStart, 2),
                    PaidOutFlex = x.PaiedOutFlex.ToString().Replace(",", "."),
                    Message = x.MessageId,
                    CommentWorker = x.WorkerComment.Replace("\r", "<br />"),
                    CommentOffice = x.CommentOffice.Replace("\r", "<br />"),
                    // CommentOfficeAll = x.CommentOfficeAll,
                    IsLocked = (x.Date < DateTime.Now.AddDays(-(int)maxDaysEditable) || x.Date == midnight),
                    IsWeekend = x.Date.DayOfWeek == DayOfWeek.Saturday || x.Date.DayOfWeek == DayOfWeek.Sunday,
                    NettoHoursOverride = x.NettoHoursOverride,
                    NettoHoursOverrideActive = x.NettoHoursOverrideActive
                })
                .ToListAsync();

            var totalDays = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

            var lastPlanning = dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.Date < model.DateFrom)
                .Where(x => x.SdkSitId == model.SiteId).OrderBy(x => x.Date).LastOrDefault();

            if (lastPlanning != null)
            {
                // lastPlanning.Date = new DateTime(lastPlanning.Date.Year, lastPlanning.Date.Month, lastPlanning.Date.Day, 0, 0, 0);


                try
                {
                    var prePlanning = new TimePlanningWorkingHoursModel
                    {
                        Id = 0,
                        CreatedAt = lastPlanning.CreatedAt,
                        UpdatedAt = lastPlanning.UpdatedAt,
                        WorkerName = site.Name,
                        WeekDay = lastPlanning != null
                            ? (int)lastPlanning.Date.DayOfWeek
                            : (int)model.DateFrom.AddDays(-1).DayOfWeek,
                        Date = lastPlanning?.Date ?? model.DateFrom.AddDays(-1),
                        PlanText = lastPlanning?.PlanText,
                        PlanHours = lastPlanning?.PlanHours ?? 0,
                        Shift1Start = lastPlanning?.Start1Id,
                        Shift1Stop = lastPlanning?.Stop1Id,
                        Shift1Pause = lastPlanning?.Pause1Id,
                        Shift2Start = lastPlanning?.Start2Id,
                        Shift2Stop = lastPlanning?.Stop2Id,
                        Shift2Pause = lastPlanning?.Pause2Id,
                        Shift3Start = lastPlanning?.Start3Id,
                        Shift3Stop = lastPlanning?.Stop3Id,
                        Shift3Pause = lastPlanning?.Pause3Id,
                        Shift4Start = lastPlanning?.Start4Id,
                        Shift4Stop = lastPlanning?.Stop4Id,
                        Shift4Pause = lastPlanning?.Pause4Id,
                        Shift5Start = lastPlanning?.Start5Id,
                        Shift5Stop = lastPlanning?.Stop5Id,
                        Shift5Pause = lastPlanning?.Pause5Id,
                        NettoHours = Math.Round(lastPlanning?.NettoHours ?? 0, 2),
                        FlexHours = Math.Round(lastPlanning?.Flex ?? 0, 2),
                        SumFlexStart = lastPlanning?.SumFlexStart ?? 0,
                        PaidOutFlex = lastPlanning?.PaiedOutFlex.ToString().Replace(",", ".") ?? "0",
                        Message = lastPlanning?.MessageId,
                        CommentWorker = lastPlanning?.WorkerComment?.Replace("\r", "<br />"),
                        CommentOffice = lastPlanning?.CommentOffice?.Replace("\r", "<br />"),
                        IsLocked = true,
                        IsWeekend = lastPlanning != null
                            ? lastPlanning.Date.DayOfWeek == DayOfWeek.Saturday ||
                              lastPlanning.Date.DayOfWeek == DayOfWeek.Sunday
                            : model.DateFrom.AddDays(-1).DayOfWeek == DayOfWeek.Saturday ||
                              model.DateFrom.AddDays(-1).DayOfWeek == DayOfWeek.Sunday
                    };

                    timePlannings.Add(prePlanning);

                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                    logger.LogError(e.Message);
                    logger.LogTrace(e.StackTrace);
                }
            }

            if (timePlannings.Count - 1 < totalDays)
            {
                var timePlanningForAdd = new List<TimePlanningWorkingHoursModel>();
                for (var i = 0; i < totalDays; i++)
                {
                    if (timePlannings.All(x => x.Date != model.DateFrom.AddDays(i)))
                    {
                        timePlanningForAdd.Add(new TimePlanningWorkingHoursModel
                        {
                            Date = model.DateFrom.AddDays(i),
                            WeekDay = (int)model.DateFrom.AddDays(i).DayOfWeek,
                            IsLocked = model.DateFrom.AddDays(i) < DateTime.Now.AddDays(-(int)maxDaysEditable) ||
                                       model.DateFrom.AddDays(i) == midnight,
                            IsWeekend = model.DateFrom.AddDays(i).DayOfWeek == DayOfWeek.Saturday
                                        || model.DateFrom.AddDays(i).DayOfWeek == DayOfWeek.Sunday
                            //WorkerId = model.WorkerId,
                        });
                    }
                }

                timePlannings.AddRange(timePlanningForAdd);
            }

            timePlannings = timePlannings.OrderBy(x => x.Date).ToList();

            var j = 0;
            double sumFlexEnd = 0;
            //double SumFlexStart = 0;
            foreach (var timePlanningWorkingHoursModel in timePlannings)
            {
                if (j == 0)
                {
                    timePlanningWorkingHoursModel.SumFlexStart =
                        Math.Round(timePlanningWorkingHoursModel.SumFlexStart, 2);
                    timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(
                        timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours -
                        (string.IsNullOrEmpty(timePlanningWorkingHoursModel.PaidOutFlex)
                            ? 0
                            : double.Parse(timePlanningWorkingHoursModel.PaidOutFlex.Replace(",", "."),
                                CultureInfo.InvariantCulture)), 2);
                    sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                }
                else
                {
                    timePlanningWorkingHoursModel.SumFlexStart = sumFlexEnd;
                    try
                    {
                        timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(
                            timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours -
                            (string.IsNullOrEmpty(timePlanningWorkingHoursModel.PaidOutFlex)
                                ? 0
                                : double.Parse(timePlanningWorkingHoursModel.PaidOutFlex.Replace(",", "."),
                                    CultureInfo.InvariantCulture)), 2);
                    }
                    catch (Exception e)
                    {
                        SentrySdk.CaptureException(e);
                        logger.LogError(e.Message);
                        logger.LogTrace(e.StackTrace);
                    }

                    sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                }

                j++;
            }

            return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                true,
                timePlannings);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                false,
                localizationService.GetString("ErrorWhileObtainingPlannings"));
        }
    }

    public async Task<OperationResult> CreateUpdate(TimePlanningWorkingHoursUpdateCreateModel model)
    {
        try
        {
            var planRegistrations = await dbContext.PlanRegistrations
                .Where(x => x.SdkSitId == model.SiteId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();
            var first = true;
            foreach (var planning in model.Plannings)
            {
                planning.Date = new DateTime(planning.Date.Year, planning.Date.Month, planning.Date.Day, 0, 0, 0);
                var planRegistration = planRegistrations.FirstOrDefault(x => x.Date == planning.Date);
                if (planRegistration != null)
                {
                    await UpdatePlanning(first, planRegistration, planning, model.SiteId);
                }
                else
                {
                    if (!first)
                    {
                        await CreatePlanning(first, planning, model.SiteId, model.SiteId, planning.CommentWorker);
                    }
                }

                first = false;
            }

            // Check if there are any plannings after the last planning in the model
            var lastPlanning = model.Plannings
                .OrderByDescending(x => x.Date)
                .FirstOrDefault();
            if (lastPlanning != null)
            {
                lastPlanning.Date = new DateTime(lastPlanning.Date.Year, lastPlanning.Date.Month, lastPlanning.Date.Day,
                    0, 0, 0);
                var planRegistrationsAfterLastPlanning = planRegistrations
                    .Where(x => x.Date > lastPlanning.Date)
                    .Where(x => x.Date < DateTime.Now.AddDays(180))
                    .ToList();
                foreach (var planRegistration in planRegistrationsAfterLastPlanning)
                {
                    var preTimePlanning =
                        await dbContext.PlanRegistrations.AsNoTracking()
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .Where(x => x.Date < planRegistration.Date
                                        && x.SdkSitId == planRegistration.SdkSitId)
                            .OrderByDescending(x => x.Date)
                            .FirstOrDefaultAsync();

                    if (preTimePlanning != null)
                    {
                        planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                        planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                                      planRegistration.PlanHours -
                                                      planRegistration.PaiedOutFlex;
                        planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                    }
                    else
                    {
                        planRegistration.SumFlexEnd = planRegistration.NettoHours - planRegistration.PlanHours -
                                                      planRegistration.PaiedOutFlex;
                        planRegistration.SumFlexStart = 0;
                        planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                    }

                    await planRegistration.Update(dbContext);
                }
            }


            return new OperationResult(
                true,
                localizationService.GetString("SuccessfullyCreateOrUpdatePlanning"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                false,
                localizationService.GetString("ErrorWhileCreateUpdatePlannings"));
        }
    }

    private async Task CreatePlanning(bool first, TimePlanningWorkingHoursModel model, int sdkSiteId,
        int microtingUid, string commentWorker)
    {
        var dateTime = DateTime.Now;
        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

        if (model.Date != midnight)
        {
            try
            {
                var planRegistration = new PlanRegistration
                {
                    MessageId = model.Message == 0 ? null : model.Message,
                    PlanText = model.PlanText,
                    SdkSitId = sdkSiteId,
                    Date = model.Date,
                    PlanHours = model.PlanHours,
                    CreatedByUserId = userService.UserId,
                    UpdatedByUserId = userService.UserId,
                    CommentOffice = model.CommentOffice,
                    CommentOfficeAll = model.CommentOfficeAll,
                    NettoHours = model.NettoHours,
                    PaiedOutFlex = double.Parse(model.PaidOutFlex.Replace(",", "."), CultureInfo.InvariantCulture),
                    Pause1Id = model.Shift1Pause ?? 0,
                    Pause2Id = model.Shift2Pause ?? 0,
                    Start1Id = model.Shift1Start ?? 0,
                    Start2Id = model.Shift2Start ?? 0,
                    Stop1Id = model.Shift1Stop ?? 0,
                    Stop2Id = model.Shift2Stop ?? 0,
                    Flex = model.FlexHours,
                    StatusCaseId = 0
                };

                var preTimePlanning =
                    await dbContext.PlanRegistrations.AsNoTracking().Where(x => x.Date < planRegistration.Date
                            && x.SdkSitId == planRegistration.SdkSitId).OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
                                                  planRegistration.PaiedOutFlex;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }

                await planRegistration.Create(dbContext);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                logger.LogError(e.Message);
                logger.LogTrace(e.StackTrace);
            }
        }
    }

    private async Task UpdatePlanning(bool first, PlanRegistration planRegistration,
        TimePlanningWorkingHoursModel model,
        int microtingUid)
    {
        var dateTime = DateTime.Now;
        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

        if (planRegistration.Date != midnight)
        {
            planRegistration.MessageId = model.Message == 10 ? null : model.Message;
            planRegistration.PlanText = model.PlanText;
            planRegistration.Date = model.Date;
            planRegistration.PlanHours = model.PlanHours;
            planRegistration.UpdatedByUserId = userService.UserId;
            planRegistration.CommentOffice = model.CommentOffice;
            planRegistration.CommentOfficeAll = model.CommentOfficeAll;
            planRegistration.NettoHours = model.NettoHours;
            planRegistration.PaiedOutFlex = string.IsNullOrEmpty(model.PaidOutFlex)
                ? 0
                : double.Parse(model.PaidOutFlex.Replace(",", "."), CultureInfo.InvariantCulture);
            planRegistration.Pause1Id = model.Shift1Pause ?? 0;
            planRegistration.Pause2Id = model.Shift2Pause ?? 0;
            planRegistration.Start1Id = model.Shift1Start ?? 0;
            planRegistration.Start2Id = model.Shift2Start ?? 0;
            planRegistration.Stop1Id = model.Shift1Stop ?? 0;
            planRegistration.Stop2Id = model.Shift2Stop ?? 0;
            planRegistration.Flex = model.FlexHours;
        }

        // var preTimePlanning =
        //     await dbContext.PlanRegistrations.AsNoTracking()
        //         .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
        //         .Where(x => x.Date < planRegistration.Date
        //                     && x.SdkSitId == planRegistration.SdkSitId)
        //         .OrderByDescending(x => x.Date)
        //         .FirstOrDefaultAsync();
        // if (preTimePlanning != null)
        // {
        //     planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
        //     planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
        //                                   planRegistration.PaiedOutFlex;
        // }
        // else
        // {
        //     planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
        //     planRegistration.SumFlexStart = 0;
        // }

        var assignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == microtingUid);
        planRegistration = await PlanRegistrationHelper
            .UpdatePlanRegistration(planRegistration, dbContext, assignedSite, DateTime.Now.AddMonths(-1));

        await planRegistration.Update(dbContext);
    }

    public async Task<OperationDataResult<TimePlanningWorkingHourSimpleModel>> ReadSimple(DateTime dateTime, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
    {
        var currentUserAsync = await userService.GetCurrentUserAsync();
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);
        var fullName = currentUser.FirstName.Trim() + " " + currentUser.LastName.Trim();
        var core = await coreHelper.GetCore();
        var sdkContext = core.DbContextHelper.GetDbContext();
        var sdkSite = await sdkContext.Sites.SingleOrDefaultAsync(x =>
            x.Name.Replace(" ", "") == fullName.Replace(" ", "") &&
            x.WorkflowState != Constants.WorkflowStates.Removed);

        if (sdkSite == null)
        {
            return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(false, "Site not found", null);
        }

        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == midnight)
            .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (planRegistration == null)
        {
            var preTimePlanning = await dbContext.PlanRegistrations.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Date < midnight
                            && x.SdkSitId == sdkSite.MicrotingUid)
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync();
            if (preTimePlanning != null)
            {
                var newTimePlanningWorkingHoursModel = new TimePlanningWorkingHourSimpleModel
                {
                    Date = midnight.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    YesterDay = midnight.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Worker = sdkSite.Name,
                    PlanText = "",
                    PlanHours = 0,
                    NettoHours = 0,
                    FlexHours = 0,
                    SumFlexStart = Math.Round(preTimePlanning.SumFlexEnd, 2).ToString(CultureInfo.InvariantCulture),
                    SumFlexEnd = Math.Round(preTimePlanning.SumFlexEnd, 2).ToString(CultureInfo.InvariantCulture),
                    PaidOutFlex = 0,
                    CommentWorker = "",
                    CommentOffice = "",
                };

                return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true,
                    localizationService.GetString("PlanRegistrationLoaded"),
                    newTimePlanningWorkingHoursModel);
            }
            else
            {
                var newTimePlanningWorkingHoursModel = new TimePlanningWorkingHourSimpleModel
                {
                    Date = midnight.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    YesterDay = midnight.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Worker = sdkSite.Name,
                    PlanText = "",
                    PlanHours = 0,
                    NettoHours = 0,
                    FlexHours = 0,
                    SumFlexStart = "0",
                    SumFlexEnd = "0",
                    PaidOutFlex = 0,
                    CommentWorker = "",
                    CommentOffice = "",
                };

                return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true,
                    localizationService.GetString("PlanRegistrationLoaded"),
                    newTimePlanningWorkingHoursModel);
            }
        }

        var timePlanningWorkingHoursModel = new TimePlanningWorkingHourSimpleModel
        {
            Date = midnight.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            YesterDay = midnight.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            Worker = sdkSite.Name,
            PlanText = planRegistration.PlanText,
            PlanHours = planRegistration.PlanHours,
            NettoHours = RoundToTwoDecimalPlaces(planRegistration.NettoHours),
            FlexHours = RoundToTwoDecimalPlaces(planRegistration.Flex),
            SumFlexStart = planRegistration.SumFlexStart == 0
                ? "0"
                : RoundToTwoDecimalPlaces(planRegistration.SumFlexStart).ToString(CultureInfo.InvariantCulture),
            SumFlexEnd = planRegistration.SumFlexEnd == 0
                ? "0"
                : RoundToTwoDecimalPlaces(planRegistration.SumFlexEnd).ToString(CultureInfo.InvariantCulture),
            PaidOutFlex = planRegistration.PaiedOutFlex,
            CommentWorker = planRegistration.WorkerComment,
            CommentOffice = planRegistration.CommentOffice,
            Start1StartedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Start1Id),
            Stop1StoppedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Stop1Id),
            Pause1TotalTime = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Pause1Id),
            Start2StartedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Start2Id),
            Stop2StoppedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Stop2Id),
            Pause2TotalTime = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Pause2Id)
        };

        if (model != null)
        {
            currentUser.TimeRegistrationModel = model;
            currentUser.TimeRegistrationManufacturer = manufacturer;
            currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
            currentUser.TimeRegistrationOsVersion = osVersion;
            await baseDbContext.SaveChangesAsync();
        }

        return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true,
            localizationService.GetString("PlanRegistrationLoaded"),
            timePlanningWorkingHoursModel);
    }

    public async Task<OperationDataResult<TimePlanningHoursSummaryModel>> CalculateHoursSummary(DateTime startDate,
        DateTime endDate, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
    {
        try
        {
            // Adjust startDate to 00:00:00 and endDate to 23:59:59
            startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);
            endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);

            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            var currentUserAsync = await userService.GetCurrentUserAsync();
            var currentUser = baseDbContext.Users
                .Single(x => x.Id == currentUserAsync.Id);
            var fullName = currentUser.FirstName.Trim() + " " + currentUser.LastName.Trim();
            var sdkSite = await sdkContext.Sites.SingleOrDefaultAsync(x =>
                x.Name.Replace(" ", "") == fullName.Replace(" ", "") &&
                x.WorkflowState != Constants.WorkflowStates.Removed);

            if (sdkSite == null)
            {
                return new OperationDataResult<TimePlanningHoursSummaryModel>(false, "Site not found", null);
            }

            var planRegistrations = await dbContext.PlanRegistrations
                .Where(x => x.Date >= startDate && x.Date <= endDate)
                .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var totalPlanHours = planRegistrations.Sum(x => x.PlanHours);
            var totalNettoHours = planRegistrations.Sum(x => x.NettoHours);
            var difference = totalNettoHours - totalPlanHours;

            var summary = new TimePlanningHoursSummaryModel
            {
                TotalPlanHours = totalPlanHours,
                TotalNettoHours = totalNettoHours,
                Difference = difference
            };

            if (model != null)
            {
                currentUser.TimeRegistrationModel = model;
                currentUser.TimeRegistrationManufacturer = manufacturer;
                currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
                currentUser.TimeRegistrationOsVersion = osVersion;
                await baseDbContext.SaveChangesAsync();
            }

            return new OperationDataResult<TimePlanningHoursSummaryModel>(true, summary);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<TimePlanningHoursSummaryModel>(false,
                "Error while calculating hours summary");
        }
    }


    private static double RoundToTwoDecimalPlaces(double value)
    {
        double roundedValue = Math.Round(value, 2);
        return roundedValue == -0 ? 0 : roundedValue;
    }

    private static string? RoundDownToNearestFiveMinutesAndFormat(DateTime date, int minutesToAdd)
    {
        if (minutesToAdd == 0)
        {
            return null;
        }

        var roundedDateTime = date.AddMinutes((minutesToAdd - 1) * 5);
        return roundedDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public async Task<OperationDataResult<TimePlanningWorkingHoursModel>> Read(int sdkSiteId, DateTime dateTime,
        string token)
    {
        var result = await PlanRegistrationHelper.ReadBySiteAndDate(dbContext, sdkSiteId, dateTime, token);
        if (result == null)
        {
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false,
                localizationService.GetString("PlanRegistrationNotFound"), null);
        }
        return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
            result);
    }

    public async Task<OperationResult> UpdateWorkingHour(TimePlanningWorkingHoursUpdateModel model)
    {
        var sdkCore = await coreHelper.GetCore();
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
            return new OperationResult(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        var assignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == sdkSite.MicrotingUid);

        var todayAtMidnight = model.Date;

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == todayAtMidnight)
            .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (planRegistration == null)
        {
            planRegistration = new PlanRegistration
            {
                MessageId = null,
                PlanText = "",
                Date = model.Date,
                PlanHours = 0,
                UpdatedByUserId = userService.UserId,
                CommentOffice = "",
                CommentOfficeAll = "",
                NettoHours = 0,
                PaiedOutFlex = 0,
                Pause1Id = model.Shift1Pause ?? 0,
                Pause2Id = model.Shift2Pause ?? 0,
                Start1Id = model.Shift1Start ?? 0,
                Start2Id = model.Shift2Start ?? 0,
                Stop1Id = model.Shift1Stop ?? 0,
                Stop2Id = model.Shift2Stop ?? 0,
                Start3Id = model.Shift3Start ?? 0,
                Stop3Id = model.Shift3Stop ?? 0,
                Pause3Id = model.Shift3Pause ?? 0,
                Start4Id = model.Shift4Start ?? 0,
                Stop4Id = model.Shift4Stop ?? 0,
                Pause4Id = model.Shift4Pause ?? 0,
                Start5Id = model.Shift5Start ?? 0,
                Stop5Id = model.Shift5Stop ?? 0,
                Pause5Id = model.Shift5Pause ?? 0,
                Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                    ? null
                    : DateTime.Parse(model.Start1StartedAt),
                Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop1StoppedAt),
                Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause1StartedAt),
                Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause1StoppedAt),
                Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                    ? null
                    : DateTime.Parse(model.Start2StartedAt),
                Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop2StoppedAt),
                Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                    ? null
                    : DateTime.Parse(model.Start3StartedAt),
                Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop3StoppedAt),
                Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                    ? null
                    : DateTime.Parse(model.Start4StartedAt),
                Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop4StoppedAt),
                Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                    ? null
                    : DateTime.Parse(model.Start5StartedAt),
                Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop5StoppedAt),
                Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause10StartedAt),
                Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause10StoppedAt),
                Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause11StartedAt),
                Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause11StoppedAt),
                Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause12StartedAt),
                Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause12StoppedAt),
                Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause13StartedAt),
                Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause13StoppedAt),
                Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause14StartedAt),
                Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause14StoppedAt),
                Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause15StartedAt),
                Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause15StoppedAt),
                Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause16StartedAt),
                Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause16StoppedAt),
                Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause17StartedAt),
                Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause17StoppedAt),
                Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause18StartedAt),
                Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause18StoppedAt),
                Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause19StartedAt),
                Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause19StoppedAt),
                Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause100StartedAt),
                Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause100StoppedAt),
                Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause101StartedAt),
                Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause101StoppedAt),
                Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause102StartedAt),
                Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause102StoppedAt),

                Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause2StartedAt),
                Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause2StoppedAt),
                Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause20StartedAt),
                Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause20StoppedAt),
                Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause21StartedAt),
                Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause21StoppedAt),
                Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause22StartedAt),
                Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause22StoppedAt),
                Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause23StartedAt),
                Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause23StoppedAt),
                Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause24StartedAt),
                Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause24StoppedAt),
                Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause25StartedAt),
                Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause25StoppedAt),
                Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause26StartedAt),
                Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause26StoppedAt),
                Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause27StartedAt),
                Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause27StoppedAt),
                Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause28StartedAt),
                Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause28StoppedAt),
                Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause29StartedAt),
                Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause29StoppedAt),
                Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause200StartedAt),
                Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause200StoppedAt),
                Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause201StartedAt),
                Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause201StoppedAt),
                Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause202StartedAt),
                Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause202StoppedAt),
                Pause3StartedAt = string.IsNullOrEmpty(model.Pause3StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause3StartedAt),
                Pause3StoppedAt = string.IsNullOrEmpty(model.Pause3StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause3StoppedAt),
                Pause4StartedAt = string.IsNullOrEmpty(model.Pause4StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause4StartedAt),
                Pause4StoppedAt = string.IsNullOrEmpty(model.Pause4StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause4StoppedAt),
                Pause5StartedAt = string.IsNullOrEmpty(model.Pause5StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause5StartedAt),
                Pause5StoppedAt = string.IsNullOrEmpty(model.Pause5StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause5StoppedAt),
                Flex = 0,
                WorkerComment = model.CommentWorker,
                SdkSitId = (int)sdkSite.MicrotingUid,
                Shift1PauseNumber = model.Shift1PauseNumber,
                Shift2PauseNumber = model.Shift2PauseNumber,
            };

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            if (planRegistration.Stop1Id >= planRegistration.Start1Id && planRegistration.Stop1Id != 0)
            {
                nettoMinutes = planRegistration.Stop1Id - planRegistration.Start1Id;
                nettoMinutes -= planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0;
            }

            if (planRegistration.Stop2Id >= planRegistration.Start2Id && planRegistration.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop2Id - planRegistration.Start2Id;
                nettoMinutes -= planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0;
            }

            if (planRegistration.Stop3Id >= planRegistration.Start3Id && planRegistration.Stop3Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop3Id - planRegistration.Start3Id;
                nettoMinutes -= planRegistration.Pause3Id > 0 ? planRegistration.Pause3Id - 1 : 0;
            }

            if (planRegistration.Stop4Id >= planRegistration.Start4Id && planRegistration.Stop4Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop4Id - planRegistration.Start4Id;
                nettoMinutes -= planRegistration.Pause4Id > 0 ? planRegistration.Pause4Id - 1 : 0;
            }

            if (planRegistration.Stop5Id >= planRegistration.Start5Id && planRegistration.Stop5Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop5Id - planRegistration.Start5Id;
                nettoMinutes -= planRegistration.Pause5Id > 0 ? planRegistration.Pause5Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planRegistration.NettoHours = hours;
            planRegistration.Flex = hours - planRegistration.PlanHours;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSite.MicrotingUid)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();
            if (preTimePlanning != null)
            {
                planRegistration.SumFlexEnd =
                    preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
            }
            else
            {
                planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = 0;
            }

            await planRegistration.Create(dbContext).ConfigureAwait(false);
        }
        else
        {
            planRegistration.UpdatedByUserId = userService.UserId;
            planRegistration.Pause1Id = model.Shift1Pause ?? 0;
            planRegistration.Pause2Id = model.Shift2Pause ?? 0;
            planRegistration.Start1Id = model.Shift1Start ?? 0;
            planRegistration.Start2Id = model.Shift2Start ?? 0;
            planRegistration.Stop1Id = model.Shift1Stop ?? 0;
            planRegistration.Stop2Id = model.Shift2Stop ?? 0;
            planRegistration.Start3Id = model.Shift3Start ?? 0;
            planRegistration.Stop3Id = model.Shift3Stop ?? 0;
            planRegistration.Pause3Id = model.Shift3Pause ?? 0;
            planRegistration.Start4Id = model.Shift4Start ?? 0;
            planRegistration.Stop4Id = model.Shift4Stop ?? 0;
            planRegistration.Pause4Id = model.Shift4Pause ?? 0;
            planRegistration.Start5Id = model.Shift5Start ?? 0;
            planRegistration.Stop5Id = model.Shift5Stop ?? 0;
            planRegistration.WorkerComment = model.CommentWorker;

            planRegistration.Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                ? null
                : DateTime.Parse(model.Start1StartedAt);
            planRegistration.Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                ? null
                : DateTime.Parse(model.Stop1StoppedAt);
            planRegistration.Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                ? null
                : DateTime.Parse(model.Pause1StartedAt);
            planRegistration.Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                ? null
                : DateTime.Parse(model.Pause1StoppedAt);
            planRegistration.Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                ? null
                : DateTime.Parse(model.Start2StartedAt);
            planRegistration.Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                ? null
                : DateTime.Parse(model.Stop2StoppedAt);
            planRegistration.Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                ? null
                : DateTime.Parse(model.Start3StartedAt);
            planRegistration.Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                ? null
                : DateTime.Parse(model.Stop3StoppedAt);
            planRegistration.Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                ? null
                : DateTime.Parse(model.Start4StartedAt);
            planRegistration.Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                ? null
                : DateTime.Parse(model.Stop4StoppedAt);
            planRegistration.Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                ? null
                : DateTime.Parse(model.Start5StartedAt);
            planRegistration.Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                ? null
                : DateTime.Parse(model.Stop5StoppedAt);

            planRegistration.Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                ? null
                : DateTime.Parse(model.Pause10StartedAt);
            planRegistration.Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                ? null
                : DateTime.Parse(model.Pause10StoppedAt);
            planRegistration.Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                ? null
                : DateTime.Parse(model.Pause11StartedAt);
            planRegistration.Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                ? null
                : DateTime.Parse(model.Pause11StoppedAt);
            planRegistration.Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                ? null
                : DateTime.Parse(model.Pause12StartedAt);
            planRegistration.Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                ? null
                : DateTime.Parse(model.Pause12StoppedAt);
            planRegistration.Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                ? null
                : DateTime.Parse(model.Pause13StartedAt);
            planRegistration.Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                ? null
                : DateTime.Parse(model.Pause13StoppedAt);
            planRegistration.Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                ? null
                : DateTime.Parse(model.Pause14StartedAt);
            planRegistration.Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                ? null
                : DateTime.Parse(model.Pause14StoppedAt);
            planRegistration.Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                ? null
                : DateTime.Parse(model.Pause15StartedAt);
            planRegistration.Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                ? null
                : DateTime.Parse(model.Pause15StoppedAt);
            planRegistration.Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                ? null
                : DateTime.Parse(model.Pause16StartedAt);
            planRegistration.Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                ? null
                : DateTime.Parse(model.Pause16StoppedAt);
            planRegistration.Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                ? null
                : DateTime.Parse(model.Pause17StartedAt);
            planRegistration.Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                ? null
                : DateTime.Parse(model.Pause17StoppedAt);
            planRegistration.Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                ? null
                : DateTime.Parse(model.Pause18StartedAt);
            planRegistration.Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                ? null
                : DateTime.Parse(model.Pause18StoppedAt);
            planRegistration.Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                ? null
                : DateTime.Parse(model.Pause19StartedAt);
            planRegistration.Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                ? null
                : DateTime.Parse(model.Pause19StoppedAt);
            planRegistration.Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                ? null
                : DateTime.Parse(model.Pause100StartedAt);
            planRegistration.Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                ? null
                : DateTime.Parse(model.Pause100StoppedAt);
            planRegistration.Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                ? null
                : DateTime.Parse(model.Pause101StartedAt);
            planRegistration.Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                ? null
                : DateTime.Parse(model.Pause101StoppedAt);
            planRegistration.Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                ? null
                : DateTime.Parse(model.Pause102StartedAt);
            planRegistration.Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                ? null
                : DateTime.Parse(model.Pause102StoppedAt);

            planRegistration.Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                ? null
                : DateTime.Parse(model.Pause2StartedAt);
            planRegistration.Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                ? null
                : DateTime.Parse(model.Pause2StoppedAt);
            planRegistration.Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                ? null
                : DateTime.Parse(model.Pause20StartedAt);
            planRegistration.Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                ? null
                : DateTime.Parse(model.Pause20StoppedAt);
            planRegistration.Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                ? null
                : DateTime.Parse(model.Pause21StartedAt);
            planRegistration.Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                ? null
                : DateTime.Parse(model.Pause21StoppedAt);
            planRegistration.Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                ? null
                : DateTime.Parse(model.Pause22StartedAt);
            planRegistration.Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                ? null
                : DateTime.Parse(model.Pause22StoppedAt);
            planRegistration.Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                ? null
                : DateTime.Parse(model.Pause23StartedAt);
            planRegistration.Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                ? null
                : DateTime.Parse(model.Pause23StoppedAt);
            planRegistration.Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                ? null
                : DateTime.Parse(model.Pause24StartedAt);
            planRegistration.Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                ? null
                : DateTime.Parse(model.Pause24StoppedAt);
            planRegistration.Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                ? null
                : DateTime.Parse(model.Pause25StartedAt);
            planRegistration.Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                ? null
                : DateTime.Parse(model.Pause25StoppedAt);
            planRegistration.Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                ? null
                : DateTime.Parse(model.Pause26StartedAt);
            planRegistration.Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                ? null
                : DateTime.Parse(model.Pause26StoppedAt);
            planRegistration.Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                ? null
                : DateTime.Parse(model.Pause27StartedAt);
            planRegistration.Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                ? null
                : DateTime.Parse(model.Pause27StoppedAt);
            planRegistration.Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                ? null
                : DateTime.Parse(model.Pause28StartedAt);
            planRegistration.Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                ? null
                : DateTime.Parse(model.Pause28StoppedAt);
            planRegistration.Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                ? null
                : DateTime.Parse(model.Pause29StartedAt);
            planRegistration.Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                ? null
                : DateTime.Parse(model.Pause29StoppedAt);
            planRegistration.Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                ? null
                : DateTime.Parse(model.Pause200StartedAt);
            planRegistration.Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                ? null
                : DateTime.Parse(model.Pause200StoppedAt);
            planRegistration.Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                ? null
                : DateTime.Parse(model.Pause201StartedAt);
            planRegistration.Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                ? null
                : DateTime.Parse(model.Pause201StoppedAt);
            planRegistration.Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                ? null
                : DateTime.Parse(model.Pause202StartedAt);
            planRegistration.Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                ? null
                : DateTime.Parse(model.Pause202StoppedAt);

            planRegistration.Pause3StartedAt = string.IsNullOrEmpty(model.Pause3StartedAt)
                ? null
                : DateTime.Parse(model.Pause3StartedAt);
            planRegistration.Pause3StoppedAt = string.IsNullOrEmpty(model.Pause3StoppedAt)
                ? null
                : DateTime.Parse(model.Pause3StoppedAt);

            planRegistration.Pause4StartedAt = string.IsNullOrEmpty(model.Pause4StartedAt)
                ? null
                : DateTime.Parse(model.Pause4StartedAt);
            planRegistration.Pause4StoppedAt = string.IsNullOrEmpty(model.Pause4StoppedAt)
                ? null
                : DateTime.Parse(model.Pause4StoppedAt);

            planRegistration.Pause5StartedAt = string.IsNullOrEmpty(model.Pause5StartedAt)
                ? null
                : DateTime.Parse(model.Pause5StartedAt);
            planRegistration.Pause5StoppedAt = string.IsNullOrEmpty(model.Pause5StoppedAt)
                ? null
                : DateTime.Parse(model.Pause5StoppedAt);

            planRegistration.Shift1PauseNumber = model.Shift1PauseNumber;
            planRegistration.Shift2PauseNumber = model.Shift2PauseNumber;

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            if (planRegistration.Stop1Id >= planRegistration.Start1Id && planRegistration.Stop1Id != 0)
            {
                nettoMinutes = planRegistration.Stop1Id - planRegistration.Start1Id;
                nettoMinutes -= planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0;
            }

            if (planRegistration.Stop2Id >= planRegistration.Start2Id && planRegistration.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop2Id - planRegistration.Start2Id;
                nettoMinutes -= planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0;
            }

            if (planRegistration.Stop3Id >= planRegistration.Start3Id && planRegistration.Stop3Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop3Id - planRegistration.Start3Id;
                nettoMinutes -= planRegistration.Pause3Id > 0 ? planRegistration.Pause3Id - 1 : 0;
            }

            if (planRegistration.Stop4Id >= planRegistration.Start4Id && planRegistration.Stop4Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop4Id - planRegistration.Start4Id;
                nettoMinutes -= planRegistration.Pause4Id > 0 ? planRegistration.Pause4Id - 1 : 0;
            }

            if (planRegistration.Stop5Id >= planRegistration.Start5Id && planRegistration.Stop5Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop5Id - planRegistration.Start5Id;
                nettoMinutes -= planRegistration.Pause5Id > 0 ? planRegistration.Pause5Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planRegistration.NettoHours = hours;
            planRegistration.Flex = hours - planRegistration.PlanHours;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSite.MicrotingUid)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();
            if (preTimePlanning != null)
            {
                planRegistration.SumFlexEnd =
                    preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
            }
            else
            {
                planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = 0;
            }

            await planRegistration.Update(dbContext).ConfigureAwait(false);
        }

        return new OperationResult(true);
    }

    public async Task<OperationResult> UpdateWorkingHour(int? sdkSiteId, TimePlanningWorkingHoursUpdateModel model,
        string? token)
    {
        if (token == null && sdkSiteId == null)
        {
            return await UpdateWorkingHour(model).ConfigureAwait(false);
            //return new OperationResult(false, "Token not found");
        }

        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.Token == token).FirstOrDefaultAsync();
        if (registrationDevice == null)
        {
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false, "Token not found");
        }

        registrationDevice.OsVersion = model.OsVersion;
        registrationDevice.Model = model.Model;
        registrationDevice.Manufacturer = model.Manufacturer;
        registrationDevice.SoftwareVersion = model.SoftwareVersion;

        await registrationDevice.Update(dbContext);

        var todayAtMidnight = model.Date;

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == todayAtMidnight)
            .Where(x => x.SdkSitId == sdkSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (planRegistration == null)
        {
            planRegistration = new PlanRegistration
            {
                MessageId = null,
                PlanText = "",
                Date = model.Date,
                PlanHours = 0,
                UpdatedByUserId = userService.UserId,
                CommentOffice = "",
                CommentOfficeAll = "",
                NettoHours = 0,
                PaiedOutFlex = 0,
                Pause1Id = model.Shift1Pause ?? 0,
                Pause2Id = model.Shift2Pause ?? 0,
                Start1Id = model.Shift1Start ?? 0,
                Start2Id = model.Shift2Start ?? 0,
                Stop1Id = model.Shift1Stop ?? 0,
                Stop2Id = model.Shift2Stop ?? 0,
                Start3Id = model.Shift3Start ?? 0,
                Stop3Id = model.Shift3Stop ?? 0,
                Pause3Id = model.Shift3Pause ?? 0,
                Start4Id = model.Shift4Start ?? 0,
                Stop4Id = model.Shift4Stop ?? 0,
                Pause4Id = model.Shift4Pause ?? 0,
                Start5Id = model.Shift5Start ?? 0,
                Stop5Id = model.Shift5Stop ?? 0,
                Pause5Id = model.Shift5Pause ?? 0,
                Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                    ? null
                    : DateTime.Parse(model.Start1StartedAt),
                Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop1StoppedAt),

                Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                    ? null
                    : DateTime.Parse(model.Start2StartedAt),
                Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop2StoppedAt),

                Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                    ? null
                    : DateTime.Parse(model.Start3StartedAt),
                Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop3StoppedAt),

                Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                    ? null
                    : DateTime.Parse(model.Start4StartedAt),
                Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop4StoppedAt),

                Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                    ? null
                    : DateTime.Parse(model.Start5StartedAt),
                Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop5StoppedAt),

                Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause1StartedAt),
                Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause1StoppedAt),
                Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause10StartedAt),
                Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause10StoppedAt),
                Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause11StartedAt),
                Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause11StoppedAt),
                Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause12StartedAt),
                Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause12StoppedAt),
                Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause13StartedAt),
                Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause13StoppedAt),
                Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause14StartedAt),
                Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause14StoppedAt),
                Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause15StartedAt),
                Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause15StoppedAt),
                Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause16StartedAt),
                Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause16StoppedAt),
                Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause17StartedAt),
                Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause17StoppedAt),
                Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause18StartedAt),
                Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause18StoppedAt),
                Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause19StartedAt),
                Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause19StoppedAt),
                Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause100StartedAt),
                Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause100StoppedAt),
                Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause101StartedAt),
                Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause101StoppedAt),
                Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause102StartedAt),
                Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause102StoppedAt),

                Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause2StartedAt),
                Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause2StoppedAt),
                Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause20StartedAt),
                Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause20StoppedAt),
                Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause21StartedAt),
                Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause21StoppedAt),
                Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause22StartedAt),
                Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause22StoppedAt),
                Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause23StartedAt),
                Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause23StoppedAt),
                Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause24StartedAt),
                Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause24StoppedAt),
                Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause25StartedAt),
                Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause25StoppedAt),
                Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause26StartedAt),
                Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause26StoppedAt),
                Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause27StartedAt),
                Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause27StoppedAt),
                Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause28StartedAt),
                Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause28StoppedAt),
                Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause29StartedAt),
                Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause29StoppedAt),
                Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause200StartedAt),
                Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause200StoppedAt),
                Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause201StartedAt),
                Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause201StoppedAt),
                Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause202StartedAt),
                Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause202StoppedAt),

                Pause3StartedAt = string.IsNullOrEmpty(model.Pause3StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause3StartedAt),
                Pause3StoppedAt = string.IsNullOrEmpty(model.Pause3StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause3StoppedAt),

                Pause4StartedAt = string.IsNullOrEmpty(model.Pause4StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause4StartedAt),
                Pause4StoppedAt = string.IsNullOrEmpty(model.Pause4StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause4StoppedAt),

                Pause5StartedAt = string.IsNullOrEmpty(model.Pause5StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause5StartedAt),
                Pause5StoppedAt = string.IsNullOrEmpty(model.Pause5StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause5StoppedAt),
                Flex = 0,
                WorkerComment = model.CommentWorker,
                SdkSitId = sdkSiteId!.Value,
                RegistrationDeviceId = registrationDevice.Id,
                Shift1PauseNumber = model.Shift1PauseNumber,
                Shift2PauseNumber = model.Shift2PauseNumber,
            };

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSiteId.Value);

            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            if (planRegistration.Stop1Id >= planRegistration.Start1Id && planRegistration.Stop1Id != 0)
            {
                nettoMinutes = planRegistration.Stop1Id - planRegistration.Start1Id;
                nettoMinutes -= planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0;
            }

            if (planRegistration.Stop2Id >= planRegistration.Start2Id && planRegistration.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop2Id - planRegistration.Start2Id;
                nettoMinutes -= planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0;
            }

            if (planRegistration.Stop3Id >= planRegistration.Start3Id && planRegistration.Stop3Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop3Id - planRegistration.Start3Id;
                nettoMinutes -= planRegistration.Pause3Id > 0 ? planRegistration.Pause3Id - 1 : 0;
            }

            if (planRegistration.Stop4Id >= planRegistration.Start4Id && planRegistration.Stop4Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop4Id - planRegistration.Start4Id;
                nettoMinutes -= planRegistration.Pause4Id > 0 ? planRegistration.Pause4Id - 1 : 0;
            }

            if (planRegistration.Stop5Id >= planRegistration.Start5Id && planRegistration.Stop5Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop5Id - planRegistration.Start5Id;
                nettoMinutes -= planRegistration.Pause5Id > 0 ? planRegistration.Pause5Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planRegistration.NettoHours = hours;
            planRegistration.Flex = hours - planRegistration.PlanHours;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();
            if (preTimePlanning != null)
            {
                planRegistration.SumFlexEnd =
                    preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
            }
            else
            {
                planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = 0;
            }

            await planRegistration.Create(dbContext).ConfigureAwait(false);
        }
        else
        {
            planRegistration.UpdatedByUserId = userService.UserId;
            planRegistration.Pause1Id = model.Shift1Pause ?? 0;
            planRegistration.Pause2Id = model.Shift2Pause ?? 0;
            planRegistration.Start1Id = model.Shift1Start ?? 0;
            planRegistration.Start2Id = model.Shift2Start ?? 0;
            planRegistration.Stop1Id = model.Shift1Stop ?? 0;
            planRegistration.Stop2Id = model.Shift2Stop ?? 0;
            planRegistration.Start3Id = model.Shift3Start ?? 0;
            planRegistration.Stop3Id = model.Shift3Stop ?? 0;
            planRegistration.Pause3Id = model.Shift3Pause ?? 0;
            planRegistration.Start4Id = model.Shift4Start ?? 0;
            planRegistration.Stop4Id = model.Shift4Stop ?? 0;
            planRegistration.Pause4Id = model.Shift4Pause ?? 0;
            planRegistration.Start5Id = model.Shift5Start ?? 0;
            planRegistration.Stop5Id = model.Shift5Stop ?? 0;
            planRegistration.Pause5Id = model.Shift5Pause ?? 0;
            planRegistration.WorkerComment = model.CommentWorker;
            planRegistration.RegistrationDeviceId = registrationDevice.Id;

            planRegistration.Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                ? null
                : DateTime.Parse(model.Start1StartedAt);
            planRegistration.Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                ? null
                : DateTime.Parse(model.Stop1StoppedAt);

            planRegistration.Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                ? null
                : DateTime.Parse(model.Start2StartedAt);
            planRegistration.Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                ? null
                : DateTime.Parse(model.Stop2StoppedAt);

            planRegistration.Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                ? null
                : DateTime.Parse(model.Start3StartedAt);
            planRegistration.Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                ? null
                : DateTime.Parse(model.Stop3StoppedAt);

            planRegistration.Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                ? null
                : DateTime.Parse(model.Start4StartedAt);
            planRegistration.Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                ? null
                : DateTime.Parse(model.Stop4StoppedAt);

            planRegistration.Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                ? null
                : DateTime.Parse(model.Start5StartedAt);
            planRegistration.Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                ? null
                : DateTime.Parse(model.Stop5StoppedAt);

            planRegistration.Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                ? null
                : DateTime.Parse(model.Pause1StartedAt);
            planRegistration.Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                ? null
                : DateTime.Parse(model.Pause1StoppedAt);
            planRegistration.Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                ? null
                : DateTime.Parse(model.Pause10StartedAt);
            planRegistration.Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                ? null
                : DateTime.Parse(model.Pause10StoppedAt);
            planRegistration.Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                ? null
                : DateTime.Parse(model.Pause11StartedAt);
            planRegistration.Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                ? null
                : DateTime.Parse(model.Pause11StoppedAt);
            planRegistration.Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                ? null
                : DateTime.Parse(model.Pause12StartedAt);
            planRegistration.Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                ? null
                : DateTime.Parse(model.Pause12StoppedAt);
            planRegistration.Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                ? null
                : DateTime.Parse(model.Pause13StartedAt);
            planRegistration.Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                ? null
                : DateTime.Parse(model.Pause13StoppedAt);
            planRegistration.Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                ? null
                : DateTime.Parse(model.Pause14StartedAt);
            planRegistration.Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                ? null
                : DateTime.Parse(model.Pause14StoppedAt);
            planRegistration.Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                ? null
                : DateTime.Parse(model.Pause15StartedAt);
            planRegistration.Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                ? null
                : DateTime.Parse(model.Pause15StoppedAt);
            planRegistration.Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                ? null
                : DateTime.Parse(model.Pause16StartedAt);
            planRegistration.Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                ? null
                : DateTime.Parse(model.Pause16StoppedAt);
            planRegistration.Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                ? null
                : DateTime.Parse(model.Pause17StartedAt);
            planRegistration.Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                ? null
                : DateTime.Parse(model.Pause17StoppedAt);
            planRegistration.Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                ? null
                : DateTime.Parse(model.Pause18StartedAt);
            planRegistration.Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                ? null
                : DateTime.Parse(model.Pause18StoppedAt);
            planRegistration.Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                ? null
                : DateTime.Parse(model.Pause19StartedAt);
            planRegistration.Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                ? null
                : DateTime.Parse(model.Pause19StoppedAt);
            planRegistration.Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                ? null
                : DateTime.Parse(model.Pause100StartedAt);
            planRegistration.Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                ? null
                : DateTime.Parse(model.Pause100StoppedAt);
            planRegistration.Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                ? null
                : DateTime.Parse(model.Pause101StartedAt);
            planRegistration.Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                ? null
                : DateTime.Parse(model.Pause101StoppedAt);
            planRegistration.Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                ? null
                : DateTime.Parse(model.Pause102StartedAt);
            planRegistration.Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                ? null
                : DateTime.Parse(model.Pause102StoppedAt);

            planRegistration.Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                ? null
                : DateTime.Parse(model.Pause2StartedAt);
            planRegistration.Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                ? null
                : DateTime.Parse(model.Pause2StoppedAt);
            planRegistration.Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                ? null
                : DateTime.Parse(model.Pause20StartedAt);
            planRegistration.Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                ? null
                : DateTime.Parse(model.Pause20StoppedAt);
            planRegistration.Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                ? null
                : DateTime.Parse(model.Pause21StartedAt);
            planRegistration.Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                ? null
                : DateTime.Parse(model.Pause21StoppedAt);
            planRegistration.Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                ? null
                : DateTime.Parse(model.Pause22StartedAt);
            planRegistration.Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                ? null
                : DateTime.Parse(model.Pause22StoppedAt);
            planRegistration.Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                ? null
                : DateTime.Parse(model.Pause23StartedAt);
            planRegistration.Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                ? null
                : DateTime.Parse(model.Pause23StoppedAt);
            planRegistration.Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                ? null
                : DateTime.Parse(model.Pause24StartedAt);
            planRegistration.Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                ? null
                : DateTime.Parse(model.Pause24StoppedAt);
            planRegistration.Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                ? null
                : DateTime.Parse(model.Pause25StartedAt);
            planRegistration.Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                ? null
                : DateTime.Parse(model.Pause25StoppedAt);
            planRegistration.Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                ? null
                : DateTime.Parse(model.Pause26StartedAt);
            planRegistration.Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                ? null
                : DateTime.Parse(model.Pause26StoppedAt);
            planRegistration.Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                ? null
                : DateTime.Parse(model.Pause27StartedAt);
            planRegistration.Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                ? null
                : DateTime.Parse(model.Pause27StoppedAt);
            planRegistration.Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                ? null
                : DateTime.Parse(model.Pause28StartedAt);
            planRegistration.Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                ? null
                : DateTime.Parse(model.Pause28StoppedAt);
            planRegistration.Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                ? null
                : DateTime.Parse(model.Pause29StartedAt);
            planRegistration.Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                ? null
                : DateTime.Parse(model.Pause29StoppedAt);
            planRegistration.Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                ? null
                : DateTime.Parse(model.Pause200StartedAt);
            planRegistration.Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                ? null
                : DateTime.Parse(model.Pause200StoppedAt);
            planRegistration.Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                ? null
                : DateTime.Parse(model.Pause201StartedAt);
            planRegistration.Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                ? null
                : DateTime.Parse(model.Pause201StoppedAt);
            planRegistration.Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                ? null
                : DateTime.Parse(model.Pause202StartedAt);
            planRegistration.Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                ? null
                : DateTime.Parse(model.Pause202StoppedAt);

            planRegistration.Shift1PauseNumber = model.Shift1PauseNumber;
            planRegistration.Shift2PauseNumber = model.Shift2PauseNumber;

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSiteId!.Value);

            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            if (planRegistration.Stop1Id >= planRegistration.Start1Id && planRegistration.Stop1Id != 0)
            {
                nettoMinutes = planRegistration.Stop1Id - planRegistration.Start1Id;
                nettoMinutes -= planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0;
            }

            if (planRegistration.Stop2Id >= planRegistration.Start2Id && planRegistration.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop2Id - planRegistration.Start2Id;
                nettoMinutes -= planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0;
            }

            if (planRegistration.Stop3Id >= planRegistration.Start3Id && planRegistration.Stop3Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop3Id - planRegistration.Start3Id;
                nettoMinutes -= planRegistration.Pause3Id > 0 ? planRegistration.Pause3Id - 1 : 0;
            }

            if (planRegistration.Stop4Id >= planRegistration.Start4Id && planRegistration.Stop4Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop4Id - planRegistration.Start4Id;
                nettoMinutes -= planRegistration.Pause4Id > 0 ? planRegistration.Pause4Id - 1 : 0;
            }

            if (planRegistration.Stop5Id >= planRegistration.Start5Id && planRegistration.Stop5Id != 0)
            {
                nettoMinutes = nettoMinutes + planRegistration.Stop5Id - planRegistration.Start5Id;
                nettoMinutes -= planRegistration.Pause5Id > 0 ? planRegistration.Pause5Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planRegistration.NettoHours = hours;
            planRegistration.Flex = hours - planRegistration.PlanHours;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();
            if (preTimePlanning != null)
            {
                planRegistration.SumFlexEnd =
                    preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
            }
            else
            {
                planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = 0;
            }

            await planRegistration.Update(dbContext).ConfigureAwait(false);
        }

        return new OperationResult(true);
    }

    public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel model)
    {
        try
        {
            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            var site = await sdkContext.Sites.FirstAsync(x => x.MicrotingUid == model.SiteId);
            var siteWorker = await sdkContext.SiteWorkers.FirstAsync(x => x.SiteId == site.Id);
            var worker = await sdkContext.Workers.FirstAsync(x => x.Id == siteWorker!.WorkerId);
            var language = await userService.GetCurrentUserLanguage();
            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstAsync(x => x.SiteId == site!.MicrotingUid);

            var isThirdShiftEnabled = assignedSite.ThirdShiftActive;

            var isFourthShiftEnabled = assignedSite.FourthShiftActive;

            var isFifthShiftEnabled = assignedSite.FifthShiftActive;

            Thread.CurrentThread.CurrentUICulture = new CultureInfo(language.LanguageCode);
            var culture = new CultureInfo(language.LanguageCode);
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

            var timeStamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var filePath = Path.Combine(Path.GetTempPath(), "results", $"{timeStamp}_.xlsx");

            using (SpreadsheetDocument
                   document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart1 = document.AddWorkbookPart();
                OpenXMLHelper.GenerateWorkbookPart1Content(workbookPart1, [new("Dashboard", "rId1")]);

                WorkbookStylesPart workbookStylesPart1 = workbookPart1.AddNewPart<WorkbookStylesPart>("rId3");
                OpenXMLHelper.GenerateWorkbookStylesPart1Content(workbookStylesPart1);

                ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>("rId2");
                OpenXMLHelper.GenerateThemePart1Content(themePart1);

                WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>("rId1");

                var headers = new[]
                {
                    Translations.Employee_no,
                    Translations.Worker,
                    Translations.DayOfWeek,
                    Translations.Date,
                    Translations.PlanText,
                    Translations.PlanHours,
                    Translations.Shift_1__start,
                    Translations.Shift_1__end,
                    Translations.Shift_1__pause,
                    Translations.Shift_2__start,
                    Translations.Shift_2__end,
                    Translations.Shift_2__pause,
                    Translations.NettoHours,
                    Translations.Flex,
                    Translations.SumFlexStart,
                    Translations.PaidOutFlex,
                    Translations.Message,
                    Translations.Comments,
                    Translations.Comment_office
                };

                if (isThirdShiftEnabled)
                {
                    headers = headers.Concat(new[]
                    {
                        Translations.Shift_3__start,
                        Translations.Shift_3__end,
                        Translations.Shift_3__pause
                    }).ToArray();
                }

                if (isFourthShiftEnabled)
                {
                    headers = headers.Concat(new[]
                    {
                        Translations.Shift_4__start,
                        Translations.Shift_4__end,
                        Translations.Shift_4__pause
                    }).ToArray();
                }

                if (isFifthShiftEnabled)
                {
                    headers = headers.Concat(new[]
                    {
                        Translations.Shift_5__start,
                        Translations.Shift_5__end,
                        Translations.Shift_5__pause
                    }).ToArray();
                }

                List<string> headerStrings = new List<string>();
                foreach (var header in headers)
                {
                    headerStrings.Add(localizationService.GetString(header));
                }

                Worksheet worksheet1 = new Worksheet()
                    { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
                worksheet1.AddNamespaceDeclaration("r",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                worksheet1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
                worksheet1.AddNamespaceDeclaration("x14ac",
                    "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
                worksheet1.AddNamespaceDeclaration("xr",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
                worksheet1.AddNamespaceDeclaration("xr2",
                    "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                worksheet1.AddNamespaceDeclaration("xr3",
                    "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                worksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));

                SheetFormatProperties sheetFormatProperties1 = new SheetFormatProperties()
                    { DefaultRowHeight = 15D, DyDescent = 0.25D };

                SheetData sheetData1 = new SheetData();

                Row row1 = new Row()
                {
                    RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:19" },
                    DyDescent = 0.25D
                };

                foreach (var header in headerStrings)
                {
                    var cell = new Cell()
                    {
                        CellValue = new CellValue(header),
                        DataType = CellValues.String,
                        StyleIndex = (UInt32Value)1U
                    };
                    row1.Append(cell);
                }

                sheetData1.Append(row1);

                // Fetch data
                var content = await Index(model);
                if (!content.Success) return new OperationDataResult<Stream>(false, content.Message);

                // remove the first entry from the content.Model
                var timePlannings = content.Model.Skip(1).ToList();

                //var timePlannings = content.Model;
                var plr = new PlanRegistration();

                // Fill data
                int rowIndex = 2;
                foreach (var planning in timePlannings)
                {
                    var dataRow = new Row() { RowIndex = (uint)rowIndex };
                    FillDataRow(dataRow, worker, site, culture, planning, plr, language, isThirdShiftEnabled, isFourthShiftEnabled, isFifthShiftEnabled);
                    sheetData1.Append(dataRow);
                    rowIndex++;
                }

                var columnLetter = GetColumnLetter(headers.Length);
                AutoFilter autoFilter1 = new AutoFilter() { Reference = $"A1:{columnLetter}{rowIndex}" };
                autoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));
                PageMargins pageMargins1 = new PageMargins()
                    { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

                worksheet1.Append(sheetFormatProperties1);
                worksheet1.Append(sheetData1);
                worksheet1.Append(autoFilter1);
                worksheet1.Append(pageMargins1);

                worksheetPart1.Worksheet = worksheet1;

            }

            ValidateExcel(filePath);

            // Return the Excel file as a Stream
            return new OperationDataResult<Stream>(true, File.Open(filePath, FileMode.Open));
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
            return new OperationDataResult<Stream>(false,
                localizationService.GetString("ErrorWhileCreatingExcelFile"));
        }
    }

    private void FillDataRow(Row dataRow, Worker worker, Microting.eForm.Infrastructure.Data.Entities.Site site, CultureInfo culture,
        TimePlanningWorkingHoursModel planning, PlanRegistration plr, Language language, bool isThirdShiftEnabled, bool isFourthShiftEnabled, bool isFifthShiftEnabled)
    {
        try {
            dataRow.Append(CreateCell(worker.EmployeeNo ?? string.Empty));
            dataRow.Append(CreateCell(site.Name));
            dataRow.Append(CreateCell(planning.Date.ToString("dddd", culture)));
            dataRow.Append(CreateDateCell(planning.Date));
            dataRow.Append(CreateCell(planning.PlanText));
            dataRow.Append(CreateNumericCell(planning.PlanHours));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift1Start)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift1Stop)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift1Pause)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift2Start)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift2Stop)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift2Pause)));
            dataRow.Append(CreateNumericCell(planning.NettoHoursOverrideActive ? planning.NettoHoursOverride : planning.NettoHours));
            dataRow.Append(CreateNumericCell(planning.FlexHours));
            dataRow.Append(CreateNumericCell(planning.SumFlexEnd));
            dataRow.Append(CreateNumericCell(string.IsNullOrEmpty(planning.PaidOutFlex)
                ? 0
                : double.Parse(planning.PaidOutFlex.Replace(",", "."), CultureInfo.InvariantCulture)));
            dataRow.Append(CreateCell(GetMessageText(planning.Message, language)));
            dataRow.Append(CreateCell(planning.CommentWorker?.Replace("<br>", "\n")));
            dataRow.Append(CreateCell(planning.CommentOffice?.Replace("<br>", "\n")));
            if (isThirdShiftEnabled)
            {
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift3Start)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift3Stop)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift3Pause)));
            }
            if (isFourthShiftEnabled)
            {
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift4Start)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift4Stop)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift4Pause)));
            }
            if (isFifthShiftEnabled)
            {
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift5Start)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift5Stop)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift5Pause)));
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError($"Error while filling data row: {ex.Message}");
            throw;
        }
    }

    private Cell CreateCell(string value)
    {
        return new Cell()
        {
            CellValue = new CellValue(value),
            DataType = CellValues.String // Explicitly setting the data type to string
        };
    }

    private Cell CreateNumericCell(double value)
    {
        return new Cell()
        {
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
            DataType = CellValues.Number
        };
    }

    private Cell CreateDateCell(DateTime dateValue)
    {
        return new Cell()
        {
            CellValue = new CellValue(dateValue.ToOADate()
                .ToString(CultureInfo.InvariantCulture)), // Excel stores dates as OLE Automation date values
            DataType = CellValues.Number, // Excel treats dates as numbers
            StyleIndex = 2 // Assuming StyleIndex 2 corresponds to the date format in the stylesheet
        };
    }


    private string GetShiftTime(PlanRegistration plr, int? shift)
    {
        if (shift == 289)
        {
            return "24:00";
        }
        return shift > 0 ? plr.Options[(int)shift - 1] : "";
    }

    private string GetMessageText(int? messageId, Language language)
    {
        if (messageId == null) return string.Empty;

        var message = dbContext.Messages.SingleOrDefault(x => x.Id == messageId);
        return message == null
            ? string.Empty
            : language.LanguageCode switch
            {
                "da" => message.DaName,
                "de" => message.DeName,
                _ => message.EnName
            };
    }

    private void ValidateExcel(string fileName)
    {
        try
        {
            var validator = new OpenXmlValidator();
            int count = 0;
            StringBuilder sb = new StringBuilder();
            var doc = SpreadsheetDocument.Open(fileName, true);
            foreach (ValidationErrorInfo error in validator.Validate(doc))
            {

                count++;
                sb.Append(("Error Count : " + count) + "\r\n");
                sb.Append(("Description : " + error.Description) + "\r\n");
                sb.Append(("Path: " + error.Path.XPath) + "\r\n");
                sb.Append(("Part: " + error.Part.Uri) + "\r\n");
                sb.Append("\r\n-------------------------------------------------\r\n");
            }

            doc.Dispose();
            if (count <= 0) return;
            sb.Append(("Total Errors in file: " + count));
            throw new Exception(sb.ToString());
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
        }
    }

    public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(
        TimePlanningWorkingHoursReportForAllWorkersRequestModel model)
    {
        try
        {
            var siteIds = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => x.SiteId)
                .Distinct()
                .ToListAsync();

            var isThirdShiftEnabled = dbContext.AssignedSites
                .Any(x => x.ThirdShiftActive && x.WorkflowState != Constants.WorkflowStates.Removed);

            var isFourthShiftEnabled = dbContext.AssignedSites
                .Any(x => x.FourthShiftActive && x.WorkflowState != Constants.WorkflowStates.Removed);

            var isFifthShiftEnabled = dbContext.AssignedSites
                .Any(x => x.FifthShiftActive && x.WorkflowState != Constants.WorkflowStates.Removed);

            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));
            var timeStamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var resultDocument = Path.Combine(Path.GetTempPath(), "results", $"{timeStamp}_.xlsx");

            using (var document =
                   SpreadsheetDocument.Create(resultDocument, SpreadsheetDocumentType.Workbook))
            {
                var siteIdCount = siteIds.Count;

                var worksheetNames = new List<KeyValuePair<string, string>>();
                worksheetNames.Add(
                    new KeyValuePair<string, string>("Total", "rId1"));

                for (int i = 0; i < siteIdCount; i++)
                {
                    var site = await sdkContext.Sites.SingleOrDefaultAsync(x =>
                        x.MicrotingUid == siteIds[i] && x.WorkflowState != Constants.WorkflowStates.Removed);
                    if (site == null) continue;
                    worksheetNames.Add(
                        new KeyValuePair<string, string>($"{site.Name.Substring(0, Math.Min(31, site.Name.Length))}",
                            $"rId{i + 2}"));
                }

                WorkbookPart workbookPart1 = document.AddWorkbookPart();
                OpenXMLHelper.GenerateWorkbookPart1Content(workbookPart1, worksheetNames);

                WorkbookStylesPart workbookStylesPart1 =
                    workbookPart1.AddNewPart<WorkbookStylesPart>($"rId{siteIdCount + 3}");
                OpenXMLHelper.GenerateWorkbookStylesPart1Content(workbookStylesPart1);

                ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>($"rId{siteIdCount + 2}");
                OpenXMLHelper.GenerateThemePart1Content(themePart1);

                #region TotalSheetSetup

                WorksheetPart totalWorksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>($"rId1");
                var totalHeaders = new[]
                {
                    Translations.From,
                    Translations.To,
                    Translations.Employee_no,
                    Translations.Worker,
                    Translations.PlanHours,
                    Translations.NettoHours,
                    Translations.SumFlexStart
                };
                List<string> totalHeaderStrings = new List<string>();
                foreach (var header in totalHeaders)
                {
                    totalHeaderStrings.Add(localizationService.GetString(header));
                }

                Worksheet totalWorksheet1 = new Worksheet()
                    { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
                totalWorksheet1.AddNamespaceDeclaration("r",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                totalWorksheet1.AddNamespaceDeclaration("mc",
                    "http://schemas.openxmlformats.org/markup-compatibility/2006");
                totalWorksheet1.AddNamespaceDeclaration("x14ac",
                    "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
                totalWorksheet1.AddNamespaceDeclaration("xr",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
                totalWorksheet1.AddNamespaceDeclaration("xr2",
                    "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                totalWorksheet1.AddNamespaceDeclaration("xr3",
                    "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                totalWorksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));

                SheetFormatProperties totalSheetFormatProperties1 = new SheetFormatProperties()
                    { DefaultRowHeight = 15D, DyDescent = 0.25D };

                SheetData totalSheetData1 = new SheetData();

                Row totalRow1 = new Row()
                {
                    RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:19" },
                    DyDescent = 0.25D
                };

                foreach (var totalHeader in totalHeaderStrings)
                {
                    var cell = new Cell()
                    {
                        CellValue = new CellValue(totalHeader),
                        DataType = CellValues.String,
                        StyleIndex = (UInt32Value)1U
                    };
                    totalRow1.Append(cell);
                }

                totalSheetData1.Append(totalRow1);

                var totalRowIndex = 2;

                #endregion

                var language = await userService.GetCurrentUserLanguage();

                var culture = new CultureInfo(language.LanguageCode);
                for (int i = 0; i < siteIdCount; i++)
                {
                    var site = await sdkContext.Sites.FirstOrDefaultAsync(x =>
                        x.MicrotingUid == siteIds[i]);
                    if (site == null) continue;
                    var siteWorker = await sdkContext.SiteWorkers.FirstAsync(x => x.SiteId == site.Id);
                    var worker = await sdkContext.Workers.FirstAsync(x => x.Id == siteWorker.WorkerId);
                    WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>($"rId{i + 2}");

                    var headers = new[]
                    {
                        Translations.Employee_no,
                        Translations.Worker,
                        Translations.DayOfWeek,
                        Translations.Date,
                        Translations.PlanText,
                        Translations.PlanHours,
                        Translations.Shift_1__start,
                        Translations.Shift_1__end,
                        Translations.Shift_1__pause,
                        Translations.Shift_2__start,
                        Translations.Shift_2__end,
                        Translations.Shift_2__pause,
                        Translations.NettoHours,
                        Translations.Flex,
                        Translations.SumFlexStart,
                        Translations.PaidOutFlex,
                        Translations.Message,
                        Translations.Comments,
                        Translations.Comment_office
                    };

                    if (isThirdShiftEnabled)
                    {
                        headers = headers.Concat(new[]
                        {
                            Translations.Shift_3__start,
                            Translations.Shift_3__end,
                            Translations.Shift_3__pause
                        }).ToArray();
                    }

                    if (isFourthShiftEnabled)
                    {
                        headers = headers.Concat(new[]
                        {
                            Translations.Shift_4__start,
                            Translations.Shift_4__end,
                            Translations.Shift_4__pause
                        }).ToArray();
                    }

                    if (isFifthShiftEnabled)
                    {
                        headers = headers.Concat(new[]
                        {
                            Translations.Shift_5__start,
                            Translations.Shift_5__end,
                            Translations.Shift_5__pause
                        }).ToArray();
                    }
                    List<string> headerStrings = new List<string>();
                    foreach (var header in headers)
                    {
                        headerStrings.Add(localizationService.GetString(header));
                    }

                    Worksheet worksheet1 = new Worksheet()
                        { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
                    worksheet1.AddNamespaceDeclaration("r",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                    worksheet1.AddNamespaceDeclaration("mc",
                        "http://schemas.openxmlformats.org/markup-compatibility/2006");
                    worksheet1.AddNamespaceDeclaration("x14ac",
                        "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
                    worksheet1.AddNamespaceDeclaration("xr",
                        "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
                    worksheet1.AddNamespaceDeclaration("xr2",
                        "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                    worksheet1.AddNamespaceDeclaration("xr3",
                        "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                    worksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                        "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                        "{00000000-0001-0000-0000-000000000000}"));

                    SheetFormatProperties sheetFormatProperties1 = new SheetFormatProperties()
                        { DefaultRowHeight = 15D, DyDescent = 0.25D };

                    SheetData sheetData1 = new SheetData();

                    Row row1 = new Row()
                    {
                        RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:19" },
                        DyDescent = 0.25D
                    };

                    foreach (var header in headerStrings)
                    {
                        var cell = new Cell()
                        {
                            CellValue = new CellValue(header),
                            DataType = CellValues.String,
                            StyleIndex = (UInt32Value)1U
                        };
                        row1.Append(cell);
                    }

                    sheetData1.Append(row1);

                    // Fetch data
                    var content = await Index(new TimePlanningWorkingHoursRequestModel
                    {
                        DateFrom = model.DateFrom,
                        DateTo = model.DateTo,
                        SiteId = (int)site!.MicrotingUid!
                    });
                    if (!content.Success) return new OperationDataResult<Stream>(false, content.Message);

                    //var timePlannings = content.Model;

                    var timePlannings = content.Model.Skip(1).ToList();
                    var plr = new PlanRegistration();

                    // Fill data
                    int rowIndex = 2;
                    foreach (var planning in timePlannings)
                    {
                        var dataRow = new Row() { RowIndex = (uint)rowIndex };
                        try
                        {
                            FillDataRow(dataRow, worker, site, culture, planning, plr, language, isThirdShiftEnabled, isFourthShiftEnabled, isFifthShiftEnabled);
                            sheetData1.Append(dataRow);
                        }
                        catch (Exception e)
                        {
                            SentrySdk.CaptureException(e);
                            logger.LogError(e.Message);
                            logger.LogError(e.StackTrace);
                            logger.LogError($"Error while filling data row for site {site.Name} on row {rowIndex}: {e.Message}");
                            throw;
                        }
                        rowIndex++;
                    }

                    var columnLetter = GetColumnLetter(headers.Length);
                    AutoFilter autoFilter1 = new AutoFilter() { Reference = $"A1:{columnLetter}{rowIndex}" };
                    autoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                        "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                        "{00000000-0001-0000-0000-000000000000}"));
                    PageMargins pageMargins1 = new PageMargins()
                        { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

                    worksheet1.Append(sheetFormatProperties1);
                    worksheet1.Append(sheetData1);
                    worksheet1.Append(autoFilter1);
                    worksheet1.Append(pageMargins1);

                    worksheetPart1.Worksheet = worksheet1;

                    #region TotalSheetFillData

                    var totalRow = new Row() { RowIndex = (uint)totalRowIndex };
                    totalRow.Append(CreateDateCell(model.DateFrom));
                    totalRow.Append(CreateDateCell(model.DateTo));
                    totalRow.Append(CreateCell(worker.EmployeeNo ?? string.Empty));
                    totalRow.Append(CreateCell(site.Name));
                    totalRow.Append(CreateNumericCell(content.Model.Skip(1).ToList().Sum(x => x.PlanHours)));
                    var nettoHoursTotal = content.Model.Skip(1).ToList().Where(x => x.NettoHoursOverrideActive == false).Sum(x => x.NettoHours);
                    var nettoHoursOverrideTotal = content.Model.Skip(1).ToList().Where(x => x.NettoHoursOverrideActive).Sum(x => x.NettoHoursOverride);
                    totalRow.Append(CreateNumericCell(nettoHoursTotal + nettoHoursOverrideTotal));
                    totalRow.Append(CreateNumericCell(content.Model.Last().SumFlexEnd));
                    totalSheetData1.Append(totalRow);
                    totalRowIndex++;

                    #endregion

                }

                #region TotalSheetFinalize

                var totalColumnLetter = GetColumnLetter(totalHeaders.Length);
                AutoFilter totalAutoFilter1 = new AutoFilter() { Reference = $"A1:{totalColumnLetter}{totalRowIndex}" };
                totalAutoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));
                PageMargins totalPageMargins1 = new PageMargins()
                    { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

                totalWorksheet1.Append(totalSheetFormatProperties1);
                totalWorksheet1.Append(totalSheetData1);
                totalWorksheet1.Append(totalAutoFilter1);
                totalWorksheet1.Append(totalPageMargins1);

                totalWorksheetPart1.Worksheet = totalWorksheet1;

                #endregion
            }

            ValidateExcel(resultDocument);

            // Return the Excel file as a Stream
            return new OperationDataResult<Stream>(true, File.Open(resultDocument, FileMode.Open));
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
            return new OperationDataResult<Stream>(false,
                localizationService.GetString("ErrorWhileCreatingExcelFile"));
        }
    }

    public async Task<OperationResult> Import(IFormFile file)
    {
        try
        {
            // Get core
            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);

                // Open the Excel document using OpenXML
                using (var spreadsheetDocument = SpreadsheetDocument.Open(stream, false))
                {
                    var workbookPart = spreadsheetDocument.WorkbookPart;
                    var sheets = workbookPart.Workbook.Sheets;

                    foreach (Sheet sheet in sheets)
                    {
                        var site = await sdkContext.Sites.FirstOrDefaultAsync(x => x.Name.Replace(" ", "").ToLower() == sheet.Name.Value.Replace(" ", "").ToLower());
                        if (site == null)
                        {
                            continue;
                        }

                        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                        var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

                        var rows = sheetData.Elements<Row>();
                        foreach (var row in rows)
                        {
                            // Skip header row
                            if (row.RowIndex == 1)
                            {
                                continue;
                            }

                            // Extract cell values
                            // var dateCell = row.Elements<Cell>().ElementAt(0); // First column
                            // var planHoursCell = row.Elements<Cell>().ElementAt(1); // Second column
                            // var planTextCell = row.Elements<Cell>().ElementAt(2); // Third column

                            var date = GetCellValue(workbookPart, row, 1);
                            var planHours = GetCellValue(workbookPart, row, 2);
                            var planText = GetCellValue(workbookPart, row, 3);

                            if (string.IsNullOrEmpty(planHours))
                            {
                                planHours = "0";
                            }

                            // Replace comma with dot if needed
                            if (planHours.Contains(','))
                            {
                                planHours = planHours.Replace(",", ".");
                            }

                            double parsedPlanHours = double.Parse(planHours, NumberStyles.AllowDecimalPoint,
                                NumberFormatInfo.InvariantInfo);

                            // Parse date and validate
                            if (!DateTime.TryParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out var _))
                            {
                                continue;
                            }

                            var dateValue = DateTime.ParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture);                            if (dateValue < DateTime.Now.AddDays(-1))
                            {
                                continue;
                            }

                            if (dateValue > DateTime.Now.AddDays(180))
                            {
                                continue;
                            }

                            var preTimePlanning = await dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < dateValue && x.SdkSitId == (int)site.MicrotingUid!)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                            var planRegistration = await dbContext.PlanRegistrations.SingleOrDefaultAsync(x =>
                                x.Date == dateValue && x.SdkSitId == site.MicrotingUid);

                            if (planRegistration == null)
                            {
                                planRegistration = new PlanRegistration
                                {
                                    Date = dateValue,
                                    PlanText = planText,
                                    PlanHours = parsedPlanHours,
                                    SdkSitId = (int)site.MicrotingUid!,
                                    CreatedByUserId = userService.UserId,
                                    UpdatedByUserId = userService.UserId,
                                    NettoHours = 0,
                                    PaiedOutFlex = 0,
                                    Pause1Id = 0,
                                    Pause2Id = 0,
                                    Start1Id = 0,
                                    Start2Id = 0,
                                    Stop1Id = 0,
                                    Stop2Id = 0,
                                    Flex = 0,
                                    StatusCaseId = 0
                                };

                                if (preTimePlanning != null)
                                {
                                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
                                                                  planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = -planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.Flex = -planRegistration.PlanHours;
                                    planRegistration.SumFlexEnd = planRegistration.Flex;
                                    planRegistration.SumFlexStart = 0;
                                }

                                await planRegistration.Create(dbContext);
                            }
                            else
                            {
                                planRegistration.PlanText = planText;
                                planRegistration.PlanHours = parsedPlanHours;
                                planRegistration.UpdatedByUserId = userService.UserId;

                                if (preTimePlanning != null)
                                {
                                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                    planRegistration.SumFlexEnd =
                                        preTimePlanning.SumFlexEnd + planRegistration.PlanHours -
                                        planRegistration.NettoHours -
                                        planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.SumFlexEnd =
                                        planRegistration.PlanHours - planRegistration.NettoHours -
                                        planRegistration.PaiedOutFlex;
                                    planRegistration.SumFlexStart = 0;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }

                                await planRegistration.Update(dbContext);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
            return new OperationResult(false, ex.Message);
        }
        return new OperationResult(true, "Imported");
    }

    private string GetCellValue(WorkbookPart workbookPart, Row row, int columnIndex)
    {
        // Get the column letter for the given columnIndex (e.g., A, B, C)
        var columnLetter = GetColumnLetter(columnIndex);

        // Create the cell reference (e.g., A1, B1, C1)
        var cellReference = columnLetter + row.RowIndex;

        // Find the cell with the matching CellReference
        var cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference.Value == cellReference);

        if (cell == null || cell.CellValue == null)
        {
            return string.Empty; // Handle empty or missing cells
        }

        // Check if the cell is using a Shared String Table
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            var sharedStringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
            if (sharedStringTablePart != null)
            {
                var sharedStringTable = sharedStringTablePart.SharedStringTable;
                return sharedStringTable.ElementAt(int.Parse(cell.CellValue.Text)).InnerText;
            }
        }

        // Check if the cell has a StyleIndex (to determine if it's a date)
        if (cell.StyleIndex != null)
        {
            var stylesPart = workbookPart.WorkbookStylesPart;
            var cellFormat = stylesPart.Stylesheet.CellFormats.ElementAt((int)cell.StyleIndex.Value) as CellFormat;
            var isDate = IsDateFormat(stylesPart, cellFormat);

            // If it's a date format, interpret the numeric value as a date
            if (isDate && double.TryParse(cell.CellValue.Text, out var oaDate))
            {
                var dateValue = DateTime.FromOADate(oaDate);
                return dateValue.ToString("dd.MM.yyyy"); // Format as a date
            }
        }

        // Handle other numbers or strings
        return cell.CellValue.Text;
    }

    private bool IsDateFormat(WorkbookStylesPart stylesPart, CellFormat cellFormat)
    {
        if (cellFormat == null || cellFormat.NumberFormatId == null)
        {
            return false;
        }

        // Check if the format ID is a known date format in Excel
        var dateFormatIds = new HashSet<uint> { 14, 15, 16, 17, 22, 164 }; // Common Excel date format IDs

        if (dateFormatIds.Contains(cellFormat.NumberFormatId.Value))
        {
            return true;
        }

        // Look for custom number formats defined in the workbook
        var numberFormats = stylesPart.Stylesheet.NumberingFormats?.Elements<NumberingFormat>();
        if (numberFormats != null)
        {
            var format = numberFormats.FirstOrDefault(nf => nf.NumberFormatId.Value == cellFormat.NumberFormatId.Value);
            if (format != null && format.FormatCode != null)
            {
                // Check if the custom format code looks like a date format
                var formatCode = format.FormatCode.Value.ToLower();
                return formatCode.Contains("m") || formatCode.Contains("d") || formatCode.Contains("y");
            }
        }

        return false;
    }

    private string GetColumnLetter(int columnIndex)
    {
        string columnLetter = "";
        while (columnIndex > 0)
        {
            int modulo = (columnIndex - 1) % 26;
            columnLetter = Convert.ToChar(65 + modulo) + columnLetter;
            columnIndex = (columnIndex - modulo) / 26;
        }

        return columnLetter;
    }
}