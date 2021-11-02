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
namespace TimePlanning.Pn.Services.TimePlanningWorkingHoursService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.WorkingHours.Index;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Entities;
    using TimePlanningLocalizationService;

    /// <summary>
    /// TimePlanningWorkingHoursService
    /// </summary>
    public class TimePlanningWorkingHoursService : ITimePlanningWorkingHoursService
    {
        private readonly ILogger<TimePlanningWorkingHoursService> _logger;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningWorkingHoursService(
            ILogger<TimePlanningWorkingHoursService> logger,
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

        public async Task<OperationDataResult<List<TimePlanningWorkingHoursViewModel>>> Index(TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                var timePlannings = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date >= model.DateFrom || x.Date <= model.DateTo)
                    .Where(x => x.AssignedSiteId == model.WorkerId)
                    .Select(pr => new TimePlanningWorkingHoursViewModel
                    {
                        WorkerId = pr.AssignedSiteId,
                        WeekDay = (int)pr.Date.DayOfWeek,
                        Date = pr.Date,
                        PlanText = pr.PlanText,
                        PlanHours = pr.PlanHours,
                        Start1Id = pr.Start1Id,
                        Stop1Id = pr.Stop1Id,
                        Pause1Id = pr.Pause1Id,
                        Start2Id = pr.Start2Id,
                        Stop2Id = pr.Stop2Id,
                        Pause2Id = pr.Pause2Id,
                        NettoHours = pr.NettoHours,
                        Flex = pr.Flex,
                        SumFlex = pr.SumFlex,
                        PaiedOutFlex = pr.PaiedOutFlex,
                        MessageId = pr.MessageId,
                        CommentWorker = "",
                        CommentOffice = pr.CommentOffice,
                        CommentOfficeAll = pr.CommentOfficeAll,
                    })
                    .ToListAsync();

                var date = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

                if (timePlannings.Count < date)
                {
                    var timePlanningForAdd = new List<TimePlanningWorkingHoursViewModel>();
                    for (var i = 0; i < date; i++)
                    {
                        if (timePlannings.All(x => x.Date != model.DateFrom.AddDays(i)))
                        {
                            timePlanningForAdd.Add(new TimePlanningWorkingHoursViewModel
                            {
                                Date = model.DateFrom.AddDays(i),
                                WeekDay = (int)model.DateFrom.AddDays(i).DayOfWeek,
                                WorkerId = model.WorkerId,
                            });
                        }
                    }
                    timePlannings.AddRange(timePlanningForAdd);
                }

                return new OperationDataResult<List<TimePlanningWorkingHoursViewModel>>(
                    true,
                    timePlannings);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningWorkingHoursViewModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            };
        }

        public async Task<OperationResult> CreateUpdate(TimePlanningWorkingHoursViewModel model)
        {
            try
            {
                var planning = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSiteId == model.WorkerId)
                    .Where(x => x.Date == model.Date)
                    .FirstOrDefaultAsync();
                if (planning != null)
                {
                    return await UpdatePlanning(planning, model);
                }

                return await CreatePlanning(model);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningWorkingHoursViewModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileCreateUpdatePlannings"));
            }
        }



        private async Task<OperationResult> CreatePlanning(TimePlanningWorkingHoursViewModel model)
        {
            try
            {
                var planning = new PlanRegistration
                {
                    MessageId = model.MessageId,
                    PlanText = model.PlanText,
                    AssignedSiteId = model.WorkerId,
                    Date = model.Date,
                    PlanHours = model.PlanHours,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId,
                    CommentOffice = model.CommentOffice,
                    CommentOfficeAll = model.CommentOfficeAll,
                    NettoHours = model.NettoHours,
                    PaiedOutFlex = model.PaiedOutFlex,
                    Pause1Id = model.Pause1Id,
                    Pause2Id = model.Pause1Id,
                    Start1Id = model.Start1Id,
                    Start2Id = model.Start2Id,
                    Stop1Id = model.Stop1Id,
                    Stop2Id = model.Stop2Id,
                    Flex = model.Flex,
                    SumFlex = model.SumFlex,
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

        private async Task<OperationResult> UpdatePlanning(PlanRegistration planning, TimePlanningWorkingHoursViewModel model)
        {
            try
            {
                planning.MessageId = model.MessageId;
                planning.PlanText = model.PlanText;
                planning.AssignedSiteId = model.WorkerId;
                planning.Date = model.Date;
                planning.PlanHours = model.PlanHours;
                planning.UpdatedByUserId = _userService.UserId;
                planning.CommentOffice = model.CommentOffice;
                planning.CommentOfficeAll = model.CommentOfficeAll;
                planning.NettoHours = model.NettoHours;
                planning.PaiedOutFlex = model.PaiedOutFlex;
                planning.Pause1Id = model.Pause1Id;
                planning.Pause2Id = model.Pause1Id;
                planning.Start1Id = model.Start1Id;
                planning.Start2Id = model.Start2Id;
                planning.Stop1Id = model.Stop1Id;
                planning.Stop2Id = model.Stop2Id;
                planning.Flex = model.Flex;
                planning.SumFlex = model.SumFlex;

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