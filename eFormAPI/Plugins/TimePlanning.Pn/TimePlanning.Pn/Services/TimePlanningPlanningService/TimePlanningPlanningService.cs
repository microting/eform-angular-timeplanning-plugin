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
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensions;
    using Infrastructure.Models.Planning;
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
                var dateFrom = DateTime.Parse(model.DateFrom);
                var dateTo = DateTime.Parse(model.DateTo);

                var timePlanningRequest = _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date >= dateFrom || x.Date <= dateTo)
                    .Where(x => x.AssignedSiteId == model.WorkerId);

                if (model.Sort == "WeekDay".ToLower())
                {
                    timePlanningRequest = model.IsSortDesc 
                        ? timePlanningRequest.OrderByDescending(x => x.Date.StartOfWeek(DayOfWeek.Monday))
                        : timePlanningRequest.OrderBy(x => x.Date.StartOfWeek(DayOfWeek.Monday));
                }
                else
                {
                    timePlanningRequest = model.IsSortDesc
                        ? timePlanningRequest.OrderByDescending(x => x.Date)
                        : timePlanningRequest.OrderBy(x => x.Date);
                }
                
                var timePlannings = await timePlanningRequest
                    .Select(x => new TimePlanningPlanningModel
                    {
                        WeekDay = (int)x.Date.DayOfWeek,
                        Date = x.Date.ToString("MM-dd-yyyy"),
                        PlanText = x.PlanText,
                        PlanHours = x.PlanHours,
                        MessageId = x.MessageId,
                    })
                    .ToListAsync();

                var date = (int)(dateTo - dateFrom).TotalDays + 1;

                if (timePlannings.Count < date)
                {
                    var timePlanningForAdd = new List<TimePlanningPlanningModel>();
                    for (var i = 0; i < date; i++)
                    {
                        if (timePlannings.All(x => x.Date != dateFrom.AddDays(i).ToString("MM-dd-yyyy")))
                        {
                            timePlanningForAdd.Add(new TimePlanningPlanningModel
                            {
                                Date = dateFrom.AddDays(i).ToString("MM-dd-yyyy"),
                                WeekDay = (int)dateFrom.AddDays(i).DayOfWeek,
                            });
                        }
                    }
                    timePlannings.AddRange(timePlanningForAdd);
                }

                timePlannings = timePlannings.OrderBy(x => x.Date).ToList();
                return new OperationDataResult<List<TimePlanningPlanningModel>>(
                    true,
                    timePlannings);
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

        public async Task<OperationResult> UpdateCreatePlanning(TimePlanningPlanningModel model)
        {
            try
            {
                var date = DateTime.Parse(model.Date);

                var planning = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSiteId == model.WorkerId)
                    .Where(x => x.Date == date)
                    .FirstOrDefaultAsync();
                if (planning != null)
                {
                    return await UpdatePlanning(planning, model);
                }

                return await CreatePlanning(model, date);
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

        private async Task<OperationResult> CreatePlanning(TimePlanningPlanningModel model, DateTime date)
        {
            try
            {
                var planning = new PlanRegistration
                {
                    MessageId = (int)model.MessageId,
                    PlanText = model.PlanText,
                    AssignedSiteId = model.WorkerId,
                    Date = date,
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

        private async Task<OperationResult> UpdatePlanning(PlanRegistration planning, TimePlanningPlanningModel model)
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
