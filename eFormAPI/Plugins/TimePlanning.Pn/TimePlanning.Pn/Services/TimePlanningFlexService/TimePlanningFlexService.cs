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

namespace TimePlanning.Pn.Services.TimePlanningFlexService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.Common;
    using Infrastructure.Models.Flex.Index;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using TimePlanningLocalizationService;

    /// <summary>
    /// TimePlanningFlexService
    /// </summary>
    public class TimePlanningFlexService : ITimePlanningFlexService
    {
        private readonly ILogger<TimePlanningFlexService> _logger;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningFlexService(
            ILogger<TimePlanningFlexService> logger,
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

        public async Task<OperationDataResult<List<TimePlanningFlexIndexModel>>> Index()
        {
            try
            {
                var core = await _core.GetCore();

                var foundWorkers = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date == DateTime.UtcNow.AddDays(-1))
                    .Select(x => new
                    {
                        x.Date,
                        SiteId = x.AssignedSiteId,
                        x.SumFlex,
                        PaidOutFlex = x.PaiedOutFlex,
                        CommentWorker = "",
                        x.CommentOffice,
                        x.CommentOfficeAll,
                    })
                    .ToListAsync();

                var resultWorkers = new List<TimePlanningFlexIndexModel>();

                foreach (var worker in foundWorkers)
                {
                    var workerInfo = await core.SiteRead(worker.SiteId);

                    resultWorkers.Add(new TimePlanningFlexIndexModel
                    {
                        Date = worker.Date,
                        Worker = new EnumerableViewModel
                        {
                            Id = worker.SiteId,
                            Name = workerInfo.SiteName,
                        },
                        SumFlex = worker.SumFlex,
                        PaidOutFlex = worker.PaidOutFlex,
                        CommentWorker = worker.CommentWorker,
                        CommentOffice = worker.CommentOffice,
                        CommentOfficeAll = worker.CommentOfficeAll,
                    });
                }

                return new OperationDataResult<List<TimePlanningFlexIndexModel>>(
                    true,
                    resultWorkers);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningFlexIndexModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            }
        }
    }
}