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
using Sentry;

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
            var datesInPeriod = new List<DateTime>();
            var date = model.DateFrom;
            while (date <= model.DateTo)
            {
                datesInPeriod.Add(date.Value);
                date = date.Value.AddDays(1);
            }
            foreach (var assignedSite in assignedSites)
            {
                var site = await sdkDbContext.Sites
                    .Where(x => x.MicrotingUid == assignedSite.SiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (site == null)
                {
                    continue;
                }
                var siteModel = new TimePlanningPlanningModel
                {
                    SiteId = assignedSite.SiteId,
                    SiteName = site.Name,
                    PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
                };

                var planningsInPeriod = await dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SdkSitId == assignedSite.SiteId)
                    .Where(x => x.Date >= model.DateFrom)
                    .Where(x => x.Date <= model.DateTo)
                    .OrderBy(x => x.Date)
                    .ToListAsync().ConfigureAwait(false);
                
                var plannedTotalHours = planningsInPeriod.Sum(x => x.PlanHours);
                var nettoHoursTotal = planningsInPeriod.Sum(x => x.NettoHours);
                
                siteModel.PlannedHours = (int)plannedTotalHours;
                siteModel.PlannedMinutes = (int)((plannedTotalHours - siteModel.PlannedHours) * 60);
                siteModel.CurrentWorkedHours = (int)nettoHoursTotal;
                siteModel.CurrentWorkedMinutes = (int)((nettoHoursTotal - siteModel.CurrentWorkedHours) * 60);
                siteModel.PercentageCompleted = (int)(nettoHoursTotal / plannedTotalHours * 100);
                
                foreach (var planning in planningsInPeriod)
                {
                    var midnight = new DateTime(planning.Date.Year, planning.Date.Month, planning.Date.Day, 0, 0, 0);

                    var planningModel = new TimePlanningPlanningPrDayModel
                    {
                        Id = planning.Id,
                        SiteName = site.Name,
                        Date = midnight,
                        PlanText = planning.PlanText,
                        PlanHours = planning.PlanHours,
                        Message = planning.MessageId,
                        SiteId = assignedSite.SiteId,
                        WeekDay = planning.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)planning.Date.DayOfWeek,
                        ActualHours = planning.NettoHours,
                        Difference = planning.NettoHours - planning.PlanHours,
                        PlanHoursMatched = Math.Abs(planning.NettoHours - planning.PlanHours) < 0.00,
                        WorkDayStarted = planning.Start1Id != 0,
                        WorkDayEnded = planning.Stop1Id != 0 || (planning.Start2Id != 0 && planning.Stop2Id != 0),
                        PlannedStartOfShift1 = planning.PlannedStartOfShift1,
                        PlannedEndOfShift1 = planning.PlannedEndOfShift1,
                        PlannedBreakOfShift1 = planning.PlannedBreakOfShift1,
                        PlannedStartOfShift2 = planning.PlannedStartOfShift2,
                        PlannedEndOfShift2 = planning.PlannedEndOfShift2,
                        PlannedBreakOfShift2 = planning.PlannedBreakOfShift2,
                        IsDoubleShift = planning.Start2StartedAt != planning.Stop2StoppedAt,
                        OnVacation = planning.OnVacation,
                        Sick = planning.Sick,
                        OtherAllowedAbsence = planning.OtherAllowedAbsence,
                        AbsenceWithoutPermission = planning.AbsenceWithoutPermission,
                        Start1StartedAt = (planning.Start1Id == 0 ? null : midnight.AddMinutes(
                            (planning.Start1Id * 5) - 5)),
                        Stop1StoppedAt = (planning.Stop1Id == 0 ? null : midnight.AddMinutes(
                            (planning.Stop1Id * 5) - 5)),
                        Start2StartedAt = (planning.Start2Id == 0 ? null : midnight.AddMinutes(
                            (planning.Start2Id * 5) - 5)),
                        Stop2StoppedAt = (planning.Stop2Id == 0 ? null : midnight.AddMinutes(
                            (planning.Stop2Id * 5) - 5)),
                    };
                    try
                    {
                        if (planning.PlannedStartOfShift1 == 0 && !string.IsNullOrEmpty(planning.PlanText) &&
                            planning.PlanHours > 0)
                        {
                            // split the planText by this regex (.*)-(.*)\/(.*)
                            // the parts are in hours, so we need to multiply by 60 to get minutes and can be like 7.30 or 7:30 so it can be 7.5, 7:30, 7½ and they are all the same
                            // so we parse the first part and multiply by 60 and just add the second part
                            // the last part is the break in minutes and can be ¾ or ½
                            var regex = new Regex(@"(.*)-(.*)\/(.*)");
                            var match = regex.Match(planning.PlanText);
                            if (match.Captures.Count == 0)
                            {
                                regex = new Regex(@"(.*)-(.*)");
                                match = regex.Match(planning.PlanText);
                            }
                            var firstPart = match.Groups[1].Value;
                            var firstPartSplit =
                                firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                            var firstPartHours = int.Parse(firstPartSplit[0]);
                            var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                            var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                            var secondPart = match.Groups[2].Value;
                            var secondPartSplit =
                                secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                            var secondPartHours = int.Parse(secondPartSplit[0]);
                            var secondPartMinutes = secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                            var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                            planning.PlannedStartOfShift1 = firstPartTotalMinutes;
                            planningModel.PlannedStartOfShift1 = planning.PlannedStartOfShift1;
                            planning.PlannedEndOfShift1 = secondPartTotalMinutes;
                            planningModel.PlannedEndOfShift1 = planning.PlannedEndOfShift1;

                            if (match.Groups.Count == 4)
                            {
                                var breakPart = match.Groups[3].Value;
                                var breakPartMinutes = breakPart switch
                                {
                                    "¾" => 45,
                                    "½" => 30,
                                    "1" => 60,
                                    _ => 0
                                };

                                planning.PlannedBreakOfShift1 = breakPartMinutes;
                                planningModel.PlannedBreakOfShift1 = planning.PlannedBreakOfShift1;
                            }

                            await planning.Update(dbContext).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Could not parse PlanText for planning with id: {planning.Id} the PlanText was: {planning.PlanText}");
                        SentrySdk.CaptureMessage($"Could not parse PlanText for planning with id: {planning.Id} the PlanText was: {planning.PlanText}");
                        //SentrySdk.CaptureException(e);
                        logger.LogError(e.Message);
                        logger.LogTrace(e.StackTrace);
                    }

                    siteModel.PlanningPrDayModels.Add(planningModel);
                }

                // check if there are any dates in the period that are not in the plannings
                // if not, then add a new planning with default values
                foreach (var dateInPeriod in datesInPeriod)
                {
                    if (siteModel.PlanningPrDayModels.All(x => x.Date != dateInPeriod))
                    {
                        var planningModel = new TimePlanningPlanningPrDayModel
                        {
                            SiteName = site.Name,
                            Date = dateInPeriod,
                            PlanText = "",
                            PlanHours = 0,
                            Message = null,
                            SiteId = assignedSite.SiteId,
                            WeekDay = dateInPeriod.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dateInPeriod.DayOfWeek,
                            ActualHours = 0,
                            Difference = 0,
                            PlanHoursMatched = true
                        };
                        siteModel.PlanningPrDayModels.Add(planningModel);
                    }
                }


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
    //
    // public async Task<OperationResult> UpdateCreatePlanning(TimePlanningPlanningUpdateModel model)
    // {
    //     try
    //     {
    //         var planning = await dbContext.PlanRegistrations
    //             .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
    //             .Where(x => x.SdkSitId == model.SiteId)
    //             .Where(x => x.Date == model.Date)
    //             .FirstOrDefaultAsync();
    //         if (planning != null)
    //         {
    //             return await UpdatePlanning(planning, model);
    //         }
    //
    //         return await CreatePlanning(model, model.SiteId);
    //     }
    //     catch (Exception e)
    //     {
    //         SentrySdk.CaptureException(e);
    //         logger.LogError(e.Message);
    //         logger.LogTrace(e.StackTrace);
    //         return new OperationResult(
    //             false,
    //             localizationService.GetString("ErrorWhileUpdatePlanning"));
    //     }
    // }
    //
    // private async Task<OperationResult> CreatePlanning(TimePlanningPlanningUpdateModel model, int sdkSiteId)
    // {
    //     try
    //     {
    //         var planning = new PlanRegistration
    //         {
    //             PlanText = model.PlanText,
    //             SdkSitId = sdkSiteId,
    //             Date = model.Date,
    //             PlanHours = model.PlanHours,
    //             CreatedByUserId = userService.UserId,
    //             UpdatedByUserId = userService.UserId,
    //             MessageId = model.Message
    //         };
    //
    //         await planning.Create(dbContext);
    //
    //         return new OperationResult(
    //             true,
    //             localizationService.GetString("SuccessfullyCreatePlanning"));
    //     }
    //     catch (Exception e)
    //     {
    //         SentrySdk.CaptureException(e);
    //         logger.LogError(e.Message);
    //         logger.LogTrace(e.StackTrace);
    //         return new OperationResult(
    //             false,
    //             localizationService.GetString("ErrorWhileCreatePlanning"));
    //     }
    // }
    //
    // private async Task<OperationResult> UpdatePlanning(PlanRegistration planning,
    //     TimePlanningPlanningUpdateModel model)
    // {
    //     try
    //     {
    //         planning.MessageId = model.Message;
    //         planning.PlanText = model.PlanText;
    //         planning.PlanHours = model.PlanHours;
    //         planning.UpdatedByUserId = userService.UserId;
    //
    //         await planning.Update(dbContext);
    //
    //         return new OperationResult(
    //             true,
    //             localizationService.GetString("SuccessfullyUpdatePlanning"));
    //     }
    //     catch (Exception e)
    //     {
    //         SentrySdk.CaptureException(e);
    //         logger.LogError(e.Message);
    //         logger.LogTrace(e.StackTrace);
    //         return new OperationResult(
    //             false,
    //             localizationService.GetString("ErrorWhileUpdatePlanning"));
    //     }
    // }
}