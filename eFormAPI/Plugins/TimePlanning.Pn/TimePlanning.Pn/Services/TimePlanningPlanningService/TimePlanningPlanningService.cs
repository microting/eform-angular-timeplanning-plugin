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

namespace TimePlanning.Pn.Services.TimePlanningPlanningService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.Planning;
    using Infrastructure.Models.Planning.HelperModel;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Entities;
    using TimePlanningLocalizationService;

    public class TimePlanningPlanningService : ITimePlanningPlanningService
    {
        private readonly ILogger<TimePlanningPlanningService> _logger;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningPlanningService(
            ILogger<TimePlanningPlanningService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
        }

        public async Task<OperationDataResult<List<TimePlanningPlanningModel>>> Index(TimePlanningPlanningRequestModel model)
        {
            try
            {
                //var dateFrom = DateTime.ParseExact(model.DateFrom, "dd-MM-yyyy", CultureInfo.InvariantCulture);
                //var dateTo = DateTime.ParseExact(model.DateTo, "dd-MM-yyyy", CultureInfo.InvariantCulture);

                var timePlanningRequest = _dbContext.PlanRegistrations
                    .Include(x => x.AssignedSite)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date >= model.DateFrom || x.Date <= model.DateTo)
                    .Where(x => x.AssignedSite.SiteId == model.SiteId);
                
                var timePlannings = await timePlanningRequest
                    .Select(x => new TimePlanningPlanningHelperModel
                    {
                        WeekDay = (int)x.Date.DayOfWeek,
                        Date = x.Date,
                        PlanText = x.PlanText,
                        PlanHours = x.PlanHours,
                        MessageId = x.MessageId,
                    })
                    .ToListAsync();

                var date = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

                if (timePlannings.Count < date)
                {
                    var daysForAdd = new List<TimePlanningPlanningHelperModel>();
                    for (var i = 0; i < date; i++)
                    {
                        if (timePlannings.All(x => x.Date != model.DateFrom.AddDays(i)))
                        {
                            daysForAdd.Add(new TimePlanningPlanningHelperModel
                            {
                                Date = model.DateFrom.AddDays(i),
                                WeekDay = (int)model.DateFrom.AddDays(i).DayOfWeek,
                            });
                        }
                    }
                    timePlannings.AddRange(daysForAdd);
                }

                if (model.Sort.ToLower() == "dayofweek")
                {
                    List<TimePlanningPlanningHelperModel> tempResult;

                    if (model.IsSortDsc)
                    {
                        tempResult = timePlannings
                            .Where(x => x.WeekDay == 0)
                            .OrderByDescending(x => x.WeekDay)
                            .ThenByDescending(x => x.Date)
                            .ToList();
                        tempResult.AddRange(timePlannings
                            .Where(x => x.WeekDay > 0)
                            .OrderByDescending(x => x.WeekDay));
                    }
                    else
                    {
                        tempResult = timePlannings
                            .Where(x => x.WeekDay > 0)
                            .OrderBy(x => x.WeekDay)
                            .ThenBy(x => x.Date)
                            .ToList();
                        tempResult.AddRange(timePlannings
                            .Where(x => x.WeekDay == 0)
                            .OrderBy(x => x.Date));
                    }

                    timePlannings = tempResult;
                }
                else
                {
                    timePlannings = model.IsSortDsc
                        ? timePlannings.OrderByDescending(x => x.Date).ToList()
                        : timePlannings.OrderBy(x => x.Date).ToList();
                }

                var result = timePlannings
                    .Select(x => new TimePlanningPlanningModel
                    {
                        WeekDay = x.WeekDay,
                        Date = x.Date.ToString("yyyy/MM/dd"),
                        PlanText = x.PlanText,
                        PlanHours = x.PlanHours,
                        MessageId = x.MessageId,
                    })
                    .ToList();

                return new OperationDataResult<List<TimePlanningPlanningModel>>(
                    true,
                    result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningPlanningModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            }
        }

        public async Task<OperationResult> UpdateCreatePlanning(TimePlanningPlanningUpdateModel model)
        {
            try
            {
                //var date = DateTime.Parse(model.Date);
                var assignedSiteId = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SiteId == model.SiteId)
                    .Select(x => x.Id)
                    .FirstAsync();
                var planning = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSiteId == assignedSiteId)
                    .Where(x => x.Date == model.Date)
                    .FirstOrDefaultAsync();
                if (planning != null)
                {
                    return await UpdatePlanning(planning, model);
                }

                return await CreatePlanning(model, assignedSiteId/*, date*/);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdatePlanning"));
            }
        }

        private async Task<OperationResult> CreatePlanning(TimePlanningPlanningUpdateModel model, int assignedSiteId /*, DateTime date*/)
        {
            try
            {
                var planning = new PlanRegistration
                {
                    MessageId = (int)model.MessageId,
                    PlanText = model.PlanText,
                    AssignedSiteId = assignedSiteId,
                    Date = model.Date,
                    PlanHours = model.PlanHours,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId,
                };

                await planning.Create(_dbContext);
                return new OperationResult(
                    true,
                    _localizationService.GetString("SuccessfullyCreatePlanning"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileCreatePlanning"));
            }
        }

        private async Task<OperationResult> UpdatePlanning(PlanRegistration planning, TimePlanningPlanningUpdateModel model)
        {
            try
            {
                planning.PlanText = model.PlanText;
                planning.MessageId = (int)model.MessageId;
                planning.PlanHours = model.PlanHours;

                await planning.Update(_dbContext);

                return new OperationResult(
                    true,
                    _localizationService.GetString("SuccessfullyUpdatePlanning"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdatePlanning"));
            }
        }
    }
}
