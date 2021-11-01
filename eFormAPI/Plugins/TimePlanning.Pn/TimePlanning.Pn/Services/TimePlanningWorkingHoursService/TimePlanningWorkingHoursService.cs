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
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
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
                var foundWorkers = await _dbContext.PlanRegistrations
                    .Where(pr => pr.Id == model.WorkerId
                                 && (pr.Date >= model.DateFrom || pr.Date <= model.DateTo))
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
                        CommentOffice = pr.CommentOffice,
                        CommentOfficeAll = pr.CommentOfficeAll,
                    })
                    .ToListAsync();

                return new OperationDataResult<List<TimePlanningWorkingHoursViewModel>>(
                    true,
                    foundWorkers);
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
    }
}