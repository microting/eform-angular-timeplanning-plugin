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
                foreach (var planning in planningsInPeriod)
                {
                    var planningModel = new TimePlanningPlanningPrDayModel
                    {
                        SiteName = site.Name,
                        Date = planning.Date,
                        PlanText = planning.PlanText,
                        PlanHours = planning.PlanHours,
                        Message = planning.MessageId,
                        SiteId = assignedSite.SiteId,
                        WeekDay = planning.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)planning.Date.DayOfWeek,
                        ActualHours = planning.NettoHours,
                        Difference = planning.NettoHours - planning.PlanHours,
                        PlanHoursMatched = Math.Abs(planning.NettoHours - planning.PlanHours) < 0.00,
                        WorkDayStarted = planning.Start1Id != 0,
                        WorkDayEnded = planning.Stop1Id != 0 && planning.Stop2Id != 0

                    };
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